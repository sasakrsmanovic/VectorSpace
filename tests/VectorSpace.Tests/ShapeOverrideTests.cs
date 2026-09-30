using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

internal static class ShapeOverrideTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Shape authoring changed unrelated corner geometry."); }
    private static (EditorSession Editor, DesignNode Definition, string Id) Fixture()
    {
        var definition = new DesignNode { Kind = NodeKind.Component, Width = 200, Height = 160 };
        definition.Add(new() { Width = 100, Height = 80, CornerRadius = 0, Corners = new(30, 10, 20, 5) });
        var editor = new EditorSession(new() { Pages = [new() { Nodes = [definition] }] });
        var instance = ComponentService.InsertInstance(editor, definition, new(300, 0));
        editor.Select(instance.Children[0]);
        return (editor, definition, instance.Children[0].Id);
    }
    public static void Register(Action<string, Action> test)
    {
        test("opacity-only instance edits retain independent source corners", () =>
        {
            var (e, definition, id) = Fixture();
            PropertyTransfer.Update(e, "Opacity", n => n.Opacity = .5);
            e.Edit("Source paint", () => definition.Children[0].Fill = "#FF0000");
            var n = e.Document.Find(id)!;
            Check(n.Corners == new CornerRadii(30, 10, 20, 5) && n.Opacity == .5);
            e.Undo(); e.Undo(); Check(e.Document.Find(id)!.Corners == new CornerRadii(30, 10, 20, 5));
        });
        test("partial appearance paste does not author an unrelated uniform radius override", () =>
        {
            var (e, definition, id) = Fixture();
            PropertyTransfer.Paste(e, new() { Opacity = .65 });
            e.Edit("Source radius", () => definition.Children[0].Corners = new(18, 8, 12, 3));
            var n = e.Document.Find(id)!;
            Check(n.Corners == new CornerRadii(18, 8, 12, 3) && n.Opacity == .65);
        });
        test("clearing independent corners to the existing uniform scalar is a persistent edit", () =>
        {
            var (e, definition, id) = Fixture();
            PropertyTransfer.Update(e, "Uniform corners", n => { n.CornerRadius = 0; n.Corners = null; });
            e.Edit("Source paint", () => definition.Children[0].Fill = "#0000FF");
            Check(e.Document.Find(id)!.Corners is null);
            e.Undo(); e.Undo(); Check(e.Document.Find(id)!.Corners == new CornerRadii(30, 10, 20, 5));
            e.Redo(); Check(e.Document.Find(id)!.Corners is null);
        });
        test("style-only copy omits unsupported independent radii and explicit uniform paste clears them", () =>
        {
            var source = new DesignNode { Corners = new(4, 8, 12, 16), CornerRadius = 0, Opacity = .6 };
            var packet = PropertyClipboard.Read(PropertyClipboard.Copy(source, PropertyGroups.Appearance));
            Check(packet.Properties.CornerRadius is null);
            var (e, _, id) = Fixture(); PropertyTransfer.Paste(e, packet.Properties);
            Check(e.Document.Find(id)!.Corners == new CornerRadii(30, 10, 20, 5));
            PropertyTransfer.Paste(e, new() { CornerRadius = 9 });
            Check(e.Document.Find(id)!.Corners is null && e.Document.Find(id)!.CornerRadius == 9);
        });
        test("Alt corner drag preserves oversized authored values on all untouched corners", () =>
        {
            var n = new DesignNode { Width = 100, Height = 80, Corners = new(300, 180, 5, 70) };
            var g = new ShapeGesture(n, 2, n.WorldMatrix.Map(new Vec2(95, 75)));
            g.Apply(n, n.WorldMatrix.Map(new Vec2(90, 70)), independentCorner: true);
            Check(n.Corners == new CornerRadii(300, 180, 10, 70));
            n.Width = 1000; n.Height = 800;
            Check(n.EffectiveCorners.TopLeft == 300 && n.EffectiveCorners.TopRight == 180);
        });
        test("oversized dragged corner starts at its visible radius rather than its latent authored radius", () =>
        {
            var n = new DesignNode { Width = 100, Height = 80, Corners = new(300, 200, 10, 20) };
            var g = new ShapeGesture(n, 0, n.WorldMatrix.Map(new Vec2(40, 40)));
            g.Apply(n, n.WorldMatrix.Map(new Vec2(35, 35)), independentCorner: true);
            Check(n.Corners == new CornerRadii(35, 200, 10, 20));
        });
        test("direct corner authoring materializes its binding and undo restores it", () =>
        {
            var n = new DesignNode { Width = 100, Height = 80, CornerRadius = 8 };
            n.VariableBindings[VariableTarget.CornerRadius] = new() { Disabled = true, Fallback = VariableResolver.Read(n, VariableTarget.CornerRadius) };
            var e = new EditorSession(new() { Pages = [new() { Nodes = [n] }] }); e.Select(n);
            e.BeginInteraction("Corner drag");
            new ShapeGesture(n, 0, new(8, 8)).Apply(n, new(18, 18)); e.Preview(false); e.CommitInteraction();
            Check(e.Primary!.CornerRadius == 18 && !e.Primary.VariableBindings.ContainsKey(VariableTarget.CornerRadius));
            e.Undo(); Check(e.Primary!.CornerRadius == 8 && e.Primary.VariableBindings.ContainsKey(VariableTarget.CornerRadius));
        });
    }
}
