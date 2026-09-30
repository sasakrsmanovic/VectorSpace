namespace VectorSpace.Core;

/// <summary>Bounded rational-quadratic to cubic conversion. Endpoint values/tangents are matched;
/// quarter, midpoint and three-quarter samples must meet the stated local-unit tolerance.
/// This is a sampled approximation, not a global-error proof or a native lossless representation.</summary>
public static class ConicApproximation
{
    public const double DefaultTolerance = .0005;
    public static void AppendCubics(Vec2 start, Vec2 control, Vec2 end, double weight, Action<PathCommand> append,
        double tolerance = DefaultTolerance, int maximumSegments = 65_536)
    {
        ArgumentNullException.ThrowIfNull(append);
        if (!start.IsFinite || !control.IsFinite || !end.IsFinite || !double.IsFinite(weight) || weight <= 0)
            throw new ArgumentException("A rational curve requires finite points and a positive finite weight.");
        if (!double.IsFinite(tolerance) || tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (maximumSegments is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(maximumSegments));
        var emitted = 0; Subdivide(0, 1, 0);
        (Vec2 Value, Vec2 Derivative) Evaluate(double t)
        {
            var u = 1 - t; var d = u * u + 2 * weight * t * u + t * t;
            var n = start * (u * u) + control * (2 * weight * t * u) + end * (t * t);
            var nd = start * (-2 * u) + control * (2 * weight * (1 - 2 * t)) + end * (2 * t);
            var dd = -2 * u + 2 * weight * (1 - 2 * t) + 2 * t;
            return (n / d, (nd * d - n * dd) / (d * d));
        }
        void Subdivide(double t0, double t1, int depth)
        {
            var a = Evaluate(t0); var b = Evaluate(t1); var h = (t1 - t0) / 3;
            var c1 = a.Value + a.Derivative * h; var c2 = b.Value - b.Derivative * h; var error = 0d;
            for (var i = 1; i <= 3; i++)
            {
                var t = i / 4d; var u = 1 - t;
                var value = a.Value * (u * u * u) + c1 * (3 * u * u * t) + c2 * (3 * u * t * t) + b.Value * (t * t * t);
                error = Math.Max(error, value.DistanceTo(Evaluate(t0 + (t1 - t0) * t).Value));
            }
            if (error <= tolerance)
            {
                if (++emitted > maximumSegments) throw new InvalidOperationException("Rational curve conversion exceeds its segment budget.");
                append(new(PathVerb.Cubic, b.Value, c1, c2)); return;
            }
            if (depth >= 16) throw new InvalidOperationException("A rational curve exceeds the bounded cubic approximation tolerance.");
            var middle = (t0 + t1) / 2; Subdivide(t0, middle, depth + 1); Subdivide(middle, t1, depth + 1);
        }
    }
}
