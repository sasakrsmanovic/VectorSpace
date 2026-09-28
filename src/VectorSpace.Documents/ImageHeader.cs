using System.Buffers.Binary;

namespace VectorSpace.Documents;

/// <summary>Reads bounded raster dimensions without decoding pixels. Actual import additionally uses
/// SKCodec; a plausible header alone is not proof that compressed image content is valid.</summary>
public static class ImageHeader
{
    public static (int Width, int Height) Read(ReadOnlySpan<byte> b)
    {
        int w = 0, h = 0;
        if (b.Length >= 24 && b[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        { w = BinaryPrimitives.ReadInt32BigEndian(b[16..]); h = BinaryPrimitives.ReadInt32BigEndian(b[20..]); }
        else if (b.Length >= 4 && b[0] == 255 && b[1] == 216)
        {
            var i = 2;
            while (i + 4 <= b.Length)
            {
                if (b[i++] != 255) break;
                while (i < b.Length && b[i] == 255) i++;
                if (i >= b.Length) break; var marker = b[i++];
                if (marker is 0xD9 or 0xDA) break;
                if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7) continue;
                if (i + 2 > b.Length) break;
                var size = BinaryPrimitives.ReadUInt16BigEndian(b[i..]);
                if (size < 2 || size > b.Length - i) break;
                if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC) && size >= 7)
                { h = BinaryPrimitives.ReadUInt16BigEndian(b[(i + 3)..]); w = BinaryPrimitives.ReadUInt16BigEndian(b[(i + 5)..]); break; }
                i += size;
            }
        }
        else if (b.Length >= 30 && b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            if (b.Slice(12, 4).SequenceEqual("VP8X"u8))
            { w = 1 + b[24] + (b[25] << 8) + (b[26] << 16); h = 1 + b[27] + (b[28] << 8) + (b[29] << 16); }
            else if (b.Slice(12, 4).SequenceEqual("VP8L"u8) && b[20] == 0x2f)
            { w = 1 + b[21] + ((b[22] & 0x3f) << 8); h = 1 + (b[22] >> 6) + (b[23] << 2) + ((b[24] & 0x0f) << 10); }
            else if (b.Slice(12, 4).SequenceEqual("VP8 "u8) && b.Slice(23, 3).SequenceEqual(new byte[] { 0x9d, 1, 0x2a }))
            { w = BinaryPrimitives.ReadUInt16LittleEndian(b[26..]) & 0x3fff; h = BinaryPrimitives.ReadUInt16LittleEndian(b[28..]) & 0x3fff; }
        }
        if (w is <= 0 or > EmbeddedImage.MaxDimension || h is <= 0 or > EmbeddedImage.MaxDimension || (long)w * h > EmbeddedImage.MaxPixels)
            throw new InvalidDataException("Invalid raster header or image exceeds 8192 pixels per edge / 16 megapixels.");
        return (w, h);
    }
}
