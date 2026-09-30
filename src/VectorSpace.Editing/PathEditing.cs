using VectorSpace.Core;

namespace VectorSpace.Editing;

public enum TangentMode { Corner, Smooth, Mirrored }

/// <summary>Double-precision operations on single or compound editable cubic contours.
/// Global anchor indices follow PathTopology; operations validate before mutating. Callers own history.</summary>
public static class PathEditing
{
    public static bool CanEdit(DesignNode node) => node.Kind == NodeKind.Path && node.PathData is null && node.Commands is null &&
        (node.Contours is { Count: > 0 } || node.Points.Count > 0);
    public static Matrix2D PointToWorld(DesignNode node) =>
        (node.PathWidth > 0 && node.PathHeight > 0 ? Matrix2D.Scale(node.Width / node.PathWidth, node.Height / node.PathHeight) : Matrix2D.Identity) * node.WorldMatrix;
    public static CubicSegment Segment(DesignNode node, int index) => new PathTopology(node).Curve(index);
    public static int Insert(DesignNode node, int segment, double t)
    {
        if (!double.IsFinite(t) || t <= 0 || t >= 1) throw new ArgumentOutOfRangeException(nameof(t));
        var topology = new PathTopology(node); var curve = topology.Curve(segment);
        if (topology.Points.Count >= PathTopology.MaxAnchors) throw new InvalidOperationException("Editable geometry is limited to 100,000 anchors.");
        var c = topology.ContourIndex(segment); var a = topology.Points[segment]; var b = topology.Points[topology.Next(segment)];
        var point = new PathPoint { Position = curve.Evaluate(t) };
        if (a.ControlOut.HasValue || b.ControlIn.HasValue)
        {
            var (left, right) = curve.Split(t); a.ControlOut = left.B; point.ControlIn = left.C; point.ControlOut = right.B; b.ControlIn = right.C;
        }
        topology.List(c).Insert(segment - topology.Contours[c].Offset + 1, point); return segment + 1;
    }
    /// <summary>Reconnects surviving neighbors in each affected contour. Does not heal or fit curves,
    /// or delete whole contours implicitly; use ContourEditing.Remove for that explicit operation.</summary>
    public static void Delete(DesignNode node, IEnumerable<int> indices)
    {
        var topology = new PathTopology(node); var selected = topology.Indices(indices);
        var groups = selected.GroupBy(topology.ContourIndex).ToArray();
        foreach (var group in groups)
            if (topology.Contours[group.Key].Count - group.Count() < 2)
                throw new InvalidOperationException("Keep at least two anchors in each contour, or use Delete contour.");
        foreach (var group in groups)
        {
            var range = topology.Contours[group.Key]; var list = topology.List(group.Key);
            foreach (var i in group.Reverse()) list.RemoveAt(i - range.Offset);
            var closed = range.Closed && list.Count >= 3;
            if (node.Contours is { } contours) contours[group.Key].Closed = closed; else node.Closed = closed;
            if (!closed) { list[0].ControlIn = null; list[^1].ControlOut = null; }
        }
    }
    public static void Move(DesignNode node, IEnumerable<int> indices, Vec2 pointDelta)
    {
        if (!pointDelta.IsFinite) throw new ArgumentException("Movement must be finite.", nameof(pointDelta));
        var topology = new PathTopology(node); var selected = topology.Indices(indices);
        foreach (var i in selected) { var p = topology.Points[i]; p.Position += pointDelta; p.ControlIn += pointDelta; p.ControlOut += pointDelta; }
    }
    public static void Reverse(DesignNode node) => ContourEditing.Reverse(node, Enumerable.Range(0, node.Contours?.Count ?? 1));
    public static void SetTangents(DesignNode node, IEnumerable<int> indices, TangentMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var topology = new PathTopology(node); var selected = topology.Indices(indices);
        foreach (var index in selected)
        {
            var p = topology.Points[index];
            if (mode == TangentMode.Corner) { p.ControlIn = p.ControlOut = null; continue; }
            var previous = topology.Previous(index) is var a && a >= 0 ? topology.Points[a] : null;
            var next = topology.Next(index) is var b && b >= 0 ? topology.Points[b] : null;
            var vector = (next?.Position ?? p.Position) - (previous?.Position ?? p.Position); var length = vector.Length;
            if (length < 1e-10) { p.ControlIn = p.ControlOut = null; continue; }
            var direction = vector / length; var before = previous is null ? 0 : p.Position.DistanceTo(previous.Position) / 3;
            var after = next is null ? 0 : p.Position.DistanceTo(next.Position) / 3;
            if (mode == TangentMode.Mirrored && previous is not null && next is not null) before = after = Math.Min(before, after);
            p.ControlIn = previous is null ? null : p.Position - direction * before;
            p.ControlOut = next is null ? null : p.Position + direction * after;
        }
    }
    public static void MoveHandle(PathPoint point, bool incoming, Vec2 position, bool independent, bool mirrored = false)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (!position.IsFinite) throw new ArgumentException("Handle position must be finite.", nameof(position));
        var opposite = incoming ? point.ControlOut : point.ControlIn;
        if (incoming) point.ControlIn = position; else point.ControlOut = position;
        if (independent || opposite is null) return;
        var delta = position - point.Position; var length = delta.Length; if (length < 1e-10) return;
        var other = point.Position - delta * (mirrored ? 1 : opposite.Value.DistanceTo(point.Position) / length);
        if (incoming) point.ControlOut = other; else point.ControlIn = other;
    }
    /// <summary>Iterative bounded Ramer-Douglas-Peucker on open straight contours. Each contour
    /// retains its endpoints. Curved/closed inputs are rejected before any contour is changed.</summary>
    public static int Simplify(DesignNode node, double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var topology = new PathTopology(node);
        if (topology.Contours.Any(c => c.Closed) || topology.Points.Any(p => p.ControlIn.HasValue || p.ControlOut.HasValue))
            throw new InvalidOperationException("Simplify applies to open, straight-segment freehand contours.");
        if (tolerance == 0) return 0;
        var output = new List<PathContour>(); var removed = 0; var work = 0L;
        var limit = Math.Min(8_000_000L, 128L * topology.Points.Count);
        for (var c = 0; c < topology.Contours.Count; c++)
        {
            var points = topology.List(c); var keep = new bool[points.Count]; keep[0] = keep[^1] = true;
            var stack = new Stack<(int Start, int End)>(); stack.Push((0, keep.Length - 1)); var squared = tolerance * tolerance;
            while (stack.TryPop(out var range))
            {
                var max = squared; var index = -1;
                for (var i = range.Start + 1; i < range.End; i++)
                {
                    if (++work > limit) return 0; // Preserve ALL input contours on budget exhaustion.
                    var d = CubicSegment.LineDistanceSquared(points[i].Position, points[range.Start].Position, points[range.End].Position);
                    if (d > max) { max = d; index = i; }
                }
                if (index < 0) continue; keep[index] = true; stack.Push((range.Start, index)); stack.Push((index, range.End));
            }
            var result = new List<PathPoint>(); for (var i = 0; i < keep.Length; i++) if (keep[i]) result.Add(points[i]);
            removed += points.Count - result.Count; output.Add(new() { Points = result });
        }
        if (removed > 0) ContourEditing.Assign(node, output); return removed;
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
