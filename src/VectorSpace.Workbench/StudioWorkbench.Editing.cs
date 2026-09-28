using VectorSpace.Skia;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private void BuildEditingInspector(DesignNode? node)
    {
        if (Surface.IsVectorEditing && node is not null)
        {
            var points = AddSection("Vector editing");
            points.Body.Children.Add(Studio.Text($"{Surface.SelectedPointIndices.Count} of {node.Points.Count} anchors selected", 11, Studio.Muted));
            points.Body.Children.Add(Studio.Columns((new StudioButton("Select all points", Surface.SelectAllPoints), -1), (new StudioButton("Done editing", Surface.EndVectorEdit), -1)));
            points.Body.Children.Add(Studio.Columns((new StudioButton("Corner", () => Run(() => Surface.SetPointTangents(TangentMode.Corner))), -1), (new StudioButton("Smooth", () => Run(() => Surface.SetPointTangents(TangentMode.Smooth))), -1), (new StudioButton("Mirrored", () => Run(() => Surface.SetPointTangents(TangentMode.Mirrored))), -1)));
            points.Body.Children.Add(new StudioButton("Split selected segments", () => Run(Surface.SplitSelectedSegments)));
            points.Body.Children.Add(new StudioButton("Delete selected anchors", () => Run(Surface.DeleteSelectedPoints)));
            points.Body.Children.Add(Studio.Columns((new StudioButton("Reverse path", () => Run(Surface.ReversePath)), -1), (new StudioButton(node.Closed ? "Open path" : "Close path", () => Run(Surface.TogglePathClosed)), -1)));
            points.Body.Children.Add(new StudioButton("Simplify freehand", () => Run(Surface.SimplifyPath)));
            points.Body.Children.Add(Wrapped("Click a segment to insert an anchor. Shift-click or drag a box to select points. Drag handles; Alt frees the opposite handle. Shift constrains angles. B smooths selected points; Alt+B removes handles.", 10));
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
