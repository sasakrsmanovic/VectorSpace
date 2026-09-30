using System.Globalization;
using System.Text;

namespace VectorSpace.Core;

/// <summary>Portable SVG geometry. Native rational conics remain exact in the document/renderer;
/// SVG converts them adaptively to cubics (0.0005 local-unit sampled tolerance, bounded depth).</summary>
public static class ShapePathSvg
{
    private static string N(double n) => n.ToString("R", CultureInfo.InvariantCulture);
    private static string P(Vec2 p) => N(p.X) + " " + N(p.Y);
    public static string Corners(DesignNode node)
    {
        var r = node.EffectiveCorners; var w = node.Width; var h = node.Height;
        var b = new StringBuilder("M" + N(r.TopLeft) + " 0 H" + N(w - r.TopRight));
        Corner(r.TopRight, new(w, r.TopRight)); b.Append('V').Append(N(h - r.BottomRight));
        Corner(r.BottomRight, new(w - r.BottomRight, h)); b.Append('H').Append(N(r.BottomLeft));
        Corner(r.BottomLeft, new(0, h - r.BottomLeft)); b.Append('V').Append(N(r.TopLeft));
        Corner(r.TopLeft, new(r.TopLeft, 0)); return b.Append('Z').ToString();
        void Corner(double radius, Vec2 end)
        {
            if (radius <= 0) b.Append('L').Append(P(end));
            else b.Append('A').Append(N(radius)).Append(' ').Append(N(radius)).Append(" 0 0 1 ").Append(P(end));
        }
    }
    public static string Arc(DesignNode node)
    {
        var arc = node.Arc ?? new(); var start = arc.StartDegrees % 360; var sweep = Math.Clamp(arc.SweepDegrees, -360, 360);
        var radius = arc.InnerRadius; var full = Math.Abs(sweep) >= 360 - 1e-9;
        if (node.Width <= 0 || node.Height <= 0 || Math.Abs(sweep) < 1e-12 || radius >= 1 && !arc.Open) return "";
        var b = new StringBuilder("M" + P(ShapeGeometry.ArcPoint(node, start)));
        Segment(start, sweep, 1);
        if (arc.Open) return b.ToString();
        if (full)
        {
            b.Append('Z');
            if (radius > 0) { b.Append('M').Append(P(ShapeGeometry.ArcPoint(node, start, radius))); Segment(start, -sweep, radius); b.Append('Z'); }
        }
        else if (radius > 0)
        {
            b.Append('L').Append(P(ShapeGeometry.ArcPoint(node, start + sweep, radius))); Segment(start + sweep, -sweep, radius); b.Append('Z');
        }
        else b.Append('L').Append(P(new(node.Width / 2, node.Height / 2))).Append('Z');
        return b.ToString();
        void Segment(double from, double amount, double ratio)
        {
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(amount) / 180));
            for (var i = 1; i <= steps; i++)
                b.Append('A').Append(N(node.Width / 2 * ratio)).Append(' ').Append(N(node.Height / 2 * ratio))
                    .Append(" 0 0 ").Append(amount > 0 ? '1' : '0').Append(' ').Append(P(ShapeGeometry.ArcPoint(node, from + amount * i / steps, ratio)));
        }
    }
    public static string Commands(IReadOnlyList<PathCommand> commands)
    {
        var b = new StringBuilder(); var position = Vec2.Zero; var contour = Vec2.Zero;
        foreach (var c in commands)
        {
            switch (c.Verb)
            {
                case PathVerb.Move: b.Append('M').Append(P(c.Point)); contour = c.Point; break;
                case PathVerb.Line: b.Append('L').Append(P(c.Point)); break;
                case PathVerb.Quadratic: b.Append('Q').Append(P(c.Control1)).Append(' ').Append(P(c.Point)); break;
                case PathVerb.Cubic: Cubic(c.Control1, c.Control2, c.Point); break;
                case PathVerb.Conic: ConicApproximation.AppendCubics(position, c.Control1, c.Point, c.Weight, curve => Cubic(curve.Control1, curve.Control2, curve.Point)); break;
                case PathVerb.Close: b.Append('Z'); position = contour; continue;
                default: throw new ArgumentOutOfRangeException(nameof(commands));
            }
            position = c.Point;
        }
        return b.ToString();
        void Cubic(Vec2 c1, Vec2 c2, Vec2 end)
        {
            if (b.Length > 32 * 1024 * 1024) throw new InvalidOperationException("SVG path exceeds the export budget.");
            b.Append('C').Append(P(c1)).Append(' ').Append(P(c2)).Append(' ').Append(P(end));
        }
    }
}
