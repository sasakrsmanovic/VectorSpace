using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

internal static class EditingWorkflowBenchmarks
{
    public static int Run()
    {
        const int count = 20000;
        var initial = Enumerable.Range(0, count).ToArray(); var selected = initial.Where(i => i % 2 == 0).ToArray(); var set = selected.ToHashSet();
        void Reference(List<int> list) { foreach (var item in selected) { list.Remove(item); list.Add(item); } }
        void Stable(List<int> list) => LayerOrdering.Move(list, set, 1, true);
        var expected = initial.ToList(); Reference(expected);
        var actual = initial.ToList(); Stable(actual);
        if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Stable order differs from the remove/insert reference.");
        var old = new List<double>(); var fast = new List<double>(); long fastAllocation = 0;
        for (var i = 0; i < 6; i++)
        {
            var a = initial.ToList(); var b = initial.ToList();
            var sw = Stopwatch.StartNew(); Reference(a); sw.Stop(); if (i > 0) old.Add(sw.Elapsed.TotalMilliseconds);
            sw.Restart(); var start = GC.GetAllocatedBytesForCurrentThread(); Stable(b); var allocated = GC.GetAllocatedBytesForCurrentThread() - start; sw.Stop();
            if (i > 0) { fast.Add(sw.Elapsed.TotalMilliseconds); fastAllocation = allocated; }
            if (!a.SequenceEqual(b)) throw new InvalidOperationException("Measured order changed results.");
        }
        var shallow = new DesignNode { Kind = NodeKind.Frame, Fills = [new() { Kind = FillKind.LinearGradient }], Strokes = [new() { Dashes = [2, 4] }] };
        var deep = new DesignNode { Kind = NodeKind.Frame, Fills = StyleCloner.Fills(shallow.Fills), Strokes = StyleCloner.Strokes(shallow.Strokes) };
        for (var i = 0; i < 10000; i++) deep.Add(new() { Name = "Unrelated descendant " + i });
        LayerProperties.Capture(shallow); LayerProperties.Capture(deep);
        long CaptureBytes(DesignNode node)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) LayerProperties.Capture(node);
            return (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
        }
        var shallowBytes = CaptureBytes(shallow); var deepBytes = CaptureBytes(deep);
        if (shallowBytes != deepBytes) throw new InvalidOperationException("Property capture allocated based on unrelated descendants.");
        var packet = PropertyClipboard.Copy(deep); var properties = PropertyClipboard.Read(packet).Properties;
        if (!StyleCloner.SameFills(properties.Fills!, shallow.Fills) || packet.Contains("Unrelated descendant")) throw new InvalidOperationException("Style packet captured subtree content.");
        old.Sort(); fast.Sort();
        Console.WriteLine(new JsonObject
        {
            ["workload"] = "20,000 siblings, 10,000 alternating selected layers, stable move to front; five warmed samples",
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["referenceMedianMilliseconds"] = old[2], ["stableMedianMilliseconds"] = fast[2], ["stableTemporaryManagedBytes"] = fastAllocation,
            ["referenceEquivalent"] = true,
            ["propertyCaptureManagedBytesNoChildren"] = shallowBytes, ["propertyCaptureManagedBytes10000Children"] = deepBytes,
            ["propertyClipboardCharacters"] = packet.Length,
            ["scope"] = "The reference is a stable remove/append algorithm, not the old incorrectly ordered operation. Input construction, snapshots, validation, layout, synchronization, rendering, network and native allocations are excluded. Stable partition trades one bounded array for avoiding quadratic list shifts. Property capture shares immutable strings and does not visit descendants. No application-wide FPS claim."
        }.ToJsonString(new() { WriteIndented = true }));
        return 0;
    }
}
