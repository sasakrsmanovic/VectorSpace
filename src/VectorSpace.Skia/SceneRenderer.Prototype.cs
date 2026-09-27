using SkiaSharp;
using VectorSpace.Core;

namespace VectorSpace.Skia;

public sealed partial class SceneRenderer
{
    /// <summary>Render a presentation viewport in frame-local coordinates. The frame background stays
    /// stationary while descendants scroll. Root clipping is enforced without mutating scene properties.</summary>
    public void DrawPrototypeFrame(SKCanvas canvas, DesignNode frame, Vec2 scroll)
    {
        canvas.Save();
        try
        {
            canvas.Concat(Matrix(frame.LocalMatrix.Inverse));
            DrawNode(canvas, frame, frame.LocalMatrix.Inverse, new RectD(scroll.X, scroll.Y, frame.Width, frame.Height), scroll);
        }
        finally { canvas.Restore(); }
    }
    /// <summary>Presentation picking ignores editor locks and uses the same nested clips and scroll
    /// coordinates as rendering. Invisible layers remain excluded; transparent hotspots are supported.</summary>
    public DesignNode? HitPrototypeFrame(DesignNode frame, Vec2 point, Vec2 scroll)
    {
        if (!frame.Visible || !InsideClip(frame, point)) return null;
        return HitChildren(frame.Children, point + scroll) ?? frame;
    }
    private DesignNode? HitChildren(IReadOnlyList<DesignNode> children, Vec2 point)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var n = children[i]; if (!n.Visible || n.Kind == NodeKind.Slice) continue;
            var p = n.LocalMatrix.Inverse.Map(point); var inside = n.LocalBounds.Contains(p);
            if (!n.ClipContent || InsideClip(n, p))
            {
                var child = HitChildren(n.Children, p); if (child is not null) return child;
            }
            if (inside && (n.Kind == NodeKind.Text || n.IsContainer || n.Reactions.Count > 0 || n.PrototypeTargetId is not null)) return n;
            var path = Geometry(n);
            if (n.Fills.Any(f => f.Visible) && path.Contains((float)p.X, (float)p.Y)) return n;
            if (n.Strokes.Any(s => s.Visible && s.Width > 0))
            {
                using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = (float)Math.Max(6, n.Strokes.Where(s => s.Visible).Max(s => s.Width)), StrokeCap = SKStrokeCap.Round };
                using var outline = new SKPath(); paint.GetFillPath(path, outline);
                if (outline.Contains((float)p.X, (float)p.Y)) return n;
            }
        }
        return null;
    }
    private static bool InsideClip(DesignNode n, Vec2 p)
    {
        if (!n.LocalBounds.Contains(p)) return false;
        if (n.CornerRadius <= 0) return true;
        var radius = Math.Min(n.CornerRadius, Math.Min(n.Width, n.Height) / 2);
        var x = Math.Clamp(p.X, radius, n.Width - radius); var y = Math.Clamp(p.Y, radius, n.Height - radius);
        return (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) <= radius * radius;
    }
}
