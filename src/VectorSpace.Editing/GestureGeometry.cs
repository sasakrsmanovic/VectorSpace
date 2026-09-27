using VectorSpace.Core;
using VectorSpace.Layout;

namespace VectorSpace.Editing;

/// <summary>Baseline-relative transforms. Repeated pointer samples never compound rounding
/// or constraint clamping; Escape remains a single transaction rollback.</summary>
public static class GestureGeometry
{
    public static void Restore(DesignNode target, DesignNode baseline)
    {
        target.X = baseline.X; target.Y = baseline.Y; target.Width = baseline.Width; target.Height = baseline.Height;
        target.Rotation = baseline.Rotation; target.FlipX = baseline.FlipX; target.FlipY = baseline.FlipY;
        target.Layout.HugWidth = baseline.Layout.HugWidth; target.Layout.HugHeight = baseline.Layout.HugHeight;
        target.FillWidth = baseline.FillWidth; target.FillHeight = baseline.FillHeight;
        for (var i = 0; i < Math.Min(target.Children.Count, baseline.Children.Count); i++)
        {
            var child = target.Children[i]; var old = baseline.Children[i];
            if (child.Id == old.Id) Restore(child, old);
        }
    }
    /// <summary>Apply an explicit local resize box while retaining its normalized anchor under clamping.</summary>
    public static void Resize(DesignNode target, DesignNode baseline, double left, double top, double width, double height)
    {
        var changesWidth = Math.Abs(width - baseline.Width) > 1e-7;
        var changesHeight = Math.Abs(height - baseline.Height) > 1e-7;
        var anchor = new Vec2(changesWidth ? left / (baseline.Width - width) : 0,
            changesHeight ? top / (baseline.Height - height) : 0);
        ApplyResize(target, baseline, new(new(left, top, width, height), anchor, changesWidth, changesHeight));
    }

    /// <summary>Resize using the captured baseline, including rotated/flipped/nested nodes.
    /// Only manipulated axes become fixed; the other axis keeps its hug/fill mode.</summary>
    public static void ResizeFromHandle(DesignNode target, DesignNode baseline, ResizeHandle handle,
        Vec2 localPointer, bool preserveAspect = false, bool fromCenter = false) =>
        ApplyResize(target, baseline, ResizeGeometry.Calculate(baseline, handle, localPointer, preserveAspect, fromCenter));

    private static void ApplyResize(DesignNode target, DesignNode baseline, ResizePlan plan)
    {
        Restore(target, baseline);
        if (plan.ChangesWidth) { target.Layout.HugWidth = false; target.FillWidth = false; }
        if (plan.ChangesHeight) { target.Layout.HugHeight = false; target.FillHeight = false; }
        var anchorBefore = baseline.LocalMatrix.Map(new Vec2(plan.Bounds.X + plan.Bounds.Width * plan.Anchor.X, plan.Bounds.Y + plan.Bounds.Height * plan.Anchor.Y));
        LayoutEngine.Resize(target, plan.Bounds.Width, plan.Bounds.Height);
        // Reflow can change the untouched hugging axis. Use the actual arranged size and
        // the original linear transform, not an axis-aligned world bounding box.
        var offset = new Vec2((.5 - plan.Anchor.X) * target.Width, (.5 - plan.Anchor.Y) * target.Height);
        var center = anchorBefore + baseline.LocalMatrix.Map(offset) - baseline.LocalMatrix.Map(Vec2.Zero);
        target.X = center.X - target.Width / 2; target.Y = center.Y - target.Height / 2;
    }
    public static void Rotate(DesignNode target, DesignNode baseline, Vec2 worldPivot, double degrees)
    {
        Restore(target, baseline);
        var parent = target.Parent?.WorldMatrix ?? Matrix2D.Identity;
        var center = parent.Map(new Vec2(baseline.X + baseline.Width / 2, baseline.Y + baseline.Height / 2));
        var rotation = Matrix2D.Translation(-worldPivot.X, -worldPivot.Y) * Matrix2D.Rotation(degrees) * Matrix2D.Translation(worldPivot.X, worldPivot.Y);
        var transformed = parent.Inverse.Map(rotation.Map(center));
        target.X = transformed.X - target.Width / 2; target.Y = transformed.Y - target.Height / 2;
        var handedness = parent.M11 * parent.M22 - parent.M12 * parent.M21 < 0 ? -1 : 1;
        target.Rotation = baseline.Rotation + degrees * handedness;
    }
}
