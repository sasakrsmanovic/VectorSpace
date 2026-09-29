using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static partial class ShapeTests
{
    private static (EditorSession Editor, DesignNode A, DesignNode B) Pair()
    {
        var a = new DesignNode { Id = "base", Width = 100, Height = 100, Fill = "#E44B37" };
        var b = new DesignNode { Id = "cut", Kind = NodeKind.Ellipse, X = 50, Width = 100, Height = 100, Fill = "#2584F5" };
        var editor = Editor(a, b); editor.Select([b.Id, a.Id]); return (editor, a, b);
    }
    private static void RegisterBooleans(Action<string, Action> test)
    {
        foreach (var operation in Enum.GetValues<BooleanKind>())
            test("live Boolean retains exact operand identities " + operation, () => {
                var (e, a, b) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, operation);
                Check(e.Page.Nodes.Count == 1 && group.Children[0] == a && group.Children[1] == b);
                Check(group.Children.Select(c => c.Id).SequenceEqual(new[] { "base", "cut" }));
                var p = r.Geometry(group);
                Check(p.Contains(10, 50) == (operation is BooleanKind.Union or BooleanKind.Subtract or BooleanKind.Exclude));
                Check(p.Contains(75, 50) == (operation is BooleanKind.Union or BooleanKind.Intersect));
                Check(p.Contains(125, 50) == (operation is BooleanKind.Union or BooleanKind.Exclude));
                e.Undo(); Check(e.Page.Nodes.Count == 2); e.Redo(); Check(e.Primary!.Boolean == operation);
            });
        test("live Boolean does not repaint retained operands over the result", () => {
            var (e, _, _) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Subtract);
            using var image = Bitmap(r, group, group.LocalBounds); Check(image.GetPixel(75, 50).Alpha == 0 && image.GetPixel(10, 50).Red > 200);
            Check(r.RenderedNodes == 1);
        });
        test("Boolean result and deep operand picking are distinct", () => {
            var (e, _, b) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Subtract);
            Check(r.HitTest([group], new(75, 50), false, 0) is null);
            Check(r.HitTest([group], new(75, 50), true, 0) == b);
            Check(r.HitTest([group], new(10, 50), false, 0) == group);
        });
        test("Boolean cache reuses paint-only changes and invalidates operand geometry", () => {
            var (e, a, b) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Union);
            r.Geometry(group); var count = r.BooleanBuilds; a.Fill = "#000000"; b.Opacity = .1; group.X = 200; group.Rotation = 15;
            r.Geometry(group); Check(r.BooleanBuilds == count); b.X += 12; r.Geometry(group); Check(r.BooleanBuilds == count + 1);
            b.Visible = false; r.Geometry(group); Check(r.BooleanBuilds == count + 2);
            b.Visible = true; a.Corners = new(12, 18, 24, 30); r.Geometry(group); Check(r.BooleanBuilds == count + 3);
        });
        test("nested Boolean groups invalidate parent results without rebuilding unchanged children", () => {
            var (e, a, _) = Pair(); using var r = new SceneRenderer(); var first = LiveBooleanOperations.Create(e, r, BooleanKind.Union);
            var third = new DesignNode { Id = "third", X = 80, Y = 80, Width = 100, Height = 100 }; e.Edit("Add", () => e.AddNode(third));
            e.Select([first.Id, third.Id]); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Subtract); r.Geometry(group); var count = r.BooleanBuilds;
            r.Geometry(group); Check(r.BooleanBuilds == count); a.Width = 110; r.Geometry(group); Check(r.BooleanBuilds == count + 2);
        });
        test("flattening preserves native conics, pixels, group identity and undo", () => {
            var (e, _, _) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Subtract);
            var id = group.Id; using var before = Bitmap(r, group); LiveBooleanOperations.Flatten(e, r);
            Check(e.Primary!.Id == id && e.Primary.Kind == NodeKind.Path && e.Primary.Commands is { Count: > 0 } && e.Primary.Children.Count == 0);
            using var after = Bitmap(r, e.Primary); Check(SamePixels(before, after));
            var json = DocumentJson.Save(e.Document); var loaded = DocumentJson.Load(json); using var restored = Bitmap(r, loaded.Find(id)!); Check(SamePixels(before, restored));
            e.Undo(); Check(e.Primary!.IsBoolean && e.Primary.Children.Count == 2); e.Redo(); Check(e.Primary!.Commands is { Count: > 0 });
        });
        test("empty Boolean results remain editable and reversible", () => {
            var (e, _, b) = Pair(); b.X = 500; using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Intersect);
            Check(r.Geometry(group).IsEmpty && group.Children.Count == 2); LiveBooleanOperations.Flatten(e, r); Check(e.Primary!.Commands is { Count: 0 });
            DocumentJson.Validate(e.Document); e.Undo(); Check(e.Primary!.IsBoolean); LiveBooleanOperations.Release(e); Check(e.Page.Nodes.Count == 2);
        });
        test("releasing a rotated Boolean preserves child world coordinates and paints", () => {
            var (e, a, b) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Subtract);
            group.Rotation = 40; group.FlipX = true; var point = b.WorldMatrix.Map(Vec2.Zero); var color = b.Fill;
            LiveBooleanOperations.Release(e); var restored = e.Document.Find(b.Id)!; Near(restored.WorldMatrix.Map(Vec2.Zero).X, point.X); Near(restored.WorldMatrix.Map(Vec2.Zero).Y, point.Y);
            Check(restored.Fill == color && e.Document.Find(a.Id) is not null && e.Document.Find(group.Id) is null);
        });
        test("Boolean group can become a component without losing its operation", () => {
            var (e, _, _) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Exclude);
            ComponentService.MakeComponent(e); Check(e.Primary!.Kind == NodeKind.Component && e.Primary.Children[0].IsBoolean);
            var instance = ComponentService.InsertInstance(e, e.Primary, new(300, 0)); Check(instance.Children[0].Boolean == BooleanKind.Exclude); DocumentJson.Validate(e.Document);
        });
        test("invalid Boolean operand selection changes nothing", () => {
            var (e, a, _) = Pair(); a.Kind = NodeKind.Text; var json = DocumentJson.Save(e.Document); using var r = new SceneRenderer();
            Throws<InvalidOperationException>(() => LiveBooleanOperations.Create(e, r, BooleanKind.Union)); Check(DocumentJson.Save(e.Document) == json && !e.CanUndo);
        });
        test("geometry changes inside instances require explicit detachment", () => {
            var c = new DesignNode { Kind = NodeKind.Component }; c.Add(Node()); c.Add(Node()); var e = Editor(c); using var r = new SceneRenderer();
            var instance = ComponentService.InsertInstance(e, c, new(200, 0)); e.Select(instance.Children.Select(n => n.Id)); var json = DocumentJson.Save(e.Document);
            Throws<InvalidOperationException>(() => LiveBooleanOperations.Create(e, r, BooleanKind.Union)); Check(DocumentJson.Save(e.Document) == json);
        });
        test("outlined fill plus multiple strokes preserves pixels and per-stroke paints", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Fill = "#279853"; n.Opacity = .7;
            n.Strokes = [new() { Width = 14, Color = "#FF0000", Alignment = StrokeAlignment.Outside }, new() { Width = 6, Color = "#0000FF", Dashes = [8, 6] }];
            var e = Editor(n); e.Select(n); using var r = new SceneRenderer(); using var before = Bitmap(r, n);
            LiveBooleanOperations.Outline(e, r); Check(n.Kind == NodeKind.Group && n.Children.Count == 3 && n.Strokes.Count == 0 && n.Opacity == .7);
            Check(n.Children[1].Fill == "#FF0000" && n.Children[2].Fill == "#0000FF"); using var after = Bitmap(r, n); Check(SamePixels(before, after));
            e.Undo(); Check(e.Primary!.Kind == NodeKind.Ellipse && e.Primary.Strokes.Count == 2);
        });
        test("outline supports compound and dashed stroke contours without closing gaps", () => {
            var n = new DesignNode { Kind = NodeKind.Line, Width = 100, Height = 0, Fills = [], Strokes = [new() { Width = 8, Dashes = [10, 10], Cap = StrokeCap.Butt }] };
            var e = Editor(n); e.Select(n); using var r = new SceneRenderer(); LiveBooleanOperations.Outline(e, r);
            Check(n.Children[0].Commands!.Count(c => c.Verb == PathVerb.Move) >= 5);
            Check(r.HitTest([n], new(5, 0), true, 0) is not null && r.HitTest([n], new(15, 0), true, 0) is null);
        });
        test("renderer-aware SVG bakes a temporary Boolean result only", () => {
            var (e, _, _) = Pair(); using var r = new SceneRenderer(); var group = LiveBooleanOperations.Create(e, r, BooleanKind.Subtract); var before = DocumentJson.Save(e.Document);
            var svg = SceneSvg.Export(r, [group], group.WorldBounds); Check(svg.Contains("path") && !svg.Contains("Retained cutout"));
            Check(DocumentJson.Save(e.Document) == before && group.IsBoolean && group.Children.Count == 2);
            Throws<InvalidOperationException>(() => SvgFormat.Export([group], group.WorldBounds));
        });
        test("Boolean cache budgets are enforced immediately", () => {
            using var r = new SceneRenderer(); for (var i = 0; i < 3; i++) { var (e, _, _) = Pair(); LiveBooleanOperations.Create(e, r, BooleanKind.Union); }
            Check(r.CachedBooleanCount == 3); r.BooleanCacheCapacity = 1; Check(r.CachedBooleanCount == 1); r.ClearCache(); Check(r.CachedBooleanCount == 0);
        });
    }
}
