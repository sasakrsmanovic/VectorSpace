using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Skia;

internal static class ImageAdmissionTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static string Image(int width = 32, int height = 16, bool noise = false)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var random = new Random(4721);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, noise
                    ? new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256))
                    : SKColors.Coral);
        bitmap.SetImmutable();
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(encoded.ToArray());
    }

    public static void Register(Action<string, Action> test)
    {
        test("raster metadata inspection reports exact dimensions without retaining encoded bytes", () =>
        {
            var data = Image();
            var info = EmbeddedImage.Inspect(data);
            Check(info.MimeType == "image/png" && info.Width == 32 && info.Height == 16 && info.DecodedBytes == 2048, "Invalid image metadata.");
            Check(info.EncodedBytes == EmbeddedImage.Decode(data).Bytes.Length, "Encoded size differs.");
            Check(EmbeddedImage.Inspect(data) == info && EmbeddedImage.Validate(data) == info.MimeType, "Memoized metadata changed.");
        });
        test("cache rejects a pixel-budget overflow before invoking the decoder", () =>
        {
            using var cache = new ImageAssetCache { ByteBudget = 2047 };
            var data = Image();
            Check(cache.Get(data) is null && cache.DecodeAttemptCount == 0 && cache.DecodeCount == 0, "Over-budget image reached pixel decoding.");
            Check(cache.Get(new string(data.AsSpan())) is null && cache.DecodeAttemptCount == 0 && cache.HitCount == 1, "Rejected payload was not negatively cached.");
            cache.ByteBudget = 2048;
            Check(cache.Get(data) is not null && cache.DecodeAttemptCount == 1 && cache.DecodedBytes == 2048, "Budget increase did not retry the image.");
        });
        test("zero image budget does not decode pixels or retain decoded memory", () =>
        {
            using var cache = new ImageAssetCache { ByteBudget = 0 };
            Check(cache.Get(Image()) is null && cache.DecodeAttemptCount == 0 && cache.DecodedBytes == 0, "Zero budget decoded pixels.");
        });
        test("codec caller budget rejects before raster allocation and preserves the original overload", () =>
        {
            var bytes = EmbeddedImage.Decode(Image()).Bytes;
            try { using var rejected = RasterImageCodec.Decode(bytes, 2047); throw new Exception("Codec ignored its pixel budget."); }
            catch (InvalidDataException) { }
            using var bounded = RasterImageCodec.Decode(bytes, 2048);
            using var original = RasterImageCodec.Decode(bytes);
            Check(bounded.Width == original.Width && bounded.Height == original.Height, "Budget changed decoded geometry.");
        });
        test("negative codec budget is rejected", () =>
        {
            try { using var image = RasterImageCodec.Decode([], -1); throw new Exception("Negative budget accepted."); }
            catch (ArgumentOutOfRangeException) { }
        });
        test("decoded image owns immutable pixels after codec locals are disposed", () =>
        {
            var bytes = EmbeddedImage.Decode(Image()).Bytes;
            using var image = RasterImageCodec.Decode(bytes);
            GC.Collect(); GC.WaitForPendingFinalizers();
            using var pixels = SKBitmap.FromImage(image);
            Check(pixels.GetPixel(12, 9) == SKColors.Coral, "Returned image lost its pixel ownership.");
        });
        test("content lookup for a cloned large payload avoids image-sized managed allocation", () =>
        {
            var data = Image(256, 256, noise: true);
            using var cache = new ImageAssetCache();
            var expected = cache.Get(data);
            cache.Get(new string(data.AsSpan())); // Warm digest and interop paths.
            var clone = new string(data.AsSpan());
            var before = GC.GetAllocatedBytesForCurrentThread();
            var actual = cache.Get(clone);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(ReferenceEquals(expected, actual) && cache.DecodeCount == 1, "Cloned content was decoded again.");
            Check(allocated < 16_384, $"Clone lookup allocated {allocated} bytes for {data.Length} characters.");
        });
        test("oversized cache keys are rejected before hashing or retaining a negative entry", () =>
        {
            using var cache = new ImageAssetCache();
            var data = new string('A', EmbeddedImage.MaxDataUriCharacters + 1);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var image = cache.Get(data);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(image is null && cache.Count == 0 && cache.DecodeAttemptCount == 0 && cache.LastError is not null, "Oversized key was admitted.");
            Check(allocated < 4096, $"Oversized key allocated {allocated} bytes.");
        });
        test("span Base64 decoder preserves whitespace compatibility and exact encoded bytes", () =>
        {
            var data = Image(); var offset = data.IndexOf(',') + 1;
            var wrapped = data[..offset] + " \r\n" + data[offset..] + "\t ";
            Check(EmbeddedImage.Decode(data).Bytes.AsSpan().SequenceEqual(EmbeddedImage.Decode(wrapped).Bytes), "Whitespace altered Base64 decoding.");
            Check(EmbeddedImage.Inspect(wrapped).DecodedBytes == 2048, "Whitespace altered dimensions.");
        });
        test("gradient capacity reduction immediately releases excess cached shaders", () =>
        {
            using var renderer = new SceneRenderer();
            var nodes = Enumerable.Range(0, 5).Select(_ => new DesignNode
            {
                Width = 20, Height = 20,
                Fills = [new() { Kind = FillKind.LinearGradient, Stops = [new() { Color = "#FF0000" }, new() { Offset = 1, Color = "#0000FF" }] }]
            }).ToArray();
            renderer.ExportPng(nodes, new(0, 0, 20, 20));
            Check(renderer.CachedGradientCount == 5, "Shaders were not cached.");
            renderer.GradientCacheCapacity = 1;
            Check(renderer.CachedGradientCount == 1, "Reducing capacity retained excess native shaders.");
            renderer.ExportPng(nodes, new(0, 0, 20, 20));
            Check(renderer.CachedGradientCount == 1, "Rendering exceeded the reduced capacity.");
        });
        test("effect capacity reduction immediately releases excess cached filter graphs", () =>
        {
            using var renderer = new SceneRenderer();
            var nodes = Enumerable.Range(0, 5).Select(_ => new DesignNode
            {
                Width = 20, Height = 20, Shadows = [new() { Blur = 2 }]
            }).ToArray();
            renderer.ExportPng(nodes, new(0, 0, 20, 20));
            Check(renderer.CachedEffectCount == 5, "Filter graphs were not cached.");
            renderer.EffectCacheCapacity = 1;
            Check(renderer.CachedEffectCount == 1, "Reducing capacity retained excess native filters.");
            renderer.ExportPng(nodes, new(0, 0, 20, 20));
            Check(renderer.CachedEffectCount == 1, "Rendering exceeded the reduced capacity.");
        });
        test("native appearance capacities reject invalid values without changing their budgets", () =>
        {
            using var renderer = new SceneRenderer();
            try { renderer.GradientCacheCapacity = 0; throw new Exception("Invalid gradient capacity accepted."); }
            catch (ArgumentOutOfRangeException) { }
            try { renderer.EffectCacheCapacity = -1; throw new Exception("Invalid effect capacity accepted."); }
            catch (ArgumentOutOfRangeException) { }
            Check(renderer.GradientCacheCapacity == 512 && renderer.EffectCacheCapacity == 256, "A rejected value changed the budget.");
        });
        foreach (var payload in new[] { "", "A", "AAAA=", "!@@@", "AA==AA==" })
            test("span Base64 rejects invalid payload " + payload, () =>
            {
                try { EmbeddedImage.Decode("data:image/png;base64," + payload); throw new Exception("Invalid Base64 accepted."); }
                catch (InvalidDataException) { }
            });
    }
}
