using VectorSpace.Skia;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private void BuildEditingInspector(DesignNode? node)
    {
        if (Surface.IsVectorEditing && node is not null)
        {
            var points = AddSection("Vector editing");
            var controls = new VectorEditingControl(new(Surface.SelectedPointIndices.Count, Surface.VectorAnchorCount,
                Surface.VectorContourCount, Surface.ActiveContourIndex, Surface.ActiveContourClosed, node.FillRule == PathFillRule.EvenOdd,
                Surface.CanCutAnchor, Surface.CanJoinEndpoints));
            controls.Command += command => Run(() => ExecuteVectorCommand(command));
            controls.FillRuleChanged += evenOdd => Run(() => Surface.SetPathFillRule(evenOdd ? PathFillRule.EvenOdd : PathFillRule.NonZero));
            points.Body.Children.Add(controls);
            points.Body.Children.Add(Wrapped("Shift-click or box-select across contours. Click a segment to insert. X cuts at an anchor; Ctrl+J joins endpoints. Alt frees handles; Shift constrains. Enter finishes.", 10));
        }
        else if (node is not null && EditablePathConversion.Supports(node))
        {
            var editing = AddSection("Vector tools");
            editing.Body.Children.Add(new StudioButton("Edit vector points", () => Run(Surface.BeginVectorEdit)));
        }
        if (Session.Tool is not EditorTool.Move and not EditorTool.Scale || node is null)
        {
            var options = AddSection("Tool settings");
            options.Body.Children.Add(Check("Keep drawing tool", Surface.KeepDrawingTool, v => Surface.KeepDrawingTool = v));
            options.Body.Children.Add(Number("Pencil tolerance", Surface.FreehandTolerance, v => Surface.FreehandTolerance = v, 0, 8));
            options.Body.Children.Add(Wrapped("Shift constrains shapes and line angles; Alt draws from the center. While dragging a polygon/star, Up/Down changes its sides; Alt+Up/Down changes the star ratio. Up/Down adjusts rectangle corners.", 10));
        }
    }
    private void ExecuteVectorCommand(VectorEditCommand command)
    {
        switch (command)
        {
            case VectorEditCommand.SelectAll: Surface.SelectAllPoints(); break;
            case VectorEditCommand.Done: Surface.EndVectorEdit(); break;
            case VectorEditCommand.Corner: Surface.SetPointTangents(TangentMode.Corner); break;
            case VectorEditCommand.Smooth: Surface.SetPointTangents(TangentMode.Smooth); break;
            case VectorEditCommand.Mirrored: Surface.SetPointTangents(TangentMode.Mirrored); break;
            case VectorEditCommand.Subdivide: Surface.SplitSelectedSegments(); break;
            case VectorEditCommand.DeleteAnchors: Surface.DeleteSelectedPoints(); break;
            case VectorEditCommand.Reverse: Surface.ReversePath(); break;
            case VectorEditCommand.ToggleClosed: Surface.TogglePathClosed(); break;
            case VectorEditCommand.Simplify: Surface.SimplifyPath(); break;
            case VectorEditCommand.PreviousContour: Surface.SelectContour(-1); break;
            case VectorEditCommand.NextContour: Surface.SelectContour(1); break;
            case VectorEditCommand.SelectContour: Surface.SelectContour(); break;
            case VectorEditCommand.Cut: Surface.CutSelectedAnchor(); break;
            case VectorEditCommand.Join: Surface.JoinSelectedEndpoints(); break;
            case VectorEditCommand.DeleteContour: Surface.DeleteActiveContour(); break;
            default: throw new ArgumentOutOfRangeException(nameof(command));
        }
    }
    private IEnumerable<QuickAction> EditingActions()
    {
        foreach (var tool in Enum.GetValues<EditorTool>())
            yield return new("Tool: " + tool, "", () => { Surface.FinishTextEdit(true); Session.Tool = tool; Surface.FocusCanvas(); });
        yield return new("Edit vector points", "Enter", () => Run(Surface.BeginVectorEdit));
        yield return new("Finish vector editing", "Enter", Surface.EndVectorEdit);
        yield return new("Select all points", "Ctrl A", Surface.SelectAllPoints);
        yield return new("Split selected segments", "", () => Run(Surface.SplitSelectedSegments));
        yield return new("Smooth points", "B", () => Run(() => Surface.SetPointTangents(TangentMode.Smooth)));
        yield return new("Corner points", "Alt B", () => Run(() => Surface.SetPointTangents(TangentMode.Corner)));
        yield return new("Select contour", "", () => Run(() => Surface.SelectContour()));
        yield return new("Next contour", "", () => Run(() => Surface.SelectContour(1)));
        yield return new("Previous contour", "", () => Run(() => Surface.SelectContour(-1)));
        yield return new("Cut at selected anchor", "X", () => Run(Surface.CutSelectedAnchor));
        yield return new("Join selected endpoints", "Ctrl J", () => Run(Surface.JoinSelectedEndpoints));
        yield return new("Delete contour", "", () => Run(Surface.DeleteActiveContour));
        yield return new("Reverse path", "", () => Run(Surface.ReversePath));
        yield return new("Simplify freehand", "", () => Run(Surface.SimplifyPath));
        yield return new("Flip horizontal", "Shift H", () => Run(() => Session.FlipSelection(true)));
        yield return new("Flip vertical", "Shift V", () => Run(() => Session.FlipSelection(false)));
        yield return new("Rotate clockwise 90°", "", () => Run(() => Session.RotateSelection(90)));
        yield return new("Rotate counterclockwise 90°", "", () => Run(() => Session.RotateSelection(-90)));
        yield return new("Distribute horizontally", "", () => Run(() => Session.Distribute(true)));
        yield return new("Distribute vertically", "", () => Run(() => Session.Distribute(false)));
        yield return new("Space horizontally 16", "", () => Run(() => Session.SpaceSelection(true, 16)));
        yield return new("Space vertically 16", "", () => Run(() => Session.SpaceSelection(false, 16)));
        yield return new("Paste in place", "Ctrl Shift V", () => RunAsync(() => PasteAsync(true)));
    }
}
