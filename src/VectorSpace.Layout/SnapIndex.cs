using VectorSpace.Core;

namespace VectorSpace.Layout;

/// <summary>An immutable, gesture-scoped index of edge/center coordinates. Build once; queries are O(log n).
/// Equal-distance results preserve the original target/anchor traversal order exactly.</summary>
public sealed class SnapIndex
{
    private readonly record struct Edge(double Position, double Start, double End, long Order, bool Guide);
    private readonly Edge[] _x, _y;
    public static SnapIndex Empty { get; } = new([]);
    public int TargetCount { get; }
    public int CoordinateCount => _x.Length + _y.Length;
    public SnapIndex(IEnumerable<RectD> targets, IEnumerable<Guide>? guides = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var x = new List<Edge>(); var y = new List<Edge>();
        long order = 0;
        foreach (var target in targets)
        {
            if (!double.IsFinite(target.X) || !double.IsFinite(target.Y) || !double.IsFinite(target.Right) || !double.IsFinite(target.Bottom)) continue;
            x.Add(new(target.X, target.Y, target.Bottom, order, false));
            x.Add(new(target.Center.X, target.Y, target.Bottom, order + 1, false));
            x.Add(new(target.Right, target.Y, target.Bottom, order + 2, false));
            y.Add(new(target.Y, target.X, target.Right, order, false));
            y.Add(new(target.Center.Y, target.X, target.Right, order + 1, false));
            y.Add(new(target.Bottom, target.X, target.Right, order + 2, false));
            order += 9; TargetCount++;
        }
        foreach (var guide in guides ?? [])
        {
            if (double.IsFinite(guide.Position)) (guide.Horizontal ? y : x).Add(new(guide.Position, 0, 0, order, true));
            order += 9;
        }
        _x = Compact(x); _y = Compact(y);
    }
    private static Edge[] Compact(List<Edge> items)
    {
        items.Sort((a, b) => { var c = a.Position.CompareTo(b.Position); return c != 0 ? c : a.Order.CompareTo(b.Order); });
        var count = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var edge = items[index];
            if (count == 0 || edge.Position != items[count - 1].Position) items[count++] = edge;
        }
        var result = new Edge[count]; items.CopyTo(0, result, 0, count); return result;
    }
    public SnapResult Snap(RectD moving, double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0) return new(Vec2.Zero, []);
        var x = Nearest(_x, moving.X, moving.Center.X, moving.Right, tolerance);
        var y = Nearest(_y, moving.Y, moving.Center.Y, moving.Bottom, tolerance);
        if (!x.Found && !y.Found) return new(Vec2.Zero, []);
        var lines = new SnapLine[(x.Found ? 1 : 0) + (y.Found ? 1 : 0)]; var i = 0;
        if (x.Found) lines[i++] = new(false, x.Edge.Position, x.Edge.Guide ? moving.Y - 100 : Math.Min(moving.Y, x.Edge.Start), x.Edge.Guide ? moving.Bottom + 100 : Math.Max(moving.Bottom, x.Edge.End));
        if (y.Found) lines[i] = new(true, y.Edge.Position, y.Edge.Guide ? moving.X - 100 : Math.Min(moving.X, y.Edge.Start), y.Edge.Guide ? moving.Right + 100 : Math.Max(moving.Right, y.Edge.End));
        return new(new(x.Delta, y.Delta), lines);
    }
    private static (bool Found, double Delta, Edge Edge) Nearest(Edge[] edges, double a, double b, double c, double tolerance)
    {
        var found = false; var distance = double.PositiveInfinity; var delta = 0d; var best = default(Edge); var rank = long.MaxValue;
        Span<double> anchors = stackalloc double[3] { a, b, c };
        for (var anchor = 0; anchor < 3; anchor++)
        {
            var value = anchors[anchor]; if (!double.IsFinite(value)) continue;
            var low = 0; var high = edges.Length;
            while (low < high) { var mid = low + (high - low) / 2; if (edges[mid].Position < value) low = mid + 1; else high = mid; }
            for (var index = Math.Max(0, low - 1); index <= Math.Min(edges.Length - 1, low); index++)
            {
                var edge = edges[index]; var d = edge.Position - value; var abs = Math.Abs(d);
                var candidateRank = edge.Order + anchor * 3L;
                if (abs <= tolerance && (abs < distance || abs == distance && candidateRank < rank))
                { found = true; distance = abs; delta = d; best = edge; rank = candidateRank; }
            }
        }
        return (found, delta, best);
    }
}
