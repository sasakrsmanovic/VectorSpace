using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static class OpenArcConversionTests
{
    public static void Register(Action<string, Action> test)
    {
        foreach (var sweep in new[] { 180d, -180d })
        test("open arc conversion cannot activate a previously suppressed fill " + sweep, () =>
        {
            var n = new DesignNode { Kind = NodeKind.Ellipse, Width = 100, Height = 100, Arc = new(-90, sweep, 0, true),
                Fills = [new() { Color = "#FF0000" }, new() { Color = "#00FF00", Opacity = .5 }],
                Strokes = [new() { Color = "#0000FF", Width = 4 }] };
            var e = new EditorSession(new() { Pages = [new() { Nodes = [n] }] }); e.Select(n);
            using var r = new SceneRenderer(); var probe = new Vec2(sweep > 0 ? 80 : 20, 50);
            var source = DocumentJson.Save(e.Document);
            using var before = SKBitmap.Decode(r.ExportPng([n], n.LocalBounds));
            if (before.GetPixel((int)probe.X, (int)probe.Y).Alpha != 0) throw new Exception("The source open arc unexpectedly painted its fill.");
            EditablePathConversion.Convert(e, r, n);
            using var after = SKBitmap.Decode(r.ExportPng([n], n.LocalBounds));
            if (after.GetPixel((int)probe.X, (int)probe.Y).Alpha != 0 || r.HitTest([n], probe, false, 0) is not null)
                throw new Exception("Explicit conversion filled the open arc's implicit chord.");
            if (n.Fills.Count != 2 || n.Fills.Any(f => f.Visible) || n.Fills[0].Color != "#FF0000" || n.Fills[1].Opacity != .5)
                throw new Exception("Suppressed paints must remain available but hidden after conversion.");
            if (n.Arc is not null || n.Closed || !PathEditing.CanEdit(n)) throw new Exception("The converted arc is not an open editable contour.");
            e.Edit("Change latent paint", () => n.Fill = "#FFFF00");
            if (n.Fills[0].Visible || r.HitTest([n], probe, false, 0) is not null) throw new Exception("Updating paint color reactivated a suppressed fill.");
            e.Undo(); e.Undo(); if (DocumentJson.Save(e.Document) != source) throw new Exception("Undo lost the original arc or fill visibility.");
            e.Redo(); var restored = DocumentJson.Load(DocumentJson.Save(e.Document)).Pages[0].Nodes[0];
            if (restored.Fills.Any(f => f.Visible) || restored.Points.Count < 2) throw new Exception("Persistence lost the suppression or editable anchors.");
        });
    }
}
