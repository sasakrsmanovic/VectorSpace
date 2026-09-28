using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Finds the visually topmost editable frame at a world point. Preserves painter order,
/// local hit geometry and ancestor clipping without allocating a flattened scene array.</summary>
public static class DrawingTargetQuery
{
    public static DesignNode? FindFrame(IReadOnlyList<DesignNode> roots, Vec2 worldPoint)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (!worldPoint.IsFinite) throw new ArgumentException("Position must be finite.", nameof(worldPoint));
        for (var i = roots.Count - 1; i >= 0; i--)
            if (Find(roots[i], Matrix2D.Identity, worldPoint) is { } found) return found;
        return null;
    }
    private static DesignNode? Find(DesignNode node, Matrix2D parent, Vec2 worldPoint)
    {
        if (!node.Visible || node.Opacity <= 0 || node.Locked || node.Kind == NodeKind.Instance) return null;
        var world = node.LocalMatrix * parent;
        if (!world.TryInvert(out var inverse)) return null;
        var local = inverse.Map(worldPoint); var inside = Contains(node, local);
        if (node.ClipContent && !inside) return null;
        for (var i = node.Children.Count - 1; i >= 0; i--)
            if (Find(node.Children[i], world, worldPoint) is { } found) return found;
        return node.IsFrame && inside ? node : null;
    }
    private static bool Contains(DesignNode node, Vec2 point)
    {
        if (!node.LocalBounds.Contains(point)) return false;
        var radius = Math.Clamp(node.CornerRadius, 0, Math.Min(node.Width, node.Height) / 2);
        if (radius <= 0) return true;
        var x = Math.Clamp(point.X, radius, node.Width - radius);
        var y = Math.Clamp(point.Y, radius, node.Height - radius);
        var dx = point.X - x; var dy = point.Y - y;
        return dx * dx + dy * dy <= radius * radius;
    }
}
