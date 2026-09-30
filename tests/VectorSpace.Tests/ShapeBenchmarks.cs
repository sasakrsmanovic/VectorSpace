using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Skia;

internal static class ShapeBenchmarks
{
    public static int Run()
    {
        using var renderer = new SceneRenderer();
        var nodes = Enumerable.Range(0, 160).Select(i => new DesignNode
        {
            Id = "stroke-" + i, Kind = i % 2 == 0 ? NodeKind.Rectangle : NodeKind.Ellipse,
            Width = 80 + i % 9, Height = 60 + i % 7, Corners = i % 2 == 0 ? new(5, 10, 15, 20) : null,
            Arc = i % 2 == 0 ? null : new(-30, 285, .4),
            Strokes = [new() { Width = 3 + i % 5, Alignment = (StrokeAlignment)(i % 3), Cap = (StrokeCap)(i % 3), Join = (StrokeJoin)(i % 3), Dashes = i % 4 == 0 ? [7, 5] : [] }]
        }).ToArray();
        foreach (var node in nodes)
        {
            var cached = renderer.StrokeGeometry(node, 0);
            using var referenceRegion = SceneRenderer.BuildStrokeRegion(renderer.Geometry(node), node.Strokes[0]);
            if (!NativeShapeGeometry.Capture(cached).SequenceEqual(NativeShapeGeometry.Capture(referenceRegion)))
                throw new InvalidOperationException("Cached stroke geometry differs from the independently rebuilt region.");
        }
        var buildCount = renderer.StrokeBuilds;
        double checksum = 0;
        void Retained() { foreach (var node in nodes) checksum += renderer.StrokeGeometry(node, 0).Bounds.Width; }
        void Rebuilt() { foreach (var node in nodes) { using var region = SceneRenderer.BuildStrokeRegion(renderer.Geometry(node), node.Strokes[0]); checksum += region.Bounds.Width; } }
        for (var i = 0; i < 3; i++) { Retained(); Rebuilt(); }
        const int repetitions = 10;
        var retained = new List<(double Time, long Bytes)>(); var rebuilt = new List<(double Time, long Bytes)>();
        for (var i = 0; i < 5; i++)
        {
            if (i % 2 == 0) { rebuilt.Add(Measure(Rebuilt)); retained.Add(Measure(Retained)); }
            else { retained.Add(Measure(Retained)); rebuilt.Add(Measure(Rebuilt)); }
        }
        if (renderer.StrokeBuilds != buildCount || checksum <= 0) throw new InvalidOperationException("Stable retained queries rebuilt native stroke regions.");
        (double Time, long Bytes) Measure(Action action)
        {
            var clock = new Stopwatch(); var bytes = GC.GetAllocatedBytesForCurrentThread(); clock.Start();
            for (var i = 0; i < repetitions; i++) action();
            clock.Stop(); return (clock.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes);
        }
        var oldTime = rebuilt.Select(x => x.Time).Order().ElementAt(2); var newTime = retained.Select(x => x.Time).Order().ElementAt(2);
        var oldBytes = rebuilt.Select(x => x.Bytes).Order().ElementAt(2); var newBytes = retained.Select(x => x.Bytes).Order().ElementAt(2);
        var result = new JsonObject
        {
            ["workload"] = "160 native rectangle/ring-sector paths; centered/inside/outside strokes, caps/joins and mixed dashes; ten passes per batch, five interleaved warmed batches",
            ["runtime"] = RuntimeInformation.FrameworkDescription, ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["rebuiltMedianMilliseconds"] = oldTime, ["retainedMedianMilliseconds"] = newTime,
            ["rebuiltManagedBytesPerQuery"] = oldBytes / (nodes.Length * repetitions), ["retainedManagedBytesPerQuery"] = newBytes / (nodes.Length * repetitions),
            ["observedGeometrySpeedRatio"] = oldTime / Math.Max(.0001, newTime), ["nativeCommandEquivalenceCases"] = nodes.Length,
            ["postWarmStrokeBuilds"] = renderer.StrokeBuilds - buildCount,
            ["scope"] = "Measures stroke-region construction versus descriptor-keyed reuse. Source paths are retained for both implementations. Native allocation, painting, UI, layout, history, networking and cold cache creation are excluded. Not an application FPS or GPU speedup."
        };
        Console.WriteLine(result.ToJsonString(new() { WriteIndented = true })); return 0;
    }
}
