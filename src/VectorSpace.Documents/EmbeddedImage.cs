using System.Runtime.CompilerServices;

namespace VectorSpace.Documents;

/// <summary>Bounded embedded raster input shared by importers and renderers. Validation is memoized
/// by immutable string identity without keeping documents alive. No network/file access is performed.</summary>
public static class EmbeddedImage
{
    public const int MaxEncodedBytes = 8 * 1024 * 1024;
    public const int MaxPixels = 16_000_000;
    public const int MaxDimension = 8192;
    public const int MaxDataUriCharacters = 65 + (MaxEncodedBytes + 2) / 3 * 4;
    private sealed record Validated(EmbeddedImageInfo Info);
    private static readonly ConditionalWeakTable<string, Validated> Valid = new();

    public static string Validate(string data) => Inspect(data).MimeType;

    /// <summary>Returns memoized dimensions for admission checks without allocating decoded pixels.</summary>
    public static EmbeddedImageInfo Inspect(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Valid.GetValue(data, static value =>
        {
            var (mime, bytes) = Decode(value);
            if (bytes.Length < 12 || mime switch
            {
                "image/png" => !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                "image/jpeg" => bytes[0] != 255 || bytes[1] != 216 || bytes[2] != 255,
                "image/webp" => !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                _ => true
            }) throw new InvalidDataException("Embedded image header does not match its raster MIME type.");
            var (width, height) = ImageHeader.Read(bytes);
            return new(new(mime, width, height, bytes.Length));
        }).Info;
    }

    public static (string MimeType, byte[] Bytes) Decode(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > MaxDataUriCharacters) throw new InvalidDataException("An embedded image exceeds 8 MiB.");
        var comma = data.AsSpan(0, Math.Min(65, data.Length)).IndexOf(',');
        if (comma < 5 || !data.StartsWith("data:", StringComparison.Ordinal))
            throw new InvalidDataException("Only embedded PNG, JPEG and WebP images are accepted.");
        var header = data.AsSpan(5, comma - 5);
        if (!header.EndsWith(";base64", StringComparison.Ordinal)) throw new InvalidDataException("Images must use base64 encoding.");
        var mime = header[..^7].ToString();
        if (mime is not ("image/png" or "image/jpeg" or "image/webp")) throw new InvalidDataException("Unsupported image MIME type.");
        var encoded = data.AsSpan(comma + 1);
        if (encoded.Length > (MaxEncodedBytes + 2) / 3 * 4) throw new InvalidDataException("An embedded image exceeds 8 MiB.");
        // Decode the existing UTF-16 span directly instead of allocating a second, image-sized string.
        // The temporary destination is bounded even for invalid Base64 and whitespace-heavy input.
        var capacity = (encoded.Length + 3) / 4 * 3;
        if (encoded.Length > 0 && encoded.Length % 4 == 0)
        {
            if (encoded[^1] == '=') capacity--;
            if (encoded.Length > 1 && encoded[^2] == '=') capacity--;
        }
        var bytes = new byte[capacity];
        if (!Convert.TryFromBase64Chars(encoded, bytes, out var written)) throw new InvalidDataException("Invalid image base64.");
        if (written is 0 or > MaxEncodedBytes) throw new InvalidDataException("Invalid embedded image size.");
        if (written != bytes.Length) Array.Resize(ref bytes, written);
        return (mime, bytes);
    }
}
