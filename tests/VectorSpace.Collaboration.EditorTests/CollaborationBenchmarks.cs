using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VectorSpace.Collaboration;
using VectorSpace.Core;
using VectorSpace.Documents;

internal static class CollaborationBenchmarks
{
    public static int Run()
    {
        var document = new DesignDocument { Id = "benchmark", Pages = [new() { Id = "page", Nodes = Enumerable.Range(0, 1000).Select(i => new DesignNode { Id = "node-" + i, X = i % 100 * 100, Y = i / 100 * 100 }).ToList() }] };
        var initial = DocumentProjection.FromDocument(document);
        document.Pages[0].Nodes[500].X += 17;
        var json = DocumentJson.Save(document);
        List<CellChange> Project() => DocumentProjection.Diff(initial, DocumentProjection.FromJson(json, initial));
        var changes = Project();
        if (changes.Count != 1) throw new InvalidOperationException("One property edit must produce exactly one wire cell.");
        var batch = new EditBatch("benchmark-edit", new string('a', 32), 1, "Move layer", 0, changes);
        var wire = JsonSerializer.SerializeToUtf8Bytes(batch, CollaborationJson.Default.EditBatch);
        var engine = new TransactionEngine(initial); var commit = engine.Prepare(batch, RoomRole.Editor, "Benchmark"); engine.Accept(commit);
        if (DocumentJson.Save(DocumentProjection.ToDocument(engine.State)) != json) throw new InvalidOperationException("Property delta differs from native document state.");
        List<CellChange> Reference() => DocumentProjection.Diff(initial, ProjectionOracle.FromJson(json, initial));
        if (!changes.SequenceEqual(Reference())) throw new InvalidOperationException("Wire cells differ from reference.");
        var checks = ProjectionChecks.Run();
        for (var i = 0; i < 3; i++) { Project(); Reference(); }
        var optimized = new List<(double Time, long Bytes)>(); var reference = new List<(double Time, long Bytes)>();
        for (var i = 0; i < 5; i++)
        {
            if (i % 2 == 0) { reference.Add(Measure(Reference)); optimized.Add(Measure(Project)); }
            else { optimized.Add(Measure(Project)); reference.Add(Measure(Reference)); }
        }
        static (double Time, long Bytes) Measure(Func<List<CellChange>> project)
        {
            var clock = new Stopwatch(); var before = GC.GetAllocatedBytesForCurrentThread(); clock.Start();
            var result = project(); clock.Stop(); var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            if (result.Count != 1) throw new InvalidOperationException("Measured projection changed results.");
            return (clock.Elapsed.TotalMilliseconds, bytes);
        }
        var fastTime = optimized.Select(x => x.Time).Order().ElementAt(2);
        var oldTime = reference.Select(x => x.Time).Order().ElementAt(2);
        var fastBytes = optimized.Select(x => x.Bytes).Order().ElementAt(2);
        var oldBytes = reference.Select(x => x.Bytes).Order().ElementAt(2);
        var result = new JsonObject
        {
            ["workload"] = "1000 native scene layers; one scalar property changed; five warmed projection/diff samples",
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["nativeDocumentUtf8Bytes"] = Encoding.UTF8.GetByteCount(json),
            ["editBatchUtf8Bytes"] = wire.Length,
            ["changedCells"] = changes.Count,
            ["referenceMedianMilliseconds"] = oldTime, ["retainedMedianMilliseconds"] = fastTime,
            ["referenceMedianManagedBytes"] = oldBytes, ["retainedMedianManagedBytes"] = fastBytes,
            ["observedProjectionSpeedRatio"] = oldTime / Math.Max(fastTime, .0001),
            ["equivalenceAndReuseChecks"] = checks,
            ["exactNativeRoundtripVerified"] = true,
            ["scope"] = "Wire reduction is verified; projection still visits the whole document. Network latency, server durability, Uno UI, painting, native allocations and embedded-image workloads are excluded. No FPS or constant-time claim."
        };
        Console.WriteLine(result.ToJsonString(new() { WriteIndented = true })); return 0;
    }
}
