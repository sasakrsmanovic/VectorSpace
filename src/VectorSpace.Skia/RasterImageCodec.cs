using SkiaSharp;
using VectorSpace.Documents;

namespace VectorSpace.Skia;

public sealed record ImportedImage(string DataUri, int Width, int Height);

/// <summary>Eager, bounded raster decoding. Header limits are checked before allocating pixel memory.
/// Imported images are orientation-normalized to self-contained PNGs; no external resources are loaded.</summary>
public static class RasterImageCodec
{
    public static ImportedImage Import(ReadOnlySpan<byte> bytes)
    {
        using var image = Decode(bytes);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidDataException("Cannot encode image.");
        if (png.Size > EmbeddedImage.MaxEncodedBytes) throw new InvalidDataException("Normalized image exceeds the 8 MiB embedded-image limit.");
        return new("data:image/png;base64," + Convert.ToBase64String(png.ToArray()), image.Width, image.Height);
    }

    public static SKImage Decode(ReadOnlySpan<byte> bytes) => Decode(bytes, long.MaxValue);

    /// <summary>Enforces a caller pixel budget before allocating the codec destination bitmap.</summary>
    public static SKImage Decode(ReadOnlySpan<byte> bytes, long maxDecodedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDecodedBytes);
        if (bytes.Length is 0 or > EmbeddedImage.MaxEncodedBytes) throw new InvalidDataException("Images are limited to 8 MiB encoded data.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported or damaged raster image.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
            throw new InvalidDataException("Choose a PNG, JPEG or WebP image.");
        var source = codec.Info;
        if (source.Width is <= 0 or > EmbeddedImage.MaxDimension || source.Height is <= 0 or > EmbeddedImage.MaxDimension ||
            (long)source.Width * source.Height > EmbeddedImage.MaxPixels)
            throw new InvalidDataException("Images are limited to 8192 pixels per edge and 16 megapixels.");
        if ((long)source.Width * source.Height * 4 > maxDecodedBytes)
            throw new InvalidDataException("Image exceeds the configured decoded-image cache budget.");
        if (codec.FrameCount > 1) throw new InvalidDataException("Animated images are not supported; export a still frame first.");
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        if (bitmap.GetPixels() == IntPtr.Zero || codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Image pixel decoding failed.");
        // The pixels will never be mutated again. Skia may share immutable pixel storage
        // with the returned image instead of making another full-sized raster copy.
        bitmap.SetImmutable();
        if (codec.EncodedOrigin == SKEncodedOrigin.TopLeft) return SKImage.FromBitmap(bitmap);
        var transpose = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        using var surface = SKSurface.Create(new SKImageInfo(transpose ? info.Height : info.Width, transpose ? info.Width : info.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Unable to allocate image orientation surface.");
        var m = codec.EncodedOrigin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, info.Width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, info.Width, 0, -1, info.Height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, info.Height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, info.Height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, info.Height, -1, 0, info.Width, 0, 0, 1),
            _ => new SKMatrix(0, 1, 0, -1, 0, info.Width, 0, 0, 1)
        };
        surface.Canvas.Clear(SKColors.Transparent); surface.Canvas.Concat(m); surface.Canvas.DrawBitmap(bitmap, 0, 0);
        return surface.Snapshot();
    }
}
