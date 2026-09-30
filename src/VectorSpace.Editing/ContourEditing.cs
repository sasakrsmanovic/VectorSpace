using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Atomic topology operations on one vector layer. Callers own document history.
/// Cutting preserves cubic geometry; joining adds a straight connector and never welds or moves endpoints.</summary>
public static class ContourEditing
{
    public static int Cut(DesignNode node, int anchor)
    {
        var topology = new PathTopology(node); var c = topology.ContourIndex(anchor); var range = topology.Contours[c];
        var local = anchor - range.Offset; var list = topology.List(c);
        if (!range.Closed && (local == 0 || local == range.Count - 1)) throw new InvalidOperationException("Choose an interior anchor, not an existing open endpoint.");
        if (topology.Points.Count >= PathTopology.MaxAnchors || !range.Closed && topology.Contours.Count >= PathTopology.MaxContours)
            throw new InvalidOperationException("Cutting this contour would exceed the editable geometry budget.");
        var contours = Snapshot(node);
        if (range.Closed)
        {
            var opened = new List<PathPoint>(range.Count + 1);
            for (var i = 0; i < range.Count; i++) opened.Add(Clone(list[(local + i) % range.Count]));
            opened.Add(Clone(opened[0])); opened[0].ControlIn = null; opened[^1].ControlOut = null;
            contours[c] = new() { Points = opened };
        }
        else
        {
            var left = list.Take(local + 1).Select(Clone).ToList();
            var right = list.Skip(local).Select(Clone).ToList();
            left[^1].ControlOut = null; right[0].ControlIn = null;
            contours[c] = new() { Points = left }; contours.Insert(c + 1, new() { Points = right });
        }
        Assign(node, contours); return range.Closed ? range.Offset : anchor;
    }
    public static void Join(DesignNode node, int first, int second)
    {
        var topology = new PathTopology(node);
        var a = topology.ContourIndex(first); var b = topology.ContourIndex(second);
        if (first == second || !topology.IsEndpoint(first) || !topology.IsEndpoint(second))
            throw new InvalidOperationException("Select two distinct endpoints of open contours.");
        var contours = Snapshot(node);
        if (a == b)
        {
            if (topology.Contours[a].Count < 3) throw new InvalidOperationException("Closing an open contour requires at least three anchors.");
            // Explicitly discard inactive endpoint handles: Join creates a straight seam.
            var points = contours[a].Points.Select(Clone).ToList(); points[0].ControlIn = null; points[^1].ControlOut = null;
            contours[a] = new() { Points = points, Closed = true };
        }
        else
        {
            var left = contours[a].Points.Select(Clone).ToList(); var right = contours[b].Points.Select(Clone).ToList();
            if (first == topology.Contours[a].Offset) Reverse(left);
            if (second != topology.Contours[b].Offset) Reverse(right);
            left[^1].ControlOut = null; right[0].ControlIn = null; left.AddRange(right);
            var before = Math.Min(a, b); var after = Math.Max(a, b);
            contours[before] = new() { Points = left }; contours.RemoveAt(after);
        }
        Assign(node, contours);
    }
    public static void Remove(DesignNode node, int contour)
    {
        var topology = new PathTopology(node);
        if ((uint)contour >= topology.Contours.Count) throw new ArgumentOutOfRangeException(nameof(contour));
        if (topology.Contours.Count == 1) throw new InvalidOperationException("Keep one contour, or delete the layer outside vector editing.");
        var contours = Snapshot(node); contours.RemoveAt(contour); Assign(node, contours);
    }
    public static void Reverse(DesignNode node, IEnumerable<int> contours)
    {
        var topology = new PathTopology(node); var selected = Contours(topology, contours);
        foreach (var c in selected) Reverse(topology.List(c));
    }
    public static void SetClosed(DesignNode node, IEnumerable<int> contours, bool closed)
    {
        var topology = new PathTopology(node); var selected = Contours(topology, contours);
        foreach (var c in selected)
            if (closed && !topology.Contours[c].Closed && topology.Contours[c].Count < 3)
                throw new InvalidOperationException("Closing an open contour requires at least three anchors.");
        foreach (var c in selected)
        {
            if (node.Contours is { } compound) compound[c].Closed = closed;
            else node.Closed = closed;
        }
    }
    internal static void Assign(DesignNode node, List<PathContour> contours)
    {
        // Preserve the longstanding single-contour representation whenever possible.
        if (contours.Count == 1) { node.Points = contours[0].Points; node.Closed = contours[0].Closed; node.Contours = null; }
        else { node.Contours = contours; node.Points = []; node.Closed = false; }
    }
    private static List<PathContour> Snapshot(DesignNode node) => node.Contours is { } c ? c.ToList() : [new() { Points = node.Points, Closed = node.Closed }];
    private static int[] Contours(PathTopology topology, IEnumerable<int> contours)
    {
        ArgumentNullException.ThrowIfNull(contours); var result = contours.Distinct().Order().ToArray();
        foreach (var c in result) if ((uint)c >= topology.Contours.Count) throw new ArgumentOutOfRangeException(nameof(contours));
        return result;
    }
    private static void Reverse(List<PathPoint> points)
    {
        points.Reverse(); foreach (var p in points) (p.ControlIn, p.ControlOut) = (p.ControlOut, p.ControlIn);
    }
    private static PathPoint Clone(PathPoint p) => new() { Position = p.Position, ControlIn = p.ControlIn, ControlOut = p.ControlOut };
}
