using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static class StrokeOutlineExtentTests
{
    public static void Register(Action<string, Action> test)
    {
        foreach (var vertical in new[] { false, true })
            foreach (var rotation in new[] { 0d, 27d })
                test($"stroke outline preserves zero-extent {(vertical ? "vertical" : "horizontal")} line at {rotation} degrees", () =>
                {
                    var n = new DesignNode
                    {
                        Kind = NodeKind.Line, X = 60, Y = 60, Width = vertical ? 0 : 100, Height = vertical ? 100 : 0,
                        Rotation = rotation, Fills = [], Strokes = [new() { Width = 12, Color = "#2266AA", Dashes = [10, 8], Cap = StrokeCap.Round }]
                    };
                    var e = new EditorSession(new() { Pages = [new() { Nodes = [n] }] }); e.Select(n);
                    using var r = new SceneRenderer(); var bounds = new RectD(0, 0, 230, 230);
                    using var before = SKBitmap.Decode(r.ExportPng([n], bounds));
                    LiveBooleanOperations.Outline(e, r);
                    using var after = SKBitmap.Decode(r.ExportPng([n], bounds));
                    for (var y = 0; y < before.Height; y++)
                        for (var x = 0; x < before.Width; x++)
                            if (before.GetPixel(x, y) != after.GetPixel(x, y)) throw new Exception($"Outline changed pixels at {x},{y}");
                    var contour = n.Children.Single();
                    if (contour.PathWidth < 1 || contour.PathHeight < 1 || r.Geometry(contour).TightBounds.Width > 120 || r.Geometry(contour).TightBounds.Height > 120)
                        throw new Exception("A zero extent distorted native stroke coordinates.");
                    e.Undo(); if (e.Primary!.Kind != NodeKind.Line || vertical && e.Primary.Width != 0 || !vertical && e.Primary.Height != 0)
                        throw new Exception("Undo did not restore the authored line extent.");
                });
    }
}
