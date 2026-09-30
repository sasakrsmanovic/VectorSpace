using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static partial class CompoundPathTests
{
    private static void RegisterWorkflows(Action<string, Action> test)
    {
        foreach (var rotation in new[] { 0d, 35d }) foreach (var flip in new[] { false, true })
            test($"sparse captured compound dragging is baseline-relative under transform {rotation}/{flip}", () =>
            {
                var n = Compound(Loop(), Loop(150)); n.Rotation = rotation; n.FlipX = flip; n.Width = 600; n.Height = 100;
                var parent = new DesignNode { Kind = NodeKind.Group, X = 37, Y = -12, Rotation = 27 }; parent.Add(n);
                var t = new PathTopology(n); var matrix = PathEditing.PointToWorld(n); var a = matrix.Map(t.Points[0].Position); var b = matrix.Map(t.Points[5].Position);
                var drag = new PathPointDrag(n, t, [0, 5]); Check(drag.CapturedAnchorCount == 2);
                for (var i = 1; i <= 20; i++) drag.Apply(new(i, -i / 2d));
                Near(matrix.Map(t.Points[0].Position), a + new Vec2(20, -10)); Near(matrix.Map(t.Points[5].Position), b + new Vec2(20, -10));
                Near(t.Points[1].Position, new(100, 0)); drag.Apply(Vec2.Zero); Near(matrix.Map(t.Points[0].Position), a);
            });
        test("sparse handle capture preserves independence and repeated samples", () =>
        {
            var n = Compound(Loop(), Loop(150)); var t = new PathTopology(n); var p = t.Points[5]; p.ControlIn = new(240, 0); p.ControlOut = new(260, 0);
            var drag = new PathPointDrag(n, t, [5], 1); drag.Apply(new(0, 10), independentHandle: true); Near(p.ControlIn!.Value, new(240, 0)); Near(p.ControlOut!.Value, new(260, 10));
            drag.Apply(new(0, 10), independentHandle: true); Near(p.ControlOut!.Value, new(260, 10));
        });
        test("sparse drag Apply allocates no managed objects on a large compound path", () =>
        {
            var n = Compound(Enumerable.Range(0, 2500).Select(i => Loop(i * 110d)).ToArray()); var t = new PathTopology(n); var drag = new PathPointDrag(n, t, [0, 9999]);
            for (var i = 0; i < 500; i++) drag.Apply(new(i % 7, 3)); var bytes = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) drag.Apply(new(i % 7, 3));
            Check(GC.GetAllocatedBytesForCurrentThread() - bytes == 0 && drag.CapturedAnchorCount == 2);
        });
        test("compound operations preserve undo redo document identity and paints", () =>
        {
            var n = Compound(Loop(), Loop(25, 25, 50, true)); var e = Editor(n); var before = DocumentJson.Save(e.Document); var id = n.Id;
            e.Edit("Cut", () => ContourEditing.Cut(n, 4)); Check(e.Primary!.Id == id && n.Contours![1].Points.Count == 5);
            e.Undo(); Check(DocumentJson.Save(e.Document) == before); e.Redo(); Check(e.Primary!.Contours![1].Points.Count == 5);
            var cut = DocumentJson.Save(e.Document); e.BeginInteraction("Move"); var active = e.Primary!;
            new PathPointDrag(active, new(active), [0, 4]).Apply(new(20, 12)); e.Preview(false); e.CancelInteraction();
            Check(DocumentJson.Save(e.Document) == cut);
        });
        test("compound geometry follows component synchronization without changing instance child IDs", () =>
        {
            var definition = new DesignNode { Kind = NodeKind.Component, Width = 300, Height = 200 }; var n = Compound(Loop(), Loop(150)); definition.Add(n);
            var e = Editor(definition); var instance = ComponentService.InsertInstance(e, definition, new(400, 0)); var id = instance.Children[0].Id;
            e.Edit("Edit main contour", () => PathEditing.Move(n, [4], new(10, 8)));
            var child = e.Document.Find(id)!; Check(child.Contours?.Count == 2); Near(child.Contours![1].Points[0].Position, new(160, 8));
            e.Undo(); Near(e.Document.Find(id)!.Contours![1].Points[0].Position, new(150, 0));
        });
        test("compound clipboard clones deeply and preserves fill rule", () =>
        {
            var n = Compound(Loop(), Loop(25, 25, 50)); n.FillRule = PathFillRule.EvenOdd;
            var clone = DocumentJson.CloneNode(n, true); Check(clone.Id != n.Id && clone.FillRule == n.FillRule);
            clone.Contours![0].Points[0].Position = new(7, 8); Check(n.Contours![0].Points[0].Position == Vec2.Zero);
            var e = Editor(n); e.Paste(e.CopySelection(), true); Check(e.Page.Nodes.Count == 2 && e.Primary!.Contours?.Count == 2);
        });
        test("conversion of an invalid singleton contour rejects before overwriting the source", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, PathData = "M0 0 L100 0 M200 100" }; var e = Editor(n); var before = DocumentJson.Save(e.Document); using var r = new SceneRenderer();
            Throws<InvalidOperationException>(() => EditablePathConversion.Convert(e, r, n)); Check(DocumentJson.Save(e.Document) == before && !e.CanUndo);
        });
    }
    private static void RegisterValidation(Action<string, Action> test)
    {
        foreach (var version in Enumerable.Range(1, 6))
            test("legacy native schema migrates to editable-contour schema from " + version, () =>
            {
                var n = new DesignNode { Kind = NodeKind.Path, Points = Line(new(0, 0), new(100, 20)).Points }; var d = Document(n); d.FormatVersion = version;
                var read = DocumentJson.Load(DocumentJson.Save(d)); Check(read.FormatVersion == 7 && read.Pages[0].Nodes[0].Contours is null);
            });
        test("schema seven compound native round trip preserves closure and winding", () =>
        {
            var n = Compound(Loop(), Line(new(150, 0), new(200, 20))); n.FillRule = PathFillRule.EvenOdd;
            var doc = DocumentJson.Load(DocumentJson.Save(Document(n))); var read = doc.Pages[0].Nodes[0];
            Check(read.Contours?.Count == 2 && read.Contours[0].Closed && !read.Contours[1].Closed && read.FillRule == PathFillRule.EvenOdd);
        });
        test("compound paths cannot masquerade as older schema", () => { var d = Document(Compound(Loop(), Loop(150))); d.FormatVersion = 6; Throws<InvalidDataException>(() => DocumentJson.Validate(d)); });
        foreach (var mode in new[] { "points", "commands", "svg", "kind", "null-contour", "null-points", "null-anchor", "singleton", "empty", "coordinate" })
            test("invalid compound representation rejected: " + mode, () =>
            {
                var n = Compound(Loop(), Loop(150));
                switch (mode)
                {
                    case "points": n.Points.Add(new()); break;
                    case "commands": n.Commands = []; break;
                    case "svg": n.PathData = "M0 0L1 1"; break;
                    case "kind": n.Kind = NodeKind.Rectangle; break;
                    case "null-contour": n.Contours![0] = null!; break;
                    case "null-points": n.Contours![0].Points = null!; break;
                    case "null-anchor": n.Contours![0].Points[0] = null!; break;
                    case "singleton": n.Contours![0].Points = [new()]; break;
                    case "empty": n.Contours = []; break;
                    case "coordinate": n.Contours![0].Points[0].Position = new(double.NaN, 0); break;
                }
                Throws<InvalidDataException>(() => DocumentJson.Validate(Document(n)));
            });
        test("aggregate anchor limit applies across all contours", () =>
        {
            var points = Enumerable.Range(0, 50_001).Select(i => new PathPoint { Position = new(i, 0) }).ToList();
            var n = Compound(new() { Points = points }, new() { Points = points }); Throws<InvalidDataException>(() => DocumentJson.Validate(Document(n)));
        });
        test("contour count limit rejects excessive disconnected paths", () =>
        {
            var contour = Line(new(0, 0), new(1, 1)); var n = Compound(Enumerable.Repeat(contour, 10_001).ToArray());
            Throws<InvalidDataException>(() => DocumentJson.Validate(Document(n)));
        });
        test("bounded conic approximation rejects invalid weight and tiny work budget", () =>
        {
            Throws<ArgumentException>(() => ConicApproximation.AppendCubics(Vec2.Zero, new(1, 1), new(2, 0), 0, _ => { }));
            Throws<InvalidOperationException>(() => ConicApproximation.AppendCubics(Vec2.Zero, new(100, 100), new(200, 0), .3, _ => { }, maximumSegments: 1));
        });
    }
}
