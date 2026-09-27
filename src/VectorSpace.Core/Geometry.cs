using System.Globalization;

namespace VectorSpace.Core;

public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 Zero => default;
    public double Length => Math.Sqrt(X * X + Y * Y);
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);
    public double DistanceTo(Vec2 p) => (this - p).Length;
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public Vec2 Center => new(X + Width / 2, Y + Height / 2);
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public bool Contains(Vec2 p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
    public bool Intersects(RectD r) => Right >= r.X && r.Right >= X && Bottom >= r.Y && r.Bottom >= Y;
    public RectD Inflate(double amount) => new(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);
    public static RectD FromPoints(Vec2 a, Vec2 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    public static RectD Union(RectD a, RectD b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Right, b.Right) - Math.Min(a.X, b.X), Math.Max(a.Bottom, b.Bottom) - Math.Min(a.Y, b.Y));
    public static RectD Bounds(IEnumerable<Vec2> points)
    {
        var list = points.ToArray();
        if (list.Length == 0) return default;
        var minX = list.Min(p => p.X); var minY = list.Min(p => p.Y);
        return new(minX, minY, list.Max(p => p.X) - minX, list.Max(p => p.Y) - minY);
    }
}

/// <summary>Double-precision affine transform, using row-vector composition: p * a * b.</summary>
public readonly record struct Matrix2D(double M11, double M12, double M21, double M22, double DX, double DY)
{
    public static Matrix2D Identity => new(1, 0, 0, 1, 0, 0);
    public static Matrix2D Translation(double x, double y) => new(1, 0, 0, 1, x, y);
    public static Matrix2D Scale(double x, double y) => new(x, 0, 0, y, 0, 0);
    public static Matrix2D Rotation(double degrees)
    {
        var a = degrees * Math.PI / 180; var c = Math.Cos(a); var s = Math.Sin(a);
        return new(c, s, -s, c, 0, 0);
    }
    public Vec2 Map(Vec2 p) => new(p.X * M11 + p.Y * M21 + DX, p.X * M12 + p.Y * M22 + DY);
    public RectD Map(RectD r)
    {
        var a = Map(new Vec2(r.X, r.Y)); var b = Map(new Vec2(r.Right, r.Y));
        var c = Map(new Vec2(r.Right, r.Bottom)); var d = Map(new Vec2(r.X, r.Bottom));
        var x = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)); var y = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        return new(x, y, Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) - x, Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) - y);
    }
    public bool TryInvert(out Matrix2D inverse)
    {
        var d = M11 * M22 - M12 * M21;
        if (!double.IsFinite(d) || Math.Abs(d) < 1e-12) { inverse = Identity; return false; }
        inverse = new(M22 / d, -M12 / d, -M21 / d, M11 / d, (M21 * DY - M22 * DX) / d, (M12 * DX - M11 * DY) / d);
        return true;
    }
    public Matrix2D Inverse => TryInvert(out var m) ? m : throw new InvalidOperationException("The transform is singular.");
    public static Matrix2D operator *(Matrix2D a, Matrix2D b) => new(
        a.M11 * b.M11 + a.M12 * b.M21, a.M11 * b.M12 + a.M12 * b.M22,
        a.M21 * b.M11 + a.M22 * b.M21, a.M21 * b.M12 + a.M22 * b.M22,
        a.DX * b.M11 + a.DY * b.M21 + b.DX, a.DX * b.M12 + a.DY * b.M22 + b.DY);
}

public static class Numbers
{
    public static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    public static double Parse(string value, double fallback = 0) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : fallback;
    public static double Clamp(double value, double min, double max) => double.IsFinite(value) ? Math.Clamp(value, min, max) : min;
}
