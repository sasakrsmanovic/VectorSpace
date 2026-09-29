using System.Text.Json.Serialization;

namespace VectorSpace.Core;

public enum StrokeAlignment { Center, Inside, Outside }
public enum StrokeCap { Butt, Round, Square }
public enum StrokeJoin { Miter, Round, Bevel }
public enum BooleanKind { Union, Subtract, Intersect, Exclude }
public enum PathFillRule { NonZero, EvenOdd }

/// <summary>Authored radii, clockwise from the top-left. Rendering clamps each value to half
/// the shorter edge without changing the authored values when a layer is resized.</summary>
public sealed record CornerRadii(double TopLeft = 0, double TopRight = 0, double BottomRight = 0, double BottomLeft = 0)
{
    public CornerRadii Clamp(double width, double height)
    {
        var limit = Math.Max(0, Math.Min(width, height) / 2);
        return new(Math.Clamp(TopLeft, 0, limit), Math.Clamp(TopRight, 0, limit), Math.Clamp(BottomRight, 0, limit), Math.Clamp(BottomLeft, 0, limit));
    }
    public double At(int index) => index switch { 0 => TopLeft, 1 => TopRight, 2 => BottomRight, 3 => BottomLeft, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public CornerRadii With(int index, double value) => index switch
    {
        0 => this with { TopLeft = value }, 1 => this with { TopRight = value },
        2 => this with { BottomRight = value }, 3 => this with { BottomLeft = value },
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

/// <summary>Clockwise angles in degrees from the positive X axis. Null Arc means a full ellipse.
/// Signed sweeps allow reversal; Open renders only the outer arc instead of a wedge/ring sector.</summary>
public sealed record EllipseArc(double StartDegrees = 0, double SweepDegrees = 360, double InnerRadius = 0, bool Open = false);

public sealed partial class StrokeStyle
{
    public StrokeAlignment Alignment { get; set; }
    // Preserve pre-schema-6 round-cap/round-join rendering for older documents.
    public StrokeCap Cap { get; set; } = StrokeCap.Round;
    public StrokeJoin Join { get; set; } = StrokeJoin.Round;
    public double MiterLimit { get; set; } = 4;
    public double DashOffset { get; set; }
}

public sealed partial class DesignNode
{
    public CornerRadii? Corners { get; set; }
    public EllipseArc? Arc { get; set; }
    /// <summary>A Group with a Boolean operation retains its editable operand children.</summary>
    public BooleanKind? Boolean { get; set; }
    public PathFillRule FillRule { get; set; }
    /// <summary>Lossless native contour commands, including rational conics. Mutually exclusive
    /// with Points and PathData. Used by flatten/outline instead of a lossy SVG intermediate.</summary>
    public List<PathCommand>? Commands { get; set; }
    [JsonIgnore] public bool IsBoolean => Kind == NodeKind.Group && Boolean.HasValue;
    [JsonIgnore] public CornerRadii EffectiveCorners => (Corners ?? new(CornerRadius, CornerRadius, CornerRadius, CornerRadius)).Clamp(Width, Height);
}

public enum PathVerb { Move, Line, Quadratic, Conic, Cubic, Close }
public readonly record struct PathCommand(PathVerb Verb, Vec2 Point = default, Vec2 Control1 = default, Vec2 Control2 = default, double Weight = 1);

public static class ShapeGeometry
{
    public static bool HasCorners(DesignNode n) => !n.IsBoolean && n.Kind is NodeKind.Rectangle or NodeKind.Frame or NodeKind.Component or NodeKind.Instance or NodeKind.Section or NodeKind.ComponentSet or NodeKind.Group;
    public static bool IsOperand(DesignNode n) => n.IsBoolean || n.Kind is NodeKind.Rectangle or NodeKind.Ellipse or NodeKind.Polygon or NodeKind.Star or NodeKind.Path;
    public static bool ContainsCornerBox(DesignNode n, Vec2 p)
    {
        if (!n.LocalBounds.Contains(p)) return false;
        if (n.Corners is null && n.CornerRadius <= 0) return true;
        var r = n.EffectiveCorners;
        var radius = p.X < n.Width / 2 ? p.Y < n.Height / 2 ? r.TopLeft : r.BottomLeft : p.Y < n.Height / 2 ? r.TopRight : r.BottomRight;
        var cx = p.X < n.Width / 2 ? radius : n.Width - radius;
        var cy = p.Y < n.Height / 2 ? radius : n.Height - radius;
        if (radius == 0 || Math.Abs(p.X - cx) <= 1e-12 || Math.Abs(p.Y - cy) <= 1e-12) return true;
        var cornerX = p.X < radius || p.X > n.Width - radius;
        var cornerY = p.Y < radius || p.Y > n.Height - radius;
        return !cornerX || !cornerY || (p - new Vec2(cx, cy)).Length <= radius;
    }
    public static Vec2 ArcPoint(DesignNode n, double degrees, double radius = 1)
    {
        var a = degrees * Math.PI / 180;
        return new(n.Width / 2 * (1 + Math.Cos(a) * radius), n.Height / 2 * (1 + Math.Sin(a) * radius));
    }
    public static double Angle(DesignNode n, Vec2 local)
        => Math.Atan2((local.Y - n.Height / 2) / Math.Max(n.Height / 2, 1e-9), (local.X - n.Width / 2) / Math.Max(n.Width / 2, 1e-9)) * 180 / Math.PI;
    public static double WrappedDelta(double degrees) => degrees - Math.Floor((degrees + 180) / 360) * 360;
    public static double StrokeOutset(DesignNode node)
    {
        var outset = 0d;
        foreach (var s in node.Strokes)
        {
            if (!s.Visible || s.Width <= 0) continue;
            // Conservative for mixed/open contours and nonuniformly scaled strokes.
            var limit = s.Join == StrokeJoin.Miter ? Math.Max(1, s.MiterLimit) : 1;
            outset = Math.Max(outset, s.Width * limit);
        }
        return outset;
    }
}
