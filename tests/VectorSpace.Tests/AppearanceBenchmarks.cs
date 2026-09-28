using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Skia;

internal static class AppearanceBenchmarks
{
    public static int Run()
    {
        const int samples = 10, trials = 5;
        using var bitmap = new SKBitmap(128, 64); bitmap.Erase(SKColors.Coral);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var asset = RasterImageCodec.Import(encoded.ToArray());
        var nodes = Enumerable.Range(0, 48).Select(i => new DesignNode
        {
            X = i % 8 * 64, Y = i / 8 * 64, Width = 56, Height = 56,
            Fills = [i % 2 == 0 ? new() { Kind = FillKind.Image, ImageData = asset.DataUri } : new FillStyle
            {
                Kind = FillKind.LinearGradient,
                Stops = [new() { Color = "#0D99FF" }, new() { Offset = .5, Color = "#14AE5C" }, new() { Offset = 1, Color = "#F24822" }]
            }],
            Shadows = i % 3 == 0 ? [new() { Blur = 4, X = 2, Y = 2 }, new() { Kind = EffectKind.InnerShadow, Blur = 3, X = 2, Y = 1 }] : []
        }).ToArray();
        DocumentJson.Validate(new() { Pages = [new() { Nodes = nodes.ToList() }] });
        using var renderer = new SceneRenderer(); using var surface = SKSurface.Create(new SKImageInfo(512, 384));
        void Draw() { surface.Canvas.Clear(); renderer.Draw(surface.Canvas, nodes); surface.Canvas.Flush(); }
        for (var i = 0; i < 10; i++) Draw();
        using var reference = surface.Snapshot(); using var referenceData = reference.Encode(SKEncodedImageFormat.Png, 100);
        var cold = Measure(true); var builds = (renderer.Images.DecodeCount, renderer.GradientBuilds, renderer.EffectBuilds);
        var warm = Measure(false);
        if (builds != (renderer.Images.DecodeCount, renderer.GradientBuilds, renderer.EffectBuilds)) throw new Exception("Stable appearance resources rebuilt during warm measurement.");
        using var result = surface.Snapshot(); using var resultData = result.Encode(SKEncodedImageFormat.Png, 100);
        if (!referenceData.AsSpan().SequenceEqual(resultData.AsSpan())) throw new Exception("Cached and rebuilt appearance pixels differ.");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            benchmark = "appearance-resource-retention", framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            nodes = nodes.Length, samples, trials, width = 512, height = 384,
            rebuiltMedianMillisecondsPerBatch = cold.Ms, retainedMedianMillisecondsPerBatch = warm.Ms,
            rebuiltManagedBytesPerFrame = cold.Bytes / samples, retainedManagedBytesPerFrame = warm.Bytes / samples,
            retainedImageDecodes = renderer.Images.DecodeCount - builds.DecodeCount,
            retainedGradientBuilds = renderer.GradientBuilds - builds.GradientBuilds,
            retainedEffectBuilds = renderer.EffectBuilds - builds.EffectBuilds,
            verifiedIdenticalPixels = true,
            scope = "CPU raster surface, 48 image/gradient layers and 16 two-shadow stacks. Rebuilt clears resource caches every frame; retained reuses them. Excludes Uno, browser, GPU, source preparation and PNG comparison; managed allocations exclude native memory. Not a comparison with Figma or whole-app FPS."
        }));
        return 0;
        (double Ms, long Bytes) Measure(bool clear)
        {
            var timings = new double[trials]; var allocations = new long[trials];
            for (var trial = 0; trial < trials; trial++)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
                for (var n = 0; n < samples; n++) { if (clear) renderer.ClearCache(); Draw(); }
                timings[trial] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                allocations[trial] = GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
            Array.Sort(timings); Array.Sort(allocations); return (timings[trials / 2], allocations[trials / 2]);
        }
    }
}
