using VectorSpace.Core;

namespace VectorSpace.Editing;

public enum TangentMode { Corner, Smooth, Mirrored }

/// <summary>Double-precision, UI-independent operations on a single editable contour.
/// Callers own history; every operation validates its inputs before changing the contour.</summary>
public static class PathEditing
{
    public static bool CanEdit(DesignNode node) => node.Kind == NodeKind.Path && node.PathData is null && node.Points.Count > 0;

    public static Matrix2D PointToWorld(DesignNode node) =>
        (node.PathWidth > 0 && node.PathHeight > 0
            ? Matrix2D.Scale(node.Width / node.PathWidth, node.Height / node.PathHeight)
            : Matrix2D.Identity) * node.WorldMatrix;

    public static CubicSegment Segment(DesignNode node, int index)
    {
        Require(node);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= node.Points.Count - (node.Closed ? 0 : 1)) throw new ArgumentOutOfRangeException(nameof(index));
        var a = node.Points[index]; var b = node.Points[(index + 1) % node.Points.Count];
        // A line uses collinear thirds, so t is also its geometric distance fraction.
        var line = a.ControlOut is null && b.ControlIn is null;
        return new(a.Position, line ? a.Position + (b.Position - a.Position) / 3 : a.ControlOut ?? a.Position,
            line ? b.Position + (a.Position - b.Position) / 3 : b.ControlIn ?? b.Position, b.Position);
    }

    public static int Insert(DesignNode node, int segment, double t)
    {
        if (!double.IsFinite(t) || t <= 0 || t >= 1) throw new ArgumentOutOfRangeException(nameof(t));
        var curve = Segment(node, segment);
        var a = node.Points[segment]; var b = node.Points[(segment + 1) % node.Points.Count];
        var point = new PathPoint { Position = curve.Evaluate(t) };
        if (a.ControlOut.HasValue || b.ControlIn.HasValue)
        {
            var (left, right) = curve.Split(t);
            a.ControlOut = left.B; point.ControlIn = left.C; point.ControlOut = right.B; b.ControlIn = right.C;
        }
        node.Points.Insert(segment + 1, point);
        return segment + 1;
    }

    /// <summary>Removes selected anchors and reconnects surviving neighbors. This does not claim
    /// curve-fitting or Figma's delete-and-heal semantics. Layer deletion remains the caller's choice.</summary>
    public static void Delete(DesignNode node, IEnumerable<int> indices)
    {
        var selected = Indices(node, indices);
        if (node.Points.Count - selected.Length < 2) throw new InvalidOperationException("Keep at least two anchors, or delete the layer outside point editing.");
        for (var i = selected.Length - 1; i >= 0; i--) node.Points.RemoveAt(selected[i]);
        if (node.Points.Count < 3) node.Closed = false;
        if (!node.Closed) { node.Points[0].ControlIn = null; node.Points[^1].ControlOut = null; }
    }

    public static void Move(DesignNode node, IEnumerable<int> indices, Vec2 pointDelta)
    {
        if (!pointDelta.IsFinite) throw new ArgumentException("Movement must be finite.", nameof(pointDelta));
        var selected = Indices(node, indices);
        foreach (var i in selected) Translate(node.Points[i], pointDelta);
    }

    public static void Reverse(DesignNode node)
    {
        Require(node); node.Points.Reverse();
        foreach (var p in node.Points) (p.ControlIn, p.ControlOut) = (p.ControlOut, p.ControlIn);
    }

    public static void SetTangents(DesignNode node, IEnumerable<int> indices, TangentMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var selected = Indices(node, indices);
        // Snapshot neighboring anchors before modifying handles; all selected points share one baseline.
        foreach (var index in selected)
        {
            var p = node.Points[index];
            if (mode == TangentMode.Corner) { p.ControlIn = p.ControlOut = null; continue; }
            var previous = index > 0 ? node.Points[index - 1] : node.Closed ? node.Points[^1] : null;
            var next = index + 1 < node.Points.Count ? node.Points[index + 1] : node.Closed ? node.Points[0] : null;
            var vector = (next?.Position ?? p.Position) - (previous?.Position ?? p.Position);
            var length = vector.Length;
            if (length < 1e-10) { p.ControlIn = p.ControlOut = null; continue; }
            var direction = vector / length;
            var before = previous is null ? 0 : p.Position.DistanceTo(previous.Position) / 3;
            var after = next is null ? 0 : p.Position.DistanceTo(next.Position) / 3;
            if (mode == TangentMode.Mirrored && previous is not null && next is not null) before = after = Math.Min(before, after);
            p.ControlIn = previous is null ? null : p.Position - direction * before;
            p.ControlOut = next is null ? null : p.Position + direction * after;
        }
    }

    public static void MoveHandle(PathPoint point, bool incoming, Vec2 position, bool independent, bool mirrored = false)
    {
        if (!position.IsFinite) throw new ArgumentException("Handle position must be finite.", nameof(position));
        var opposite = incoming ? point.ControlOut : point.ControlIn;
        if (incoming) point.ControlIn = position; else point.ControlOut = position;
        if (independent || opposite is null) return;
        var delta = position - point.Position; var length = delta.Length;
        if (length < 1e-10) return;
        var other = point.Position - delta * (mirrored ? 1 : opposite.Value.DistanceTo(point.Position) / length);
        if (incoming) point.ControlOut = other; else point.ControlIn = other;
    }

    /// <summary>Iterative Ramer-Douglas-Peucker simplification of an open polyline. Curved or closed
    /// contours are rejected instead of discarding their shape. Input and output keep both endpoints.</summary>
    public static int Simplify(DesignNode node, double tolerance)
    {
        Require(node);
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (node.Closed || node.Points.Any(p => p.ControlIn.HasValue || p.ControlOut.HasValue))
            throw new InvalidOperationException("Simplify applies to open, straight-segment freehand paths.");
        if (node.Points.Count < 3 || tolerance == 0) return 0;
        var keep = new bool[node.Points.Count]; keep[0] = keep[^1] = true;
        var stack = new Stack<(int Start, int End)>(); stack.Push((0, keep.Length - 1));
        var squared = tolerance * tolerance;
        var work = 0L; var limit = Math.Min(8_000_000L, 128L * node.Points.Count);
        while (stack.TryPop(out var range))
        {
            var max = squared; var index = -1;
            for (var i = range.Start + 1; i < range.End; i++)
            {
                // Adversarial polylines must not monopolize the UI thread; keeping the original
                // contour is an error-preserving fallback, unlike silently dropping points.
                if (++work > limit) return 0;
                var distance = CubicSegment.LineDistanceSquared(node.Points[i].Position, node.Points[range.Start].Position, node.Points[range.End].Position);
                if (distance > max) { max = distance; index = i; }
            }
            if (index < 0) continue;
            keep[index] = true; stack.Push((range.Start, index)); stack.Push((index, range.End));
        }
        var result = new List<PathPoint>();
        for (var i = 0; i < keep.Length; i++) if (keep[i]) result.Add(node.Points[i]);
        var removed = node.Points.Count - result.Count; node.Points = result; return removed;
    }

    private static void Translate(PathPoint p, Vec2 delta)
    {
        p.Position += delta;
        if (p.ControlIn.HasValue) p.ControlIn += delta;
        if (p.ControlOut.HasValue) p.ControlOut += delta;
    }
    private static int[] Indices(DesignNode node, IEnumerable<int> indices)
    {
        Require(node); ArgumentNullException.ThrowIfNull(indices);
        var result = indices.Distinct().Order().ToArray();
        foreach (var i in result) if ((uint)i >= node.Points.Count) throw new ArgumentOutOfRangeException(nameof(indices));
        return result;
    }
    private static void Require(DesignNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!CanEdit(node)) throw new InvalidOperationException("Convert this layer to an editable contour first.");
    }
}

