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
                case PathVerb.Conic: Conic(position, c.Control1, c.Point, c.Weight, 0, 1, 0); break;
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
        void Conic(Vec2 a, Vec2 c, Vec2 z, double w, double t0, double t1, int depth)
        {
            (Vec2 Value, Vec2 Derivative) Evaluate(double t)
            {
                var u = 1 - t; var d = u * u + 2 * w * t * u + t * t;
                var numerator = a * (u * u) + c * (2 * w * t * u) + z * (t * t);
                var nd = a * (-2 * u) + c * (2 * w * (1 - 2 * t)) + z * (2 * t);
                var dd = -2 * u + 2 * w * (1 - 2 * t) + 2 * t;
                return (numerator / d, (nd * d - numerator * dd) / (d * d));
            }
            var left = Evaluate(t0); var right = Evaluate(t1); var h = (t1 - t0) / 3;
            var c1 = left.Value + left.Derivative * h; var c2 = right.Value - right.Derivative * h;
            var error = 0d;
            for (var i = 1; i <= 3; i++)
            {
                var t = i / 4d; var u = 1 - t;
                var cubic = left.Value * (u * u * u) + c1 * (3 * u * u * t) + c2 * (3 * u * t * t) + right.Value * (t * t * t);
                error = Math.Max(error, cubic.DistanceTo(Evaluate(t0 + (t1 - t0) * t).Value));
            }
            if (error <= .0005) { Cubic(c1, c2, right.Value); return; }
            if (depth >= 16) throw new InvalidOperationException("A rational curve exceeds the bounded SVG approximation tolerance.");
            var middle = (t0 + t1) / 2;
            Conic(a, c, z, w, t0, middle, depth + 1); Conic(a, c, z, w, middle, t1, depth + 1);
        }
    }
}
