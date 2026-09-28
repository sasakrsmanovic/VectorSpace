using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Absolute-baseline shape construction, independent of pointer frequency or UI backend.</summary>
public static class DrawingGeometry
{
    public static Vec2 ConstrainAngle(Vec2 vector, double increment = 45)
    {
        if (!vector.IsFinite || !double.IsFinite(increment) || increment <= 0) throw new ArgumentException("Invalid angular constraint.");
        var angle = Math.Atan2(vector.Y, vector.X) * 180 / Math.PI;
        var snapped = Math.Round(angle / increment, MidpointRounding.AwayFromZero) * increment * Math.PI / 180;
        var x = Math.Cos(snapped); var y = Math.Sin(snapped);
        // Cardinal directions remain exact rather than retaining floating-point trig residue.
        if (Math.Abs(x) < 1e-12) x = 0;
        if (Math.Abs(y) < 1e-12) y = 0;
        return new(x * vector.Length, y * vector.Length);
    }
    public static void Apply(DesignNode node, Vec2 start, Vec2 current, bool constrain, bool fromCenter)
    {
        if (!start.IsFinite || !current.IsFinite) throw new ArgumentException("Drawing coordinates must be finite.");
        var vector = current - start;
        var linear = node.Kind is NodeKind.Line or NodeKind.Arrow;
        if (constrain)
        {
            if (linear) vector = ConstrainAngle(vector);
            else
            {
                var size = Math.Max(Math.Abs(vector.X), Math.Abs(vector.Y));
                vector = new(vector.X < 0 ? -size : size, vector.Y < 0 ? -size : size);
            }
        }
        var a = fromCenter ? start - vector : start; var b = start + vector;
        var bounds = RectD.FromPoints(a, b);
        node.X = bounds.X; node.Y = bounds.Y; node.Width = Math.Max(1, bounds.Width); node.Height = Math.Max(1, bounds.Height);
        if (!linear) return;
        // Canonical SVG data expresses true zero-height/zero-width lines while keeping a nonzero
        // editor box. No new file-format fields, fake diagonal or accumulated angle correction.
        var origin = new Vec2(bounds.X, bounds.Y); a -= origin; b -= origin;
        string P(Vec2 p) => p.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " " + p.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var path = "M" + P(a) + " L" + P(b);
        if (node.Kind == NodeKind.Arrow && (b - a).Length > 1e-8)
        {
            var angle = Math.Atan2(b.Y - a.Y, b.X - a.X); var length = Math.Min(16, (b - a).Length * .25);
            var left = b - new Vec2(Math.Cos(angle - .5), Math.Sin(angle - .5)) * length;
            var right = b - new Vec2(Math.Cos(angle + .5), Math.Sin(angle + .5)) * length;
            path += " M" + P(left) + " L" + P(b) + " L" + P(right);
        }
        node.PathData = path; node.PathWidth = node.Width; node.PathHeight = node.Height;
        node.FlipX = node.FlipY = false;
    }
}
