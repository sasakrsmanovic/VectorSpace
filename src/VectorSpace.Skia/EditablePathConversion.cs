using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Editing;

namespace VectorSpace.Skia;

/// <summary>Converts native/SVG geometry into editable cubic contours without losing contour
/// boundaries, winding, identity or placement. Quadratics elevate exactly. Rational conics use the
/// shared bounded, sampled .0005 local-unit approximation; undo restores the exact source commands.</summary>
public static class EditablePathConversion
{
    public static bool Supports(DesignNode node) => !node.IsContainer && node.Kind is not NodeKind.Text and not NodeKind.Slice && !node.IsEffectivelyLocked;
    public static void Convert(EditorSession editor, SceneRenderer renderer, DesignNode node)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(renderer); ArgumentNullException.ThrowIfNull(node);
        if (!Supports(node)) throw new InvalidOperationException("Choose an unlocked vector layer.");
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (parent.Kind == NodeKind.Instance) throw new InvalidOperationException("Detach the instance before editing its vector geometry.");
        if (PathEditing.CanEdit(node)) return;
        var contours = new List<PathContour>(); List<PathPoint>? points = null; var closed = false; var total = 0;
        using var iterator = renderer.Geometry(node).CreateRawIterator(); Span<SKPoint> data = stackalloc SKPoint[4];
        while (true)
        {
            var verb = iterator.Next(data); if (verb == SKPathVerb.Done) break;
            switch (verb)
            {
                case SKPathVerb.Move: Finish(); points = []; Add(new() { Position = V(data[0]) }); break;
                case SKPathVerb.Line: Add(new() { Position = V(data[1]) }); break;
                case SKPathVerb.Quad:
                    var a = V(data[0]); var b = V(data[1]); var c = V(data[2]);
                    Cubic(new(PathVerb.Cubic, c, a + (b - a) * (2d / 3), c + (b - c) * (2d / 3))); break;
                case SKPathVerb.Cubic: Cubic(new(PathVerb.Cubic, V(data[3]), V(data[1]), V(data[2]))); break;
                case SKPathVerb.Conic: ConicApproximation.AppendCubics(V(data[0]), V(data[1]), V(data[2]), iterator.ConicWeight(), Cubic); break;
                case SKPathVerb.Close: closed = true; break;
                default: throw new InvalidOperationException("The native path contains an unsupported command.");
            }
        }
        Finish();
        if (contours.Count == 0) throw new InvalidOperationException("This layer has no editable contour.");
        editor.Edit("Convert to editable path", () =>
        {
            // Line/arrow layers permit zero and subpixel extents; editable paths use
            // a nonzero layout frame. Retain the captured geometry and local transform
            // rather than letting layout inflate/rotate it around a new center.
            var width = FrameExtent(node.Width, node.MinWidth, node.MaxWidth);
            var height = FrameExtent(node.Height, node.MinHeight, node.MaxHeight);
            if (width != node.Width || height != node.Height)
            {
                var local = node.LocalMatrix;
                node.Width = width; node.Height = height;
                var next = node.LocalMatrix;
                node.X += local.DX - next.DX; node.Y += local.DY - next.DY;
            }
            // Open ellipse arcs suppress their fill stack. Clearing the arc modifier
            // must not turn the converted open path's implicit chord into visible fill.
            // Retain the authored paints as hidden values rather than discarding them.
            if (node.Kind == NodeKind.Ellipse && node.Arc?.Open == true)
                foreach (var fill in node.Fills) fill.Visible = false;
            node.Kind = NodeKind.Path; node.Commands = null; node.Arc = null; node.Corners = null; node.PathData = null;
            node.PathWidth = node.Width; node.PathHeight = node.Height;
            if (contours.Count == 1) { node.Points = contours[0].Points; node.Closed = contours[0].Closed; node.Contours = null; }
            else { node.Points = []; node.Closed = false; node.Contours = contours; }
        });
        void Add(PathPoint point)
        {
            if (points is null || closed) throw new InvalidOperationException("The contour must start with a Move before adding anchors.");
            if (++total > PathTopology.MaxAnchors) throw new InvalidOperationException("Editable conversion is limited to 100,000 anchors.");
            points.Add(point);
        }
        void Cubic(PathCommand curve)
        {
            if (points is not { Count: > 0 }) throw new InvalidOperationException("A curve needs a starting anchor.");
            points[^1].ControlOut = curve.Control1; Add(new() { Position = curve.Point, ControlIn = curve.Control2 });
        }
        void Finish()
        {
            if (points is null) return;
            if (closed && points.Count > 1 && points[0].Position.DistanceTo(points[^1].Position) < 1e-7)
            { points[0].ControlIn = points[^1].ControlIn; points.RemoveAt(points.Count - 1); total--; }
            if (points.Count < 2) throw new InvalidOperationException("A contour has fewer than two anchors; the original geometry is preserved unchanged.");
            if (contours.Count >= PathTopology.MaxContours) throw new InvalidOperationException("Editable conversion is limited to 10,000 contours.");
            contours.Add(new() { Points = points, Closed = closed }); points = null; closed = false;
        }
    }
    private static double FrameExtent(double value, double minimum, double maximum)
    {
        var min = Math.Max(1, minimum); return Math.Clamp(value, min, Math.Max(min, maximum));
    }
    private static Vec2 V(SKPoint p) => new(p.X, p.Y);
}
