namespace VectorSpace.Core;

/// <summary>A connected editable contour. Its last anchor connects to its first only when Closed.
/// Contours in one layer share paints and a fill rule, but never acquire implicit connecting edges.</summary>
public sealed class PathContour
{
    public List<PathPoint> Points { get; set; } = [];
    public bool Closed { get; set; }
}

public sealed partial class DesignNode
{
    /// <summary>Compound editable cubic contours. Mutually exclusive with Points, Commands and
    /// PathData. Legacy single-contour documents continue to use Points and Closed.</summary>
    public List<PathContour>? Contours { get; set; }
}
