using VectorSpace.Core;

namespace VectorSpace.Layout;

/// <summary>Deterministic linear auto-layout with hug/fill sizing and independent edge constraints.</summary>
public static class LayoutEngine
{
    public static void Arrange(IEnumerable<DesignNode> roots)
    {
        foreach (var node in roots) Arrange(node);
    }
    public static void Arrange(DesignNode node)
    {
        foreach (var child in node.Children) Arrange(child);
        var layout = node.Layout;
        if (layout.Direction == LayoutDirection.None || node.Children.Count == 0) return;
        var children = node.Children.Where(n => n.Visible).ToArray();
        if (children.Length == 0) return;
        var horizontal = layout.Direction == LayoutDirection.Horizontal;
        var gap = Math.Max(0, layout.Gap);
        var l = Math.Max(0, layout.PaddingLeft); var r = Math.Max(0, layout.PaddingRight);
        var t = Math.Max(0, layout.PaddingTop); var b = Math.Max(0, layout.PaddingBottom);
        if (layout.HugWidth) node.Width = l + r + (horizontal ? children.Sum(n => n.Width) + gap * (children.Length - 1) : children.Max(n => n.Width));
        if (layout.HugHeight) node.Height = t + b + (horizontal ? children.Max(n => n.Height) : children.Sum(n => n.Height) + gap * (children.Length - 1));
        var availableMain = horizontal ? node.Width - l - r : node.Height - t - b;
        var availableCross = horizontal ? node.Height - t - b : node.Width - l - r;
        var fillCount = children.Count(n => horizontal ? n.FillWidth : n.FillHeight);
        var fixedMain = children.Where(n => !(horizontal ? n.FillWidth : n.FillHeight)).Sum(n => horizontal ? n.Width : n.Height);
        var fillSize = fillCount == 0 ? 0 : Math.Max(1, (availableMain - fixedMain - gap * (children.Length - 1)) / fillCount);
        var cursor = horizontal ? l : t;
        foreach (var child in children)
        {
            if (horizontal && child.FillWidth && !layout.HugWidth) child.Width = fillSize;
            if (!horizontal && child.FillHeight && !layout.HugHeight) child.Height = fillSize;
            if (layout.Alignment == LayoutAlignment.Stretch || (horizontal ? child.FillHeight : child.FillWidth))
            {
                if (horizontal && !layout.HugHeight) child.Height = Math.Max(1, availableCross);
                if (!horizontal && !layout.HugWidth) child.Width = Math.Max(1, availableCross);
            }
            var crossSize = horizontal ? child.Height : child.Width;
            var crossOffset = layout.Alignment switch
            {
                LayoutAlignment.Center => (availableCross - crossSize) / 2,
                LayoutAlignment.End => availableCross - crossSize,
                _ => 0
            };
            if (horizontal) { child.X = cursor; child.Y = t + crossOffset; cursor += child.Width + gap; }
            else { child.X = l + crossOffset; child.Y = cursor; cursor += child.Height + gap; }
        }
    }
    public static void Resize(DesignNode node, double width, double height)
    {
        width = Math.Max(1, width); height = Math.Max(1, height);
        var oldW = Math.Max(1, node.Width); var oldH = Math.Max(1, node.Height);
        if (node.Layout.Direction == LayoutDirection.None)
        {
            foreach (var child in node.Children)
            {
                var (x, w) = Constrain(child.X, child.Width, oldW, width, child.HorizontalConstraint);
                var (y, h) = Constrain(child.Y, child.Height, oldH, height, child.VerticalConstraint);
                child.X = x; child.Y = y; Resize(child, w, h);
            }
        }
        node.Width = width; node.Height = height; Arrange(node);
    }
    private static (double Position, double Size) Constrain(double p, double size, double oldSize, double newSize, AxisConstraint mode) => mode switch
    {
        AxisConstraint.End => (p + newSize - oldSize, size),
        AxisConstraint.Center => (p + (newSize - oldSize) / 2, size),
        AxisConstraint.Stretch => (p, Math.Max(1, size + newSize - oldSize)),
        AxisConstraint.Scale => (p * newSize / oldSize, Math.Max(1, size * newSize / oldSize)),
        _ => (p, size)
    };
}

public readonly record struct SnapLine(bool Horizontal, double Position, double Start, double End);
public readonly record struct SnapResult(Vec2 Correction, IReadOnlyList<SnapLine> Lines);

public static class SnapEngine
{
    public static SnapResult Snap(RectD moving, IEnumerable<RectD> targets, double tolerance, IEnumerable<Guide>? guides = null)
    {
        var x = new[] { moving.X, moving.Center.X, moving.Right };
        var y = new[] { moving.Y, moving.Center.Y, moving.Bottom };
        double dx = 0, dy = 0, bx = tolerance + 1, by = tolerance + 1;
        SnapLine? lx = null, ly = null;
        foreach (var target in targets)
        {
            foreach (var a in x) foreach (var v in new[] { target.X, target.Center.X, target.Right })
                if (Math.Abs(v - a) < bx && Math.Abs(v - a) <= tolerance) { bx = Math.Abs(v - a); dx = v - a; lx = new(false, v, Math.Min(moving.Y, target.Y), Math.Max(moving.Bottom, target.Bottom)); }
            foreach (var a in y) foreach (var v in new[] { target.Y, target.Center.Y, target.Bottom })
                if (Math.Abs(v - a) < by && Math.Abs(v - a) <= tolerance) { by = Math.Abs(v - a); dy = v - a; ly = new(true, v, Math.Min(moving.X, target.X), Math.Max(moving.Right, target.Right)); }
        }
        foreach (var guide in guides ?? [])
        {
            foreach (var a in guide.Horizontal ? y : x)
            {
                var d = guide.Position - a;
                if (guide.Horizontal && Math.Abs(d) < by && Math.Abs(d) <= tolerance) { dy = d; by = Math.Abs(d); ly = new(true, guide.Position, moving.X - 100, moving.Right + 100); }
                if (!guide.Horizontal && Math.Abs(d) < bx && Math.Abs(d) <= tolerance) { dx = d; bx = Math.Abs(d); lx = new(false, guide.Position, moving.Y - 100, moving.Bottom + 100); }
            }
        }
        var lines = new List<SnapLine>(); if (lx.HasValue) lines.Add(lx.Value); if (ly.HasValue) lines.Add(ly.Value);
        return new(new(dx, dy), lines);
    }
}
