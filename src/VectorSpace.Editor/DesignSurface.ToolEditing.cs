namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    public bool KeepDrawingTool { get; set; }
    private double _freehandTolerance = .65;
    /// <summary>Simplification error in screen pixels when a Pencil stroke completes; zero disables it.</summary>
    public double FreehandTolerance
    {
        get => _freehandTolerance;
        set
        {
            if (!double.IsFinite(value) || value < 0 || value > 8) throw new ArgumentOutOfRangeException(nameof(value));
            _freehandTolerance = value;
        }
    }
    /// <summary>Resolves live geometry before serializing or invoking a separate editing command.</summary>
    public void CommitPendingEdits()
    {
        FinishTextEdit(true);
        if (_penNode is not null) FinishPath(false);
        if (_gesture == Gesture.ImageCrop) EndImageCrop();
        if (Session?.IsInteracting == true) Session.CommitInteraction();
        _gesture = Gesture.None; _canvas.ReleasePointerCaptures();
    }
    public bool HandleToolKey(VirtualKey key, bool control, bool shift, bool alt)
    {
        if (_penNode is not null)
        {
            if (key == VirtualKey.Enter) { FinishPath(false); return true; }
            if (key == VirtualKey.Escape) { CancelGesture(); return true; }
            if (control && key == VirtualKey.Z) { CancelGesture(); return true; }
            if (key is VirtualKey.Back or VirtualKey.Delete)
            {
                if (_penNode.Points.Count <= 1) CancelGesture();
                else { _penNode.Points.RemoveAt(_penNode.Points.Count - 1); Session?.Preview(false); }
                return true;
            }
        }
        // Undo/redo must end capture as well as restoring the document. Otherwise a
        // following PointerMoved can reapply a canceled baseline outside history.
        // Point mode owns its cancellation so it can retain the active contour.
        if (control && key is VirtualKey.Z or VirtualKey.Y && Session?.IsInteracting == true &&
            _gesture is not Gesture.None and not Gesture.Vertex and not Gesture.VertexMarquee)
        {
            CancelGesture(); return true;
        }
        if (_gesture == Gesture.Create && _created is { } node)
        {
            if (key == VirtualKey.Escape) { CancelGesture(); return true; }
            if (key is VirtualKey.Up or VirtualKey.Down)
            {
                var direction = key == VirtualKey.Up ? 1 : -1;
                if (node.Kind is NodeKind.Polygon or NodeKind.Star)
                {
                    if (alt && node.Kind == NodeKind.Star) node.StarRatio = Math.Clamp(node.StarRatio + direction * .05, .01, 1);
                    else node.Sides = Math.Clamp(node.Sides + direction, 3, 128);
                }
                else if (node.Kind == NodeKind.Rectangle) node.CornerRadius = Math.Clamp(node.CornerRadius + direction * (shift ? 10 : 1), 0, Math.Min(node.Width, node.Height) / 2);
                Session?.Preview(false); return true;
            }
        }
        return false;
    }
    private bool PressGuide(Vec2 screen, Vec2 world)
    {
        if (Session is not { Tool: EditorTool.Move, RulersVisible: true } editor || IsVectorEditing) return false;
        var nearest = editor.Page.Guides.Where(g => Math.Abs((g.Horizontal ? world.Y : world.X) - g.Position) * editor.Viewport.Zoom <= 4)
            .OrderBy(g => Math.Abs((g.Horizontal ? world.Y : world.X) - g.Position)).FirstOrDefault();
        if (nearest is null) return false;
        editor.BeginInteraction("Move guide"); _guide = nearest; _gesture = Gesture.Guide; return true;
    }
}
