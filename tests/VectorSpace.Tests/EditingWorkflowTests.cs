using System.Text.Json;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

internal static class EditingWorkflowTests
{
    private static void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static EditorSession Editor(params DesignNode[] nodes) => new(new() { Pages = [new() { Nodes = nodes.ToList() }] });
    private static DesignNode Source() => new()
    {
        Id = "source", Name = "Source typography", Kind = NodeKind.Text, Text = "Never paste this text", X = 70, Y = 90, Width = 310, Height = 190,
        FontFamily = "Inter", FontSize = 37, FontWeight = 600, LineHeight = 1.8, LetterSpacing = 1.25, TextAlign = TextAlignment.Right,
        Opacity = .7, Blend = BlendKind.Multiply,
        Fills = [new() { Kind = FillKind.LinearGradient, Opacity = .6, Blend = BlendKind.Screen, Start = new(.1, .3), End = new(.8, .6), Spread = GradientSpread.Reflect,
            GradientTransform = Matrix2D.Translation(2, 3), Stops = [new() { Color = "#FF0000", Opacity = .4 }, new() { Offset = 1, Color = "#0000FF" }] }],
        Strokes = [new() { Color = "#14AE5C", Width = 3, Opacity = .8, Dashes = [0, 4] }],
        Shadows = [new() { Kind = EffectKind.InnerShadow, X = 3, Y = -2, Blur = 7, Spread = 2, Opacity = .3 }]
    };
    private static (EditorSession Editor, DesignNode Definition, DesignNode Instance) Instance()
    {
        var definition = new DesignNode { Id = "definition", Name = "Card", Kind = NodeKind.Component, Width = 300, Height = 160 };
        definition.Add(new() { Id = "label", Kind = NodeKind.Text, Name = "Label", Text = "Original", FontSize = 16 });
        var editor = Editor(definition); var instance = ComponentService.InsertInstance(editor, definition, new(400, 0));
        return (editor, definition, instance);
    }
    public static void Register(Action<string, Action> test)
    {
        test("property clipboard captures every supported paint field without scene content", () => {
            var n = Source(); n.Add(new() { Name = "Private child content" });
            var text = PropertyClipboard.Copy(n); var p = PropertyClipboard.Read(text).Properties;
            Check(!text.Contains("Private child content") && !text.Contains("Never paste this text") && !text.Contains("prototypeTargetId"));
            Check(StyleCloner.SameFills(p.Fills!, n.Fills) && StyleCloner.SameStrokes(p.Strokes!, n.Strokes) && StyleCloner.SameEffects(p.Effects!, n.Shadows));
            Check(p.Typography == TypographyStyle.Capture(n) && p.Opacity == .7 && p.Blend == BlendKind.Multiply);
        });
        test("style snapshots own mutable lists but share immutable raster storage", () => {
            var n = Source(); var asset = new string('A', 1000); n.Fills[0].ImageData = asset;
            var p = LayerProperties.Capture(n); Check(ReferenceEquals(asset, p.Fills![0].ImageData));
            n.Fills[0].Stops[0].Color = "#FFFFFF"; n.Strokes[0].Dashes[0] = 8;
            Check(p.Fills[0].Stops[0].Color == "#FF0000" && p.Strokes![0].Dashes[0] == 0);
        });
        test("property paste preserves node identity geometry hierarchy content and prototype data", () => {
            var node = new DesignNode { Id = "target", Kind = NodeKind.Text, Name = "Keep name", Text = "Keep content", X = 12, Y = 17, Width = 145, Height = 73, Rotation = 25, FlipY = true };
            var child = node.Add(new() { Id = "keep-child", X = 10 }); var editor = Editor(node); editor.Select(node); var matrix = node.WorldMatrix;
            PropertyTransfer.Paste(editor, LayerProperties.Capture(Source()));
            Check(node.Id == "target" && node.Name == "Keep name" && node.Text == "Keep content" && node.WorldMatrix == matrix && ReferenceEquals(child, node.Children[0]));
            Check(node.FontSize == 37 && node.Strokes[0].Width == 3 && node.Opacity == .7);
            editor.Undo(); Check(editor.Primary!.FontSize == 24 && editor.Primary.Text == "Keep content"); editor.Redo(); Check(editor.Primary!.FontSize == 37);
        });
        test("partial fill paste leaves strokes effects opacity and fonts alone", () => {
            var node = new DesignNode { Kind = NodeKind.Text, FontSize = 20, Opacity = .9 }; var editor = Editor(node); editor.Select(node);
            PropertyTransfer.Paste(editor, LayerProperties.Capture(Source()), PropertyGroups.Fills);
            Check(node.Fills[0].Kind == FillKind.LinearGradient && node.Strokes.Count == 0 && node.Shadows.Count == 0 && node.FontSize == 20 && node.Opacity == .9);
        });
        test("empty property arrays intentionally clear while omitted arrays preserve", () => {
            var n = Source(); var e = Editor(n); e.Select(n);
            PropertyTransfer.Paste(e, new() { Strokes = [] }); Check(n.Strokes.Count == 0 && n.Fills.Count == 1 && n.Shadows.Count == 1);
        });
        test("repeated identical property paste does not add undo history", () => {
            var n = new DesignNode(); var e = Editor(n); e.Select(n); var p = LayerProperties.Capture(Source());
            PropertyTransfer.Paste(e, p); var count = e.History.Count; PropertyTransfer.Paste(e, p); Check(e.History.Count == count);
        });
        test("each paste target owns its independent paint and dash lists", () => {
            var a = new DesignNode(); var b = new DesignNode(); var e = Editor(a, b); e.Select([a.Id, b.Id]);
            PropertyTransfer.Paste(e, LayerProperties.Capture(Source())); a.Fills[0].Stops[0].Color = "#FFFFFF"; a.Strokes[0].Dashes[0] = 10;
            Check(b.Fills[0].Stops[0].Color == "#FF0000" && b.Strokes[0].Dashes[0] == 0);
        });
        test("property paste skips locked targets and only text accepts typography", () => {
            var a = new DesignNode { Kind = NodeKind.Text }; var b = new DesignNode { Kind = NodeKind.Text, Locked = true }; var c = new DesignNode();
            var e = Editor(a, b, c); e.Select([a.Id, b.Id, c.Id]);
            Check(PropertyTransfer.Paste(e, LayerProperties.Capture(Source()), PropertyGroups.Typography) == 1);
            Check(a.FontSize == 37 && b.FontSize == 24 && c.FontSize == 24);
        });
        test("property paste materializes only transferred variable values", () => {
            var n = new DesignNode(); var e = Editor(n); var collection = VariableService.CreateCollection(e, "Tokens");
            var fill = VariableService.Create(e, collection.Id, "Fill", VariableValue.Color("#FFFFFF")); var width = VariableService.Create(e, collection.Id, "Width", VariableValue.Float(200));
            VariableService.Bind(e, n.Id, VariableTarget.Fill, fill.Id); VariableService.Bind(e, n.Id, VariableTarget.Width, width.Id); e.Select(n);
            PropertyTransfer.Paste(e, LayerProperties.Capture(Source()), PropertyGroups.Fills);
            Check(!n.VariableBindings.ContainsKey(VariableTarget.Fill) && n.VariableBindings.ContainsKey(VariableTarget.Width) && n.Width == 200);
        });
        test("full styles survive component synchronization and native schema roundtrip", () => {
            var (e, definition, instance) = Instance(); var target = instance.Children[0]; var id = target.Id; e.Select(target);
            PropertyTransfer.Paste(e, LayerProperties.Capture(Source()));
            e.Edit("Change source", () => { definition.Children[0].FontSize = 55; definition.Children[0].Strokes = [new() { Width = 9 }]; });
            var copy = e.Document.Find(id)!; Check(copy.FontSize == 37 && copy.Strokes[0].Width == 3 && copy.Shadows[0].Kind == EffectKind.InnerShadow);
            var d = DocumentJson.Load(DocumentJson.Save(e.Document)); ComponentService.Synchronize(d);
            Check(d.Find(id)!.FontSize == 37 && d.Find(id)!.Strokes[0].Width == 3 && d.FormatVersion == DesignDocument.CurrentFormatVersion);
        });
        test("inspector style edits persist on instance descendants", () => {
            var (e, definition, instance) = Instance(); var id = instance.Children[0].Id; e.Select(instance.Children[0]);
            PropertyTransfer.Update(e, "Font size", n => n.FontSize = 41);
            PropertyTransfer.Update(e, "Stroke", n => n.Strokes = [new() { Width = 5, Color = "#FF0000" }]);
            e.Edit("Source name", () => definition.Name = "Changed"); Check(e.Document.Find(id)!.FontSize == 41 && e.Document.Find(id)!.Strokes[0].Width == 5);
        });
        test("reset instance overrides restores inherited styles and bindings", () => {
            var (e, definition, instance) = Instance(); e.Select(instance.Children[0]); PropertyTransfer.Paste(e, LayerProperties.Capture(Source()));
            e.Select(instance); ComponentService.ResetOverrides(e);
            Check(instance.Children[0].FontSize == 16 && instance.Children[0].Strokes.Count == 0 && instance.Children[0].VariableBindings.Count == 0);
        });
        test("root instance appearance persists without rewriting definition geometry", () => {
            var (e, definition, instance) = Instance(); var bounds = instance.Bounds; e.Select(instance);
            PropertyTransfer.Paste(e, new() { Opacity = .4, Blend = BlendKind.Screen, CornerRadius = 19 });
            e.Edit("Source change", () => definition.Name = "Updated");
            Check(instance.Opacity == .4 && instance.Blend == BlendKind.Screen && instance.CornerRadius == 19 && instance.Bounds == bounds && definition.Opacity == 1);
        });
        test("style and name overrides follow structurally matched variant layers", () => {
            var a = new DesignNode { Id = "variant-a", Kind = NodeKind.Component, Name = "A" }; a.Add(new() { Id = "a-label", Kind = NodeKind.Text, Name = "Label", FontSize = 16 });
            var b = new DesignNode { Id = "variant-b", Kind = NodeKind.Component, Name = "B" }; b.Add(new() { Id = "b-label", Kind = NodeKind.Text, Name = "Label", FontSize = 20 });
            var e = Editor(a, b); var instance = ComponentService.InsertInstance(e, a, new(400, 0)); var id = instance.Children[0].Id; e.Select(instance.Children[0]);
            PropertyTransfer.Paste(e, LayerProperties.Capture(Source()));
            LayerRename.Apply(e, LayerRename.Plan(LayerRename.SelectedTargets(e), new("Custom label")));
            ComponentVariants.Switch(e, instance.Id, b.Id);
            Check(instance.Children[0].Id == id && instance.Children[0].FontSize == 37 && instance.Children[0].Name == "Custom label");
        });
        test("property clipboard rejects foreign versions and does not mutate editor state", () => {
            Throws<InvalidDataException>(() => PropertyClipboard.Read("OtherApp/1\n{}"));
            Throws<InvalidDataException>(() => PropertyClipboard.Read(PropertyClipboard.Prefix + "{\"version\":2,\"properties\":{}}"));
            Throws<InvalidDataException>(() => PropertyClipboard.Read(PropertyClipboard.Prefix + "{\"version\":1,\"properties\":null}"));
        });
        test("invalid transferred typography and strokes fail before history capture", () => {
            var n = new DesignNode(); var e = Editor(n); e.Select(n);
            Throws<InvalidDataException>(() => PropertyTransfer.Paste(e, new() { Typography = new() { FontSize = double.NaN } }));
            Throws<InvalidDataException>(() => PropertyTransfer.Paste(e, new() { Strokes = [new() { Dashes = [-1, 2] }] }));
            Throws<InvalidDataException>(() => PropertyTransfer.Paste(e, new() { Opacity = 2 })); Check(!e.CanUndo);
        });
        test("new instance property records are bounded by native validation", () => {
            var (e, _, instance) = Instance(); instance.Overrides[instance.ComponentId!] = new() { Typography = new() { FontFamily = null! } };
            Throws<InvalidDataException>(() => DocumentJson.Validate(e.Document));
        });
        test("schema four migrates to five without changing old appearance", () => {
            var d = new DesignDocument { FormatVersion = 4, Pages = [new() { Nodes = [Source()] }] };
            var copy = DocumentJson.Load(DocumentJson.Save(d)); Check(copy.FormatVersion == 6 && copy.Pages[0].Nodes[0].FontSize == 37);
        });
        test("stable send-to-back retains relative selected and untouched order", () => {
            var nodes = Enumerable.Range(0, 5).Select(i => new DesignNode { Id = "n" + i }).ToArray(); var e = Editor(nodes); e.Select(["n1", "n3"]); e.Reorder(-1, true);
            Check(string.Join(',', e.Page.Nodes.Select(n => n.Id)) == "n1,n3,n0,n2,n4"); e.Undo(); Check(e.Page.Nodes[0].Id == "n0"); e.Redo(); Check(e.Page.Nodes[1].Id == "n3");
        });
        test("single-step order moves selected runs without crossing selected peers", () => {
            var list = new List<int> { 0, 1, 2, 3, 4, 5 }; LayerOrdering.Move(list, new HashSet<int> { 1, 2, 4 }, 1);
            Check(list.SequenceEqual(new[] { 0, 3, 1, 2, 5, 4 }));
        });
        test("already extreme selection is a no-op without undo snapshots", () => {
            var a = new DesignNode(); var b = new DesignNode(); var e = Editor(a, b); e.Select([a.Id, b.Id]); e.Reorder(1, true); e.Reorder(-1);
            Check(!e.CanUndo);
        });
        test("layer order groups distinct parents and does not move locked selections", () => {
            var p = new DesignNode { Kind = NodeKind.Frame }; var q = new DesignNode { Kind = NodeKind.Frame };
            var a = p.Add(new()); p.Add(new()); var b = q.Add(new()); q.Add(new()); var locked = q.Add(new() { Locked = true });
            var e = Editor(p, q); e.Select([a.Id, b.Id, locked.Id]); e.Reorder(1, true);
            Check(p.Children[^1] == a && q.Children[^1] == b && locked.Parent == q);
        });
        test("reordering instance children fails atomically with a clear editing boundary", () => {
            var (e, _, instance) = Instance(); e.Select(instance.Children[0]);
            Throws<InvalidOperationException>(() => e.Reorder(1)); Check(instance.Children.Count == 1);
        });
        test("stable ordering matches independent block-move oracle exhaustively", () => {
            for (var count = 1; count <= 8; count++) for (var mask = 0; mask < (1 << count); mask++) foreach (var direction in new[] { -1, 1 }) foreach (var extreme in new[] { false, true })
            {
                var values = Enumerable.Range(0, count).ToList(); var selected = values.Where(i => (mask & (1 << i)) != 0).ToHashSet();
                var expected = OrderOracle(values, selected, direction, extreme); LayerOrdering.Move(values, selected, direction, extreme);
                Check(values.SequenceEqual(expected), $"Order differs for {count}/{mask}/{direction}/{extreme}");
            }
        });
        test("rename patterns support current name ascending descending and padded numbers", () => {
            RenameTarget[] targets = [new("a", "First"), new("b", "Second"), new("c", "Third")];
            var plan = LayerRename.Plan(targets, new("$nn $& $NN", StartAt: 7));
            Check(plan.Select(p => p.After).SequenceEqual(new[] { "07 First 09", "08 Second 08", "09 Third 07" }));
        });
        test("literal rename matching does not interpret metacharacters", () => {
            var plan = LayerRename.Plan([new("a", "Icon.* / Small")], new("Image", "Icon.*")); Check(plan[0].After == "Image / Small");
        });
        test("regex renaming supports capture groups and preserves unmatched names", () => {
            var plan = LayerRename.Plan([new("a", "Icon_003"), new("b", "Untouched")], new("$2_$1", "([a-zA-Z]+)_(\\d+)", true));
            Check(plan.Count == 1 && plan[0].After == "003_Icon");
        });
        test("rename escaped dollar and zero-width matches are deterministic", () => {
            var plan = LayerRename.Plan([new("a", "Icon")], new("$$n-$&", "^", true)); Check(plan[0].After == "$n-Icon");
        });
        test("rename rejects unsupported backtracking syntax and oversized expansion", () => {
            Throws<ArgumentException>(() => LayerRename.Plan([new("a", "Icon")], new("X", "(?=Icon)", true)));
            Throws<ArgumentException>(() => LayerRename.Plan([new("a", new string('A', 4096))], new("$&$&")));
            Throws<ArgumentException>(() => LayerRename.Plan([new("a", "Icon")], new("")));
        });
        test("batch naming is preflighted and rolls back as a single transaction", () => {
            var a = new DesignNode { Name = "A" }; var b = new DesignNode { Name = "B" }; var e = Editor(a, b); e.Select([a.Id, b.Id]);
            var plan = LayerRename.Plan(LayerRename.SelectedTargets(e), new("Card $nn")); LayerRename.Apply(e, plan);
            Check(a.Name == "Card 02" && b.Name == "Card 01" && e.History.Count == 1); e.Undo(); Check(e.Page.Nodes[0].Name == "A" && e.Page.Nodes[1].Name == "B");
        });
        test("stale rename plans cannot overwrite peer names or partially apply", () => {
            var a = new DesignNode { Name = "A" }; var b = new DesignNode { Name = "B" }; var e = Editor(a, b); e.Select([a.Id, b.Id]);
            var plan = LayerRename.Plan(LayerRename.SelectedTargets(e), new("Renamed")); b.Name = "Peer name";
            Throws<InvalidOperationException>(() => LayerRename.Apply(e, plan)); Check(a.Name == "A" && b.Name == "Peer name" && !e.CanUndo);
        });
        test("rename selection skips initially locked nodes and rejects later locks", () => {
            var a = new DesignNode(); var b = new DesignNode { Locked = true }; var e = Editor(a, b); e.Select([a.Id, b.Id]);
            var targets = LayerRename.SelectedTargets(e); Check(targets.Count == 1); var plan = LayerRename.Plan(targets, new("Card")); a.Locked = true;
            Throws<InvalidOperationException>(() => LayerRename.Apply(e, plan));
        });
        test("renaming nested selected layers preserves their geometry and identity", () => {
            var root = new DesignNode { Kind = NodeKind.Frame, Name = "Frame", X = 50, Rotation = 30 }; var child = root.Add(new() { Name = "Child", X = 10 });
            var e = Editor(root); e.Select([root.Id, child.Id]); var matrix = child.WorldMatrix;
            LayerRename.Apply(e, LayerRename.Plan(LayerRename.SelectedTargets(e), new("Layer $n")));
            Check(root.Name == "Layer 1" && child.Name == "Layer 2" && child.WorldMatrix == matrix && child.Parent == root);
        });
        test("compact transferred typography uses explicit constructor defaults", () => {
            var packet = PropertyClipboard.Read(PropertyClipboard.Prefix + "{\"properties\":{\"typography\":{\"fontSize\":19}}}");
            Check(packet.Properties.Typography!.FontSize == 19 && packet.Properties.Typography.FontFamily == "Inter" && packet.Properties.Typography.LineHeight == 1.25);
        });
        test("public rename plans validate null entries and preserve no-op history", () => {
            var n = new DesignNode(); var e = Editor(n);
            Throws<ArgumentException>(() => LayerRename.Apply(e, [null!]));
            Check(LayerRename.Apply(e, [new(n.Id, n.Name, n.Name)]) == 0 && !e.CanUndo);
        });
        test("style cloning covers every serialized paint property", () => {
            var f = Source().Fills[0]; f.ImageMode = ImageScaleMode.Tile; f.ImageScale = 1.7; f.ImageRotation = 37; f.ImageOffset = new(.1, -.2); f.Exposure = .3; f.Contrast = -.4; f.Saturation = .7;
            f.GradientRadius = 3; f.GradientFocal = new(.2, .7); f.GradientUserSpace = true;
            var a = new LayerProperties { Fills = [f] }; var b = new LayerProperties { Fills = StyleCloner.Fills([f]) };
            Check(JsonSerializer.Serialize(a, PropertyClipboardJson.Default.LayerProperties) == JsonSerializer.Serialize(b, PropertyClipboardJson.Default.LayerProperties));
        });
    }
    private static List<int> OrderOracle(List<int> input, HashSet<int> chosen, int direction, bool extreme)
    {
        if (extreme) return direction > 0 ? input.Where(x => !chosen.Contains(x)).Concat(input.Where(chosen.Contains)).ToList() : input.Where(chosen.Contains).Concat(input.Where(x => !chosen.Contains(x))).ToList();
        var result = input.ToList(); var runs = new List<int[]>();
        for (var i = 0; i < input.Count;)
        {
            if (!chosen.Contains(input[i])) { i++; continue; }
            var start = i; while (i < input.Count && chosen.Contains(input[i])) i++;
            runs.Add(input.GetRange(start, i - start).ToArray());
        }
        foreach (var run in direction > 0 ? runs.AsEnumerable().Reverse() : runs)
        {
            var start = result.IndexOf(run[0]); var neighbour = direction > 0 ? start + run.Length : start - 1;
            if (neighbour < 0 || neighbour >= result.Count || chosen.Contains(result[neighbour])) continue;
            result.RemoveRange(start, run.Length); result.InsertRange(direction > 0 ? start + 1 : start - 1, run);
        }
        return result;
    }
}
