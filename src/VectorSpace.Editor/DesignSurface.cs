using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using VectorSpace.Documents;
using VectorSpace.Layout;
using VectorSpace.Skia;
using Microsoft.UI.Xaml.Automation;

namespace VectorSpace.Editor;

/// <summary>An embeddable, real Uno canvas. All document mutations go through EditorSession transactions.</summary>
public sealed partial class DesignSurface : UserControl, IDisposable
{
    private sealed class DrawingCanvas : SKCanvasElement
    {
        public Action<SKCanvas, Size>? Draw { get; set; }
        protected override void RenderOverride(SKCanvas canvas, Size area) => Draw?.Invoke(canvas, area);
    }
    private enum Gesture { None, Move, Resize, Rotate, Create, Marquee, Pan, PenControl, Pencil, Guide, Pinch, Vertex }
    private readonly DrawingCanvas _canvas = new();
    private readonly Canvas _overlay = new();
    private EditorSession? _session;
    private Gesture _gesture;
    private Vec2 _startScreen, _startWorld, _startPan;
    private RectD _startBounds;
    private readonly Dictionary<string, DesignNode> _originals = [];
    private DesignNode? _created, _penNode, _vectorNode, _hover;
    private int _resizeHandle, _vertexIndex;
    private Matrix2D _resizeMatrix;
    private Guide? _guide;
    private RectD? _marquee;
    private IReadOnlyList<SnapLine> _snapLines = [];
    private TextBox? _textEditor;
    private DesignNode? _textNode;
    private bool _finishingText, _disposed;
    private readonly Dictionary<uint, Vec2> _touches = [];
    private double _pinchDistance, _pinchZoom;
    private Vec2 _pinchCenter, _pinchPan;
    private string? _presentedFrame;
    public SceneRenderer Renderer { get; } = new();
    public bool IsSpaceDown { get; set; }
    public bool IsPresenting => _presentedFrame is not null;
    public bool IsTextEditing => _textEditor is not null;
    public event Action<Vec2, CommentThread?>? CommentRequested;
    public event Action<Point>? CanvasContextRequested;
    public event Action<bool>? PresentationChanged;
    public event Action<string>? StatusChanged;
    public EditorSession? Session
    {
        get => _session;
        set
        {
            if (_session == value) return;
            if (_session is not null) _session.Changed -= SessionChanged;
            _session = value;
            if (_session is not null) _session.Changed += SessionChanged;
            _canvas.Invalidate();
        }
    }
    public DesignSurface()
    {
        IsTabStop = true; HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Design canvas");
        var root = new Grid(); root.Children.Add(_canvas); root.Children.Add(_overlay); Content = root;
        _canvas.Draw = Paint;
        _canvas.PointerPressed += Pressed; _canvas.PointerMoved += Moved; _canvas.PointerReleased += Released;
        _canvas.PointerCanceled += (_, _) => CancelGesture();
        _canvas.PointerCaptureLost += (_, _) => { if (_gesture is not Gesture.None and not Gesture.PenControl) CancelGesture(); };
        _canvas.PointerWheelChanged += Wheel;
        _canvas.DoubleTapped += (_, e) =>
        {
            if (Session is null || IsPresenting) return;
            if (_penNode is not null) { FinishPath(false); e.Handled = true; return; }
            var p = e.GetPosition(_canvas); var hit = Renderer.HitTest(Session.Page.Nodes, Session.Viewport.ScreenToWorld(new(p.X, p.Y)), true, 4 / Session.Viewport.Zoom);
            if (hit?.Kind == NodeKind.Text) { BeginTextEdit(hit); e.Handled = true; }
            else if (hit?.Kind == NodeKind.Path && hit.Points.Count > 0) { Session.Select(hit); _vectorNode = hit; _canvas.Invalidate(); e.Handled = true; }
        };
        _canvas.RightTapped += (_, e) => { CanvasContextRequested?.Invoke(e.GetPosition(this)); e.Handled = true; };
        _canvas.SizeChanged += (_, _) =>
        {
            if (IsPresenting && Session?.Document.Find(_presentedFrame) is { } frame)
                Session.Viewport.Fit(frame.WorldBounds, ActualWidth, ActualHeight, 32);
            _canvas.Invalidate();
        };
    }
    private void SessionChanged(object? sender, EditorChangedEventArgs e)
    {
        if (e.Kind == EditorChangeKind.Document)
        {
            Renderer.ClearCache(); _hover = null;
            if (_vectorNode is not null) _vectorNode = Session?.Document.Find(_vectorNode.Id);
        }
        if (e.Kind == EditorChangeKind.Tool && _penNode is not null) FinishPath(false);
        _canvas.Invalidate();
    }
    public void Invalidate() => _canvas.Invalidate();
    public void FocusCanvas() => Focus(FocusState.Programmatic);
    public void ZoomTo(double zoom)
    {
        if (Session is null) return; Session.Viewport.ZoomAt(zoom, new(ActualWidth / 2, ActualHeight / 2)); Session.Notify(EditorChangeKind.Viewport);
    }
    public void Fit(bool selection = false, bool firstFrame = false)
    {
        if (Session is null || ActualWidth < 1 || ActualHeight < 1) return;
        var nodes = selection && Session.Selection.Count > 0 ? Session.SelectionRoots : firstFrame ? Session.Page.Nodes.Take(1).ToArray() : Session.Page.Nodes.Where(n => n.Visible).ToArray();
        var bounds = nodes.Count == 0 ? new RectD(0, 0, 800, 600) : nodes.Select(n => n.WorldBounds).Aggregate(RectD.Union);
        Session.Viewport.Fit(bounds, ActualWidth, ActualHeight, 64); Session.Notify(EditorChangeKind.Viewport);
    }
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (Session is not { } editor) return;
        var point = e.GetCurrentPoint(_canvas); var screen = new Vec2(point.Position.X, point.Position.Y); var world = editor.Viewport.ScreenToWorld(screen);
        if (point.Properties.IsRightButtonPressed) return;
        if (IsPresenting) { NavigatePrototype(world); e.Handled = true; return; }
        FinishTextEdit(true); FocusCanvas();
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch)
        {
            _touches[e.Pointer.PointerId] = screen;
            if (_touches.Count == 2)
            {
                CancelGesture(); _gesture = Gesture.Pinch; var p = _touches.Values.ToArray(); _pinchCenter = (p[0] + p[1]) / 2; _pinchDistance = Math.Max(1, p[0].DistanceTo(p[1])); _pinchZoom = editor.Viewport.Zoom; _pinchPan = editor.Viewport.Pan; _canvas.CapturePointer(e.Pointer); e.Handled = true; return;
            }
        }
        _startScreen = screen; _startWorld = world; _startPan = editor.Viewport.Pan;
        _canvas.CapturePointer(e.Pointer); e.Handled = true;
        if (IsSpaceDown || editor.Tool == EditorTool.Hand || point.Properties.IsMiddleButtonPressed) { _gesture = Gesture.Pan; return; }
        if (editor.RulersVisible && (screen.X < 20 || screen.Y < 20))
        {
            editor.BeginInteraction("Add guide"); _guide = new() { Horizontal = screen.Y < 20, Position = screen.Y < 20 ? world.Y : world.X }; editor.Page.Guides.Add(_guide); _gesture = Gesture.Guide; return;
        }
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift); var alt = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu);
        if (editor.Tool == EditorTool.Comment)
        {
            var comment = editor.Document.Comments.FirstOrDefault(c => c.PageId == editor.Page.Id && !c.Resolved && c.Anchor.DistanceTo(world) * editor.Viewport.Zoom < 16);
            CommentRequested?.Invoke(world, comment); return;
        }
        if (_vectorNode is not null && editor.Tool == EditorTool.Move)
        {
            for (var i = 0; i < _vectorNode.Points.Count; i++)
            {
                if (editor.Viewport.WorldToScreen(_vectorNode.WorldMatrix.Map(_vectorNode.Points[i].Position)).DistanceTo(screen) < 9)
                {
                    editor.BeginInteraction("Move vector point"); _gesture = Gesture.Vertex; _vertexIndex = i; return;
                }
            }
            _vectorNode = null;
        }
        if (editor.Tool is EditorTool.Pen or EditorTool.Pencil) { StartPath(world, editor.Tool == EditorTool.Pencil); return; }
        if (editor.Tool is not EditorTool.Move and not EditorTool.Scale)
        {
            editor.BeginInteraction("Draw " + editor.Tool); _created = NewNode(editor.Tool, world);
            var parent = editor.Page.AllNodes().Reverse().FirstOrDefault(n => n.IsFrame && !n.IsEffectivelyLocked && n.WorldBounds.Contains(world));
            if (_created.Kind is NodeKind.Frame or NodeKind.Section or NodeKind.Slice) parent = null;
            if (parent is not null) { var local = parent.WorldMatrix.Inverse.Map(world); _created.X = local.X; _created.Y = local.Y; }
            editor.AddNode(_created, parent); editor.Select(_created); _gesture = Gesture.Create; editor.Preview(); return;
        }
        var handles = GetHandles();
        for (var i = 0; i < handles.Length; i++)
        {
            if (handles[i].DistanceTo(screen) <= 7 && editor.SelectionRoots.All(n => !n.IsEffectivelyLocked))
            {
                editor.BeginInteraction(i == 8 ? "Rotate layers" : "Resize layers"); CaptureOriginals(); _resizeHandle = i;
                _gesture = i == 8 ? Gesture.Rotate : Gesture.Resize;
                if (editor.SelectionRoots.Count == 1) _resizeMatrix = editor.SelectionRoots[0].WorldMatrix;
                return;
            }
        }
        var hit = Hit(world, screen, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control));
        if (hit is not null)
        {
            if (shift) { editor.Select(hit, true); if (!editor.SelectedIds.Contains(hit.Id)) return; }
            else if (!editor.SelectedIds.Contains(hit.Id)) editor.Select(hit);
            editor.BeginInteraction(alt ? "Duplicate layers" : "Move layers");
            if (alt) editor.DuplicateInTransaction(editor.SelectionRoots);
            CaptureOriginals(); _gesture = Gesture.Move;
        }
        else
        {
            if (!shift) editor.Select((DesignNode?)null); _gesture = Gesture.Marquee; _marquee = new(world.X, world.Y, 0, 0);
        }
        _canvas.Invalidate();
    }
    private DesignNode? Hit(Vec2 world, Vec2 screen, bool deep)
    {
        if (Session is null) return null;
        foreach (var node in Session.Page.Nodes.Where(n => n.IsContainer && !n.Locked).Reverse())
        {
            var p = Session.Viewport.WorldToScreen(new(node.WorldBounds.X, node.WorldBounds.Y));
            if (screen.Y >= p.Y - 24 && screen.Y <= p.Y - 3 && screen.X >= p.X && screen.X <= p.X + Math.Max(70, node.Name.Length * 6)) return node;
        }
        return Renderer.HitTest(Session.Page.Nodes, world, deep, 4 / Session.Viewport.Zoom);
    }
    private void CaptureOriginals()
    {
        _originals.Clear(); if (Session is null) return;
        foreach (var node in Session.SelectionRoots) _originals[node.Id] = DocumentJson.CloneNode(node);
        _startBounds = Session.SelectionBounds();
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (Session is not { } editor) return;
        var p = e.GetCurrentPoint(_canvas).Position; var screen = new Vec2(p.X, p.Y); var world = editor.Viewport.ScreenToWorld(screen);
        if (_touches.ContainsKey(e.Pointer.PointerId)) _touches[e.Pointer.PointerId] = screen;
        if (_gesture == Gesture.Pinch && _touches.Count >= 2)
        {
            var points = _touches.Values.Take(2).ToArray(); var center = (points[0] + points[1]) / 2; var ratio = points[0].DistanceTo(points[1]) / _pinchDistance;
            editor.Viewport.ZoomAt(_pinchZoom, Vec2.Zero); editor.Viewport.Pan = _pinchPan; editor.Viewport.ZoomAt(_pinchZoom * ratio, _pinchCenter); editor.Viewport.Pan += center - _pinchCenter; editor.Notify(EditorChangeKind.Viewport); return;
        }
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        switch (_gesture)
        {
            case Gesture.Pan: editor.Viewport.Pan = _startPan + screen - _startScreen; editor.Notify(EditorChangeKind.Viewport); break;
            case Gesture.Move:
                var delta = world - _startWorld;
                if (shift) delta = Math.Abs(delta.X) > Math.Abs(delta.Y) ? new(delta.X, 0) : new(0, delta.Y);
                _snapLines = [];
                if (editor.SnapEnabled && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
                {
                    var roots = editor.SelectionRoots;
                    var targets = editor.Page.AllNodes().Where(n => n.IsEffectivelyVisible && !editor.SelectedIds.Contains(n.Id) && !roots.Any(n.IsDescendantOf)).Select(n => n.WorldBounds);
                    var moving = _startBounds with { X = _startBounds.X + delta.X, Y = _startBounds.Y + delta.Y };
                    var snap = SnapEngine.Snap(moving, targets, 5 / editor.Viewport.Zoom, editor.Page.Guides); delta += snap.Correction; _snapLines = snap.Lines;
                }
                foreach (var node in editor.SelectionRoots)
                {
                    if (!_originals.TryGetValue(node.Id, out var original)) continue;
                    var inverse = node.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity; var d = inverse.Map(delta) - inverse.Map(Vec2.Zero); node.X = original.X + d.X; node.Y = original.Y + d.Y;
                }
                editor.Preview(); break;
            case Gesture.Resize: ResizeSelection(world, shift, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu)); editor.Preview(); break;
            case Gesture.Rotate:
                var centerWorld = _startBounds.Center; var angle = Math.Atan2(world.Y - centerWorld.Y, world.X - centerWorld.X) - Math.Atan2(_startWorld.Y - centerWorld.Y, _startWorld.X - centerWorld.X);
                foreach (var node in editor.SelectionRoots) if (_originals.TryGetValue(node.Id, out var original)) { var degrees = original.Rotation + angle * 180 / Math.PI; node.Rotation = shift ? Math.Round(degrees / 15) * 15 : degrees; }
                editor.Preview(); break;
            case Gesture.Create:
                if (_created is null) break;
                var matrix = _created.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity; var a = matrix.Map(_startWorld); var b = matrix.Map(world); var d2 = b - a;
                if (shift) { var size = Math.Max(Math.Abs(d2.X), Math.Abs(d2.Y)); b = a + new Vec2(Math.Sign(d2.X) * size, Math.Sign(d2.Y) * size); }
                var box = RectD.FromPoints(a, b);
                if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu)) box = new(a.X - Math.Abs(b.X - a.X), a.Y - Math.Abs(b.Y - a.Y), Math.Abs(b.X - a.X) * 2, Math.Abs(b.Y - a.Y) * 2);
                _created.X = box.X; _created.Y = box.Y; _created.Width = Math.Max(1, box.Width); _created.Height = Math.Max(1, box.Height);
                if (_created.Kind is NodeKind.Line or NodeKind.Arrow) { _created.FlipX = b.X < a.X; _created.FlipY = b.Y < a.Y; }
                editor.Preview(); break;
            case Gesture.Marquee: _marquee = RectD.FromPoints(_startWorld, world); _canvas.Invalidate(); break;
            case Gesture.Guide: if (_guide is not null) _guide.Position = _guide.Horizontal ? world.Y : world.X; editor.Preview(); break;
            case Gesture.Pencil:
                if (_penNode is null) break;
                var local = _penNode.WorldMatrix.Inverse.Map(world);
                if (_penNode.Points[^1].Position.DistanceTo(local) * editor.Viewport.Zoom >= 2) { _penNode.Points.Add(new() { Position = local }); editor.Preview(); } break;
            case Gesture.PenControl:
                if (_penNode is null || screen.DistanceTo(_startScreen) < 3) break;
                var control = _penNode.WorldMatrix.Inverse.Map(world); var last = _penNode.Points[^1]; last.ControlOut = control; last.ControlIn = last.Position * 2 - control; editor.Preview(); break;
            case Gesture.Vertex:
                if (_vectorNode is null) break;
                var vertex = _vectorNode.Points[_vertexIndex]; var position = _vectorNode.WorldMatrix.Inverse.Map(world);
                if (_vectorNode.PathWidth > 0 && _vectorNode.PathHeight > 0) position = new(position.X * _vectorNode.PathWidth / _vectorNode.Width, position.Y * _vectorNode.PathHeight / _vectorNode.Height);
                var change = position - vertex.Position; vertex.Position = position; if (vertex.ControlIn.HasValue) vertex.ControlIn += change; if (vertex.ControlOut.HasValue) vertex.ControlOut += change; editor.Preview(); break;
            default:
                if (editor.Tool is EditorTool.Move or EditorTool.Scale) { var hover = Hit(world, screen, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)); if (_hover != hover) { _hover = hover; _canvas.Invalidate(); } }
                break;
        }
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (Session is not { } editor) return;
        _touches.Remove(e.Pointer.PointerId);
        var gesture = _gesture; _gesture = Gesture.None;
        _canvas.ReleasePointerCapture(e.Pointer); e.Handled = true;
        if (gesture == Gesture.Pinch) return;
        if (gesture == Gesture.Marquee && _marquee is { } box)
        {
            var candidates = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control) ? editor.Page.AllNodes() : editor.Page.Nodes;
            var ids = candidates.Where(n => n.IsEffectivelyVisible && !n.IsEffectivelyLocked && n.WorldBounds.Intersects(box)).Select(n => n.Id);
            editor.Select(e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift) ? editor.SelectedIds.Concat(ids).Distinct().ToArray() : ids.ToArray());
        }
        if (gesture == Gesture.Create && _created is not null)
        {
            if (_created.Width < 3 && _created.Height < 3) { _created.Width = _created.Kind == NodeKind.Text ? 180 : 100; _created.Height = _created.Kind == NodeKind.Text ? 32 : 100; }
            var node = _created; _created = null; editor.CommitInteraction(); editor.Tool = EditorTool.Move;
            if (node.Kind == NodeKind.Text) BeginTextEdit(node);
        }
        else if (gesture == Gesture.Pencil) FinishPath(false);
        else if (gesture is not Gesture.None and not Gesture.Pan and not Gesture.Marquee and not Gesture.PenControl) editor.CommitInteraction();
        _marquee = null; _guide = null; _snapLines = []; _canvas.Invalidate();
    }
    private void ResizeSelection(Vec2 world, bool aspect, bool center)
    {
        if (Session is not { } editor || _originals.Count == 0) return;
        var single = editor.SelectionRoots.Count == 1; var original = _originals.Values.First();
        var point = single ? _resizeMatrix.Inverse.Map(world) : world - new Vec2(_startBounds.X, _startBounds.Y);
        var width = single ? original.Width : _startBounds.Width; var height = single ? original.Height : _startBounds.Height;
        var left = 0d; var top = 0d; var right = width; var bottom = height;
        if (_resizeHandle is 0 or 6 or 7) left = Math.Min(point.X, width - 1);
        if (_resizeHandle is 2 or 3 or 4) right = Math.Max(1, point.X);
        if (_resizeHandle is 0 or 1 or 2) top = Math.Min(point.Y, height - 1);
        if (_resizeHandle is 4 or 5 or 6) bottom = Math.Max(1, point.Y);
        if (center) { if (_resizeHandle is 0 or 6 or 7) right = width - left; else if (_resizeHandle is 2 or 3 or 4) left = width - right; if (_resizeHandle is 0 or 1 or 2) bottom = height - top; else if (_resizeHandle is 4 or 5 or 6) top = height - bottom; }
        var newW = Math.Max(1, right - left); var newH = Math.Max(1, bottom - top);
        if (aspect) { var ratio = Math.Max(newW / Math.Max(1, width), newH / Math.Max(1, height)); newW = width * ratio; newH = height * ratio; if (_resizeHandle is 0 or 6 or 7) left = right - newW; if (_resizeHandle is 0 or 1 or 2) top = bottom - newH; }
        if (single)
        {
            var node = editor.SelectionRoots[0]; var localCenter = original.LocalMatrix.Map(new Vec2(left + newW / 2, top + newH / 2));
            node.Layout.HugWidth = node.Layout.HugHeight = false; LayoutEngine.Resize(node, newW, newH); node.X = localCenter.X - newW / 2; node.Y = localCenter.Y - newH / 2;
        }
        else
        {
            foreach (var node in editor.SelectionRoots)
            {
                if (!_originals.TryGetValue(node.Id, out var old)) continue;
                var parent = node.Parent?.WorldMatrix ?? Matrix2D.Identity; var oldCenter = parent.Map(new Vec2(old.X + old.Width / 2, old.Y + old.Height / 2));
                var transformed = new Vec2(_startBounds.X + left + (oldCenter.X - _startBounds.X) * newW / width, _startBounds.Y + top + (oldCenter.Y - _startBounds.Y) * newH / height);
                var localCenter = parent.Inverse.Map(transformed); node.Width = Math.Max(1, old.Width * newW / width); node.Height = Math.Max(1, old.Height * newH / height); node.X = localCenter.X - node.Width / 2; node.Y = localCenter.Y - node.Height / 2;
            }
        }
    }
    private void Wheel(object sender, PointerRoutedEventArgs e)
    {
        if (Session is not { } editor || IsPresenting) return;
        var point = e.GetCurrentPoint(_canvas); var delta = point.Properties.MouseWheelDelta;
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)) editor.Viewport.ZoomAt(editor.Viewport.Zoom * Math.Exp(delta * .0015), new(point.Position.X, point.Position.Y));
        else if (point.Properties.IsHorizontalMouseWheel || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) editor.Viewport.Pan += new Vec2(delta * .65, 0);
        else editor.Viewport.Pan += new Vec2(0, delta * .65);
        editor.Notify(EditorChangeKind.Viewport); e.Handled = true;
    }
    public void CancelGesture()
    {
        _gesture = Gesture.None; _created = null; _marquee = null; _snapLines = []; _guide = null; _penNode = null; _vectorNode = null;
        Session?.CancelInteraction(); _canvas.Invalidate();
    }
    private static DesignNode NewNode(EditorTool tool, Vec2 point)
    {
        var kind = Enum.TryParse<NodeKind>(tool.ToString(), out var k) ? k : NodeKind.Rectangle;
        var node = new DesignNode { Kind = kind, Name = kind.ToString(), X = point.X, Y = point.Y, Width = 1, Height = 1 };
        if (kind == NodeKind.Frame) { node.Fill = "#FFFFFF"; node.ClipContent = true; }
        else if (kind == NodeKind.Section) { node.Fill = "#D3D3D3"; node.CornerRadius = 8; }
        else if (kind is NodeKind.Line or NodeKind.Arrow or NodeKind.Slice) { node.Fills.Clear(); node.Strokes.Add(new() { Width = kind == NodeKind.Slice ? 1 : 2, Color = kind == NodeKind.Slice ? "#9747FF" : "#333333" }); }
        else if (kind == NodeKind.Text) { node.Fill = "#242424"; node.FontSize = 24; }
        return node;
    }
    private void StartPath(Vec2 world, bool pencil)
    {
        if (Session is not { } editor) return;
        if (_penNode is null)
        {
            editor.BeginInteraction(pencil ? "Draw freehand path" : "Draw vector path"); _penNode = new() { Kind = NodeKind.Path, Name = pencil ? "Pencil" : "Vector", X = world.X, Y = world.Y, Width = 1, Height = 1, Fills = [], Strokes = [new() { Color = "#333333", Width = 2 }] }; editor.AddNode(_penNode); editor.Select(_penNode);
        }
        var local = _penNode.WorldMatrix.Inverse.Map(world);
        if (!pencil && _penNode.Points.Count >= 3 && _penNode.Points[0].Position.DistanceTo(local) * editor.Viewport.Zoom < 8) { FinishPath(true); return; }
        _penNode.Points.Add(new() { Position = local }); _gesture = pencil ? Gesture.Pencil : Gesture.PenControl; editor.Preview();
    }
    public void FinishPath(bool closed)
    {
        if (Session is not { } editor || _penNode is null) return;
        var node = _penNode; _penNode = null; _gesture = Gesture.None;
        if (node.Points.Count < 2) { editor.CancelInteraction(); return; }
        node.Closed = closed;
        using var path = SKPath.ParseSvgPathData(VectorPath.Build(node)); var bounds = path?.TightBounds ?? default;
        var offset = new Vec2(bounds.Left, bounds.Top);
        foreach (var point in node.Points) { point.Position -= offset; if (point.ControlIn.HasValue) point.ControlIn -= offset; if (point.ControlOut.HasValue) point.ControlOut -= offset; }
        node.X += offset.X; node.Y += offset.Y; node.Width = node.PathWidth = Math.Max(1, bounds.Width); node.Height = node.PathHeight = Math.Max(1, bounds.Height);
        editor.CommitInteraction(); editor.Tool = EditorTool.Move;
    }
    public void BeginTextEdit(DesignNode node)
    {
        if (Session is not { } editor || node.IsEffectivelyLocked) return;
        FinishTextEdit(true); if (editor.IsInteracting) editor.CommitInteraction(); editor.Select(node); editor.BeginInteraction("Edit text"); _textNode = node;
        var bounds = node.WorldBounds; var topLeft = editor.Viewport.WorldToScreen(new(bounds.X, bounds.Y));
        var box = Studio.Input(node.Text, "Edit canvas text"); box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap; box.FontSize = Math.Max(8, node.FontSize * editor.Viewport.Zoom); box.Width = Math.Max(80, bounds.Width * editor.Viewport.Zoom + 12); box.Height = Math.Max(40, bounds.Height * editor.Viewport.Zoom + 12); box.Background = Studio.Brush("#FFFFFF"); box.BorderBrush = Studio.Brush(Studio.Accent); box.Padding = new(4); _textEditor = box;
        Canvas.SetLeft(box, topLeft.X - 4); Canvas.SetTop(box, topLeft.Y - 4); _overlay.Children.Add(box);
        box.TextChanged += (_, _) => { if (_textNode is null) return; _textNode.Text = box.Text; ComponentService.SetOverride(_textNode, text: box.Text); };
        box.LostFocus += (_, _) => { if (!_finishingText) FinishTextEdit(true); };
        box.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { FinishTextEdit(false); e.Handled = true; } else if (e.Key == VirtualKey.Enter && Keyboard.Control) { FinishTextEdit(true); e.Handled = true; } };
        box.Focus(FocusState.Programmatic); box.SelectAll();
    }
    public void FinishTextEdit(bool commit)
    {
        if (_textEditor is null || _finishingText) return;
        _finishingText = true; var box = _textEditor; _textEditor = null; _textNode = null; _overlay.Children.Remove(box);
        if (commit) Session?.CommitInteraction(); else Session?.CancelInteraction(); _finishingText = false; _canvas.Invalidate();
    }
    public void Present()
    {
        if (Session is not { } editor) return;
        FinishTextEdit(true); if (editor.IsInteracting) editor.CommitInteraction();
        var frame = editor.SelectionRoots.FirstOrDefault(n => n.IsFrame) ?? editor.Page.Nodes.FirstOrDefault(n => n.IsFrame);
        if (frame is null) { StatusChanged?.Invoke("Create a frame to present a prototype."); return; }
        _presentedFrame = frame.Id; editor.Viewport.Fit(frame.WorldBounds, ActualWidth, ActualHeight, 32); PresentationChanged?.Invoke(true); _canvas.Invalidate();
    }
    public void ExitPresentation() { _presentedFrame = null; PresentationChanged?.Invoke(false); Fit(true); }
    private void NavigatePrototype(Vec2 world)
    {
        if (Session is not { } editor || editor.Document.Find(_presentedFrame) is not { } frame) return;
        var hit = Renderer.HitTest([frame], world, true);
        for (var n = hit; n is not null; n = n.Parent)
        {
            if (n.PrototypeTargetId is { } target && editor.Document.Find(target) is { } destination)
            {
                _presentedFrame = target; editor.Viewport.Fit(destination.WorldBounds, ActualWidth, ActualHeight, 32); _canvas.Invalidate(); return;
            }
        }
    }
    public new void Dispose()
    {
        if (_disposed) return; _disposed = true; if (_session is not null) _session.Changed -= SessionChanged; Renderer.Dispose();
    }
}

public static class Keyboard
{
    public static bool Down(VirtualKey key) => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
    public static bool Control => Down(VirtualKey.Control) || Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows);
    public static bool Shift => Down(VirtualKey.Shift);
    public static bool Alt => Down(VirtualKey.Menu);
    public static bool IsTextInput(DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current)) if (current is TextBox or PasswordBox or RichEditBox) return true;
        return false;
    }
}
