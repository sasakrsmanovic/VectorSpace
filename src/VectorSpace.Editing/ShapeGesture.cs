using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Captured local-space shape gesture. Repeated samples do not accumulate corner edits.
/// Arc angles are unwrapped incrementally so crossing the positive X axis does not jump the sweep.</summary>
public sealed class ShapeGesture
{
    private readonly string _id;
    private readonly Matrix2D _inverse;
    private readonly Vec2 _start;
    private readonly double _width, _height;
    private readonly int _handle;
    private readonly CornerRadii _corners;
    private readonly EllipseArc? _arc;
    private double _lastAngle, _angleTravel;
    public ShapeGesture(DesignNode node, int handle, Vec2 startWorld)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!startWorld.IsFinite || !node.WorldMatrix.TryInvert(out _inverse)) throw new ArgumentException("Shape editing requires finite invertible geometry.");
        if (node.Kind != NodeKind.Ellipse && !ShapeGeometry.HasCorners(node)) throw new InvalidOperationException("Select an ellipse or a corner-bearing shape.");
        if (handle < 0 || handle >= (node.Kind == NodeKind.Ellipse ? 3 : 4)) throw new ArgumentOutOfRangeException(nameof(handle));
        _id = node.Id; _handle = handle; _start = _inverse.Map(startWorld); _width = node.Width; _height = node.Height;
        _corners = node.EffectiveCorners;
        if (node.Kind == NodeKind.Ellipse) { _arc = node.Arc ?? new(); _lastAngle = Angle(_start); }
    }
    public void Apply(DesignNode node, Vec2 world, bool independentCorner = false, bool constrainAngle = false)
    {
        if (node.Id != _id || !world.IsFinite) throw new InvalidOperationException("This shape gesture no longer owns its target.");
        var local = _inverse.Map(world);
        if (_arc is null)
        {
            var xSign = _handle is 0 or 3 ? 1 : -1; var ySign = _handle is 0 or 1 ? 1 : -1;
            var delta = local - _start;
            var radius = Math.Clamp(_corners.At(_handle) + (delta.X * xSign + delta.Y * ySign) / 2, 0, Math.Min(_width, _height) / 2);
            node.Corners = independentCorner ? _corners.With(_handle, radius) : null;
            if (!independentCorner) node.CornerRadius = radius;
            return;
        }
        if (_handle == 2)
        {
            var normalized = new Vec2((local.X - _width / 2) / Math.Max(_width / 2, 1e-9), (local.Y - _height / 2) / Math.Max(_height / 2, 1e-9));
            node.Arc = _arc with { InnerRadius = Math.Clamp(normalized.Length, 0, 1) };
            return;
        }
        var angle = Angle(local); _angleTravel += ShapeGeometry.WrappedDelta(angle - _lastAngle); _lastAngle = angle;
        var travel = constrainAngle ? Math.Round(_angleTravel / 15) * 15 : _angleTravel;
        node.Arc = _handle == 0
            ? _arc with { StartDegrees = Math.Clamp(_arc.StartDegrees + travel, -360000, 360000), SweepDegrees = Math.Clamp(_arc.SweepDegrees - travel, -360, 360) }
            : _arc with { SweepDegrees = Math.Clamp(_arc.SweepDegrees + travel, -360, 360) };
    }
    private double Angle(Vec2 local) => Math.Atan2((local.Y - _height / 2) / Math.Max(_height / 2, 1e-9), (local.X - _width / 2) / Math.Max(_width / 2, 1e-9)) * 180 / Math.PI;
}