/// <summary>Reusable cubic geometry. Closest point uses a coarse bracket followed by safeguarded
/// local minimization; splitting is exact de Casteljau and does not approximate the curve.</summary>
public readonly record struct CubicSegment(Vec2 A, Vec2 B, Vec2 C, Vec2 D)
{
    public Vec2 Evaluate(double t)
    {
        var u = 1 - t;
        return A * (u * u * u) + B * (3 * u * u * t) + C * (3 * u * t * t) + D * (t * t * t);
    }
    public (CubicSegment Left, CubicSegment Right) Split(double t)
    {
        if (!double.IsFinite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
        var ab = Lerp(A, B, t); var bc = Lerp(B, C, t); var cd = Lerp(C, D, t);
        var abc = Lerp(ab, bc, t); var bcd = Lerp(bc, cd, t); var middle = Lerp(abc, bcd, t);
        return (new(A, ab, abc, middle), new(middle, bcd, cd, D));
    }
    public CubicSegment Transform(Matrix2D matrix) => new(matrix.Map(A), matrix.Map(B), matrix.Map(C), matrix.Map(D));
    public (double T, double Distance) Closest(Vec2 point)
    {
        const int samples = 24;
        var best = 0d; var distance = double.PositiveInfinity;
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples; var d = Evaluate(t).DistanceTo(point);
            if (d < distance) { best = t; distance = d; }
        }
        var lo = Math.Max(0, best - 1d / samples); var hi = Math.Min(1, best + 1d / samples);
        for (var i = 0; i < 28; i++)
        {
            var a = lo + (hi - lo) / 3; var b = hi - (hi - lo) / 3;
            if (Evaluate(a).DistanceTo(point) <= Evaluate(b).DistanceTo(point)) hi = b; else lo = a;
        }
        var candidate = (lo + hi) / 2; var refined = Evaluate(candidate).DistanceTo(point);
        return refined < distance ? (candidate, refined) : (best, distance);
    }
    internal static double LineDistanceSquared(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a; var length = d.X * d.X + d.Y * d.Y;
        var t = length < 1e-24 ? 0 : Math.Clamp(((p.X - a.X) * d.X + (p.Y - a.Y) * d.Y) / length, 0, 1);
        var v = p - (a + d * t); return v.X * v.X + v.Y * v.Y;
    }
    private static Vec2 Lerp(Vec2 a, Vec2 b, double t) => a + (b - a) * t;
}
