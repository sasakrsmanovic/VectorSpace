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
    /// coordinates as rendering. Explicit reactive hotspots may be transparent.</summary>
    public DesignNode? HitPrototypeFrame(DesignNode frame, Vec2 point, Vec2 scroll)
    {
        if (!frame.Visible || !ShapeGeometry.ContainsCornerBox(frame, point)) return null;
        return HitChildren(frame.Children, point + scroll) ?? frame;
    }
    private DesignNode? HitChildren(IReadOnlyList<DesignNode> children, Vec2 point)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var n = children[i]; if (!n.Visible || n.Kind == NodeKind.Slice || !n.LocalMatrix.TryInvert(out var inverse)) continue;
            var p = inverse.Map(point); var inside = n.LocalBounds.Contains(p);
            // Boolean operands are retained authoring data, not separately painted hotspots.
            if (!n.IsBoolean && (!n.ClipContent || ShapeGeometry.ContainsCornerBox(n, p)))
            {
                var child = HitChildren(n.Children, p); if (child is not null) return child;
            }
            if (inside && (n.Kind == NodeKind.Text || n.IsContainer && !n.IsBoolean || n.Reactions.Count > 0 || n.PrototypeTargetId is not null)) return n;
            var path = Geometry(n);
            if (n.Arc?.Open != true && n.Fills.Any(f => f.Visible) && path.Contains((float)p.X, (float)p.Y)) return n;
            for (var j = 0; j < n.Strokes.Count; j++)
                if (n.Strokes[j] is { Visible: true, Width: > 0 } && StrokeContains(n, j, p, 3)) return n;
        }
        return null;
    }
}
