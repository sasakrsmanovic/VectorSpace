using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>A retained, non-owning topology view. Anchor indices are flattened in contour order;
/// segment indices are their START anchor, not an ordinal that can cross a Move boundary.
/// Rebuild after structural edits (insert, delete, reorder, closure) or document replacement.
/// Position/handle changes remain visible through retained PathPoint references.</summary>
public sealed class PathTopology
{
    public const int MaxAnchors = 100_000;
    public const int MaxContours = 10_000;
    public readonly record struct Contour(int Offset, int Count, bool Closed);
    public readonly record struct SegmentIndex(int Start, int End, int Contour);
    private readonly DesignNode _node;
    private readonly List<PathContour>? _source;
    private readonly List<PathPoint>[] _lists;
    private readonly int[] _owners;
    public IReadOnlyList<PathPoint> Points { get; }
    public IReadOnlyList<Contour> Contours { get; }
    public IReadOnlyList<SegmentIndex> Segments { get; }

    public PathTopology(DesignNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!PathEditing.CanEdit(node)) throw new InvalidOperationException("Convert this layer to editable contours first.");
        _node = node; _source = node.Contours;
        var count = _source?.Count ?? 1;
        if (count is < 1 or > MaxContours) throw new InvalidOperationException("Editable contour count is outside the supported limit.");
        _lists = new List<PathPoint>[count]; var contours = new Contour[count]; var total = 0;
        for (var i = 0; i < count; i++)
        {
            var list = _source is null ? node.Points : _source[i].Points;
            if (list is null || list.Count < 1 || list.Count > MaxAnchors - total) throw new InvalidOperationException("Invalid editable anchor count.");
            _lists[i] = list; contours[i] = new(total, list.Count, _source is null ? node.Closed : _source[i].Closed); total += list.Count;
        }
        _owners = new int[total];
        var points = new PathPoint[total]; var segments = new List<SegmentIndex>(total);
        for (var c = 0; c < count; c++)
        {
            var range = contours[c];
            for (var j = 0; j < range.Count; j++)
            {
                var index = range.Offset + j; points[index] = _lists[c][j]; _owners[index] = c;
                if (j + 1 < range.Count) segments.Add(new(index, index + 1, c));
                else if (range.Closed) segments.Add(new(index, range.Offset, c));
            }
        }
        Points = Array.AsReadOnly(points); Contours = Array.AsReadOnly(contours); Segments = segments.AsReadOnly();
    }
    /// <summary>Fast identity/count/closure guard. Callers must invalidate the view after
    /// same-count in-place reordering of a source list.</summary>
    public bool Matches(DesignNode node)
    {
        if (!ReferenceEquals(node, _node) || !ReferenceEquals(node.Contours, _source) || (_source?.Count ?? 1) != _lists.Length) return false;
        for (var i = 0; i < _lists.Length; i++)
        {
            var list = _source is null ? node.Points : _source[i].Points;
            if (!ReferenceEquals(list, _lists[i]) || list.Count != Contours[i].Count || (_source is null ? node.Closed : _source[i].Closed) != Contours[i].Closed) return false;
        }
        return true;
    }
    public int ContourIndex(int anchor)
    {
        if ((uint)anchor >= _owners.Length) throw new ArgumentOutOfRangeException(nameof(anchor));
        return _owners[anchor];
    }
    public int Next(int anchor)
    {
        var c = Contours[ContourIndex(anchor)];
        return anchor + 1 < c.Offset + c.Count ? anchor + 1 : c.Closed ? c.Offset : -1;
    }
    public int Previous(int anchor)
    {
        var c = Contours[ContourIndex(anchor)];
        return anchor > c.Offset ? anchor - 1 : c.Closed ? c.Offset + c.Count - 1 : -1;
    }
    public bool IsEndpoint(int anchor) => Previous(anchor) < 0 || Next(anchor) < 0;
    public CubicSegment Curve(int start)
    {
        var end = Next(start);
        if (end < 0) throw new ArgumentOutOfRangeException(nameof(start), "An open contour's final anchor has no outgoing segment.");
        var a = Points[start]; var b = Points[end]; var line = a.ControlOut is null && b.ControlIn is null;
        return new(a.Position, line ? a.Position + (b.Position - a.Position) / 3 : a.ControlOut ?? a.Position,
            line ? b.Position + (a.Position - b.Position) / 3 : b.ControlIn ?? b.Position, b.Position);
    }
    internal List<PathPoint> List(int contour) => _lists[contour];
    internal int[] Indices(IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices); var result = indices.Distinct().Order().ToArray();
        foreach (var i in result) if ((uint)i >= Points.Count) throw new ArgumentOutOfRangeException(nameof(indices));
        return result;
    }
}
