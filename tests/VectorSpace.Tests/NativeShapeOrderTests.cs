using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static class NativeShapeOrderTests
{
    public static void Register(Action<string, Action> test)
    {
        test("native rectangle conversion keeps top-left clockwise anchor identities", () =>
        {
            var node = new DesignNode { Width = 100, Height = 80 };
            var editor = new EditorSession(new() { Pages = [new() { Nodes = [node] }] }); editor.Select(node);
            using var renderer = new SceneRenderer();
            EditablePathConversion.Convert(editor, renderer, node);
            Vec2[] expected = [new(0, 0), new(100, 0), new(100, 80), new(0, 80)];
            if (!node.Points.Select(p => p.Position).SequenceEqual(expected)) throw new Exception("Native conversion reindexed the rectangle's anchors.");
            PathEditing.Move(node, [0, 1], new(21, 15));
            if (node.Points[0].Position != new Vec2(21, 15) || node.Points[1].Position != new Vec2(121, 15) || node.Points[2].Position != expected[2])
                throw new Exception("Top-edge editing changed the wrong anchors.");
        });
        test("native corner contour starts at the same tangent as the portable path", () =>
        {
            foreach (var radii in new[] { new CornerRadii(0, 0, 0, 0), new CornerRadii(12, 12, 12, 12), new CornerRadii(9, 0, 24, 16), new CornerRadii(0, 20, 0, 8) })
            {
                var n = new DesignNode { Width = 100, Height = 80, Corners = radii };
                using var native = NativeShapeGeometry.Build(n);
                using var svg = SKPath.ParseSvgPathData(VectorPath.Build(n));
                var a = NativeShapeGeometry.Capture(native); var b = NativeShapeGeometry.Capture(svg);
                if (a[0].Verb != PathVerb.Move || a[0].Point != b[0].Point) throw new Exception("Native and portable contour origins differ.");
                if (a.Count(c => c.Verb == PathVerb.Conic) != new[] { radii.TopLeft, radii.TopRight, radii.BottomRight, radii.BottomLeft }.Count(r => r > 0))
                    throw new Exception("Native radii did not retain one rational quadrant per corner.");
                var random = new Random(7281);
                for (var i = 0; i < 1000; i++)
                {
                    var x = (float)(random.NextDouble() * 100); var y = (float)(random.NextDouble() * 80);
                    if (native.Contains(x, y) != svg.Contains(x, y)) throw new Exception("Native/portable corner silhouettes differ.");
                }
            }
        });
        test("native primitive anchor origin survives history and renderer cache replacement", () =>
        {
            var node = new DesignNode { Width = 240, Height = 240 };
            var editor = new EditorSession(new() { Pages = [new() { Nodes = [node] }] }); editor.Select(node);
            using var renderer = new SceneRenderer();
            EditablePathConversion.Convert(editor, renderer, node); editor.Undo(); renderer.ClearCache();
            EditablePathConversion.Convert(editor, renderer, editor.Primary!);
            if (editor.Primary!.Points[0].Position != Vec2.Zero || editor.Primary.Points.Count != 4)
                throw new Exception("Cache replacement changed the contour's first anchor.");
            editor.BeginInteraction("Move top edge"); PathEditing.Move(editor.Primary, [0, 1], new(20, 30)); editor.CancelInteraction();
            if (editor.Primary!.Points[0].Position != Vec2.Zero || editor.Primary.Points[1].Position != new Vec2(240, 0))
                throw new Exception("Cancellation did not restore the original indexed top edge.");
        });
    }
}
