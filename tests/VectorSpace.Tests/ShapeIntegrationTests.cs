using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static class ShapeIntegrationTests
{
    private static void Check(bool value, string message = "Shape integration assertion failed") { if (!value) throw new Exception(message); }
    public static void Register(Action<string, Action> test)
    {
        ShapeOverrideTests.Register(test);
        LegacyBooleanTests.Register(test);
        test("normal selection ignores a Boolean hole while entered scope reaches retained operands", () =>
        {
            var frame = new DesignNode { Kind = NodeKind.Frame, Width = 300, Height = 200 };
            var group = frame.Add(new() { Kind = NodeKind.Group, Boolean = BooleanKind.Subtract, Width = 200, Height = 100 });
            group.Add(new() { Width = 200, Height = 100 }); var hole = group.Add(new() { Kind = NodeKind.Ellipse, X = 50, Width = 100, Height = 100 });
            using var renderer = new SceneRenderer();
            Check(renderer.HitTestForSelection([frame], new(100, 50), null, false, 0) == frame);
            Check(renderer.HitTestForSelection([frame], new(100, 50), group, false, 0) == hole);
            Check(renderer.HitTestForSelection([frame], new(100, 50), null, true, 0) == hole);
            Check(renderer.HitTestForSelection([frame], new(20, 50), null, false, 0) == group);
        });
        test("prototype presentation uses independent frame corner clipping", () =>
        {
            var frame = new DesignNode { Kind = NodeKind.Frame, Width = 100, Height = 100, Corners = new(40, 0, 0, 0) };
            var child = frame.Add(new() { Width = 100, Height = 100 }); using var renderer = new SceneRenderer();
            Check(renderer.HitPrototypeFrame(frame, new(2, 2), Vec2.Zero) is null);
            Check(renderer.HitPrototypeFrame(frame, new(98, 2), Vec2.Zero) == child);
        });
        test("prototype picking cannot activate an unpainted retained Boolean operand", () =>
        {
            var frame = new DesignNode { Kind = NodeKind.Frame, Width = 300, Height = 200 };
            var group = frame.Add(new() { Kind = NodeKind.Group, Boolean = BooleanKind.Subtract, Width = 200, Height = 100 });
            group.Add(new() { Width = 200, Height = 100 }); group.Add(new() { Kind = NodeKind.Ellipse, X = 50, Width = 100, Height = 100, PrototypeTargetId = "frame" });
            using var renderer = new SceneRenderer(); Check(renderer.HitPrototypeFrame(frame, new(100, 50), Vec2.Zero) == frame);
            Check(renderer.HitPrototypeFrame(frame, new(20, 50), Vec2.Zero) == group);
        });
        test("uniform corner override clears source independent corners across synchronization", () =>
        {
            var component = new DesignNode { Kind = NodeKind.Component, Width = 200, Height = 100 };
            component.Add(new() { Width = 100, Height = 80, Corners = new(30, 10, 20, 5) });
            var editor = new EditorSession(new() { Pages = [new() { Nodes = [component] }] });
            var instance = ComponentService.InsertInstance(editor, component, new(300, 0)); var id = instance.Children[0].Id;
            editor.Select(instance.Children[0]);
            PropertyTransfer.Update(editor, "Uniform radius", n => { n.CornerRadius = 12; n.Corners = null; });
            editor.Edit("Change source", () => component.Children[0].Fill = "#FF0000");
            var child = editor.Document.Find(id)!; Check(child.Corners is null && child.EffectiveCorners == new CornerRadii(12, 12, 12, 12));
        });
        test("both legacy and new property clipboard packets remain readable", () =>
        {
            var old = PropertyClipboard.Read(PropertyClipboard.Prefix + "{\"version\":1,\"properties\":{\"strokes\":[{\"width\":4}]}}");
            Check(old.Properties.Strokes![0].Cap == StrokeCap.Round && old.Properties.Strokes[0].Alignment == StrokeAlignment.Center);
            var node = new DesignNode { Strokes = [new() { Alignment = StrokeAlignment.Outside, Cap = StrokeCap.Square, Join = StrokeJoin.Bevel, DashOffset = 2 }] };
            var text = PropertyClipboard.Copy(node); var current = PropertyClipboard.Read(text);
            Check(current.Version == 2 && StyleCloner.SameStrokes(node.Strokes, current.Properties.Strokes!));
        });
    }
}
