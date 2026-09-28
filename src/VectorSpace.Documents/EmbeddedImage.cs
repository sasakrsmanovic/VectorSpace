using System.Runtime.CompilerServices;

namespace VectorSpace.Documents;

/// <summary>Bounded embedded raster input shared by importers and renderers. Validation is memoized
/// by immutable string identity without keeping documents alive. No network/file access is performed.</summary>
public static class EmbeddedImage
{
    public const int MaxEncodedBytes = 8 * 1024 * 1024;
    public const int MaxPixels = 16_000_000;
    public const int MaxDimension = 8192;
    private sealed record Validated(string MimeType);
    private static readonly ConditionalWeakTable<string, Validated> Valid = new();

    public static string Validate(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Valid.GetValue(data, value =>
        {
            var (mime, bytes) = Decode(value);
            if (bytes.Length < 12 || mime switch
            {
                "image/png" => !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                "image/jpeg" => bytes[0] != 255 || bytes[1] != 216 || bytes[2] != 255,
                "image/webp" => !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                _ => true
            }) throw new InvalidDataException("Embedded image header does not match its raster MIME type.");
            ImageHeader.Read(bytes);
            return new(mime);
        }).MimeType;
    }

    public static (string MimeType, byte[] Bytes) Decode(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var comma = data.IndexOf(',');
        if (comma < 0 || comma > 64 || !data.StartsWith("data:", StringComparison.Ordinal))
            throw new InvalidDataException("Only embedded PNG, JPEG and WebP images are accepted.");
        var header = data.AsSpan(5, comma - 5);
        if (!header.EndsWith(";base64", StringComparison.Ordinal)) throw new InvalidDataException("Images must use base64 encoding.");
        var mime = header[..^7].ToString();
        if (mime is not ("image/png" or "image/jpeg" or "image/webp")) throw new InvalidDataException("Unsupported image MIME type.");
        var encoded = data.AsSpan(comma + 1);
        if (encoded.Length > (MaxEncodedBytes + 2) / 3 * 4) throw new InvalidDataException("An embedded image exceeds 8 MiB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(data[(comma + 1)..]); }
        catch (FormatException e) { throw new InvalidDataException("Invalid image base64.", e); }
        if (bytes.Length is 0 or > MaxEncodedBytes) throw new InvalidDataException("Invalid embedded image size.");
        return (mime, bytes);
    }
}
