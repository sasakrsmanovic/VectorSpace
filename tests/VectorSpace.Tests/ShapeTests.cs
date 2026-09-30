using SkiaSharp;
using System.Xml.Linq;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static partial class ShapeTests
{
    private static void Check(bool condition, string message = "Shape assertion failed") { if (!condition) throw new Exception(message); }
    private static void Near(double actual, double expected, double tolerance = .001) { if (Math.Abs(actual - expected) > tolerance) throw new Exception($"Expected {expected}; got {actual}."); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static DesignNode Node() => new() { Width = 100, Height = 80 };
    private static EditorSession Editor(params DesignNode[] nodes) => new(new() { Pages = [new() { Nodes = nodes.ToList() }] });
    private static SKBitmap Bitmap(SceneRenderer renderer, DesignNode node, RectD? bounds = null) => SKBitmap.Decode(renderer.ExportPng([node], bounds ?? new RectD(-40, -40, 240, 220)));
    private static bool SamePixels(SKBitmap a, SKBitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;
        for (var y = 0; y < a.Height; y++) for (var x = 0; x < a.Width; x++) if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
        return true;
    }
    public static void Register(Action<string, Action> test)
    {
        test("independent corners clamp for drawing without changing authored radii", () => {
            var n = Node(); n.Corners = new(300, 10, 20, 0); var r = n.EffectiveCorners;
            Check(r == new CornerRadii(40, 10, 20, 0) && n.Corners.TopLeft == 300); n.Width = 1000; n.Height = 800; Check(n.EffectiveCorners.TopLeft == 300);
        });
        test("corner geometry and portable rounded-box picking agree", () => {
            var n = Node(); n.Corners = new(35, 0, 20, 8); using var path = NativeShapeGeometry.Build(n);
            var random = new Random(8791);
            for (var i = 0; i < 1000; i++) { var p = new Vec2(random.NextDouble() * 120 - 10, random.NextDouble() * 100 - 10); Check(path.Contains((float)p.X, (float)p.Y) == ShapeGeometry.ContainsCornerBox(n, p)); }
        });
        test("independent corner pixels preserve asymmetric square corners", () => {
            var n = Node(); n.Corners = new(40, 0, 25, 0); n.Fill = "#FF0000"; using var renderer = new SceneRenderer(); using var image = Bitmap(renderer, n, n.LocalBounds);
            Check(image.GetPixel(2, 2).Alpha == 0 && image.GetPixel(97, 2).Red == 255 && image.GetPixel(97, 2).Alpha == 255);
            Check(image.GetPixel(97, 77).Alpha == 0 && image.GetPixel(2, 77).Alpha == 255);
        });
        test("frame clipping uses independent corners for rendering and picking", () => {
            var frame = Node(); frame.Kind = NodeKind.Frame; frame.Corners = new(35, 0, 0, 0); frame.Fills.Clear(); frame.ClipContent = true;
            frame.Add(new() { Width = 100, Height = 80, Fill = "#00FF00" }); using var r = new SceneRenderer(); using var image = Bitmap(r, frame, frame.LocalBounds);
            Check(image.GetPixel(2, 2).Alpha == 0 && r.HitTest([frame], new(2, 2), true, 0) is null);
            Check(r.HitTest([frame], new(95, 2), true, 0) == frame.Children[0]);
        });
        test("a full ring has an exact native hole", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(0, 360, .5); using var r = new SceneRenderer(); var p = r.Geometry(n);
            Check(!p.Contains(50, 40) && p.Contains(90, 40)); Check(r.HitTest([n], new(50, 40), false, 0) is null);
            using var image = Bitmap(r, n, n.LocalBounds); Check(image.GetPixel(50, 40).Alpha == 0);
        });
        foreach (var sweep in new[] { 90d, -90d })
            test("signed ellipse sector " + sweep, () => {
                var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(0, sweep); using var p = NativeShapeGeometry.Build(n);
                Check(p.Contains(70, sweep > 0 ? 55 : 25)); Check(!p.Contains(30, 40)); Check(!p.Contains(70, sweep > 0 ? 25 : 55));
            });
        test("zero sweep and fully hollow ellipse produce empty geometry", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(10, 0); using var a = NativeShapeGeometry.Build(n); Check(a.IsEmpty);
            n.Arc = new(0, 360, 1); using var b = NativeShapeGeometry.Build(n); Check(b.IsEmpty);
        });
        test("open ellipse arc never fills its implicit chord", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(0, 270, 0, true); n.Fill = "#FF0000"; n.Strokes = [new() { Width = 4 }]; using var r = new SceneRenderer();
            using var image = Bitmap(r, n, n.LocalBounds); Check(image.GetPixel(50, 40).Alpha == 0); Check(r.HitTest([n], new(50, 40), false, 0) is null);
        });
        foreach (var alignment in Enum.GetValues<StrokeAlignment>())
            test("stroke region bounds " + alignment, () => {
                var n = Node(); n.Strokes = [new() { Width = 10, Alignment = alignment }]; using var r = new SceneRenderer(); var b = r.StrokeGeometry(n, 0).TightBounds;
                var outset = alignment == StrokeAlignment.Center ? 5 : alignment == StrokeAlignment.Outside ? 10 : 0;
                Near(b.Left, -outset); Near(b.Top, -outset); Near(b.Right, 100 + outset); Near(b.Bottom, 80 + outset);
            });
        test("inside versus outside picking agrees with rendered stroke at zero tolerance", () => {
            var n = Node(); n.Fills.Clear(); n.Strokes = [new() { Width = 10, Alignment = StrokeAlignment.Inside }]; using var r = new SceneRenderer();
            Check(r.HitTest([n], new(-5, 40), false, 0) is null && r.HitTest([n], new(5, 40), false, 0) == n);
            n.Strokes[0].Alignment = StrokeAlignment.Outside;
            Check(r.HitTest([n], new(-5, 40), false, 0) == n && r.HitTest([n], new(5, 40), false, 0) is null);
        });
        test("butt round and square caps have distinct finite endpoint regions", () => {
            var n = new DesignNode { Kind = NodeKind.Line, Width = 100, Height = 0, Fills = [], Strokes = [new() { Width = 10, Cap = StrokeCap.Butt }] }; using var r = new SceneRenderer();
            Check(!r.StrokeGeometry(n, 0).Contains(104, 0)); n.Strokes[0].Cap = StrokeCap.Round;
            Check(r.StrokeGeometry(n, 0).Contains(104, 0) && !r.StrokeGeometry(n, 0).Contains(104, 4)); n.Strokes[0].Cap = StrokeCap.Square;
            Check(r.StrokeGeometry(n, 0).Contains(104, 4));
        });
        test("miter limit controls sharp joins", () => {
            var n = new DesignNode { Kind = NodeKind.Path, Width = 100, Height = 100, Points = [new() { Position = new(0, 100) }, new() { Position = new(50, 0) }, new() { Position = new(100, 100) }], Fills = [], Strokes = [new() { Width = 20, Join = StrokeJoin.Miter, MiterLimit = 4 }] };
            using var r = new SceneRenderer(); var full = r.StrokeGeometry(n, 0).TightBounds.Top; n.Strokes[0].MiterLimit = 1;
            Check(r.StrokeGeometry(n, 0).TightBounds.Top > full + 5);
        });
        test("dash phase and odd patterns affect exact stroke regions", () => {
            var n = new DesignNode { Kind = NodeKind.Line, Width = 100, Height = 0, Fills = [], Strokes = [new() { Width = 4, Cap = StrokeCap.Butt, Dashes = [10] }] };
            using var r = new SceneRenderer(); var p = r.StrokeGeometry(n, 0); Check(p.Contains(5, 0) && !p.Contains(15, 0));
            n.Strokes[0].DashOffset = 10; p = r.StrokeGeometry(n, 0); Check(!p.Contains(5, 0) && p.Contains(15, 0));
        });
        test("open-path alignment falls back to centered stroke", () => {
            var n = new DesignNode { Kind = NodeKind.Line, Width = 100, Height = 0, Strokes = [new() { Width = 8, Alignment = StrokeAlignment.Outside }] };
            using var r = new SceneRenderer(); Near(r.StrokeGeometry(n, 0).TightBounds.Top, -4); n.Strokes[0].Alignment = StrokeAlignment.Inside; Near(r.StrokeGeometry(n, 0).TightBounds.Top, -4);
        });
        test("stroke resource reuse excludes color opacity and layer movement", () => {
            var n = Node(); n.Strokes = [new() { Width = 8 }]; using var r = new SceneRenderer(); var p = r.StrokeGeometry(n, 0);
            n.Strokes[0].Color = "#FF0000"; n.Strokes[0].Opacity = .4; n.X = 80; n.Rotation = 25;
            Check(ReferenceEquals(p, r.StrokeGeometry(n, 0)) && r.StrokeBuilds == 1);
            n.Strokes[0].Dashes = [2, 5]; r.StrokeGeometry(n, 0); Check(r.StrokeBuilds == 2);
            n.Corners = new(5, 10, 15, 20); r.StrokeGeometry(n, 0); Check(r.StrokeBuilds == 3);
        });
        test("stroke cache shrinking and disposal release entries immediately", () => {
            using var r = new SceneRenderer(); for (var i = 0; i < 4; i++) { var n = Node(); n.Strokes = [new()]; r.StrokeGeometry(n, 0); }
            Check(r.CachedStrokeCount == 4); r.StrokeCacheCapacity = 1; Check(r.CachedStrokeCount == 1);
            r.ClearCache(); Check(r.CachedStrokeCount == 0); Throws<ArgumentOutOfRangeException>(() => r.StrokeCacheCapacity = 0);
        });
        test("native rational paths survive schema roundtrip without changing pixels", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(-35, 310, .6); n.Fill = "#8D42E8";
            using var r = new SceneRenderer(); using var before = Bitmap(r, n);
            var commands = NativeShapeGeometry.Capture(r.Geometry(n)); Check(commands.Any(c => c.Verb == PathVerb.Conic));
            n.Kind = NodeKind.Path; n.Arc = null; n.Commands = commands; n.PathWidth = n.Width; n.PathHeight = n.Height;
            var copy = DocumentJson.Load(DocumentJson.Save(Editor(n).Document)).Pages[0].Nodes[0]; using var after = Bitmap(r, copy);
            Check(SamePixels(before, after)); Check(commands.SequenceEqual(NativeShapeGeometry.Capture(r.Geometry(copy))));
        });
        test("mutating native path commands invalidates retained geometry", () => {
            var n = Node(); using var original = NativeShapeGeometry.Build(n); n.Kind = NodeKind.Path; n.Commands = NativeShapeGeometry.Capture(original);
            using var r = new SceneRenderer(); r.Geometry(n); var count = r.GeometryBuilds;
            n.Commands[1] = n.Commands[1] with { Point = new(75, 0) }; r.Geometry(n); Check(r.GeometryBuilds == count + 1);
        });
        test("stroke geometry survives deep clones and style-only transfer", () => {
            var n = Node(); n.Strokes = [new() { Cap = StrokeCap.Square, Join = StrokeJoin.Bevel, Alignment = StrokeAlignment.Outside, MiterLimit = 7, DashOffset = -3, Dashes = [3, 4, 5] }];
            var packet = PropertyClipboard.Read(PropertyClipboard.Copy(n)); var clone = DocumentJson.CloneNode(n);
            Check(StyleCloner.SameStrokes(n.Strokes, clone.Strokes) && StyleCloner.SameStrokes(n.Strokes, packet.Properties.Strokes!));
            clone.Strokes[0].Dashes[0] = 99; Check(n.Strokes[0].Dashes[0] == 3);
        });
        test("stroke options survive instance synchronization and undo", () => {
            var c = new DesignNode { Kind = NodeKind.Component }; var child = c.Add(Node()); child.Strokes = [new()]; var e = Editor(c);
            var instance = ComponentService.InsertInstance(e, c, new(200, 0)); var id = instance.Children[0].Id; e.Select(instance.Children[0]);
            PropertyTransfer.Update(e, "Stroke geometry", n => { n.Strokes[0].Alignment = StrokeAlignment.Outside; n.Strokes[0].Cap = StrokeCap.Square; });
            e.Edit("Source change", () => child.Fill = "#FF0000"); Check(e.Document.Find(id)!.Strokes[0].Alignment == StrokeAlignment.Outside);
            e.Undo(); Check(e.Document.Find(id)!.Strokes[0].Cap == StrokeCap.Square); e.Undo(); Check(e.Document.Find(id)!.Strokes[0].Alignment == StrokeAlignment.Center);
        });
        test("SVG stroke import preserves inherited cap join phase and fill rule", () => {
            var imported = SvgFormat.Import("<svg width='100' height='80'><g stroke='red' stroke-linecap='square' stroke-linejoin='bevel' stroke-miterlimit='7' stroke-dasharray='3 5 8' stroke-dashoffset='-2' fill-rule='evenodd'><path d='M0 0H100V80H0Z M20 20H80V60H20Z'/></g></svg>");
            var n = imported.Document.AllNodes().First(n => n.Kind == NodeKind.Path); var s = n.Strokes[0];
            Check(n.FillRule == PathFillRule.EvenOdd && s.Cap == StrokeCap.Square && s.Join == StrokeJoin.Bevel && s.DashOffset == -2 && s.Dashes.Count == 3);
        });
        test("renderer-aware SVG export outlines aligned strokes without mutating the document", () => {
            var n = Node(); n.Corners = new(20, 0, 12, 5); n.Strokes = [new() { Width = 7, Alignment = StrokeAlignment.Outside }]; var before = DocumentJson.SaveNodes([n]);
            using var r = new SceneRenderer(); var svg = SceneSvg.Export(r, [n], n.LocalBounds.Inflate(10)); var xml = XDocument.Parse(svg);
            Check(xml.Descendants().Count(e => e.Name.LocalName == "path") >= 2); Check(!svg.Contains("stroke-width=\"7\""));
            Check(DocumentJson.SaveNodes([n]) == before);
            Throws<InvalidOperationException>(() => SvgFormat.Export([n], n.LocalBounds));
        });
        test("shape sample validates and renders without external assets", () => {
            var doc = ShapeSample.Create(); DocumentJson.Validate(doc); using var r = new SceneRenderer(); using var image = Bitmap(r, doc.Pages[0].Nodes[0], doc.Pages[0].Nodes[0].LocalBounds);
            Check(image.Width == 980 && r.BooleanBuilds > 0 && r.StrokeBuilds > 0);
        });
        RegisterBooleans(test); RegisterGestures(test); RegisterValidation(test);
    }
}
