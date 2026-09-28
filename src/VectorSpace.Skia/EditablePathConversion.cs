using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Editing;

namespace VectorSpace.Skia;

/// <summary>Converts one contour to editable cubic anchors, retaining local placement and paints.
/// Quadratics are degree-elevated exactly; rational conics use sixteen quadratic pieces.
/// Multiple contours are rejected atomically, preserving holes and disjoint SVG geometry.</summary>
public static class EditablePathConversion
{
    public static bool Supports(DesignNode node) => !node.IsContainer && node.Kind is not NodeKind.Text and not NodeKind.Slice && !node.IsEffectivelyLocked;

    public static void Convert(EditorSession editor, SceneRenderer renderer, DesignNode node)
    {
        if (!Supports(node)) throw new InvalidOperationException("Choose an unlocked vector shape or single-contour path.");
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (parent.Kind == NodeKind.Instance) throw new InvalidOperationException("Detach the instance before editing its vector geometry.");
        if (PathEditing.CanEdit(node)) return;
        var points = new List<PathPoint>(); var closed = false;
        using var iterator = renderer.Geometry(node).CreateRawIterator();
        Span<SKPoint> data = stackalloc SKPoint[4];
        while (true)
        {
            var verb = iterator.Next(data);
            if (verb == SKPathVerb.Done) break;
            if (points.Count > 100_000) throw new InvalidOperationException("Editable conversion is limited to 100,000 anchors.");
            switch (verb)
            {
                case SKPathVerb.Move:
                    if (points.Count != 0) throw new InvalidOperationException("This path has multiple contours. It is preserved unchanged; single-contour conversion is required.");
                    points.Add(new() { Position = V(data[0]) }); break;
                case SKPathVerb.Line: points.Add(new() { Position = V(data[1]) }); break;
                case SKPathVerb.Quad: Quadratic(V(data[0]), V(data[1]), V(data[2])); break;
                case SKPathVerb.Cubic:
                    points[^1].ControlOut = V(data[1]); points.Add(new() { Position = V(data[3]), ControlIn = V(data[2]) }); break;
                case SKPathVerb.Conic:
                    var quads = SKPath.ConvertConicToQuads(data[0], data[1], data[2], iterator.ConicWeight(), 4);
                    for (var i = 0; i + 2 < quads.Length; i += 2) Quadratic(V(quads[i]), V(quads[i + 1]), V(quads[i + 2]));
                    break;
                case SKPathVerb.Close: closed = true; break;
            }
        }
        if (closed && points.Count > 1 && points[0].Position.DistanceTo(points[^1].Position) < 1e-7)
        { points[0].ControlIn = points[^1].ControlIn; points.RemoveAt(points.Count - 1); }
        if (points.Count < 2) throw new InvalidOperationException("This layer has no editable contour.");
        editor.Edit("Convert to editable path", () =>
        {
            node.Kind = NodeKind.Path; node.Points = points; node.PathData = null; node.PathWidth = node.Width; node.PathHeight = node.Height; node.Closed = closed;
        });
        void Quadratic(Vec2 a, Vec2 b, Vec2 c)
        {
            points[^1].ControlOut = a + (b - a) * (2d / 3);
            points.Add(new() { Position = c, ControlIn = c + (b - c) * (2d / 3) });
        }
    }
    private static Vec2 V(SKPoint p) => new(p.X, p.Y);
}
