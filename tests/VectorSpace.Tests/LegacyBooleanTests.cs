using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static class LegacyBooleanTests
{
    private static void Check(bool condition) { if (!condition) throw new Exception("Legacy Boolean compatibility assertion failed."); }
    private static EditorSession Editor(params DesignNode[] nodes) => new(new() { Pages = [new() { Nodes = nodes.ToList() }] });
    public static void Register(Action<string, Action> test)
    {
        test("legacy Boolean API consumes native conic paths without conflicting representations", () =>
        {
            var a = new DesignNode { Kind = NodeKind.Ellipse, Width = 100, Height = 100, Fill = "#E84323" };
            using (var path = NativeShapeGeometry.Build(a))
            {
                a.Commands = NativeShapeGeometry.Capture(path); a.Kind = NodeKind.Path;
                a.PathWidth = a.Width; a.PathHeight = a.Height;
            }
            var b = new DesignNode { X = 50, Width = 100, Height = 100 };
            var e = Editor(a, b); e.Select([a.Id, b.Id]); var before = DocumentJson.Save(e.Document);
            using var renderer = new SceneRenderer(); BooleanOperations.Apply(e, renderer, BooleanOperation.Union);
            var result = e.Primary!;
            Check(result.Kind == NodeKind.Path && result.Commands is { Count: > 0 } && result.PathData is null && result.Points.Count == 0);
            Check(result.Width == 150 && result.Height == 100 && result.Fill == "#E84323");
            Check(renderer.HitTest([result], new(10, 50), false, 0) == result);
            Check(renderer.HitTest([result], new(140, 50), false, 0) == result);
            DocumentJson.Validate(e.Document); DocumentJson.Load(DocumentJson.Save(e.Document));
            e.Undo(); Check(DocumentJson.Save(e.Document) == before);
        });
        test("legacy Boolean result clears source arc modifiers and retains native winding", () =>
        {
            var a = new DesignNode { Kind = NodeKind.Ellipse, Width = 100, Height = 100, Arc = new(0, 360, .5) };
            var b = new DesignNode { X = 140, Width = 30, Height = 30 };
            var e = Editor(a, b); e.Select([a.Id, b.Id]); using var renderer = new SceneRenderer();
            BooleanOperations.Apply(e, renderer, BooleanOperation.Union);
            Check(e.Primary!.Arc is null && e.Primary.Corners is null && e.Primary.Boolean is null);
            Check(renderer.HitTest([e.Primary], new(50, 50), false, 0) is null);
            Check(renderer.HitTest([e.Primary], new(90, 50), false, 0) == e.Primary);
        });
        test("empty legacy Boolean result removes only participating shapes", () =>
        {
            var a = new DesignNode { Width = 40, Height = 40 };
            var b = new DesignNode { X = 100, Width = 40, Height = 40 };
            var text = new DesignNode { Kind = NodeKind.Text, Text = "Keep unrelated selection" };
            var e = Editor(a, b, text); e.Select([a.Id, b.Id, text.Id]); using var renderer = new SceneRenderer();
            BooleanOperations.Apply(e, renderer, BooleanOperation.Intersect);
            Check(e.Page.Nodes.Count == 1 && e.Page.Nodes[0].Id == text.Id && e.History.Count == 1);
            e.Undo(); Check(e.Page.Nodes.Count == 3);
        });
        test("legacy Boolean structural edits cannot silently change linked instance children", () =>
        {
            var component = new DesignNode { Kind = NodeKind.Component };
            component.Add(new() { Width = 50, Height = 50 }); component.Add(new() { X = 20, Width = 50, Height = 50 });
            var e = Editor(component); var instance = ComponentService.InsertInstance(e, component, new(200, 0));
            e.Select(instance.Children.Select(n => n.Id)); var before = DocumentJson.Save(e.Document); using var renderer = new SceneRenderer();
            try { BooleanOperations.Apply(e, renderer, BooleanOperation.Union); }
            catch (InvalidOperationException) { Check(DocumentJson.Save(e.Document) == before); return; }
            throw new Exception("A structural instance edit was accepted.");
        });
    }
}
