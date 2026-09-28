using VectorSpace.Skia;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    private readonly HashSet<int> _pointSelection = [];
    private readonly record struct PointBaseline(Vec2 Position, Vec2? Incoming, Vec2? Outgoing);
    private PointBaseline[] _pointBaseline = [];
    private int[] _movingPoints = [], _pointMarqueeBaseline = [];
    private Matrix2D _pointTransform;
    private RectD? _pointMarquee;
    private string? _vectorPageId;
    public bool IsVectorEditing => _vectorNode is not null;
    public IReadOnlyCollection<int> SelectedPointIndices => _pointSelection.ToArray();
    public event Action? VectorSelectionChanged;

    public void BeginVectorEdit()
    {
        if (Session is not { Primary: { } node } editor || IsPresenting) return;
        if (editor.IsInteracting) CancelGesture();
        FinishTextEdit(true);
        node = editor.Document.Find(node.Id) ?? throw new InvalidOperationException("The selected layer no longer exists.");
        if (node.IsEffectivelyLocked) throw new InvalidOperationException("Unlock the layer before editing its points.");
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (parent.Kind == NodeKind.Instance) throw new InvalidOperationException("Detach the instance before editing its vector geometry.");
        EditablePathConversion.Convert(editor, Renderer, node);
        editor.Tool = EditorTool.Move; editor.Select(node);
        _vectorNode = node; _vectorPageId = editor.Page.Id; _pointSelection.Clear();
        _cropNodeId = null; _cropDocument = null;
        PointSelectionChanged(); FocusCanvas();
        StatusChanged?.Invoke("Edit vector · Shift-click multiple anchors · click a segment to split · drag a box to select points · Enter to finish");
    }
    public void EndVectorEdit()
    {
        if (_gesture is Gesture.Vertex or Gesture.VertexMarquee) CancelPointGesture();
        _vectorNode = null; _vectorPageId = null; _pointSelection.Clear(); _pointBaseline = []; _movingPoints = []; _pointMarquee = null;
        PointSelectionChanged();
    }
    private void ValidateVectorTarget()
    {
        if (_vectorNode is null) return;
        var node = Session?.Document.Find(_vectorNode.Id);
        if (Session?.Page.Id != _vectorPageId || Session?.Primary?.Id != node?.Id || node is null || !PathEditing.CanEdit(node) || node.IsEffectivelyLocked)
        { _vectorNode = null; _pointSelection.Clear(); _pointMarquee = null; }
        else { _vectorNode = node; _pointSelection.RemoveWhere(i => i >= node.Points.Count); }
    }
    private void PointSelectionChanged() { VectorSelectionChanged?.Invoke(); RequestFrame(); }
    public void SelectAllPoints()
    {
        if (_vectorNode is null) return;
        _pointSelection.Clear(); for (var i = 0; i < _vectorNode.Points.Count; i++) _pointSelection.Add(i);
        PointSelectionChanged();
    }
    public void SetPointTangents(TangentMode mode) => EditPoints("Change point tangents", node => PathEditing.SetTangents(node, _pointSelection, mode));
    public void ReversePath() => EditPoints("Reverse path", node =>
    {
        var indices = _pointSelection.Select(i => node.Points.Count - 1 - i).ToArray();
        PathEditing.Reverse(node); _pointSelection.Clear(); _pointSelection.UnionWith(indices);
    });
    public void TogglePathClosed() => EditPoints("Toggle closed path", node =>
    {
        if (node.Points.Count < 3 && !node.Closed) throw new InvalidOperationException("A closed path needs at least three anchors.");
        node.Closed = !node.Closed;
    });
    public void DeleteSelectedPoints() => EditPoints("Delete anchors", node =>
    {
        if (_pointSelection.Count == 0) return;
        PathEditing.Delete(node, _pointSelection); _pointSelection.Clear();
    });
    public void SplitSelectedSegments() => EditPoints("Split selected segments", node =>
    {
        var segments = Enumerable.Range(0, node.Points.Count - (node.Closed ? 0 : 1))
            .Where(i => _pointSelection.Contains(i) && _pointSelection.Contains((i + 1) % node.Points.Count)).Reverse().ToArray();
        if (segments.Length == 0) throw new InvalidOperationException("Select both ends of a segment first.");
        foreach (var i in segments) PathEditing.Insert(node, i, .5);
        _pointSelection.Clear();
    });
    public void SimplifyPath() => EditPoints("Simplify freehand", node =>
    {
        var transform = PathEditing.PointToWorld(node);
        var scale = Math.Max(transform.Map(new Vec2(1, 0)).DistanceTo(transform.Map(Vec2.Zero)), transform.Map(new Vec2(0, 1)).DistanceTo(transform.Map(Vec2.Zero)));
        PathEditing.Simplify(node, .75 / Math.Max(1e-9, scale * Session!.Viewport.Zoom)); _pointSelection.Clear();
    });
    private void EditPoints(string label, Action<DesignNode> change)
    {
        ValidateVectorTarget();
        if (_vectorNode is not { } node || Session is not { } editor) return;
        if (editor.IsInteracting) CancelPointGesture();
        node = _vectorNode!;
        if (node is null) return;
        editor.Edit(label, () => change(node)); PointSelectionChanged(); FocusCanvas();
    }
    public void NudgePoints(Vec2 worldDelta) => EditPoints("Nudge anchors", node =>
    {
        var inverse = PathEditing.PointToWorld(node).Inverse;
        PathEditing.Move(node, _pointSelection, inverse.Map(worldDelta) - inverse.Map(Vec2.Zero));
    });
    private int HitPoint(Vec2 screen, out int handle)
    {
        handle = 0; if (_vectorNode is null || Session is null) return -1;
        var matrix = PathEditing.PointToWorld(_vectorNode);
        // Visible selected handles take precedence over anchors, consistently with the overlay.
        foreach (var i in _pointSelection)
        {
            var point = _vectorNode.Points[i];
            if (point.ControlIn is { } a && Session.Viewport.WorldToScreen(matrix.Map(a)).DistanceTo(screen) <= 7) { handle = -1; return i; }
            if (point.ControlOut is { } b && Session.Viewport.WorldToScreen(matrix.Map(b)).DistanceTo(screen) <= 7) { handle = 1; return i; }
        }
        for (var i = 0; i < _vectorNode.Points.Count; i++)
            if (Session.Viewport.WorldToScreen(matrix.Map(_vectorNode.Points[i].Position)).DistanceTo(screen) <= 8) return i;
        return -1;
    }
    private bool PressVectorEdit(Vec2 screen, Vec2 world, bool shift)
    {
        ValidateVectorTarget();
        if (_vectorNode is not { } node || Session is not { } editor || editor.Tool != EditorTool.Move) return false;
        var index = HitPoint(screen, out _controlHandle);
        if (index >= 0)
        {
            if (_controlHandle == 0)
            {
                if (shift && _pointSelection.Remove(index)) { PointSelectionChanged(); return true; }
                if (!shift && !_pointSelection.Contains(index)) _pointSelection.Clear();
                _pointSelection.Add(index);
            }
            editor.BeginInteraction(_controlHandle == 0 ? "Move anchors" : "Move Bézier handle");
        }
        else
        {
            var matrix = PathEditing.PointToWorld(node) * Matrix2D.Scale(editor.Viewport.Zoom, editor.Viewport.Zoom) * Matrix2D.Translation(editor.Viewport.Pan.X, editor.Viewport.Pan.Y);
            var distance = 6d; var segment = -1; var split = .5;
            for (var i = 0; i < node.Points.Count - (node.Closed ? 0 : 1); i++)
            {
                var curve = PathEditing.Segment(node, i).Transform(matrix);
                // Bounding-box rejection avoids 24 evaluations for distant segments.
                var minX = Math.Min(Math.Min(curve.A.X, curve.B.X), Math.Min(curve.C.X, curve.D.X)) - distance;
                var maxX = Math.Max(Math.Max(curve.A.X, curve.B.X), Math.Max(curve.C.X, curve.D.X)) + distance;
                var minY = Math.Min(Math.Min(curve.A.Y, curve.B.Y), Math.Min(curve.C.Y, curve.D.Y)) - distance;
                var maxY = Math.Max(Math.Max(curve.A.Y, curve.B.Y), Math.Max(curve.C.Y, curve.D.Y)) + distance;
                if (screen.X < minX || screen.X > maxX || screen.Y < minY || screen.Y > maxY) continue;
                var nearest = curve.Closest(screen);
                if (nearest.Distance < distance && nearest.T > .001 && nearest.T < .999) { distance = nearest.Distance; segment = i; split = nearest.T; }
            }
            if (segment >= 0)
            {
                editor.BeginInteraction("Insert and move anchor"); index = PathEditing.Insert(node, segment, split);
                _pointSelection.Clear(); _pointSelection.Add(index);
            }
            else
            {
                _pointMarqueeBaseline = shift ? _pointSelection.ToArray() : [];
                _pointMarquee = new(screen.X, screen.Y, 0, 0); _gesture = Gesture.VertexMarquee;
                if (!shift) _pointSelection.Clear(); PointSelectionChanged(); return true;
            }
        }
        _vertexIndex = index; _gesture = Gesture.Vertex;
        _pointTransform = PathEditing.PointToWorld(node);
        _pointBaseline = node.Points.Select(p => new PointBaseline(p.Position, p.ControlIn, p.ControlOut)).ToArray();
        _movingPoints = _pointSelection.Order().ToArray(); PointSelectionChanged(); return true;
    }
    private void MoveVectorEdit(Vec2 screen, Vec2 world, bool shift, bool alt)
    {
        if (_vectorNode is not { } node || Session is not { } editor) return;
        if (_gesture == Gesture.VertexMarquee)
        {
            _pointMarquee = RectD.FromPoints(_startScreen, screen); _pointSelection.Clear(); _pointSelection.UnionWith(_pointMarqueeBaseline);
            var transform = PathEditing.PointToWorld(node);
            for (var i = 0; i < node.Points.Count; i++) if (_pointMarquee.Value.Contains(editor.Viewport.WorldToScreen(transform.Map(node.Points[i].Position)))) _pointSelection.Add(i);
            RequestFrame(); return;
        }
        if (_gesture != Gesture.Vertex || _pointBaseline.Length != node.Points.Count) return;
       
        var inverse = _pointTransform.Inverse;
        var delta = world - _startWorld;
        if (shift && _controlHandle == 0) delta = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? new(delta.X, 0) : new(0, delta.Y);
        var local = inverse.Map(delta) - inverse.Map(Vec2.Zero);
        if (_controlHandle == 0)
        {
            foreach (var i in _movingPoints)
            {
                var p = node.Points[i]; var baseline = _pointBaseline[i]; p.Position = baseline.Position + local;
                p.ControlIn = baseline.Incoming + local; p.ControlOut = baseline.Outgoing + local;
            }
        }
        else
        {
            var p = node.Points[_vertexIndex]; var baseline = _pointBaseline[_vertexIndex]; p.ControlIn = baseline.Incoming; p.ControlOut = baseline.Outgoing;
            var original = (_controlHandle < 0 ? baseline.Incoming : baseline.Outgoing) ?? baseline.Position;
            var position = original + local;
            if (shift) position = p.Position + DrawingGeometry.ConstrainAngle(position - p.Position);
            PathEditing.MoveHandle(p, _controlHandle < 0, position, alt);
        }
        editor.Preview(false);
    }
    private void CancelPointGesture()
    {
        _gesture = Gesture.None; _pointMarquee = null;
        Session?.CancelInteraction(); _canvas.ReleasePointerCaptures(); ValidateVectorTarget(); PointSelectionChanged();
    }
    public bool HandlePointKey(VirtualKey key, bool control, bool shift, bool alt)
    {
        if (!IsVectorEditing) return false;
        if (key is VirtualKey.Escape or VirtualKey.Enter)
        {
            if (_gesture is Gesture.Vertex or Gesture.VertexMarquee) CancelPointGesture(); else EndVectorEdit();
            return true;
        }
        if (control)
        {
            if (key == VirtualKey.A) { SelectAllPoints(); return true; }
            if (key == VirtualKey.Z) { if (shift) Session?.Redo(); else Session?.Undo(); PointSelectionChanged(); return true; }
            if (key == VirtualKey.Y) { Session?.Redo(); PointSelectionChanged(); return true; }
            return false;
        }
        var step = shift ? 10 : 1;
        switch (key)
        {
            case VirtualKey.Left: NudgePoints(new(-step, 0)); break;
            case VirtualKey.Right: NudgePoints(new(step, 0)); break;
            case VirtualKey.Up: NudgePoints(new(0, -step)); break;
            case VirtualKey.Down: NudgePoints(new(0, step)); break;
            case VirtualKey.Delete: case VirtualKey.Back: DeleteSelectedPoints(); break;
            case VirtualKey.B: SetPointTangents(alt ? TangentMode.Corner : TangentMode.Smooth); break;
            default: return false;
        }
        return true;
    }
}
