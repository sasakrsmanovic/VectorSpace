using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Captured sparse baseline for anchor/handle dragging. Setup validates retained contour
/// lists and deduplicates/sorts selection; only selected anchors are copied. Apply runs in O(k)
/// for k captured anchors without managed allocation. The caller owns the document transaction.</summary>
public sealed class PathPointDrag
{
    private readonly record struct Baseline(PathPoint Target, Vec2 Position, Vec2? Incoming, Vec2? Outgoing);
    private readonly Baseline[] _baseline;
    private readonly Matrix2D _inverse;
    private readonly int _handle;
    public int CapturedAnchorCount => _baseline.Length;
    public PathPointDrag(DesignNode node, PathTopology topology, IEnumerable<int> selection, int handle = 0)
    {
        ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(topology);
        if (!topology.Matches(node)) throw new InvalidOperationException("The topology no longer owns this layer.");
        if (handle is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(handle));
        if (!PathEditing.PointToWorld(node).TryInvert(out _inverse)) throw new InvalidOperationException("Anchor editing requires an invertible transform.");
        var indices = topology.Indices(selection);
        if (handle != 0 && indices.Length != 1) throw new ArgumentException("A handle drag requires exactly one anchor.", nameof(selection));
        _handle = handle; _baseline = new Baseline[indices.Length];
        for (var i = 0; i < indices.Length; i++) { var p = topology.Points[indices[i]]; _baseline[i] = new(p, p.Position, p.ControlIn, p.ControlOut); }
    }
    public void Apply(Vec2 worldDelta, bool constrain = false, bool independentHandle = false)
    {
        if (!worldDelta.IsFinite) throw new ArgumentException("Pointer movement must be finite.", nameof(worldDelta));
        if (constrain && _handle == 0) worldDelta = Math.Abs(worldDelta.X) >= Math.Abs(worldDelta.Y) ? new(worldDelta.X, 0) : new(0, worldDelta.Y);
        var delta = _inverse.Map(worldDelta) - _inverse.Map(Vec2.Zero);
        foreach (var b in _baseline)
        {
            var p = b.Target; p.Position = b.Position; p.ControlIn = b.Incoming; p.ControlOut = b.Outgoing;
            if (_handle == 0) { p.Position += delta; p.ControlIn += delta; p.ControlOut += delta; }
            else
            {
                var position = (_handle < 0 ? b.Incoming : b.Outgoing) ?? b.Position; position += delta;
                if (constrain) position = b.Position + DrawingGeometry.ConstrainAngle(position - b.Position);
                PathEditing.MoveHandle(p, _handle < 0, position, independentHandle);
            }
        }
    }
}
