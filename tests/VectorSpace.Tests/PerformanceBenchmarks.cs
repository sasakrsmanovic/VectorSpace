using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using VectorSpace.Core;
using VectorSpace.Layout;

internal static class PerformanceBenchmarks
{
    public static int Run()
    {
        const int count = 10000, queries = 500;
        var random = new Random(90210);
        var targets = Enumerable.Range(0, count).Select(_ => new RectD(random.NextDouble() * 100000, random.NextDouble() * 100000, 40 + random.NextDouble() * 120, 40 + random.NextDouble() * 120)).ToArray();
        var moving = Enumerable.Range(0, queries).Select(_ => new RectD(random.NextDouble() * 100000, random.NextDouble() * 100000, 80, 60)).ToArray();
        var start = Stopwatch.GetTimestamp(); var index = new SnapIndex(targets); var buildMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        for (var i = 0; i < 100; i++) { index.Snap(moving[i], 5); SnapEngine.Snap(moving[i], targets, 5); }
        var linear = Measure(false); var indexed = Measure(true);
        // Compare values before reporting a speedup. The linear implementation is the retained oracle.
        foreach (var rectangle in moving)
        {
            var reference = SnapEngine.Snap(rectangle, targets, 5); var actual = index.Snap(rectangle, 5);
            if (reference.Correction != actual.Correction || !reference.Lines.SequenceEqual(actual.Lines)) throw new InvalidOperationException("Benchmark result mismatch.");
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            benchmark = "gesture-snap-index", framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            targets = count, queries, trials = 5, buildMilliseconds = buildMs,
            linearMedianMilliseconds = linear.Ms, indexedMedianMilliseconds = indexed.Ms,
            linearBytesPerQuery = linear.Bytes / queries, indexedBytesPerQuery = indexed.Bytes / queries,
            querySpeedup = linear.Ms / indexed.Ms, verifiedEquivalent = true,
            scope = "Synthetic CPU query benchmark; excludes index build, UI, rendering and GPU frame time."
        }));
        return 0;
        (double Ms, long Bytes) Measure(bool fast)
        {
            var samples = new double[5]; long bytes = 0; double checksum = 0;
            for (var trial = 0; trial < samples.Length; trial++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp();
                foreach (var rectangle in moving)
                {
                    var result = fast ? index.Snap(rectangle, 5) : SnapEngine.Snap(rectangle, targets, 5);
                    checksum += result.Correction.X + result.Correction.Y;
                }
                samples[trial] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            GC.KeepAlive(checksum); Array.Sort(samples); return (samples[2], bytes);
        }
    }
}
