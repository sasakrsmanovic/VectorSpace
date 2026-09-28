using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Skia;

internal static class EditingBenchmarks
{
    public static int Run()
    {
        var node = new DesignNode { Kind = NodeKind.Path, Width = 512, Height = 384, PathWidth = 512, PathHeight = 384, Fills = [], Strokes = [new() { Width = 1 }] };
        for (var i = 0; i < 1000; i++) node.Points.Add(new() { Position = new(i % 500, (i * 7) % 380) });
        using var renderer = new SceneRenderer();
        using var reference = SKPath.ParseSvgPathData(VectorPath.Build(node));
        var direct = renderer.Geometry(node);
        if (reference.ToSvgPathData() != direct.ToSvgPathData()) throw new InvalidOperationException("Geometry differs.");
        for (var i = 0; i < 15; i++) { Reference(); Direct(); }
        var results = new List<(double ReferenceMs, long ReferenceBytes, double DirectMs, long DirectBytes)>();
        for (var batch = 0; batch < 5; batch++)
        {
            var first = Measure(Reference); var second = Measure(Direct); results.Add((first.Time, first.Bytes, second.Time, second.Bytes));
        }
        var output = new
        {
            Workload = "1000-anchor open path; 60 geometry rebuilds/batch; five warmed batches; CPU geometry only",
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            Segments = 999, FramesPerBatch = 60,
            SvgRoundTripMedianMs = results.Select(r => r.ReferenceMs).Order().ElementAt(2),
            DirectMedianMs = results.Select(r => r.DirectMs).Order().ElementAt(2),
            SvgRoundTripBytesPerRebuild = results.Select(r => r.ReferenceBytes / 60).Order().ElementAt(2),
            DirectBytesPerRebuild = results.Select(r => r.DirectBytes / 60).Order().ElementAt(2),
            EquivalentGeometry = true,
            Scope = "Managed allocation excludes native Skia. No UI, painting, history, hit testing or end-to-end FPS claim. Direct path includes renderer cache descriptor storage."
        };
        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true })); return 0;
        void Reference() { using var path = SKPath.ParseSvgPathData(VectorPath.Build(node)); }
        void Direct() { node.Points[0].Position = new(node.Points[0].Position.X == 0 ? 1 : 0, 0); renderer.Geometry(node); }
        static (double Time, long Bytes) Measure(Action action)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < 60; i++) action();
            return (Stopwatch.GetElapsedTime(start).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
    }
}
