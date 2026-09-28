namespace VectorSpace.Core;

public enum ImageScaleMode { Fill, Fit, Crop, Tile }
public enum GradientSpread { Pad, Repeat, Reflect }
public enum EffectKind { DropShadow, InnerShadow, LayerBlur }

public sealed partial class FillStyle
{
    public BlendKind Blend { get; set; }
    /// <summary>Embedded raster data only. Never an HTTP, file, SVG or executable URL.</summary>
    public string? ImageData { get; set; }
    public ImageScaleMode ImageMode { get; set; }
    public double ImageScale { get; set; } = 1;
    /// <summary>Translation as a fraction of layer width/height; used by Crop and Tile.</summary>
    public Vec2 ImageOffset { get; set; }
    public double ImageRotation { get; set; }
    public double Exposure { get; set; }
    public double Contrast { get; set; }
    public double Saturation { get; set; }
    /// <summary>Maps gradient coordinates to local layer coordinates. With UserSpace false,
    /// normalized coordinates are scaled to the layer after this matrix.</summary>
    public Matrix2D GradientTransform { get; set; } = Matrix2D.Identity;
    public bool GradientUserSpace { get; set; }
    public GradientSpread Spread { get; set; }
    /// <summary>When supplied, radial gradients use this radius and focal point in gradient coordinates.
    /// Null retains legacy circular Start/End rendering in local layer coordinates.</summary>
    public double? GradientRadius { get; set; }
    public Vec2? GradientFocal { get; set; }
}

public sealed partial class ShadowStyle
{
    public EffectKind Kind { get; set; }
    /// <summary>Signed alpha-mask expansion in local units. Positive expands outer shadows,
    /// and erodes the occluding alpha for inner shadows.</summary>
    public double Spread { get; set; }
}

/// <summary>Backend-independent mapping from source image pixels to a layer's local coordinates.</summary>
public static class ImagePlacement
{
    public static Matrix2D Calculate(FillStyle fill, double width, double height, double imageWidth, double imageHeight)
    {
        ArgumentNullException.ThrowIfNull(fill);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 0 || height < 0 ||
            !double.IsFinite(imageWidth) || !double.IsFinite(imageHeight) || imageWidth <= 0 || imageHeight <= 0 ||
            !double.IsFinite(fill.ImageScale) || fill.ImageScale <= 0 || !fill.ImageOffset.IsFinite || !double.IsFinite(fill.ImageRotation))
            throw new ArgumentException("Image placement requires finite geometry and a positive scale.");
        var angle = fill.ImageRotation * Math.PI / 180;
        var c = Math.Abs(Math.Cos(angle)); var s = Math.Abs(Math.Sin(angle));
        double scale;
        if (fill.ImageMode == ImageScaleMode.Tile) scale = fill.ImageScale;
        else if (fill.ImageMode == ImageScaleMode.Fit)
            scale = Math.Min(width / (imageWidth * c + imageHeight * s), height / (imageWidth * s + imageHeight * c));
        else
            scale = Math.Max((width * c + height * s) / imageWidth, (width * s + height * c) / imageHeight)
                * (fill.ImageMode == ImageScaleMode.Crop ? fill.ImageScale : 1);
        scale = Math.Max(scale, 1e-9);
        var offset = fill.ImageMode is ImageScaleMode.Crop or ImageScaleMode.Tile ? fill.ImageOffset : Vec2.Zero;
        return Matrix2D.Translation(-imageWidth / 2, -imageHeight / 2) * Matrix2D.Scale(scale, scale)
            * Matrix2D.Rotation(fill.ImageRotation) * Matrix2D.Translation(width * (.5 + offset.X), height * (.5 + offset.Y));
    }
}
