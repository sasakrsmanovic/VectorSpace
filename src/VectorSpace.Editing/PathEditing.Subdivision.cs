using VectorSpace.Core;

namespace VectorSpace.Editing;

public static partial class PathEditing
{
    /// <summary>Subdivides selected segment starts as one preflighted topology operation.
    /// Builds each affected contour once instead of rebuilding topology/shifting arrays per edge.
    /// Returns inserted global anchor indices in the resulting contour order.</summary>
    public static IReadOnlyList<int> Subdivide(DesignNode node, IEnumerable<int> segments, double t = .5)
    {
        if (!double.IsFinite(t) || t <= 0 || t >= 1) throw new ArgumentOutOfRangeException(nameof(t));
        var topology = new PathTopology(node); var starts = topology.Indices(segments);
        foreach (var start in starts)
            if (topology.Next(start) < 0) throw new ArgumentOutOfRangeException(nameof(segments), "An open endpoint has no outgoing segment.");
        if (starts.Length > PathTopology.MaxAnchors - topology.Points.Count) throw new InvalidOperationException("Subdivision exceeds the editable anchor budget.");
        if (starts.Length == 0) return Array.Empty<int>();
        var selected = starts.ToHashSet(); var output = new List<PathContour>(topology.Contours.Count);
        var inserted = new List<int>(starts.Length); var offset = 0;
        foreach (var range in topology.Contours)
        {
            var count = 0;
            for (var i = range.Offset; i < range.Offset + range.Count; i++) if (selected.Contains(i)) count++;
            var source = topology.List(topology.ContourIndex(range.Offset));
            if (count == 0) { output.Add(new() { Points = source, Closed = range.Closed }); offset += range.Count; continue; }
            // Detached clones ensure no earlier contour is changed if preflight/construction fails.
            var anchors = source.Select(p => new PathPoint { Position = p.Position, ControlIn = p.ControlIn, ControlOut = p.ControlOut }).ToArray();
            var midpoints = new PathPoint?[range.Count];
            for (var i = 0; i < range.Count; i++)
            {
                var start = range.Offset + i; if (!selected.Contains(start)) continue;
                var next = topology.Next(start); var curve = topology.Curve(start); var middle = new PathPoint { Position = curve.Evaluate(t) };
                if (source[i].ControlOut.HasValue || topology.Points[next].ControlIn.HasValue)
                {
                    var (left, right) = curve.Split(t);
                    anchors[i].ControlOut = left.B; anchors[next - range.Offset].ControlIn = right.C;
                    middle.ControlIn = left.C; middle.ControlOut = right.B;
                }
                midpoints[i] = middle;
            }
            var points = new List<PathPoint>(range.Count + count);
            for (var i = 0; i < anchors.Length; i++)
            {
                points.Add(anchors[i]);
                if (midpoints[i] is { } middle) { inserted.Add(offset + points.Count); points.Add(middle); }
            }
            output.Add(new() { Points = points, Closed = range.Closed }); offset += points.Count;
        }
        ContourEditing.Assign(node, output); return inserted.AsReadOnly();
    }
}
