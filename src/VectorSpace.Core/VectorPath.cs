using System.Text;

namespace VectorSpace.Core;

/// <summary>Shared SVG path construction used by Skia rendering and interchange.</summary>
public static class VectorPath
{
    public static string Build(DesignNode node)
    {
        if (node.IsBoolean) throw new InvalidOperationException("Use a renderer-aware export for live Boolean groups.");
        if (node.Commands is { } commands) return ShapePathSvg.Commands(commands);
        if (node.Kind == NodeKind.Ellipse && node.Arc is not null) return ShapePathSvg.Arc(node);
        if (ShapeGeometry.HasCorners(node) && node.Corners is not null) return ShapePathSvg.Corners(node);
        var w = node.Width; var h = node.Height;
        if (!string.IsNullOrWhiteSpace(node.PathData)) return node.PathData;
        if (node.Kind == NodeKind.Path && node.Contours is { } contours)
        {
            var result = new StringBuilder();
            foreach (var contour in contours) AppendContour(result, contour.Points, contour.Closed);
            return result.ToString();
        }
        if (node.Kind == NodeKind.Path && node.Points.Count > 0)
        {
            var result = new StringBuilder("M" + P(node.Points[0].Position));
            for (var i = 1; i < node.Points.Count; i++) Segment(result, node.Points[i - 1], node.Points[i]);
            if (node.Closed) { Segment(result, node.Points[^1], node.Points[0]); result.Append('Z'); }
            return result.ToString();
        }
        if (node.Kind == NodeKind.Ellipse) return $"M0 {N(h / 2)} A{N(w / 2)} {N(h / 2)} 0 1 0 {N(w)} {N(h / 2)} A{N(w / 2)} {N(h / 2)} 0 1 0 0 {N(h / 2)} Z";
        if (node.Kind is NodeKind.Line or NodeKind.Arrow)
        {
            var path = $"M0 0 L{N(w)} {N(h)}";
            if (node.Kind == NodeKind.Arrow)
            {
                var angle = Math.Atan2(h, w); var length = Math.Min(16, Math.Sqrt(w * w + h * h) * .25);
                path += $" M{N(w - length * Math.Cos(angle - .5))} {N(h - length * Math.Sin(angle - .5))} L{N(w)} {N(h)} L{N(w - length * Math.Cos(angle + .5))} {N(h - length * Math.Sin(angle + .5))}";
            }
            return path;
        }
        if (node.Kind is NodeKind.Polygon or NodeKind.Star)
        {
            var count = Math.Clamp(node.Sides, 3, 128) * (node.Kind == NodeKind.Star ? 2 : 1); var result = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                var angle = -Math.PI / 2 + i * Math.PI * 2 / count; var ratio = node.Kind == NodeKind.Star && i % 2 == 1 ? node.StarRatio : 1;
                result.Append(i == 0 ? 'M' : 'L').Append(P(new(w / 2 + Math.Cos(angle) * w / 2 * ratio, h / 2 + Math.Sin(angle) * h / 2 * ratio)));
            }
            return result.Append('Z').ToString();
        }
        var r = Math.Clamp(node.CornerRadius, 0, Math.Min(w, h) / 2);
        if (r < .001) return $"M0 0 H{N(w)} V{N(h)} H0 Z";
        return $"M{N(r)} 0 H{N(w - r)} A{N(r)} {N(r)} 0 0 1 {N(w)} {N(r)} V{N(h - r)} A{N(r)} {N(r)} 0 0 1 {N(w - r)} {N(h)} H{N(r)} A{N(r)} {N(r)} 0 0 1 0 {N(h - r)} V{N(r)} A{N(r)} {N(r)} 0 0 1 {N(r)} 0 Z";
    }
    private static void AppendContour(StringBuilder builder, IReadOnlyList<PathPoint> points, bool closed)
    {
        if (points.Count == 0) return;
        builder.Append('M').Append(P(points[0].Position));
        for (var i = 1; i < points.Count; i++) Segment(builder, points[i - 1], points[i]);
        if (closed) { Segment(builder, points[^1], points[0]); builder.Append('Z'); }
    }
    private static void Segment(StringBuilder builder, PathPoint a, PathPoint b)
    {
        if (a.ControlOut.HasValue || b.ControlIn.HasValue) builder.Append('C').Append(P(a.ControlOut ?? a.Position)).Append(' ').Append(P(b.ControlIn ?? b.Position)).Append(' ').Append(P(b.Position));
        else builder.Append('L').Append(P(b.Position));
    }
    private static string N(double n) => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    private static string P(Vec2 p) => N(p.X) + " " + N(p.Y);
}
