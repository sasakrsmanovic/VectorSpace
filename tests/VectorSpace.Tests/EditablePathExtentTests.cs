using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static class EditablePathExtentTests
{
    public static void Register(Action<string, Action> test)
    {
        foreach (var kind in new[] { NodeKind.Line, NodeKind.Arrow })
        foreach (var extent in new[] { new Vec2(100, 0), new Vec2(0, 100), new Vec2(100, .25), new Vec2(.25, 100) })
        foreach (var rotation in new[] { 0d, 27d })
        {
            test($"editable conversion preserves {kind} {extent.X}x{extent.Y} at {rotation} degrees", () =>
            {
                var parent = new DesignNode { Kind = NodeKind.Group, X = 50, Y = 70, Width = 180, Height = 160, Rotation = 13, FlipX = true, Fills = [] };
                var n = parent.Add(new() { Kind = kind, X = 35, Y = 30, Width = extent.X, Height = extent.Y,
                    Rotation = rotation, FlipX = kind == NodeKind.Arrow, FlipY = extent.X < 1, Fills = [], Strokes = [new() { Color = "#2266AA", Width = 6, Cap = StrokeCap.Round }] });
                var editor = new EditorSession(new() { Pages = [new() { Nodes = [parent] }] }); editor.Select(n);
                var source = DocumentJson.Save(editor.Document); var matrix = n.WorldMatrix;
                using var renderer = new SceneRenderer(); var bounds = new RectD(-50, -50, 400, 400);
                var before = renderer.ExportPng([parent], bounds);
                EditablePathConversion.Convert(editor, renderer, n);
                if (n.PathWidth <= 0 || n.PathHeight <= 0) throw new Exception("Editable geometry needs a nonzero scaling basis.");
                foreach (var p in new[] { Vec2.Zero, extent, new Vec2(20, 5) })
                    if (matrix.Map(p).DistanceTo(n.WorldMatrix.Map(p)) > 1e-9) throw new Exception("Conversion moved the layer's authored local coordinates.");
                using var a = SKBitmap.Decode(before); using var b = SKBitmap.Decode(renderer.ExportPng([parent], bounds));
                for (var y = 0; y < a.Height; y++) for (var x = 0; x < a.Width; x++)
                    if (a.GetPixel(x, y) != b.GetPixel(x, y)) throw new Exception($"Conversion changed pixels at {x},{y}.");
                if (n.Rotation != rotation || n.FlipX != (kind == NodeKind.Arrow) || n.FlipY != (extent.X < 1))
                    throw new Exception("Conversion rewrote the authored rotation/reflection fields.");
                var topology = new PathTopology(n);
                if (topology.Contours.Count != (kind == NodeKind.Arrow ? 2 : 1)) throw new Exception("Conversion lost a contour.");
                var end = topology.Points[1].Position;
                if (end.DistanceTo(extent) > 1e-6) throw new Exception("Conversion scaled the original line endpoints.");
                DocumentJson.Load(DocumentJson.Save(editor.Document));
                editor.Edit("Resize editable path", () => { n.Width *= 2; n.Height *= 2; });
                using var scaled = new SKPath(renderer.Geometry(n));
                if ((new Vec2(scaled.Points[1].X, scaled.Points[1].Y) - end * 2).Length > 1e-5)
                    throw new Exception("Converted zero-extent geometry stopped participating in path resizing.");
                editor.Undo(); editor.Undo();
                if (DocumentJson.Save(editor.Document) != source) throw new Exception("Undo did not restore the original line dimensions and representation.");
                editor.Redo();
                if (editor.Primary?.Kind != NodeKind.Path || editor.Primary.PathWidth <= 0 || editor.Primary.PathHeight <= 0)
                    throw new Exception("Redo lost the editable scaling basis.");
            });
        }
    }
}
