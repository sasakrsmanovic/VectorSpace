using VectorSpace.Skia;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    private readonly HashSet<int> _pointSelection = [];
    private PathTopology? _pointTopology;
    private PathPointDrag? _pointDrag;
    private int _activeContour;
    private int[] _pointMarqueeBaseline = [];
    private PathTopology Topology => _pointTopology ??= new PathTopology(_vectorNode!);
    public int VectorAnchorCount => _vectorNode is null ? 0 : Topology.Points.Count;
    public int VectorContourCount => _vectorNode is null ? 0 : Topology.Contours.Count;
    public int ActiveContourIndex => _activeContour;
    public bool ActiveContourClosed => _vectorNode is not null && Topology.Contours[_activeContour].Closed;
    public bool CanCutAnchor => _vectorNode is not null && _pointSelection.Count == 1 &&
        (Topology.Contours[Topology.ContourIndex(_pointSelection.First())].Closed || !Topology.IsEndpoint(_pointSelection.First()));
    public bool CanJoinEndpoints => _vectorNode is not null && _pointSelection.Count == 2 && _pointSelection.All(Topology.IsEndpoint) &&
        (_pointSelection.Select(Topology.ContourIndex).Distinct().Count() == 2 || Topology.Contours[Topology.ContourIndex(_pointSelection.First())].Count >= 3);
    private RectD? _pointMarquee;
    private int _pointClickSelection = -1;
    private bool _pointDragStarted;
    private int[] _pointMarqueeOriginal = [];
    private string? _vectorPageId;
    public bool IsVectorEditing => _vectorNode is not null;
    public IReadOnlyCollection<int> SelectedPointIndices => _pointSelection.ToArray();
    public event Action? VectorSelectionChanged;

    public void BeginVectorEdit()
    {
        if (Session is not { Primary: { } node } editor || IsPresenting) return;
        if (editor.IsInteracting) CancelGesture();
        FinishTextEdit(true);
        EndShapeEdit();
        node = editor.Document.Find(node.Id) ?? throw new InvalidOperationException("The selected layer no longer exists.");
        if (node.IsEffectivelyLocked) throw new InvalidOperationException("Unlock the layer before editing its points.");
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (parent.Kind == NodeKind.Instance) throw new InvalidOperationException("Detach the instance before editing its vector geometry.");
        EditablePathConversion.Convert(editor, Renderer, node);
        editor.Tool = EditorTool.Move; editor.Select(node);
        _vectorNode = node; _vectorPageId = editor.Page.Id; _pointSelection.Clear(); _pointTopology = new(node); _activeContour = 0;
        _cropNodeId = null; _cropDocument = null;
        PointSelectionChanged(); FocusCanvas();
        StatusChanged?.Invoke("Edit vector · Shift-click multiple anchors · click a segment to split · drag a box to select points · Enter to finish");
    }
    public void EndVectorEdit()
    {
        if (_gesture is Gesture.Vertex or Gesture.VertexMarquee) CancelPointGesture();
        _vectorNode = null; _vectorPageId = null; _pointSelection.Clear(); _pointTopology = null; _pointDrag = null; _pointMarquee = null;
        PointSelectionChanged();
    }
    private void ValidateVectorTarget()
    {
        if (_vectorNode is null) return;
        var node = Session?.Document.Find(_vectorNode.Id);
        if (Session?.Page.Id != _vectorPageId || Session?.Primary?.Id != node?.Id || node is null || !PathEditing.CanEdit(node) || node.IsEffectivelyLocked)
        { _vectorNode = null; _pointTopology = null; _pointDrag = null; _pointSelection.Clear(); _pointMarquee = null; }
        else
        {
            _vectorNode = node;
            if (_pointTopology?.Matches(node) != true) _pointTopology = new(node);
            _activeContour = Math.Clamp(_activeContour, 0, Topology.Contours.Count - 1);
            _pointSelection.RemoveWhere(i => i < 0 || i >= Topology.Points.Count);
        }
    }
    private void PointSelectionChanged() { VectorSelectionChanged?.Invoke(); RequestFrame(); }
    public void SelectAllPoints()
    {
        if (_gesture is Gesture.Vertex or Gesture.VertexMarquee) CancelPointGesture();
        if (_vectorNode is null) return;
        _pointSelection.Clear(); for (var i = 0; i < Topology.Points.Count; i++) _pointSelection.Add(i);
        PointSelectionChanged();
    }
    public void SetPointTangents(TangentMode mode) => EditPoints("Change point tangents", node => PathEditing.SetTangents(node, _pointSelection, mode));
    private int[] AffectedContours() => _pointSelection.Count == 0 ? [_activeContour] : _pointSelection.Select(Topology.ContourIndex).Distinct().Order().ToArray();
    public void ReversePath() => EditPoints("Reverse contour", node =>
    {
        var topology = Topology; var contours = AffectedContours().ToHashSet();
        var indices = _pointSelection.Select(i => { var c = topology.Contours[topology.ContourIndex(i)]; return contours.Contains(topology.ContourIndex(i)) ? c.Offset + c.Count - 1 - (i - c.Offset) : i; }).ToArray();
        ContourEditing.Reverse(node, contours); _pointSelection.Clear(); _pointSelection.UnionWith(indices);
    });
    public void TogglePathClosed() => EditPoints("Toggle contour closure", node => ContourEditing.SetClosed(node, AffectedContours(), !Topology.Contours[_activeContour].Closed));
    public void SelectContour(int step = 0)
    {
        if (_gesture is Gesture.Vertex or Gesture.VertexMarquee) CancelPointGesture();
        ValidateVectorTarget(); if (_vectorNode is null) return;
        _activeContour = (_activeContour + step % Topology.Contours.Count + Topology.Contours.Count) % Topology.Contours.Count;
        var contour = Topology.Contours[_activeContour]; _pointSelection.Clear();
        for (var i = contour.Offset; i < contour.Offset + contour.Count; i++) _pointSelection.Add(i);
        PointSelectionChanged(); FocusCanvas();
    }
    public void CutSelectedAnchor() => EditPoints("Cut contour at anchor", node =>
    {
        if (_pointSelection.Count != 1) throw new InvalidOperationException("Select one anchor to cut.");
        ContourEditing.Cut(node, _pointSelection.First()); _pointSelection.Clear();
    });
    public void JoinSelectedEndpoints() => EditPoints("Join contour endpoints", node =>
    {
        if (_pointSelection.Count != 2) throw new InvalidOperationException("Select two open endpoints to join.");
        var points = _pointSelection.Order().ToArray(); ContourEditing.Join(node, points[0], points[1]); _pointSelection.Clear();
    });
    public void DeleteActiveContour() => EditPoints("Delete contour", node => { ContourEditing.Remove(node, _activeContour); _pointSelection.Clear(); });
    public void SetPathFillRule(PathFillRule rule) => EditPoints("Change vector fill rule", node =>
    {
        if (!Enum.IsDefined(rule)) throw new ArgumentOutOfRangeException(nameof(rule)); node.FillRule = rule;
    });
    public void DeleteSelectedPoints() => EditPoints("Delete anchors", node =>
    {
        if (_pointSelection.Count == 0) return;
        PathEditing.Delete(node, _pointSelection); _pointSelection.Clear();
    });
    public void SplitSelectedSegments() => EditPoints("Split selected segments", node =>
    {
        var segments = Topology.Segments.Where(s => _pointSelection.Contains(s.Start) && _pointSelection.Contains(s.End)).Select(s => s.Start).Reverse().ToArray();
        if (segments.Length == 0) throw new InvalidOperationException("Select both ends of a segment first.");
        if (segments.Length > PathTopology.MaxAnchors - Topology.Points.Count) throw new InvalidOperationException("Subdivision exceeds the editable anchor budget.");
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
        editor.Edit(label, () => change(node)); _pointTopology = null; ValidateVectorTarget(); PointSelectionChanged(); FocusCanvas();
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
            var point = Topology.Points[i];
            if (point.ControlIn is { } a && Topology.Previous(i) >= 0 && Session.Viewport.WorldToScreen(matrix.Map(a)).DistanceTo(screen) <= 7) { handle = -1; return i; }
            if (point.ControlOut is { } b && Topology.Next(i) >= 0 && Session.Viewport.WorldToScreen(matrix.Map(b)).DistanceTo(screen) <= 7) { handle = 1; return i; }
        }
        for (var i = 0; i < Topology.Points.Count; i++)
            if (Session.Viewport.WorldToScreen(matrix.Map(Topology.Points[i].Position)).DistanceTo(screen) <= 8) return i;
        return -1;
    }
    private bool PressVectorEdit(Vec2 screen, Vec2 world, bool shift)
    {
        ValidateVectorTarget();
        if (_vectorNode is not { } node || Session is not { } editor || editor.Tool != EditorTool.Move) return false;
        _pointClickSelection = -1; _pointDragStarted = false;
        var index = HitPoint(screen, out _controlHandle);
        if (index >= 0)
        {
            if (_controlHandle == 0)
            {
                if (shift && _pointSelection.Remove(index)) { PointSelectionChanged(); return true; }
                // Preserve a multi-selection for dragging, but collapse a plain click on release.
                if (!shift && _pointSelection.Count > 1 && _pointSelection.Contains(index)) _pointClickSelection = index;
                if (!shift && !_pointSelection.Contains(index)) _pointSelection.Clear();
                _pointSelection.Add(index);
            }
            editor.BeginInteraction(_controlHandle == 0 ? "Move anchors" : "Move Bézier handle");
        }
        else
        {
            var matrix = PathEditing.PointToWorld(node) * Matrix2D.Scale(editor.Viewport.Zoom, editor.Viewport.Zoom) * Matrix2D.Translation(editor.Viewport.Pan.X, editor.Viewport.Pan.Y);
            var distance = 6d; var segment = -1; var split = .5;
            foreach (var edge in Topology.Segments)
            {
                var i = edge.Start; var curve = Topology.Curve(i).Transform(matrix);
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
                editor.BeginInteraction("Insert and move anchor"); index = PathEditing.Insert(node, segment, split); _pointTopology = new(node);
                _pointSelection.Clear(); _pointSelection.Add(index);
            }
            else
            {
                _pointMarqueeOriginal = _pointSelection.ToArray();
                _pointMarqueeBaseline = shift ? _pointMarqueeOriginal : [];
                _pointMarquee = new(screen.X, screen.Y, 0, 0); _gesture = Gesture.VertexMarquee;
                if (!shift) _pointSelection.Clear(); PointSelectionChanged(); return true;
            }
        }
        _vertexIndex = index; _gesture = Gesture.Vertex; _activeContour = Topology.ContourIndex(index);
        _pointDrag = new(node, Topology, _controlHandle == 0 ? _pointSelection : [index], _controlHandle);
        PointSelectionChanged(); return true;
    }
    private void MoveVectorEdit(Vec2 screen, Vec2 world, bool shift, bool alt)
    {
        if (_vectorNode is not { } node || Session is not { } editor) return;
        if (_gesture == Gesture.VertexMarquee)
        {
            _pointMarquee = RectD.FromPoints(_startScreen, screen); _pointSelection.Clear(); _pointSelection.UnionWith(_pointMarqueeBaseline);
            var transform = PathEditing.PointToWorld(node);
            for (var i = 0; i < Topology.Points.Count; i++) if (_pointMarquee.Value.Contains(editor.Viewport.WorldToScreen(transform.Map(Topology.Points[i].Position)))) _pointSelection.Add(i);
            RequestFrame(); return;
        }
        if (_gesture != Gesture.Vertex || _pointDrag is null) return;
        if (!_pointDragStarted && screen.DistanceTo(_startScreen) < 3) return;
        _pointDragStarted = true; _pointClickSelection = -1;
        _pointDrag.Apply(world - _startWorld, shift, alt);
        editor.Preview(false);
    }
    private void CancelPointGesture()
    {
        if (_gesture == Gesture.VertexMarquee)
        { _pointSelection.Clear(); _pointSelection.UnionWith(_pointMarqueeOriginal); }
        _gesture = Gesture.None; _pointMarquee = null; _pointClickSelection = -1; _pointDragStarted = false;
        _pointDrag = null; _pointTopology = null;
        Session?.CancelInteraction(); _canvas.ReleasePointerCaptures(); ValidateVectorTarget(); PointSelectionChanged();
    }
    private void FinishPointGesture(bool marquee)
    {
        if (!marquee)
        {
            Session?.CommitInteraction();
            if (_pointClickSelection >= 0 && !_pointDragStarted)
            { _pointSelection.Clear(); _pointSelection.Add(_pointClickSelection); }
        }
        _pointClickSelection = -1; _pointDragStarted = false;
        _pointTopology = null; _pointDrag = null; _pointMarquee = null; _pointMarqueeOriginal = [];
        ValidateVectorTarget(); PointSelectionChanged();
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
            if (key is VirtualKey.Z or VirtualKey.Y && _gesture is Gesture.Vertex or Gesture.VertexMarquee)
            {
                CancelPointGesture(); return true;
            }
            if (key == VirtualKey.A) { SelectAllPoints(); return true; }
            if (key == VirtualKey.J) { JoinSelectedEndpoints(); return true; }
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
            case VirtualKey.X: CutSelectedAnchor(); break;
            case VirtualKey.B: SetPointTangents(alt ? TangentMode.Corner : TangentMode.Smooth); break;
            default: return false;
        }
        return true;
    }
}
