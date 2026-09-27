using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Clockwise resize handles, starting at the top-left corner.</summary>
public enum ResizeHandle { TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

/// <summary>A resize in the gesture's original local coordinate system. Anchor coordinates
/// are normalized (0 = start, .5 = center, 1 = end), independent of rotation and reflection.</summary>
public readonly record struct ResizePlan(RectD Bounds, Vec2 Anchor, bool ChangesWidth, bool ChangesHeight);

/// <summary>Pure, allocation-free handle geometry shared by native and browser editors.</summary>
public static class ResizeGeometry
{
    public static ResizePlan Calculate(DesignNode baseline, ResizeHandle handle, Vec2 pointer,
        bool preserveAspect = false, bool fromCenter = false)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return Calculate(new(baseline.Width, baseline.Height), handle, pointer,
            new(baseline.MinWidth, baseline.MinHeight), new(baseline.MaxWidth, baseline.MaxHeight),
            preserveAspect, fromCenter);
    }

    /// <param name="pointer">Pointer position expressed in the original selection's local coordinates.</param>
    /// <remarks>Dimensions stop at their minimum instead of inverting the shape. If an aspect ratio
    /// cannot satisfy both axes' limits, the explicit limits win over aspect preservation.</remarks>
    public static ResizePlan Calculate(Vec2 size, ResizeHandle handle, Vec2 pointer, Vec2 minimum,
        Vec2 maximum, bool preserveAspect = false, bool fromCenter = false)
    {
        if ((uint)handle > (uint)ResizeHandle.Left) throw new ArgumentOutOfRangeException(nameof(handle));
        if (!Finite(size) || size.X < 1 || size.Y < 1) throw new ArgumentOutOfRangeException(nameof(size));
        if (!Finite(pointer)) throw new ArgumentOutOfRangeException(nameof(pointer));
        if (!Finite(minimum) || !Finite(maximum) || maximum.X < Math.Max(1, minimum.X) || maximum.Y < Math.Max(1, minimum.Y))
            throw new ArgumentOutOfRangeException(nameof(maximum));
        minimum = new(Math.Max(1, minimum.X), Math.Max(1, minimum.Y));
        var x = handle switch { ResizeHandle.TopLeft or ResizeHandle.BottomLeft or ResizeHandle.Left => -1,
            ResizeHandle.TopRight or ResizeHandle.Right or ResizeHandle.BottomRight => 1, _ => 0 };
        var y = handle switch { ResizeHandle.TopLeft or ResizeHandle.Top or ResizeHandle.TopRight => -1,
            ResizeHandle.BottomLeft or ResizeHandle.Bottom or ResizeHandle.BottomRight => 1, _ => 0 };
        var changesWidth = x != 0 || preserveAspect;
        var changesHeight = y != 0 || preserveAspect;
        var width = RequestedSize(size.X, pointer.X, x, fromCenter);
        var height = RequestedSize(size.Y, pointer.Y, y, fromCenter);
        if (preserveAspect)
        {
            // Side handles must use their active axis: including the unchanged scale (1)
            // would make Shift-drag unable to shrink. Corners retain the containing scale.
            var scale = x == 0 ? height / size.Y : y == 0 ? width / size.X : Math.Max(width / size.X, height / size.Y);
            var lower = Math.Max(minimum.X / size.X, minimum.Y / size.Y);
            var upper = Math.Min(maximum.X / size.X, maximum.Y / size.Y);
            if (lower <= upper) scale = Math.Clamp(scale, lower, upper);
            width = size.X * scale; height = size.Y * scale;
        }
        if (changesWidth) width = Math.Clamp(width, minimum.X, maximum.X);
        if (changesHeight) height = Math.Clamp(height, minimum.Y, maximum.Y);
        var anchor = new Vec2(Anchor(x, changesWidth, fromCenter), Anchor(y, changesHeight, fromCenter));
        return new(new((size.X - width) * anchor.X, (size.Y - height) * anchor.Y, width, height),
            anchor, changesWidth, changesHeight);
    }

    private static double RequestedSize(double original, double pointer, int direction, bool centered) => direction switch
    {
        -1 => Math.Max(1, original - pointer * (centered ? 2 : 1)),
        1 => Math.Max(1, centered ? pointer * 2 - original : pointer),
        _ => original
    };
    private static double Anchor(int direction, bool changed, bool centered) =>
        !changed ? 0 : centered || direction == 0 ? .5 : direction < 0 ? 1 : 0;
    private static bool Finite(Vec2 value) => double.IsFinite(value.X) && double.IsFinite(value.Y);
}
