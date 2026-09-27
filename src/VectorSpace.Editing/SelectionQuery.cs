using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Pure scene selection, independent of Uno and Skia. Parent-first resolution and
/// transformed-rectangle marquee tests share the same semantics in native and browser hosts.</summary>
public static class SelectionQuery
{
    public static DesignNode? Resolve(DesignNode? deepest, IReadOnlySet<string> selected, DesignNode? scope = null, bool deep = false)
    {
        if (deepest is null || deepest.IsEffectivelyLocked || !deepest.IsEffectivelyVisible) return null;
        if (deep) return deepest;
        for (var n = deepest; n is not null; n = n.Parent)
            if (selected.Contains(n.Id)) return n;
        var result = deepest;
        while (result.Parent is { } parent && parent != scope && parent.Kind != NodeKind.Section) result = parent;
        return result;
    }

    public static IReadOnlyList<DesignNode> Marquee(IEnumerable<DesignNode> roots, RectD box, bool deep = false, bool contained = false)
    {
        var result = new List<DesignNode>();
        foreach (var n in roots) Visit(n);
        return result;
        void Visit(DesignNode n)
        {
            if (!n.IsEffectivelyVisible || n.IsEffectivelyLocked || n.Kind == NodeKind.Slice) return;
            var intersects = Intersects(n, box, contained);
            if (!deep && n.Kind != NodeKind.Section) { if (intersects) result.Add(n); return; }
            if (n.ClipContent && !Intersects(n, box, false)) return;
            if (n.Children.Count == 0) { if (intersects) result.Add(n); }
            else foreach (var child in n.Children) Visit(child);
        }
    }

    public static bool Intersects(DesignNode n, RectD rect, bool contained)
    {
        var m = n.WorldMatrix;
        Span<Vec2> quad = stackalloc Vec2[4] { m.Map(Vec2.Zero), m.Map(new Vec2(n.Width, 0)), m.Map(new Vec2(n.Width, n.Height)), m.Map(new Vec2(0, n.Height)) };
        if (contained) { foreach (var p in quad) if (!rect.Contains(p)) return false; return true; }
        if (!n.WorldBounds.Intersects(rect)) return false;
        // Separating-axis test avoids false positives in corners of rotated AABBs.
        Span<Vec2> corners = stackalloc Vec2[4] { new(rect.X, rect.Y), new(rect.Right, rect.Y), new(rect.Right, rect.Bottom), new(rect.X, rect.Bottom) };
        for (var i = 0; i < 2; i++)
        {
            var edge = quad[i + 1] - quad[i]; var axis = new Vec2(-edge.Y, edge.X);
            var minA = double.PositiveInfinity; var maxA = double.NegativeInfinity;
            var minB = double.PositiveInfinity; var maxB = double.NegativeInfinity;
            foreach (var p in quad) { var dot = p.X * axis.X + p.Y * axis.Y; minA = Math.Min(minA, dot); maxA = Math.Max(maxA, dot); }
            foreach (var p in corners) { var dot = p.X * axis.X + p.Y * axis.Y; minB = Math.Min(minB, dot); maxB = Math.Max(maxB, dot); }
            if (maxA < minB || maxB < minA) return false;
        }
        return true;
    }
}
