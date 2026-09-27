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
    public static void Resize(DesignNode target, DesignNode baseline, double left, double top, double width, double height)
    {
        Restore(target, baseline);
        target.Layout.HugWidth = target.Layout.HugHeight = false;
        target.FillWidth = target.FillHeight = false;
        width = Math.Clamp(width, Math.Max(1, target.MinWidth), Math.Max(target.MinWidth, target.MaxWidth));
        height = Math.Clamp(height, Math.Max(1, target.MinHeight), Math.Max(target.MinHeight, target.MaxHeight));
        var center = baseline.LocalMatrix.Map(new Vec2(left + width / 2, top + height / 2));
        LayoutEngine.Resize(target, width, height);
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
