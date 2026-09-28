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
    private enum Gesture { None, Move, Resize, Rotate, Create, Marquee, Pan, PenControl, Pencil, Guide, Pinch, Vertex, VertexMarquee, ImageCrop }
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
    private Vec2? _pathPreviewWorld;
    private string[] _marqueeBaseline = [];
    private SnapIndex _snapIndex = SnapIndex.Empty;
    private DesignNode? _pendingDuplicate;
    private int _controlHandle;
    private bool _frameQueued;
    private IReadOnlyList<SnapLine> _snapLines = [];
    private TextBox? _textEditor;
    private DesignNode? _textNode;
    private bool _finishingText, _disposed;
    private readonly Dictionary<uint, Vec2> _touches = [];
    private double _pinchDistance, _pinchZoom;
    private Vec2 _pinchCenter, _pinchPan;
    public SceneRenderer Renderer { get; } = new();
    public bool ShowDiagnostics { get; set; }
    public bool HasActivePath => _penNode is not null;
    public bool IsSpaceDown { get; set; }
    public bool IsPresenting => _prototypePlayer is not null;
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
            FinishTextEdit(false); CancelGesture();
            if (_session is not null) _session.Changed -= SessionChanged;
            ExitPresentation(); _session = value;
            if (_session is not null) _session.Changed += SessionChanged;
            RequestFrame();
        }
    }
    public DesignSurface()
    {
        IsTabStop = true; HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Design canvas");
        var root = new Grid(); root.Children.Add(_canvas); root.Children.Add(_overlay); Content = root;
        _canvas.Draw = (canvas, size) => { _frameQueued = false; Paint(canvas, size); };
        _canvas.PointerPressed += Pressed; _canvas.PointerMoved += Moved; _canvas.PointerReleased += Released;
        _canvas.PointerCanceled += (_, _) => CancelGesture();
        _canvas.PointerCaptureLost += (_, _) => { if (_gesture is not Gesture.None and not Gesture.PenControl) CancelGesture(); };
        _canvas.PointerWheelChanged += Wheel;
        _canvas.DoubleTapped += (_, e) =>
        {
            if (Session is null || IsPresenting || IsImageCropping) return;
            if (_penNode is not null) { FinishPath(false); e.Handled = true; return; }
            if (_vectorNode is { } vector)
            {
                var at = e.GetPosition(_canvas); var index = HitPoint(new(at.X, at.Y), out var handle);
                if (index >= 0 && handle == 0)
                {
                    _pointSelection.Clear(); _pointSelection.Add(index);
                    var point = vector.Points[index]; SetPointTangents(point.ControlIn is null && point.ControlOut is null ? TangentMode.Smooth : TangentMode.Corner);
                }
                e.Handled = true; return;
            }
            var p = e.GetPosition(_canvas);
            var deepest = Renderer.HitTest(Session.Page.Nodes, Session.Viewport.ScreenToWorld(new(p.X, p.Y)), true, 4 / Session.Viewport.Zoom);
            if (Session.Primary is { IsContainer: true } parent && deepest is not null && deepest.IsDescendantOf(parent))
            {
                var next = deepest;
                while (next.Parent != parent && next.Parent is not null) next = next.Parent;
                Session.Select(next); e.Handled = true; return;
            }
            if (deepest?.Kind == NodeKind.Text) { BeginTextEdit(deepest); e.Handled = true; }
            else if (deepest is not null && EditablePathConversion.Supports(deepest))
            {
                Session.Select(deepest);
                try { BeginVectorEdit(); } catch (InvalidOperationException error) { StatusChanged?.Invoke(error.Message); }
                e.Handled = true;
            }
        };
        _canvas.RightTapped += (_, e) => { CanvasContextRequested?.Invoke(e.GetPosition(this)); e.Handled = true; };
        _canvas.SizeChanged += (_, _) =>
        {
            RequestFrame();
        };
    }
    private void SessionChanged(object? sender, EditorChangedEventArgs e)
    {
        if (IsImageCropping) CropTarget(out _, out _);
        if (e.Kind == EditorChangeKind.Tool) { _cropNodeId = null; _cropDocument = null; }
        if (e.Kind == EditorChangeKind.Document)
        {
            // Undo/load can replace every node while a pen or pointer gesture is active.
            // Never let transient gesture references survive the transaction they belong to.
            if (Session?.IsInteracting != true)
            {
                _gesture = Gesture.None; _created = null; _penNode = null;
                _marquee = null; _guide = null; _snapLines = []; _originals.Clear();
                _canvas.ReleasePointerCaptures();
            }
            Renderer.TrimCache(Session?.Page.AllNodes().Select(n => n.Id) ?? []); _hover = null;
            if (_vectorNode is not null) _vectorNode = Session?.Document.Find(_vectorNode.Id);
        }
        if (e.Kind == EditorChangeKind.Tool)
        {
            if (_penNode is not null) CompletePath(false, false);
            else if (Session?.IsInteracting == true && !IsTextEditing) CancelGesture();
            if (Session?.Tool != EditorTool.Move) { _vectorNode = null; _pointSelection.Clear(); }
        }
        if (e.Kind is EditorChangeKind.Document or EditorChangeKind.Selection) ValidateVectorTarget();
        RequestFrame();
    }
    private void RequestFrame()
    {
        if (_disposed || _frameQueued) return;
        _frameQueued = true;
        _canvas.Invalidate();
    }
    public void Invalidate() => RequestFrame();
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
        if (IsPresenting) { e.Handled = true; return; }
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
        if (PressImageCrop(world)) return;
        if (editor.RulersVisible && (screen.X < 20 || screen.Y < 20))
        {
            editor.BeginInteraction("Add guide"); _guide = new() { Horizontal = screen.Y < 20, Position = screen.Y < 20 ? world.Y : world.X }; editor.Page.Guides.Add(_guide); _gesture = Gesture.Guide; return;
        }
        if (PressGuide(screen, world)) return;
        var shift = Keyboard.Shift || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift); var alt = (Keyboard.Alt || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu));
        if (editor.Tool == EditorTool.Comment)
        {
            var comment = editor.Document.Comments.FirstOrDefault(c => c.PageId == editor.Page.Id && !c.Resolved && c.Anchor.DistanceTo(world) * editor.Viewport.Zoom < 16);
            CommentRequested?.Invoke(world, comment); return;
        }
        if (PressVectorEdit(screen, world, shift)) return;
        if (editor.Tool is EditorTool.Pen or EditorTool.Pencil) { StartPath(world, editor.Tool == EditorTool.Pencil); return; }
        if (editor.Tool is not EditorTool.Move and not EditorTool.Scale)
        {
            editor.BeginInteraction("Draw " + editor.Tool); _created = NewNode(editor.Tool, world);
            var parent = DrawingTargetQuery.FindFrame(editor.Page.Nodes, world);
            if (_created.Kind is NodeKind.Frame or NodeKind.Section or NodeKind.Slice) parent = null;
            if (parent is not null) { var local = parent.WorldMatrix.Inverse.Map(world); _created.X = local.X; _created.Y = local.Y; }
            if (parent?.Layout.Direction != LayoutDirection.None && parent is not null) _created.AbsolutePosition = true;
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
        var hit = Hit(world, screen, Keyboard.Control || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control));
        if (hit is not null)
        {
            if (shift) { editor.Select(hit, true); if (!editor.SelectedIds.Contains(hit.Id)) return; }
            else if (!editor.SelectedIds.Contains(hit.Id)) editor.Select(hit);
            editor.BeginInteraction(alt ? "Duplicate layers" : "Move layers");
            _pendingDuplicate = alt ? hit : null;
            CaptureOriginals(); _gesture = Gesture.Move;
        }
        else
        {
            _marqueeBaseline = shift ? editor.SelectedIds.ToArray() : [];
            if (!shift) editor.Select((DesignNode?)null); _gesture = Gesture.Marquee; _marquee = new(world.X, world.Y, 0, 0);
        }
        RequestFrame();
    }
    private DesignNode? Hit(Vec2 world, Vec2 screen, bool deep)
    {
        if (Session is null) return null;
        foreach (var node in Session.Page.Nodes.Where(n => n.IsContainer && !n.Locked).Reverse())
        {
            var p = Session.Viewport.WorldToScreen(new(node.WorldBounds.X, node.WorldBounds.Y));
            if (screen.Y >= p.Y - 24 && screen.Y <= p.Y - 3 && screen.X >= p.X && screen.X <= p.X + Math.Max(70, node.Name.Length * 6)) return node;
        }
        return Session.ResolveSelection(Renderer.HitTest(Session.Page.Nodes, world, true, 4 / Session.Viewport.Zoom), deep);
    }
    private static Vec2 VectorPointPosition(DesignNode node, Vec2 point) =>
        node.PathWidth > 0 && node.PathHeight > 0
            ? new(point.X * node.Width / node.PathWidth, point.Y * node.Height / node.PathHeight)
            : point;
    private void CaptureOriginals()
    {
        _originals.Clear(); if (Session is null) return;
        foreach (var node in Session.SelectionRoots.Where(n => !n.IsEffectivelyLocked)) _originals[node.Id] = DocumentJson.CloneNode(node);
        _startBounds = Session.SelectionRoots.Where(n => _originals.ContainsKey(n.Id)).Select(n => n.WorldBounds).DefaultIfEmpty().Aggregate(RectD.Union);
        var roots = Session.SelectionRoots;
        _snapIndex = new SnapIndex(Session.Page.AllNodes().Where(n => n.IsEffectivelyVisible && !Session.SelectedIds.Contains(n.Id) && !roots.Any(n.IsDescendantOf)).Select(n => n.WorldBounds), Session.Page.Guides);
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
        var shift = Keyboard.Shift || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        switch (_gesture)
        {
            case Gesture.ImageCrop: MoveImageCrop(world); break;
            case Gesture.Pan: editor.Viewport.Pan = _startPan + screen - _startScreen; editor.Notify(EditorChangeKind.Viewport); break;
            case Gesture.Move:
                if (screen.DistanceTo(_startScreen) < 3) break;
                if (_pendingDuplicate is not null)
                {
                    _pendingDuplicate = null; editor.DuplicateInTransaction(editor.SelectionRoots); CaptureOriginals();
                }
                if (editor.SelectionRoots.Count > 0 && editor.SelectionRoots[0].Parent is { } flowParent && flowParent.Layout.Direction != LayoutDirection.None && editor.SelectionRoots.All(n => n.Parent == flowParent && !n.AbsolutePosition))
                {
                    editor.ReorderAutoLayout(world); editor.Preview(false); break;
                }
                var delta = world - _startWorld;
                if (shift) delta = Math.Abs(delta.X) > Math.Abs(delta.Y) ? new(delta.X, 0) : new(0, delta.Y);
                _snapLines = [];
                if (editor.SnapEnabled && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
                {
                    var moving = _startBounds with { X = _startBounds.X + delta.X, Y = _startBounds.Y + delta.Y };
                    var snap = _snapIndex.Snap(moving, 5 / editor.Viewport.Zoom);
                    if (shift)
                    {
                        var horizontal = Math.Abs(delta.X) >= Math.Abs(delta.Y);
                        delta += horizontal ? new Vec2(snap.Correction.X, 0) : new Vec2(0, snap.Correction.Y);
                        _snapLines = snap.Lines.Where(line => line.Horizontal != horizontal).ToArray();
                    }
                    else { delta += snap.Correction; _snapLines = snap.Lines; }
                }
                foreach (var node in editor.SelectionRoots)
                {
                    if (!_originals.TryGetValue(node.Id, out var original)) continue;
                    var inverse = node.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity; var d = inverse.Map(delta) - inverse.Map(Vec2.Zero); node.X = original.X + d.X; node.Y = original.Y + d.Y;
                }
                editor.Preview(false); break;
            case Gesture.Resize: ResizeSelection(world, shift, (Keyboard.Alt || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu))); editor.Preview(); break;
            case Gesture.Rotate:
                var centerWorld = _startBounds.Center; var angle = Math.Atan2(world.Y - centerWorld.Y, world.X - centerWorld.X) - Math.Atan2(_startWorld.Y - centerWorld.Y, _startWorld.X - centerWorld.X);
                var degrees = angle * 180 / Math.PI;
                if (shift) degrees = Math.Round(degrees / 15) * 15;
                foreach (var node in editor.SelectionRoots) if (_originals.TryGetValue(node.Id, out var original)) GestureGeometry.Rotate(node, original, centerWorld, degrees);
                editor.Preview(false); break;
            case Gesture.Create:
                if (_created is null) break;
                var matrix = _created.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity;
                DrawingGeometry.Apply(_created, matrix.Map(_startWorld), matrix.Map(world), shift, Keyboard.Alt || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu));
                editor.Preview(); break;
            case Gesture.Marquee:
                _marquee = RectD.FromPoints(_startWorld, world);
                var matches = SelectionQuery.Marquee(editor.Page.Nodes, _marquee.Value, Keyboard.Control || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control));
                editor.Select(_marqueeBaseline.Concat(matches.Select(n => n.Id)).Distinct());
                RequestFrame(); break;
            case Gesture.Guide: if (_guide is not null) _guide.Position = _guide.Horizontal ? world.Y : world.X; editor.Preview(); break;
            case Gesture.Pencil:
                if (_penNode is null) break;
                var local = _penNode.WorldMatrix.Inverse.Map(world);
                if (_penNode.Points[^1].Position.DistanceTo(local) * editor.Viewport.Zoom >= 2) { _penNode.Points.Add(new() { Position = local }); editor.Preview(); } break;
            case Gesture.PenControl:
                if (_penNode is null || screen.DistanceTo(_startScreen) < 3) break;
                var control = _penNode.WorldMatrix.Inverse.Map(world); var last = _penNode.Points[^1]; last.ControlOut = control; last.ControlIn = last.Position * 2 - control; editor.Preview(); break;
            case Gesture.Vertex: case Gesture.VertexMarquee:
                MoveVectorEdit(screen, world, shift, Keyboard.Alt || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu)); break;
            default:
                if (_penNode is not null) { _pathPreviewWorld = world; RequestFrame(); break; }
                if (editor.Tool is EditorTool.Move or EditorTool.Scale) { var hover = Hit(world, screen, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)); if (_hover != hover) { _hover = hover; RequestFrame(); } }
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
        if (gesture is Gesture.Vertex or Gesture.VertexMarquee)
        {
            FinishPointGesture(gesture == Gesture.VertexMarquee); return;
        }
        if (gesture == Gesture.Guide && _guide is { } movedGuide)
        {
            var p = e.GetCurrentPoint(_canvas).Position;
            if ((movedGuide.Horizontal ? p.Y : p.X) < 20 || p.X < 0 || p.Y < 0 || p.X > _canvas.ActualWidth || p.Y > _canvas.ActualHeight)
                editor.Page.Guides.Remove(movedGuide);
        }
        if (gesture == Gesture.ImageCrop && CropTarget(out var cropped, out _)) ComponentService.SetAppearanceOverride(cropped, true, false);
        if (gesture == Gesture.Marquee && _marquee is { } box)
        {
            var ids = SelectionQuery.Marquee(editor.Page.Nodes, box, Keyboard.Control || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)).Select(n => n.Id);
            editor.Select(_marqueeBaseline.Concat(ids).Distinct());
        }
        if (gesture == Gesture.Create && _created is not null)
        {
            if (_created.Width < 3 && _created.Height < 3) { _created.Width = _created.Kind == NodeKind.Text ? 180 : 100; _created.Height = _created.Kind == NodeKind.Text ? 32 : 100; }
            var node = _created; _created = null; editor.CommitInteraction(); if (!KeepDrawingTool) editor.Tool = EditorTool.Move;
            if (node.Kind == NodeKind.Text) BeginTextEdit(node);
        }
        else if (gesture == Gesture.Pencil) FinishPath(false);
        else if (gesture is not Gesture.None and not Gesture.Pan and not Gesture.Marquee and not Gesture.PenControl) editor.CommitInteraction();
        _marquee = null; _guide = null; _snapLines = []; RequestFrame();
    }
    private void Wheel(object sender, PointerRoutedEventArgs e)
    {
        if (Session is not { } editor || IsPresenting) return;
        var point = e.GetCurrentPoint(_canvas); var delta = point.Properties.MouseWheelDelta;
        if (ZoomImageCrop(new(point.Position.X, point.Position.Y), delta)) { e.Handled = true; return; }
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)) editor.Viewport.ZoomAt(editor.Viewport.Zoom * Math.Exp(delta * .0015), new(point.Position.X, point.Position.Y));
        else if (point.Properties.IsHorizontalMouseWheel || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) editor.Viewport.Pan += new Vec2(delta * .65, 0);
        else editor.Viewport.Pan += new Vec2(0, delta * .65);
        editor.Notify(EditorChangeKind.Viewport); e.Handled = true;
    }
    public void CancelGesture()
    {
        _cropNodeId = null; _cropDocument = null; _pendingDuplicate = null; _gesture = Gesture.None; _created = null; _marquee = null; _snapLines = []; _guide = null; _penNode = null; _pathPreviewWorld = null; _vectorNode = null; _pointSelection.Clear(); _pointMarquee = null;
        Session?.CancelInteraction(); _canvas.ReleasePointerCaptures(); RequestFrame();
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
            editor.BeginInteraction(pencil ? "Draw freehand path" : "Draw vector path");
            var parent = DrawingTargetQuery.FindFrame(editor.Page.Nodes, world);
            var origin = parent?.WorldMatrix.Inverse.Map(world) ?? world;
            _penNode = new() { Kind = NodeKind.Path, Name = pencil ? "Pencil" : "Vector", X = origin.X, Y = origin.Y, Width = 1, Height = 1, Fills = [], Strokes = [new() { Color = "#333333", Width = 2 }], AbsolutePosition = parent is not null && parent.Layout.Direction != LayoutDirection.None };
            editor.AddNode(_penNode, parent); editor.Select(_penNode);
        }
        var local = _penNode.WorldMatrix.Inverse.Map(world);
        if (!pencil && _penNode.Points.Count >= 3 && _penNode.Points[0].Position.DistanceTo(local) * editor.Viewport.Zoom < 8) { FinishPath(true); return; }
        if (!pencil && Keyboard.Shift && _penNode.Points.Count > 0)
            local = _penNode.Points[^1].Position + DrawingGeometry.ConstrainAngle(local - _penNode.Points[^1].Position);
        _penNode.Points.Add(new() { Position = local }); _gesture = pencil ? Gesture.Pencil : Gesture.PenControl; editor.Preview();
    }
    public void FinishPath(bool closed) => CompletePath(closed, true);
    private void CompletePath(bool closed, bool returnToMove)
    {
        if (Session is not { } editor || _penNode is null) return;
        var node = _penNode; _penNode = null; _pathPreviewWorld = null; _gesture = Gesture.None;
        if (node.Points.Count < 2) { editor.CancelInteraction(); return; }
        node.Closed = closed;
        if (node.Name == "Pencil" && !closed) PathEditing.Simplify(node, FreehandTolerance / editor.Viewport.Zoom);
        using var path = SKPath.ParseSvgPathData(VectorPath.Build(node)); var bounds = path?.TightBounds ?? default;
        var offset = new Vec2(bounds.Left, bounds.Top);
        foreach (var point in node.Points) { point.Position -= offset; if (point.ControlIn.HasValue) point.ControlIn -= offset; if (point.ControlOut.HasValue) point.ControlOut -= offset; }
        node.X += offset.X; node.Y += offset.Y; node.Width = node.PathWidth = Math.Max(1, bounds.Width); node.Height = node.PathHeight = Math.Max(1, bounds.Height);
        editor.CommitInteraction(); if (returnToMove && !KeepDrawingTool) editor.Tool = EditorTool.Move;
    }
    public void BeginTextEdit(DesignNode node)
    {
        if (Session is not { } editor || node.IsEffectivelyLocked) return;
        EndVectorEdit(); FinishTextEdit(true); if (editor.IsInteracting) editor.CommitInteraction(); editor.Select(node); editor.BeginInteraction("Edit text"); _textNode = node;
        var bounds = node.WorldBounds; var topLeft = editor.Viewport.WorldToScreen(new(bounds.X, bounds.Y));
        var box = Studio.Input(node.Text, "Edit canvas text"); box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap; box.FontSize = Math.Max(8, node.FontSize * editor.Viewport.Zoom); box.Width = Math.Max(80, bounds.Width * editor.Viewport.Zoom + 12); box.Height = Math.Max(40, bounds.Height * editor.Viewport.Zoom + 12); box.Background = Studio.Brush("#FFFFFF"); box.BorderBrush = Studio.Brush(Studio.Accent); box.Padding = new(4); _textEditor = box;
        Canvas.SetLeft(box, topLeft.X - 4); Canvas.SetTop(box, topLeft.Y - 4); _overlay.Children.Add(box);
        // TextChanged is asynchronous; immediate Save/Enter can otherwise commit the
        // previous value. Synchronize model data without touching the visual tree here.
        box.TextChanging += (_, _) => { if (!ReferenceEquals(_textEditor, box) || _textNode is null) return; _textNode.Text = box.Text; ComponentService.SetOverride(_textNode, text: box.Text); };
        box.LostFocus += (_, _) => { if (!_finishingText) FinishTextEdit(true); };
        box.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { FinishTextEdit(false); e.Handled = true; } else if (e.Key == VirtualKey.Enter && Keyboard.Control) { FinishTextEdit(true); e.Handled = true; } };
        box.Focus(FocusState.Programmatic); box.SelectAll();
    }
    public void FinishTextEdit(bool commit)
    {
        if (_textEditor is null || _finishingText) return;
        _finishingText = true; var box = _textEditor;
        var returnFocus = box.FocusState != FocusState.Unfocused;
        if (commit && _textNode is { } node)
        {
            // Read the editor synchronously before teardown as a final commit barrier.
            node.Text = box.Text; ComponentService.SetOverride(node, text: box.Text);
        }
        _textEditor = null; _textNode = null; _overlay.Children.Remove(box);
        try { if (commit) Session?.CommitInteraction(); else Session?.CancelInteraction(); }
        finally
        {
            _finishingText = false;
            // Completing/canceling a focused editor must not leave keyboard input on
            // its removed text box. A normal LostFocus to an inspector keeps that focus.
            if (returnFocus && !_disposed) FocusCanvas();
            RequestFrame();
        }
    }
    public new void Dispose()
    {
        if (_disposed) return; FinishTextEdit(false); CancelGesture(); ExitPresentation(); _disposed = true; if (_session is not null) _session.Changed -= SessionChanged; Renderer.Dispose();
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
