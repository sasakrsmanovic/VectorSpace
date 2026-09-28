using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Prototyping;
using VectorSpace.Skia;

internal static class PrototypeTests
{
    public static void Register(Action<string, Action> test)
    {
        test("prototype playback does not mutate source geometry or history", () =>
        {
            var d = Document(); var editor = new EditorSession(d); editor.Viewport.Pan = new(12, 34); editor.Select(d.Find("a"));
            var before = DocumentJson.Save(d); var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "go", userInitiated: true); p.Back();
            Check(before == DocumentJson.Save(d)); Equal(editor.History.Count, 0); Check(editor.Viewport.Pan == new Vec2(12, 34)); Check(editor.Primary!.Id == "a");
        });
        test("prototype legacy click links do not navigate on press", () =>
        {
            var d = Document(); d.Find("go")!.Reactions.Clear(); d.Find("go")!.PrototypeTargetId = "b";
            var p = new PrototypeSession(d); Check(!p.Dispatch(PrototypeTrigger.MouseDown, "go")); Check(p.View.FrameId == "a");
            Check(p.Dispatch(PrototypeTrigger.Click, "go")); Check(p.View.FrameId == "b");
        });
        test("prototype ancestor reaction bubbles from a child hotspot", () =>
        {
            var d = Document(); var go = d.Find("go")!; go.Add(new() { Id = "label" });
            var p = new PrototypeSession(d); Check(p.TriggerOwner("label", PrototypeTrigger.Click) == "go"); p.Dispatch(PrototypeTrigger.Click, "label"); Check(p.View.FrameId == "b");
        });
        test("prototype hidden hotspots and inactive frames cannot dispatch", () =>
        {
            var d = Document(); d.Find("go")!.Visible = false; var p = new PrototypeSession(d);
            Check(!p.Dispatch(PrototypeTrigger.Click, "go")); Check(!p.Dispatch(PrototypeTrigger.Click, "back"));
        });
        test("prototype locked design layers remain interactive in presentation", () =>
        {
            var d = Document(); d.Find("go")!.Locked = true; var p = new PrototypeSession(d);
            p.Dispatch(PrototypeTrigger.Click, "go"); Check(p.View.FrameId == "b");
        });
        test("prototype back preserves scroll state and restart resets it", () =>
        {
            var d = Document(); var a = d.Find("a")!; a.PrototypeOverflow = PrototypeOverflow.Vertical; a.Add(new() { Id = "bottom", Y = 800 });
            var p = new PrototypeSession(d); p.ScrollBy(new(0, 120)); p.Dispatch(PrototypeTrigger.Click, "go"); p.Back(); Equal(p.View.Scroll.Y, 120);
            p.Restart(); Equal(p.View.Scroll.Y, 0); Equal(p.HistoryCount, 0);
        });
        test("prototype history is bounded", () =>
        {
            var d = Document(); d.Find("back")!.Reactions = [Reaction(new PrototypeAction { TargetId = "a" })];
            var p = new PrototypeSession(d); for (var i = 0; i < 150; i++) p.Dispatch(PrototypeTrigger.Click, p.View.FrameId == "a" ? "go" : "back");
            Equal(p.HistoryCount, 128);
        });
        test("prototype modal input cannot click through to the parent", () =>
        {
            var p = OpenModal(); Check(p.View.Overlays.Count == 1); Check(!p.Dispatch(PrototypeTrigger.Click, "go")); Check(p.View.FrameId == "a");
            Check(p.OutsideClick()); Equal(p.View.Overlays.Count, 0); Equal(p.HistoryCount, 0);
        });
        test("prototype nondismissible outside clicks remain consumed", () =>
        {
            var d = Document(); d.Find("open")!.Reactions[0].Actions[0].Overlay.CloseOnOutsideClick = false;
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "open"); Check(p.OutsideClick()); Equal(p.View.Overlays.Count, 1);
            p.Back(); Equal(p.View.Overlays.Count, 0);
        });
        test("prototype explicit overlay close and swap preserve navigation", () =>
        {
            var d = Document(); var m = d.Find("modal")!;
            Hot(m, "swap", new PrototypeAction { Kind = PrototypeActionKind.SwapOverlay, TargetId = "b" });
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "open"); p.Dispatch(PrototypeTrigger.Click, "swap");
            Check(p.View.Overlays[^1].FrameId == "b"); Equal(p.HistoryCount, 0); p.Back(); Check(p.View.FrameId == "a");
            p.Dispatch(PrototypeTrigger.Click, "open"); p.Dispatch(PrototypeTrigger.Click, "close"); Equal(p.View.Overlays.Count, 0);
        });
        test("prototype overlay stack limit rejects the entire action batch", () =>
        {
            var d = Document(); Hot(d.Find("modal")!, "again", new PrototypeAction { Kind = PrototypeActionKind.OpenOverlay, TargetId = "modal" });
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "open");
            for (var i = 1; i < 16; i++) p.Dispatch(PrototypeTrigger.Click, "again");
            Throws<InvalidOperationException>(() => p.Dispatch(PrototypeTrigger.Click, "again")); Equal(p.View.Overlays.Count, 16);
        });
        test("prototype missing destinations are retained for repair and fail atomically", () =>
        {
            var d = Document(); d.Find("go")!.Reactions[0].Actions.Add(new PrototypeAction { TargetId = "missing" }); DocumentJson.Validate(d);
            var p = new PrototypeSession(d); Throws<InvalidOperationException>(() => p.Dispatch(PrototypeTrigger.Click, "go"));
            Check(p.View.FrameId == "a"); Equal(p.HistoryCount, 0);
        });
        test("prototype variable assignments and conditions execute in order", () =>
        {
            var d = Document(); Variable(d, "flag", VariableValue.Bool(false));
            d.Find("go")!.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.SetVariable, VariableId = "flag", Value = VariableValue.Bool(true) }, new PrototypeAction { Kind = PrototypeActionKind.Conditional, Condition = new() { VariableId = "flag", Value = VariableValue.Bool(true) }, Then = [new PrototypeAction { TargetId = "b" }], Else = [new PrototypeAction { TargetId = "modal" }] })];
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "go"); Check(p.View.FrameId == "b");
            Check(new VariableResolver(p.Document).Resolve("flag").Boolean); Check(!new VariableResolver(d).Resolve("flag").Boolean);
        });
        test("prototype variable operations and bindings render private values", () =>
        {
            var d = Document(); Variable(d, "n", VariableValue.Float(20));
            d.Find("go")!.VariableBindings[VariableTarget.Width] = new() { VariableId = "n", Fallback = VariableValue.Float(20) };
            d.Find("go")!.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.SetVariable, VariableId = "n", Operation = PrototypeVariableOperation.Add, Value = VariableValue.Float(7) })];
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "go"); Equal(p.Find("go")!.Width, 27); p.Restart(); Equal(p.Find("go")!.Width, 20);
        });
        test("prototype toggle requires a boolean and supports aliases", () =>
        {
            var d = Document(); Variable(d, "flag", VariableValue.Bool(false)); Variable(d, "alias", VariableValue.Alias(d.Variables[0]));
            d.Find("go")!.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.SetVariable, VariableId = "alias", Operation = PrototypeVariableOperation.Toggle })];
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "go"); Check(new VariableResolver(p.Document).Resolve("alias").Boolean); Check(!new VariableResolver(p.Document).Resolve("flag").Boolean);
        });
        test("prototype mixed navigation and invalid variable batch rolls back", () =>
        {
            var d = Document(); Variable(d, "flag", VariableValue.Bool(false));
            d.Find("go")!.Reactions[0].Actions.Add(new PrototypeAction { Kind = PrototypeActionKind.SetVariable, VariableId = "flag", Value = VariableValue.Float(42) });
            var p = new PrototypeSession(d); var before = DocumentJson.Save(p.Document);
            Throws<InvalidOperationException>(() => p.Dispatch(PrototypeTrigger.Click, "go")); Check(p.View.FrameId == "a" && DocumentJson.Save(p.Document) == before);
        });
        test("prototype numeric overflow rolls back without committing NaN or infinity", () =>
        {
            var d = Document(); Variable(d, "n", VariableValue.Float(double.MaxValue));
            d.Find("go")!.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.SetVariable, VariableId = "n", Operation = PrototypeVariableOperation.Add, Value = VariableValue.Float(double.MaxValue) })];
            var p = new PrototypeSession(d); Throws<InvalidDataException>(() => p.Dispatch(PrototypeTrigger.Click, "go")); Equal(new VariableResolver(p.Document).Resolve("n").Number, double.MaxValue);
        });
        foreach (var comparison in Enum.GetValues<PrototypeComparison>())
        {
            var c = comparison;
            test("prototype numeric comparison " + c, () =>
            {
                var expected = c is PrototypeComparison.NotEqual or PrototypeComparison.Less or PrototypeComparison.LessOrEqual;
                Check(PrototypeSession.Compare(VariableValue.Float(2), VariableValue.Float(3), c) == expected);
            });
        }
        test("prototype string and color equality remain typed", () =>
        {
            Check(PrototypeSession.Compare(VariableValue.String("Hi"), VariableValue.String("Hi"), PrototypeComparison.Equal));
            Check(PrototypeSession.Compare(VariableValue.Color("#ffffff"), VariableValue.Color("#FFFFFFFF"), PrototypeComparison.Equal));
            Throws<InvalidOperationException>(() => PrototypeSession.Compare(VariableValue.Bool(true), VariableValue.Float(1), PrototypeComparison.Equal));
            Throws<InvalidOperationException>(() => PrototypeSession.Compare(VariableValue.String("a"), VariableValue.String("b"), PrototypeComparison.Less));
        });
        test("prototype timers fire only at deadline and invalidate old scene work", () =>
        {
            var d = Document(); var a = d.Find("a")!;
            a.Reactions = [new() { Trigger = PrototypeTrigger.AfterDelay, DelayMilliseconds = 100, Actions = [new PrototypeAction { TargetId = "b" }] }, new() { Trigger = PrototypeTrigger.AfterDelay, DelayMilliseconds = 100, Actions = [new PrototypeAction { TargetId = "modal" }] }];
            var p = new PrototypeSession(d); p.AdvanceTo(99); Check(p.View.FrameId == "a"); p.AdvanceTo(100); Check(p.View.FrameId == "b"); p.AdvanceTo(1000); Check(p.View.FrameId == "b");
        });
        test("prototype self navigation delays cannot loop synchronously", () =>
        {
            var d = Document(); d.Find("a")!.Reactions = [new() { Trigger = PrototypeTrigger.AfterDelay, DelayMilliseconds = 16, Actions = [new PrototypeAction { TargetId = "a" }] }];
            var p = new PrototypeSession(d); p.AdvanceTo(1_000_000); Equal(p.HistoryCount, 1); p.AdvanceTo(1_000_016); Equal(p.HistoryCount, 2);
        });
        test("prototype monotonic clock rejects invalid samples", () =>
        {
            var p = new PrototypeSession(Document()); p.AdvanceTo(5); Throws<ArgumentOutOfRangeException>(() => p.AdvanceTo(4)); Throws<ArgumentOutOfRangeException>(() => p.AdvanceTo(double.NaN));
        });
        test("prototype invalid timed actions report errors without leaving the frame", () =>
        {
            var d = Document(); d.Find("a")!.Reactions = [new() { Trigger = PrototypeTrigger.AfterDelay, DelayMilliseconds = 16, Actions = [new PrototypeAction { TargetId = "missing" }] }];
            var p = new PrototypeSession(d); p.AdvanceTo(16); Check(p.LastError is not null && p.View.FrameId == "a"); Check(p.NextWakeMilliseconds is null);
        });
        test("prototype key chords dispatch without modifying editor shortcuts", () =>
        {
            var d = Document(); d.Find("go")!.Reactions[0].Trigger = PrototypeTrigger.KeyDown; d.Find("go")!.Reactions[0].Key = "ctrl + shift + k";
            var p = new PrototypeSession(d); Check(!p.DispatchKey("K")); Check(p.DispatchKey("CTRL+SHIFT+K")); Check(p.View.FrameId == "b");
        });
        test("prototype external links are requests only and require direct input", () =>
        {
            var d = Document(); var go = d.Find("go")!;
            go.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.OpenUrl, Url = "https://example.com/path" })];
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "go"); Equal(p.DrainRequestedUrls().Length, 0);
            p.Dispatch(PrototypeTrigger.Click, "go", userInitiated: true); Check(p.DrainRequestedUrls().Single() == "https://example.com/path");
            go.Reactions[0].Trigger = PrototypeTrigger.MouseEnter; p = new(d); p.Dispatch(PrototypeTrigger.MouseEnter, "go", userInitiated: true); Equal(p.DrainRequestedUrls().Length, 0);
        });
        foreach (var url in new[] { "javascript:alert(1)", "file:///etc/passwd", "data:text/html,hi", "https://user:password@example.com" })
        {
            var u = url;
            test("prototype unsafe URL rejected: " + u.Split(':')[0] + (u.Contains("password") ? " credentials" : ""), () =>
            {
                var d = Document(); d.Find("go")!.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.OpenUrl, Url = u })]; Throws<InvalidDataException>(() => DocumentJson.Validate(d));
            });
        }
        test("prototype overlay placement and finite scroll clamping", () =>
        {
            var d = Document(); var a = d.Find("a")!; var m = d.Find("modal")!;
            var settings = new PrototypeOverlay(m.Id, PrototypePlacement.BottomRight, new(4, -3), "#000000", .3, true, default);
            Check(PrototypeGeometry.OverlayPosition(a, m, settings) == new Vec2(204, 197));
            a.PrototypeOverflow = PrototypeOverflow.Vertical; a.Add(new() { Y = 800, Height = 100 });
            Check(PrototypeGeometry.ClampScroll(a, new(90, 1000)) == new Vec2(0, 600));
        });
        test("prototype scroll-to is scoped to the modal or active frame", () =>
        {
            var d = Document(); var a = d.Find("a")!; a.PrototypeOverflow = PrototypeOverflow.Vertical; a.Add(new() { Id = "end", Y = 800 });
            d.Find("go")!.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.ScrollTo, TargetId = "end" })];
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "go"); Equal(p.View.Scroll.Y, 600);
            d.Find("go")!.Reactions[0].Actions[0].TargetId = "close"; p = new(d); Throws<InvalidOperationException>(() => p.Dispatch(PrototypeTrigger.Click, "go"));
        });
        test("prototype transitions use caller supplied time and retain immutable views", () =>
        {
            var d = Document(); var t = d.Find("go")!.Reactions[0].Actions[0].Transition; t.Kind = PrototypeTransitionKind.Dissolve; t.DurationMilliseconds = 200; t.Easing = PrototypeEasing.Linear;
            var p = new PrototypeSession(d); p.AdvanceTo(10); p.Dispatch(PrototypeTrigger.Click, "go"); Equal(p.Animation!.Progress(110), .5); Check(p.Animation.From.FrameId == "a" && p.View.FrameId == "b");
            p.AdvanceTo(210); Check(p.Animation is null && p.NextWakeMilliseconds is null);
        });
        test("prototype prepared smart animation reuses nodes and preserves source", () =>
        {
            var a = Frame("a", 200, 100); var b = Frame("b", 200, 100);
            a.Add(new() { Id = "ac", Name = "Match", X = 10, Rotation = 350, Fill = "#000000" }); b.Add(new() { Id = "bc", Name = "Match", X = 110, Rotation = 10, Fill = "#FFFFFF" });
            var tween = new PrototypeTween(a, b); var first = tween.Sample(.25); var half = tween.Sample(.5);
            Check(ReferenceEquals(first, half)); Equal(half.Children[0].X, 60); Equal(half.Children[0].Rotation, 360); Check(half.Children[0].Fill == "#808080");
            Equal(a.Children[0].X, 10); Equal(b.Children[0].X, 110);
        });
        test("prototype smart animation fades unmatched branches once", () =>
        {
            var a = Frame("a"); var b = Frame("b"); var added = b.Add(new() { Name = "New" }); added.Add(new() { Name = "Nested" });
            var tween = new PrototypeTween(a, b); var frame = tween.Sample(.5); Equal(frame.Children[0].Opacity, .5); Equal(frame.Children[0].Children[0].Opacity, 1);
        });
        test("prototype smart overlay transitions explicitly fall back to dissolve", () =>
        {
            var d = Document(); d.Find("open")!.Reactions[0].Actions[0].Transition.Kind = PrototypeTransitionKind.SmartAnimate;
            var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, "open"); Check(p.Animation!.Kind == PrototypeTransitionKind.Dissolve);
        });
        test("prototype picking ignores locks but respects rounded clips", () =>
        {
            var f = Frame("f", 100, 100); f.CornerRadius = 30; f.Locked = true; var hot = Hot(f, "hot", new PrototypeAction { Kind = PrototypeActionKind.Back }); hot.Width = hot.Height = 100; hot.X = hot.Y = 0; hot.Opacity = 0;
            using var r = new SceneRenderer(); Check(r.HitPrototypeFrame(f, new(1, 1), default) is null); Check(r.HitPrototypeFrame(f, new(50, 50), default)?.Id == "hot");
        });
        test("prototype frame scroll does not move its background or leak clipped content", () =>
        {
            var f = Frame("f", 100, 100); f.X = 600; f.Y = -200; f.Rotation = 30; f.Fill = "#FFFFFF";
            f.Add(new() { Id = "off", X = 0, Y = 200, Width = 60, Height = 40, Fill = "#FF0000" });
            using var r = new SceneRenderer(); using var bitmap = new SKBitmap(120, 120); using var c = new SKCanvas(bitmap); c.Clear(SKColors.Blue); r.DrawPrototypeFrame(c, f, new(0, 200));
            Check(bitmap.GetPixel(20, 20).Red > 240 && bitmap.GetPixel(20, 20).Green < 10);
            Check(bitmap.GetPixel(80, 80) == SKColors.White); Check(bitmap.GetPixel(110, 110) == SKColors.Blue);
            Check(r.HitPrototypeFrame(f, new(20, 20), new(0, 200))?.Id == "off");
        });
        test("prototype compositor draws modal backdrop and aligns overlay hit coordinates", () =>
        {
            var p = OpenModal(); using var r = new SceneRenderer(); var compositor = new PrototypeSceneRenderer(r);
            using var bitmap = new SKBitmap(400, 300); using var c = new SKCanvas(bitmap); c.Clear(SKColors.Transparent); compositor.Draw(c, p);
            Check(bitmap.GetPixel(10, 290).Red < 220); Check(bitmap.GetPixel(290, 110).Blue > 200);
            Check(compositor.Hit(p, new(130, 130), out var outside)?.Id == "close" && !outside);
            Check(compositor.Hit(p, new(10, 290), out outside) is null && outside);
        });
        test("prototype new references remap when cloning a connected subtree", () =>
        {
            var root = Frame("root"); var a = root.Add(Frame("a")); root.Add(Frame("b")); Hot(a, "go", new PrototypeAction { TargetId = "b" });
            var copy = DocumentJson.CloneNode(root, true); Check(copy.Children[0].Children[0].Reactions[0].Actions[0].TargetId == copy.Children[1].Id);
        });
        test("prototype cross-document clipboard remaps action and condition variables", () =>
        {
            var d = Document(); Variable(d, "flag", VariableValue.Bool(false)); d.Find("go")!.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.SetVariable, VariableId = "flag", Value = VariableValue.Bool(true) }, new PrototypeAction { Kind = PrototypeActionKind.Conditional, Condition = new() { VariableId = "flag" } })];
            var source = new EditorSession(d); source.Select(d.Find("go")); var target = new EditorSession(new()); target.Paste(source.CopySelection());
            var id = target.Document.Variables.Single().Id; Check(id != "flag"); Check(target.Primary!.Reactions[0].Actions[0].VariableId == id && target.Primary.Reactions[0].Actions[1].Condition!.VariableId == id);
        });
        test("prototype component reactions inherit, locally override and reset", () =>
        {
            var component = Frame("definition"); component.Kind = NodeKind.Component; component.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.Back })];
            var editor = new EditorSession(new() { Pages = [new() { Nodes = [component] }] }); var instance = ComponentService.InsertInstance(editor, component, new(500, 0));
            Check(instance.Reactions.Count == 1 && !instance.PrototypeReactionsOverride);
            instance.PrototypeReactionsOverride = true; instance.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.CloseOverlay })]; ComponentService.Synchronize(editor.Document); Check(instance.Reactions[0].Actions[0].Kind == PrototypeActionKind.CloseOverlay);
            instance.PrototypeReactionsOverride = false; ComponentService.Synchronize(editor.Document); Check(instance.Reactions[0].Actions[0].Kind == PrototypeActionKind.Back);
        });
        test("prototype interactive variant changes the instance and not its source", () =>
        {
            var d = Document(); var set = new DesignNode { Id = "set", Kind = NodeKind.ComponentSet, Fills = [] };
            var off = set.Add(Frame("off", 120, 40)); off.Kind = NodeKind.Component; off.Fill = "#FF0000";
            var on = set.Add(Frame("on", 120, 40)); on.Kind = NodeKind.Component; on.Fill = "#00FF00";
            off.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.ChangeVariant, TargetId = "on" })]; on.Reactions = [Reaction(new PrototypeAction { Kind = PrototypeActionKind.ChangeVariant, TargetId = "off" })];
            d.Pages.Add(new() { Nodes = [set] }); d.RebuildParents(); var editor = new EditorSession(d); var instance = ComponentService.InsertInstance(editor, off, new(10, 10)); editor.RemoveNode(instance); d.Find("a")!.Add(instance); ComponentService.Synchronize(d);
            var before = DocumentJson.Save(d); var p = new PrototypeSession(d); p.Dispatch(PrototypeTrigger.Click, instance.Id);
            Check(p.Find(instance.Id)!.ComponentId == "on" && p.Find(instance.Id)!.Fill == "#00FF00"); p.Dispatch(PrototypeTrigger.Click, instance.Id); Check(p.Find(instance.Id)!.ComponentId == "off"); Check(DocumentJson.Save(d) == before);
        });
        test("prototype schema v3 round-trip preserves reactions and old links", () =>
        {
            var d = Document(); d.FormatVersion = 2; d.Find("go")!.PrototypeTargetId = "b";
            var round = DocumentJson.Load(DocumentJson.Save(d)); Equal(round.FormatVersion, 4); Check(round.Find("go")!.Reactions.Count == 1 && round.Find("go")!.PrototypeTargetId == "b");
        });
        test("prototype playground is valid and its smart interactions render", () =>
        {
            var doc = PrototypeSample.Create(); var player = new PrototypeSession(doc);
            var target = player.View.Frame.Children.First(n => n.Name.StartsWith("Explore the details"));
            player.Dispatch(PrototypeTrigger.Click, target.Id); Check(player.View.FrameId == "prototype-detail");
            using var renderer = new SceneRenderer(); var compositor = new PrototypeSceneRenderer(renderer);
            using var bitmap = new SKBitmap(640, 600); using var canvas = new SKCanvas(bitmap);
            player.AdvanceTo(200); compositor.Draw(canvas, player); Check(bitmap.GetPixel(620, 580).Alpha > 0);
        });
        test("prototype action edits participate in editor undo redo", () =>
        {
            var editor = new EditorSession(Document()); editor.Edit("Change transition", () => editor.Document.Find("go")!.Reactions[0].Actions[0].Transition.Kind = PrototypeTransitionKind.Push);
            editor.Undo(); Check(editor.Document.Find("go")!.Reactions[0].Actions[0].Transition.Kind == PrototypeTransitionKind.Instant);
            editor.Redo(); Check(editor.Document.Find("go")!.Reactions[0].Actions[0].Transition.Kind == PrototypeTransitionKind.Push);
        });
        test("prototype invalid structures and zero-delay loops are rejected", () =>
        {
            var d = Document(); d.Find("go")!.Reactions[0].DelayMilliseconds = 0; Throws<InvalidDataException>(() => DocumentJson.Validate(d));
            d.Find("go")!.Reactions[0].DelayMilliseconds = 16; d.Find("go")!.Reactions[0].Actions[0].Overlay.BackdropOpacity = double.NaN; Throws<InvalidDataException>(() => DocumentJson.Validate(d));
        });
    }
    internal static DesignDocument Document()
    {
        var a = Frame("a", 400, 300); a.PrototypeFlowName = "Main"; var b = Frame("b", 400, 300); b.X = 600; var modal = Frame("modal", 200, 100); modal.X = 1200; modal.Fill = "#0000FF";
        Hot(a, "go", new PrototypeAction { TargetId = "b" }); var open = Hot(a, "open", new PrototypeAction { Kind = PrototypeActionKind.OpenOverlay, TargetId = "modal" }); open.Y = 100;
        Hot(b, "back", new PrototypeAction { Kind = PrototypeActionKind.Back }); Hot(modal, "close", new PrototypeAction { Kind = PrototypeActionKind.CloseOverlay });
        return new() { Pages = [new() { Id = "page", Name = "Prototype", Nodes = [a, b, modal] }] };
    }
    internal static DesignNode Frame(string id, double width = 400, double height = 300) => new() { Id = id, Name = id, Kind = NodeKind.Frame, Width = width, Height = height, Fill = "#FFFFFF", ClipContent = true };
    internal static DesignNode Hot(DesignNode parent, string id, params PrototypeAction[] actions) => parent.Add(new() { Id = id, Name = id, X = 20, Y = 20, Width = 100, Height = 50, Fill = "#DDDDDD", Reactions = [Reaction(actions)] });
    private static PrototypeReaction Reaction(params PrototypeAction[] actions) => new() { Actions = actions.ToList() };
    private static void Variable(DesignDocument d, string id, VariableValue value)
    {
        if (d.VariableCollections.Count == 0) d.VariableCollections.Add(new() { Id = "collection", DefaultModeId = "default", Modes = [new() { Id = "default" }] });
        d.Variables.Add(new() { Id = id, Name = id, CollectionId = "collection", Type = value.Type, Values = new() { ["default"] = value } });
    }
    private static PrototypeSession OpenModal() { var p = new PrototypeSession(Document()); p.Dispatch(PrototypeTrigger.Click, "open"); return p; }
    private static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Equal(double actual, double expected) { if (actual != expected && Math.Abs(actual - expected) > .0001) throw new Exception($"Expected {expected}, got {actual}"); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
