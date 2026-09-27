using System.Diagnostics;
using SkiaSharp;
using VectorSpace.Skia;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Layout;

internal static class DesignSystemTests
{
    private static void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Equal(double a, double b) => Check(Math.Abs(a - b) < .0001, $"Expected {b}, got {a}");
    private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected failure"); }
    private static EditorSession Editor(params DesignNode[] nodes) => new(new() { Pages = [new() { Nodes = nodes.ToList() }] });
    private static DesignNode Component(string text = "Label") => new() { Kind = NodeKind.Component, Name = "Button", Width = 120, Height = 40, Children = [new() { Kind = NodeKind.Text, Name = "Label", Text = text, Width = 80, Height = 24 }] };
    public static void Register(Action<string, Action> test)
    {
        test("variable mode changes render actual Skia fill pixels", () =>
        {
            var node = new DesignNode { Width = 100, Height = 100 }; var e = Editor(node);
            var c = VariableService.CreateCollection(e, "Theme"); var mode = VariableService.AddMode(e, c.Id, "Dark");
            var color = VariableService.Create(e, c.Id, "Surface", VariableValue.Color("#FFFFFF"));
            VariableService.SetValue(e, color.Id, mode.Id, VariableValue.Color("#111111")); VariableService.Bind(e, node.Id, VariableTarget.Fill, color.Id);
            using var renderer = new SceneRenderer(); using var light = SKBitmap.Decode(renderer.ExportPng([node], node.WorldBounds));
            VariableService.SetMode(e, c.Id, mode.Id); using var dark = SKBitmap.Decode(renderer.ExportPng([node], node.WorldBounds));
            Check(light.GetPixel(50, 50).Red == 255 && dark.GetPixel(50, 50).Red == 17);
        });
        test("browser design-system fixture is valid and hit-testable", () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "design-system.vectorspace");
            var document = DocumentJson.Load(File.ReadAllText(path)); var e = new EditorSession(document);
            ComponentService.Synchronize(document); DocumentJson.Validate(document);
            using var renderer = new SceneRenderer(); Check(renderer.HitTest(e.Page.Nodes, new(380, 130), true)?.Id == "swatch");
            Check(renderer.HitTest(e.Page.Nodes, new(324, 304), true)?.Id == "button-instance");
        });
        test("component expansion rejects exponential graphs before materializing them", () =>
        {
            var nodes = new List<DesignNode>();
            var leaf = Component(); nodes.Add(leaf);
            for (var i = 0; i < 18; i++)
            {
                var parent = Component(); parent.Children = [new() { Kind = NodeKind.Instance, ComponentId = leaf.Id }, new() { Kind = NodeKind.Instance, ComponentId = leaf.Id }];
                nodes.Add(parent); leaf = parent;
            }
            var e = Editor(nodes.ToArray()); var before = DocumentJson.Save(e.Document);
            Throws(() => ComponentService.Synchronize(e.Document)); Check(DocumentJson.Save(e.Document) == before);
        });
        test("reset instance overrides restores inherited variable binding", () =>
        {
            var component = Component(); var e = Editor(component); var collection = VariableService.CreateCollection(e, "Theme");
            var color = VariableService.Create(e, collection.Id, "Color", VariableValue.Color("#FF0000"));
            VariableService.Bind(e, component.Id, VariableTarget.Fill, color.Id); var instance = ComponentService.InsertInstance(e, component, new(300, 0));
            VariableService.Unbind(e, instance.Id, VariableTarget.Fill); VariableService.SetValue(e, color.Id, collection.DefaultModeId, VariableValue.Color("#0000FF"));
            Check(instance.Fill == "#FF0000"); e.Select(instance); ComponentService.ResetOverrides(e); Check(instance.Fill == "#0000FF");
        });
        test("v1 documents migrate to v3 without geometry changes", () =>
        {
            var doc = new DesignDocument { FormatVersion = 1 }; doc.Pages[0].Nodes.Add(new() { X = 77 });
            var read = DocumentJson.Load(DocumentJson.Save(doc)); Check(read.FormatVersion == 3); Equal(read.Pages[0].Nodes[0].X, 77);
        });
        test("variable collection creation is one undo entry", () => { var e = Editor(); VariableService.CreateCollection(e, "Theme"); Equal(e.History.Count, 1); e.Undo(); Equal(e.Document.VariableCollections.Count, 0); e.Redo(); Equal(e.Document.VariableCollections.Count, 1); });
        test("typed color binding updates scene and survives serialization", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Theme"); var v = VariableService.Create(e, c.Id, "Primary", VariableValue.Color("#FF0000"));
            VariableService.Bind(e, n.Id, VariableTarget.Fill, v.Id); Check(n.Fill == "#FF0000"); var d = DocumentJson.Load(DocumentJson.Save(e.Document)); Check(d.Pages[0].Nodes[0].VariableBindings[VariableTarget.Fill].VariableId == v.Id);
        });
        test("mode values inherit through frame and allow child override", () =>
        {
            var frame = new DesignNode { Kind = NodeKind.Frame }; var n = frame.Add(new()); var e = Editor(frame);
            var c = VariableService.CreateCollection(e, "Theme"); var dark = VariableService.AddMode(e, c.Id, "Dark"); var v = VariableService.Create(e, c.Id, "Surface", VariableValue.Color("#FFFFFF"));
            VariableService.SetValue(e, v.Id, dark.Id, VariableValue.Color("#111111")); VariableService.Bind(e, n.Id, VariableTarget.Fill, v.Id);
            VariableService.SetMode(e, c.Id, dark.Id, frame.Id); Check(n.Fill == "#111111");
            VariableService.SetMode(e, c.Id, c.DefaultModeId, n.Id); Check(n.Fill == "#FFFFFF");
            VariableService.SetMode(e, c.Id, null, n.Id); Check(n.Fill == "#111111");
        });
        test("document mode and inherited mode return to default", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Theme"); var dark = VariableService.AddMode(e, c.Id, "Dark"); var v = VariableService.Create(e, c.Id, "Size", VariableValue.Float(20)); VariableService.SetValue(e, v.Id, dark.Id, VariableValue.Float(40)); VariableService.Bind(e, n.Id, VariableTarget.Width, v.Id);
            VariableService.SetMode(e, c.Id, dark.Id); Equal(n.Width, 40); VariableService.SetMode(e, c.Id, null); Equal(n.Width, 20);
        });
        test("aliases resolve across collections and inherit target modes", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Base"); var dark = VariableService.AddMode(e, c.Id, "Large"); var baseVar = VariableService.Create(e, c.Id, "Space", VariableValue.Float(8)); VariableService.SetValue(e, baseVar.Id, dark.Id, VariableValue.Float(24));
            var other = VariableService.CreateCollection(e, "Semantic"); var alias = VariableService.Create(e, other.Id, "Gap", VariableValue.Alias(baseVar)); VariableService.Bind(e, n.Id, VariableTarget.Gap, alias.Id); Equal(n.Layout.Gap, 8); VariableService.SetMode(e, c.Id, dark.Id); Equal(n.Layout.Gap, 24);
        });
        test("alias cycle rolls back atomically", () =>
        {
            var e = Editor(); var c = VariableService.CreateCollection(e, "Base"); var a = VariableService.Create(e, c.Id, "A", VariableValue.Float(4)); var b = VariableService.Create(e, c.Id, "B", VariableValue.Alias(a)); var before = DocumentJson.Save(e.Document);
            Throws(() => VariableService.SetValue(e, a.Id, c.DefaultModeId, VariableValue.Alias(b))); Check(DocumentJson.Save(e.Document) == before); Check(!e.IsInteracting);
        });
        test("cross-mode alias cycle is rejected", () =>
        {
            var e = Editor(); var c = VariableService.CreateCollection(e, "Base"); var mode = VariableService.AddMode(e, c.Id, "Other"); var a = VariableService.Create(e, c.Id, "A", VariableValue.Float(4)); var b = VariableService.Create(e, c.Id, "B", VariableValue.Float(8));
            VariableService.SetValue(e, a.Id, c.DefaultModeId, VariableValue.Alias(b)); Throws(() => VariableService.SetValue(e, b.Id, mode.Id, VariableValue.Alias(a)));
        });
        test("incompatible binding and nonfinite values roll back", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Base"); var v = VariableService.Create(e, c.Id, "Space", VariableValue.Float(12));
            Throws(() => VariableService.Bind(e, n.Id, VariableTarget.Fill, v.Id)); Throws(() => VariableService.SetValue(e, v.Id, c.DefaultModeId, VariableValue.Float(double.NaN))); Equal(e.Document.Variables[0].Values[c.DefaultModeId].Number, 12);
        });
        test("unbind keeps current literal or explicitly restores fallback", () =>
        {
            var n = new DesignNode { Width = 100 }; var e = Editor(n); var c = VariableService.CreateCollection(e, "Base"); var v = VariableService.Create(e, c.Id, "Width", VariableValue.Float(40));
            VariableService.Bind(e, n.Id, VariableTarget.Width, v.Id); VariableService.Unbind(e, n.Id, VariableTarget.Width, true); Equal(n.Width, 100);
            VariableService.Bind(e, n.Id, VariableTarget.Width, v.Id); VariableService.Unbind(e, n.Id, VariableTarget.Width); Equal(n.Width, 40);
        });
        test("deleting a bound variable preserves resolved appearance", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Base"); var v = VariableService.Create(e, c.Id, "Width", VariableValue.Float(48)); VariableService.Bind(e, n.Id, VariableTarget.Width, v.Id); VariableService.Delete(e, v.Id); Equal(n.Width, 48); Equal(n.VariableBindings.Count, 0); e.Undo(); Check(e.Document.Variables.Count == 1);
        });
        test("deleting aliased variable does not break dependencies", () =>
        {
            var e = Editor(); var c = VariableService.CreateCollection(e, "Base"); var a = VariableService.Create(e, c.Id, "A", VariableValue.Float(4)); VariableService.Create(e, c.Id, "B", VariableValue.Alias(a)); Throws(() => VariableService.Delete(e, a.Id)); Equal(e.Document.Variables.Count, 2);
        });
        test("deleting a mode clears obsolete node and document overrides", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Base"); var mode = VariableService.AddMode(e, c.Id, "Other"); VariableService.SetMode(e, c.Id, mode.Id, n.Id); VariableService.SetMode(e, c.Id, mode.Id); VariableService.RemoveMode(e, c.Id, mode.Id); Check(n.VariableModes.Count == 0 && e.Document.VariableModes.Count == 0); Throws(() => VariableService.RemoveMode(e, c.Id, c.DefaultModeId));
        });
        test("boolean and text variables affect real node properties", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Text }; var e = Editor(n); var c = VariableService.CreateCollection(e, "Content"); var text = VariableService.Create(e, c.Id, "Label", VariableValue.String("Hello")); var visible = VariableService.Create(e, c.Id, "Show", VariableValue.Bool(false)); VariableService.Bind(e, n.Id, VariableTarget.Text, text.Id); VariableService.Bind(e, n.Id, VariableTarget.Visible, visible.Id); Check(n.Text == "Hello" && !n.Visible);
        });
        test("variable gap feeds real auto layout", () =>
        {
            var frame = new DesignNode { Kind = NodeKind.Frame, Width = 400, Layout = new() { Direction = LayoutDirection.Horizontal } }; frame.Add(new() { Width = 20 }); frame.Add(new() { Width = 20 }); var e = Editor(frame); var c = VariableService.CreateCollection(e, "Spacing"); var v = VariableService.Create(e, c.Id, "Gap", VariableValue.Float(30)); VariableService.Bind(e, frame.Id, VariableTarget.Gap, v.Id); Equal(frame.Children[1].X, 66);
        });
        test("copy paste within document reuses variables", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Base"); var v = VariableService.Create(e, c.Id, "Width", VariableValue.Float(48)); VariableService.Bind(e, n.Id, VariableTarget.Width, v.Id); e.Select(n); e.Paste(e.CopySelection()); Equal(e.Document.Variables.Count, 1); Equal(e.Primary!.Width, 48); Check(e.Primary.VariableBindings[VariableTarget.Width].VariableId == v.Id);
        });
        test("cross document clipboard remaps variables modes and aliases", () =>
        {
            var n = new DesignNode(); var e = Editor(n); var c = VariableService.CreateCollection(e, "Base"); var v = VariableService.Create(e, c.Id, "Width", VariableValue.Float(48)); var alias = VariableService.Create(e, c.Id, "Alias", VariableValue.Alias(v)); VariableService.Bind(e, n.Id, VariableTarget.Width, alias.Id); e.Select(n);
            var other = Editor(); other.Paste(e.CopySelection()); Equal(other.Document.Variables.Count, 2); Equal(other.Primary!.Width, 48); Check(other.Primary.VariableBindings[VariableTarget.Width].VariableId != alias.Id); DocumentJson.Validate(other.Document);
        });
        test("combine variants preserves world positions and component ids", () =>
        {
            var a = Component(); var b = Component(); b.X = 200; var id = a.Id; var e = Editor(a, b); e.Select([a.Id, b.Id]); var set = ComponentVariants.Combine(e, "Button"); Check(set.Kind == NodeKind.ComponentSet && set.Children[0].Id == id); Equal(a.WorldBounds.X, 0); Equal(b.WorldBounds.X, 200); e.Undo(); Equal(e.Page.Nodes.Count, 2);
        });
        test("add variant creates a reusable component set", () => { var c = Component(); var e = Editor(c); var b = ComponentVariants.Add(e, c.Id); Check(b.Parent!.Kind == NodeKind.ComponentSet && b.Parent.Children.Count == 2); Check(c.Id != b.Id && c.Children[0].Id != b.Children[0].Id); DocumentJson.Validate(e.Document); });
        test("swap variant preserves matching text override and selected descendant", () =>
        {
            var c = Component("Default"); var e = Editor(c); var variant = ComponentVariants.Add(e, c.Id); variant.Fill = "#FF0000"; variant.Children[0].Text = "Different";
            var instance = ComponentService.InsertInstance(e, c, new(400, 0)); var child = instance.Children[0]; var childId = child.Id;
            e.Edit("Override", () => ComponentService.SetOverride(child, text: "Custom")); e.Select(childId is null ? [] : [childId]); ComponentVariants.Switch(e, instance.Id, variant.Id);
            Check(instance.ComponentId == variant.Id && instance.Fill == "#FF0000"); Check(instance.Children[0].Text == "Custom" && instance.Children[0].Id == childId); Check(e.Primary!.Id == childId); e.Undo(); Check(e.Document.Find(instance.Id)!.ComponentId == c.Id);
        });
        test("swap variant preserves explicit instance size", () =>
        {
            var c = Component(); var e = Editor(c); var variant = ComponentVariants.Add(e, c.Id); variant.Width = 160; var instance = ComponentService.InsertInstance(e, c, new(400, 0)); e.Edit("Resize", () => instance.Width = 200); ComponentVariants.Switch(e, instance.Id, variant.Id); Equal(instance.Width, 200);
        });
        test("swap variant adopts new size when not overridden", () =>
        {
            var c = Component(); var e = Editor(c); var variant = ComponentVariants.Add(e, c.Id); variant.Width = 160; var instance = ComponentService.InsertInstance(e, c, new(400, 0)); ComponentVariants.Switch(e, instance.Id, variant.Id); Equal(instance.Width, 160);
        });
        test("duplicate variant property combination rolls back", () =>
        {
            var c = Component(); var e = Editor(c); var variant = ComponentVariants.Add(e, c.Id); Throws(() => ComponentVariants.SetProperty(e, variant.Id, "Variant", "Default")); Check(e.Document.Find(variant.Id)!.VariantProperties["Variant"] != "Default");
        });
        test("invalid component-set child is rejected", () => Throws(() => Editor(new DesignNode { Kind = NodeKind.ComponentSet, Children = [new()] })));
        test("unchanged components skip clone synchronization", () =>
        {
            var c = Component(); var e = Editor(c); var instance = ComponentService.InsertInstance(e, c, new(400, 0)); var child = instance.Children[0]; var stats = ComponentService.Synchronize(e.Document); Equal(stats.InstancesRebuilt, 0); Check(ReferenceEquals(child, instance.Children[0]));
            instance.X += 20; stats = ComponentService.Synchronize(e.Document); Equal(stats.InstancesRebuilt, 0);
        });
        test("nested instances preserve distinct stable descendant identities", () =>
        {
            var inner = Component("Inner"); var outer = Component("Outer"); var e = Editor(inner, outer);
            var a = ComponentService.InsertInstance(e, inner, new()); var b = ComponentService.InsertInstance(e, inner, new());
            e.Edit("Nest", () => { e.RemoveNode(a); e.RemoveNode(b); outer.Add(a); outer.Add(b); });
            var instance = ComponentService.InsertInstance(e, outer, new(600, 0)); var before = instance.DescendantsAndSelf().Select(n => n.Id).ToArray();
            e.Edit("Source edit", () => inner.Children[0].Text = "Changed"); var after = instance.DescendantsAndSelf().Select(n => n.Id).ToArray();
            Check(before.SequenceEqual(after)); Check(after.Distinct().Count() == after.Length); Check(instance.Children.Where(n => n.Kind == NodeKind.Instance).All(n => n.Children[0].Text == "Changed")); DocumentJson.Validate(e.Document);
        });
        test("component source cycle is rejected atomically", () =>
        {
            var c = Component(); var e = Editor(c); var i = ComponentService.InsertInstance(e, c, new(300, 0)); var before = DocumentJson.Save(e.Document);
            Throws(() => e.Edit("Invalid nesting", () => { e.RemoveNode(i); c.Add(i); })); Check(DocumentJson.Save(e.Document) == before);
        });
        test("source variable bindings resolve independently in instance modes", () =>
        {
            var c = Component(); var e = Editor(c); var collection = VariableService.CreateCollection(e, "Theme"); var dark = VariableService.AddMode(e, collection.Id, "Dark"); var v = VariableService.Create(e, collection.Id, "Ink", VariableValue.Color("#FFFFFF")); VariableService.SetValue(e, v.Id, dark.Id, VariableValue.Color("#111111")); VariableService.Bind(e, c.Children[0].Id, VariableTarget.Fill, v.Id);
            var i = ComponentService.InsertInstance(e, c, new(300, 0)); VariableService.SetMode(e, collection.Id, dark.Id, i.Id); Check(i.Children[0].Fill == "#111111" && c.Children[0].Fill == "#FFFFFF");
        });
        test("instance binding override survives source synchronization and unbind", () =>
        {
            var c = Component(); var e = Editor(c); var collection = VariableService.CreateCollection(e, "Theme"); var v = VariableService.Create(e, collection.Id, "Ink", VariableValue.Color("#111111")); VariableService.Bind(e, c.Children[0].Id, VariableTarget.Fill, v.Id);
            var i = ComponentService.InsertInstance(e, c, new(300, 0)); var childId = i.Children[0].Id; VariableService.Unbind(e, childId, VariableTarget.Fill); VariableService.SetValue(e, v.Id, collection.DefaultModeId, VariableValue.Color("#FF0000")); Check(i.Children[0].Fill == "#111111" && c.Children[0].Fill == "#FF0000");
        });
        test("snap index matches linear reference for 500 seeded randomized queries", () =>
        {
            var random = new Random(9747); var targets = Enumerable.Range(0, 600).Select(_ => new RectD(random.Next(-300, 300), random.Next(-300, 300), random.Next(1, 160), random.Next(1, 160))).ToArray(); var guides = new Guide[] { new() { Position = 22 }, new() { Horizontal = true, Position = 91 } }; var index = new SnapIndex(targets, guides);
            for (var i = 0; i < 500; i++) { var moving = new RectD(random.Next(-350, 350), random.Next(-350, 350), random.Next(1, 150), random.Next(1, 150)); var tolerance = random.NextDouble() * 10; var actual = index.Snap(moving, tolerance); var expected = SnapEngine.Snap(moving, targets, tolerance, guides); Check(actual.Correction == expected.Correction && actual.Lines.SequenceEqual(expected.Lines), $"Mismatch in query {i}"); }
        });
        test("snap index retains target order at equal coordinates and distances", () =>
        {
            var targets = new RectD[] { new(20, 80, 10, 10), new(20, 0, 10, 10), new(40, 0, 10, 10) }; var moving = new RectD(30, 30, 2, 2); var actual = new SnapIndex(targets).Snap(moving, 10); var expected = SnapEngine.Snap(moving, targets, 10); Check(actual.Correction == expected.Correction && actual.Lines.SequenceEqual(expected.Lines));
        });
        test("snap index snapshots guide and target inputs", () =>
        {
            var targets = new List<RectD> { new(20, 0, 10, 10) }; var guide = new Guide { Position = 101 }; var index = new SnapIndex(targets, [guide]); targets.Clear(); guide.Position = 99; Equal(index.Snap(new(100, 50, 20, 20), 2).Correction.X, 1);
        });
        test("snap index query allocation is independent of target count", () =>
        {
            var targets = Enumerable.Range(0, 10000).Select(i => new RectD(i * 10, i * 10, 5, 5)).ToArray(); var index = new SnapIndex(targets); var box = new RectD(100, 100, 20, 20); for (var i = 0; i < 10; i++) index.Snap(box, 5);
            var before = GC.GetAllocatedBytesForCurrentThread(); for (var i = 0; i < 100; i++) index.Snap(box, 5); var bytes = GC.GetAllocatedBytesForCurrentThread() - before; Check(bytes < 20000, $"Allocated {bytes} bytes");
        });
    }
}
