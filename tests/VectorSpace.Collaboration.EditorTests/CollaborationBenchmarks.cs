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
        for (var i = 0; i < 3; i++) Project();
        var elapsed = new List<double>(); var allocated = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew();
            Project(); clock.Stop(); elapsed.Add(clock.Elapsed.TotalMilliseconds); allocated.Add(GC.GetAllocatedBytesForCurrentThread() - before);
        }
        elapsed.Sort(); allocated.Sort();
        var result = new JsonObject
        {
            ["workload"] = "1000 native scene layers; one scalar property changed; five warmed projection/diff samples",
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["nativeDocumentUtf8Bytes"] = Encoding.UTF8.GetByteCount(json),
            ["editBatchUtf8Bytes"] = wire.Length,
            ["changedCells"] = changes.Count,
            ["projectionMedianMilliseconds"] = elapsed[2],
            ["projectionMedianManagedBytes"] = allocated[2],
            ["exactNativeRoundtripVerified"] = true,
            ["scope"] = "Wire reduction is verified; projection still visits the whole document. Network latency, server durability, Uno UI, painting, native allocations and embedded-image workloads are excluded. No FPS or constant-time claim."
        };
        Console.WriteLine(result.ToJsonString(new() { WriteIndented = true })); return 0;
    }
}
