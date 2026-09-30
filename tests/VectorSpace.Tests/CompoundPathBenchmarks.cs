using System.Diagnostics;
using System.Text.Json;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

internal static class CompoundPathBenchmarks
{
    // Test-only reference keeps the former whole-path snapshot/reset strategy. Both
    // workloads expose the same flattened anchor order and receive identical input.
    private sealed class FullCapture(PathTopology topology)
    {
        private readonly PathPoint[] _snapshot = topology.Points.Select(Clone).ToArray();
        public void Apply(Vec2 delta)
        {
            for (var i = 0; i < _snapshot.Length; i++)
            {
                var p = topology.Points[i]; var b = _snapshot[i];
                p.Position = b.Position; p.ControlIn = b.ControlIn; p.ControlOut = b.ControlOut;
            }
            foreach (var i in Selection)
            {
                var p = topology.Points[i]; p.Position += delta; p.ControlIn += delta; p.ControlOut += delta;
            }
        }
        private static PathPoint Clone(PathPoint p) => new() { Position = p.Position, ControlIn = p.ControlIn, ControlOut = p.ControlOut };
    }
    private static readonly int[] Selection = [0, 9999];
    public static int Run()
    {
        var node = new DesignNode { Kind = NodeKind.Path, Width = 1000, Height = 1000, PathWidth = 1000, PathHeight = 1000, Contours = [] };
        for (var c = 0; c < 2500; c++) node.Contours.Add(new() { Closed = true, Points = Enumerable.Range(0, 4).Select(i => new PathPoint
        { Position = new((c % 50) * 20 + (i is 1 or 2 ? 10 : 0), (c / 50) * 20 + (i >= 2 ? 10 : 0)) }).ToList() });
        var reference = DocumentJson.CloneNode(node); var rt = new PathTopology(reference); var st = new PathTopology(node);
        var full = new FullCapture(rt); var sparse = new PathPointDrag(node, st, Selection);
        for (var i = 0; i < 61; i++)
        {
            var delta = new Vec2(i % 7, i % 11); full.Apply(delta); sparse.Apply(delta);
            for (var a = 0; a < rt.Points.Count; a++)
                if (rt.Points[a].Position != st.Points[a].Position || rt.Points[a].ControlIn != st.Points[a].ControlIn || rt.Points[a].ControlOut != st.Points[a].ControlOut)
                    throw new Exception("Sparse drag differs from the full snapshot oracle.");
        }
        full.Apply(Vec2.Zero); sparse.Apply(Vec2.Zero);
        const int interactions = 10, samples = 60;
        void Reference()
        {
            for (var drag = 0; drag < interactions; drag++) { var capture = new FullCapture(rt); for (var i = 0; i < samples; i++) capture.Apply(new(i % 7, i % 11)); capture.Apply(Vec2.Zero); }
        }
        void Sparse()
        {
            for (var drag = 0; drag < interactions; drag++) { var capture = new PathPointDrag(node, st, Selection); for (var i = 0; i < samples; i++) capture.Apply(new(i % 7, i % 11)); capture.Apply(Vec2.Zero); }
        }
        for (var i = 0; i < 3; i++) { Reference(); Sparse(); }
        (double Ms, long Bytes) Measure(Action action)
        {
            var before = GC.GetAllocatedBytesForCurrentThread(); var stamp = Stopwatch.GetTimestamp(); action();
            return (Stopwatch.GetElapsedTime(stamp).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        var old = new List<(double Ms, long Bytes)>(); var next = new List<(double Ms, long Bytes)>();
        for (var i = 0; i < 5; i++) { if (i % 2 == 0) { old.Add(Measure(Reference)); next.Add(Measure(Sparse)); } else { next.Add(Measure(Sparse)); old.Add(Measure(Reference)); } }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            workload = "10,000 anchors in 2,500 contours; two selected anchors; ten captures and 60 samples per capture; five interleaved warmed batches",
            equivalenceAnchorComparisons = 61 * 10000,
            fullSnapshotMedianMilliseconds = old.Select(x => x.Ms).Order().ElementAt(2),
            sparseMedianMilliseconds = next.Select(x => x.Ms).Order().ElementAt(2),
            fullSnapshotManagedBytesPerCapture = old.Select(x => x.Bytes).Order().ElementAt(2) / interactions,
            sparseManagedBytesPerCapture = next.Select(x => x.Bytes).Order().ElementAt(2) / interactions,
            capturedAnchors = sparse.CapturedAnchorCount,
            scope = "Capture and baseline-relative anchor mutation only. Topology retained in both paths. Excludes history/document cloning, layout, renderer cache scans, hit testing, painting, native allocation, UI, networking and cold startup. Not whole-editor FPS."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
