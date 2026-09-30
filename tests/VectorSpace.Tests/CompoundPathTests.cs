using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static partial class CompoundPathTests
{
    private static void Check(bool value, string message = "Compound path assertion failed") { if (!value) throw new Exception(message); }
    private static void Near(Vec2 a, Vec2 b, double tolerance = 1e-6) => Check(a.DistanceTo(b) <= tolerance, $"{a} != {b}");
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static PathContour Loop(double x = 0, double y = 0, double size = 100, bool reverse = false)
    {
        var p = new[] { new Vec2(x, y), new Vec2(x + size, y), new Vec2(x + size, y + size), new Vec2(x, y + size) };
        if (reverse) Array.Reverse(p);
        return new() { Closed = true, Points = p.Select(v => new PathPoint { Position = v }).ToList() };
    }
    private static PathContour Line(params Vec2[] points) => new() { Points = points.Select(p => new PathPoint { Position = p }).ToList() };
    private static DesignNode Compound(params PathContour[] contours) => new()
    {
        Kind = NodeKind.Path, Width = 300, Height = 200, PathWidth = 300, PathHeight = 200,
        Contours = [.. contours], Fills = [new() { Color = "#2468CC" }]
    };
    private static DesignDocument Document(DesignNode n) => new() { Pages = [new() { Nodes = [n] }] };
    private static EditorSession Editor(DesignNode n) { var e = new EditorSession(Document(n)); e.Select(n); return e; }
    private static void PixelsEqual(byte[] a, byte[] b)
    {
        using var first = SKBitmap.Decode(a); using var second = SKBitmap.Decode(b);
        Check(first.Width == second.Width && first.Height == second.Height);
        for (var y = 0; y < first.Height; y++) for (var x = 0; x < first.Width; x++)
            Check(first.GetPixel(x, y) == second.GetPixel(x, y), $"Pixels changed at {x},{y}");
    }
    public static void Register(Action<string, Action> test)
    {
        RegisterGeometry(test); RegisterTopology(test); RegisterValidation(test); RegisterWorkflows(test);
        CompoundSubdivisionTests.Register(test);
    }
    private static void RegisterGeometry(Action<string, Action> test)
    {
        foreach (var evenOdd in new[] { false, true })
            test("editable compound holes preserve winding, rendering, picking and SVG " + evenOdd, () =>
            {
                var n = Compound(Loop(), Loop(25, 25, 50, !evenOdd), Loop(150, 0, 60)); n.FillRule = evenOdd ? PathFillRule.EvenOdd : PathFillRule.NonZero;
                using var renderer = new SceneRenderer(); var path = renderer.Geometry(n);
                Check(path.Contains(10, 10) && !path.Contains(50, 50) && path.Contains(170, 20) && !path.Contains(125, 30));
                Check(renderer.HitTest([n], new(50, 50), false, 0) is null);
                using var svg = SKPath.ParseSvgPathData(VectorPath.Build(n)); svg.FillType = path.FillType;
                Check(NativeShapeGeometry.Capture(svg).SequenceEqual(NativeShapeGeometry.Capture(path)));
                Check(VectorPath.Build(n).Count(c => c == 'M') == 3 && VectorPath.Build(n).Count(c => c == 'Z') == 3);
                var export = SceneSvg.Export(renderer, [n], n.LocalBounds); Check(export.Contains(evenOdd ? "evenodd" : "nonzero"));
            });
        test("mixed open and closed contours never add an inter-contour stroke", () =>
        {
            var n = Compound(Loop(0, 0, 50), Line(new(150, 10), new(200, 10))); n.Fills.Clear(); n.Strokes = [new() { Width = 4 }];
            using var r = new SceneRenderer(); Check(!NativeShapeGeometry.IsClosed(r.Geometry(n)));
            Check(r.HitTest([n], new(175, 10), false, 0) == n && r.HitTest([n], new(90, 10), false, 0) is null);
        });
        test("SVG compound conversion preserves exact polygon pixels and identities", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, Width = 100, Height = 100, PathData = "M0 0H100V100H0Z M20 20V80H80V20Z", Fill = "#1468C0" };
            var e = Editor(n); using var r = new SceneRenderer(); var before = r.ExportPng([n], new(-5, -5, 110, 110)); var json = DocumentJson.Save(e.Document);
            EditablePathConversion.Convert(e, r, n); Check(n.Contours?.Count == 2 && n.Points.Count == 0 && n.Commands is null && n.PathData is null);
            PixelsEqual(before, r.ExportPng([n], new(-5, -5, 110, 110))); Check(!r.Geometry(n).Contains(50, 50));
            e.Undo(); Check(DocumentJson.Save(e.Document) == json); e.Redo(); Check(e.Primary!.Contours?.Count == 2);
        });
        test("native ring conic conversion keeps both contours within its sampled approximation tolerance", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Ellipse, Width = 200, Height = 140, Arc = new(0, 360, .5) };
            var e = Editor(n); using var r = new SceneRenderer(); using var native = new SKPath(r.Geometry(n));
            EditablePathConversion.Convert(e, r, n); Check(n.Contours?.Count == 2);
            using var first = new SKPathMeasure(native, false, 1000); using var second = new SKPathMeasure(r.Geometry(n), false, 1000);
            do
            {
                Check(Math.Abs(first.Length - second.Length) < .02);
                for (var i = 0; i <= 500; i++) { var a = first.GetPosition(first.Length * i / 500); var b = second.GetPosition(second.Length * i / 500); Near(new(a.X, a.Y), new(b.X, b.Y), .01); }
                var more = first.NextContour(); Check(more == second.NextContour()); if (!more) break;
            } while (true);
            Check(!r.Geometry(n).Contains(100, 70));
        });
        test("contour cache invalidates handle, closure and boundary changes without paint invalidations", () =>
        {
            var n = Compound(Loop(), Loop(150, 0, 50)); using var r = new SceneRenderer(); var a = r.Geometry(n); var count = r.GeometryBuilds;
            n.Fill = "#FF0000"; n.X = 10; Check(ReferenceEquals(a, r.Geometry(n)) && r.GeometryBuilds == count);
            n.Contours![1].Points[0].ControlOut = new(170, 40); r.Geometry(n); Check(r.GeometryBuilds == ++count);
            n.Contours[1].Closed = false; r.Geometry(n); Check(r.GeometryBuilds == ++count);
            n.Contours.Reverse(); r.Geometry(n); Check(r.GeometryBuilds == ++count);
            n.Contours[0].Points[0].Position = new(151, 0); r.Geometry(n); Check(r.GeometryBuilds == ++count);
        });
        test("warm compound geometry comparisons allocate no managed objects", () =>
        {
            var n = Compound(Loop(), Loop(25, 25, 50, true)); using var r = new SceneRenderer();
            for (var i = 0; i < 500; i++) r.Geometry(n); var builds = r.GeometryBuilds; var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 500; i++) r.Geometry(n);
            Check(GC.GetAllocatedBytesForCurrentThread() - before == 0 && builds == r.GeometryBuilds);
        });
        test("native flatten and stroke outline clear the alternate editable representation", () =>
        {
            var n = Compound(Loop(), Loop(25, 25, 50, true)); n.Strokes = [new() { Width = 6, Alignment = StrokeAlignment.Outside }];
            var e = Editor(n); using var r = new SceneRenderer(); var before = r.ExportPng([n], new(-10, -10, 120, 120));
            LiveBooleanOperations.Outline(e, r); Check(n.Contours is null && n.Kind == NodeKind.Group && n.Children.All(c => c.Commands is not null && c.Contours is null));
            PixelsEqual(before, r.ExportPng([n], new(-10, -10, 120, 120))); DocumentJson.Validate(e.Document);
            e.Undo(); Check(e.Primary!.Contours?.Count == 2);
        });
    }
}
