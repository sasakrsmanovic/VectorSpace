using System.Globalization;
using VectorSpace.Skia;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private void BuildShapeInspector(DesignNode node)
    {
        if (node.IsBoolean)
        {
            var section = AddSection("Boolean operation");
            section.Body.Children.Add(Studio.Choice(Enum.GetNames<BooleanKind>(), node.Boolean!.Value.ToString(), value => Run(() => LiveBooleanOperations.SetOperation(Session, Enum.Parse<BooleanKind>(value))), "Boolean operation"));
            section.Body.Children.Add(Wrapped($"{node.Children.Count} editable operands · first layer is the subtraction base", 10));
            section.Body.Children.Add(Studio.Columns((new StudioButton("Flatten result", () => Run(() => LiveBooleanOperations.Flatten(Session, Surface.Renderer))), -1), (new StudioButton("Release operands", () => Run(() => LiveBooleanOperations.Release(Session))), -1)));
        }
        if (ShapeGeometry.HasCorners(node))
        {
            var section = AddSection("Corner radius"); var r = node.Corners ?? new(node.CornerRadius, node.CornerRadius, node.CornerRadius, node.CornerRadius);
            var control = new CornerOptionsControl(new(node.Corners is not null, r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft)) { IsEnabled = ShapeEditable(node) };
            control.ValueCommitted += value => Run(() => Session.Edit("Edit shape corners", () =>
            {
                foreach (var n in Session.SelectionRoots.Where(n => ShapeGeometry.HasCorners(n) && ShapeEditable(n)))
                {
                    n.CornerRadius = value.TopLeft;
                    n.Corners = value.Independent ? new(value.TopLeft, value.TopRight, value.BottomRight, value.BottomLeft) : null;
                    n.VariableBindings.Remove(VariableTarget.CornerRadius);
                }
            }));
            section.Body.Children.Add(control);
            ShapeButton(section, node);
        }
        if (node.Kind == NodeKind.Ellipse)
        {
            var section = AddSection("Arc"); var arc = node.Arc ?? new();
            var control = new ArcOptionsControl(new(arc.StartDegrees, arc.SweepDegrees, arc.InnerRadius, arc.Open)) { IsEnabled = ShapeEditable(node) };
            control.ValueCommitted += value => Run(() => Session.Edit("Edit ellipse arc", () =>
            {
                foreach (var n in Session.SelectionRoots.Where(n => n.Kind == NodeKind.Ellipse && ShapeEditable(n)))
                    n.Arc = new(value.Start, value.Sweep, value.InnerRadius, value.Open);
            }));
            section.Body.Children.Add(control);
            section.Body.Children.Add(new StudioButton("Reset to ellipse", () => Run(() => Session.Edit("Reset ellipse", () =>
            {
                foreach (var n in Session.SelectionRoots.Where(n => n.Kind == NodeKind.Ellipse && ShapeEditable(n))) n.Arc = null;
            }))) { IsEnabled = ShapeEditable(node) });
            ShapeButton(section, node);
        }
    }
    private void ShapeButton(InspectorSection section, DesignNode node)
    {
        section.Body.Children.Add(new StudioButton(Surface.IsShapeEditing ? "Done shape editing" : "Edit shape on canvas", () => Run(() =>
        {
            if (Surface.IsShapeEditing) Surface.EndShapeEdit(); else Surface.BeginShapeEdit();
            RefreshInspector();
        })) { IsEnabled = ShapeEditable(node) && Session.SelectionRoots.Count == 1 });
        if (!ShapeEditable(node)) section.Body.Children.Add(Wrapped("Edit the main component or detach this instance before changing shape geometry.", 10));
    }
    private static bool ShapeEditable(DesignNode node)
    {
        if (node.IsEffectivelyLocked) return false;
        for (var n = node; n is not null; n = n.Parent) if (n.Kind == NodeKind.Instance) return false;
        return true;
    }
    private void BuildStrokeGeometryControls(InspectorSection section, DesignNode node, int index)
    {
        var stroke = node.Strokes[index];
        var values = new StrokeOptions((int)stroke.Alignment, (int)stroke.Cap, (int)stroke.Join, stroke.MiterLimit, stroke.DashOffset, string.Join(", ", stroke.Dashes.Select(d => d.ToString("R", CultureInfo.InvariantCulture))));
        var control = new StrokeOptionsControl(values, node.Kind != NodeKind.Text && NativeShapeGeometry.IsClosed(Surface.Renderer.Geometry(node)));
        control.ValueCommitted += value => Change("Stroke geometry", n =>
        {
            if (index >= n.Strokes.Count) return;
            var s = n.Strokes[index]; s.Alignment = (StrokeAlignment)value.Alignment; s.Cap = (StrokeCap)value.Cap; s.Join = (StrokeJoin)value.Join;
            s.MiterLimit = value.MiterLimit; s.DashOffset = value.DashOffset;
            s.Dashes = value.Dashes.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToList();
        });
        section.Body.Children.Add(control);
        if (index == node.Strokes.Count - 1 && (ShapeGeometry.IsOperand(node) || node.Kind is NodeKind.Line or NodeKind.Arrow))
            section.Body.Children.Add(new StudioButton("Outline stroke", () => Run(() => LiveBooleanOperations.Outline(Session, Surface.Renderer))) { IsEnabled = ShapeEditable(node) });
    }
    private IEnumerable<QuickAction> ShapeActions()
    {
        yield return new("Edit shape on canvas", "", () => Run(Surface.BeginShapeEdit));
        yield return new("Finish shape editing", "Enter", () => Run(Surface.EndShapeEdit));
        foreach (var op in Enum.GetValues<BooleanKind>())
            yield return new(op + " shapes (live)", "", () => Run(() => LiveBooleanOperations.Create(Session, Surface.Renderer, op)));
        yield return new("Flatten Boolean result", "", () => Run(() => LiveBooleanOperations.Flatten(Session, Surface.Renderer)));
        yield return new("Release Boolean operands", "", () => Run(() => LiveBooleanOperations.Release(Session)));
        yield return new("Outline stroke", "", () => Run(() => LiveBooleanOperations.Outline(Session, Surface.Renderer)));
        yield return new("Shape playground", "", () => RunAsync(async () =>
        {
            if (await ConfirmAsync("Open shape playground?", "Save your current design before replacing it with the editable arc, corner and stroke studies."))
            { Session.Load(ShapeSample.Create()); Surface.Fit(firstFrame: true); }
        }));
    }
}
