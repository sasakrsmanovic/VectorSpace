namespace VectorSpace.Documents;

/// <summary>Validated encoded-raster metadata; it does not certify pixel-stream decoding.</summary>
public readonly record struct EmbeddedImageInfo(string MimeType, int Width, int Height, int EncodedBytes)
{
    /// <summary>Required RGBA8 pixel storage, before codec scratch space or orientation copies.</summary>
    public long DecodedBytes => (long)Width * Height * 4;
}
