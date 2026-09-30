using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

internal static class CompoundSubdivisionTests
{
    public static void Register(Action<string, Action> test)
    {
        OpenArcConversionTests.Register(test);
        EditablePathExtentTests.Register(test);
        CoordinateSerializationTests.Register(test);
        test("vector playground contains genuine editable compound studies", () =>
        {
            var document = VectorSample.Create(); DocumentJson.Validate(document);
            if (document.Find("vector-holes")?.Contours?.Count != 3 || document.Find("vector-curves")?.Contours?.Count != 2 || document.Find("vector-endpoints")?.Contours?.Count != 2)
                throw new Exception("The vector sample must contain editable geometry.");
            var loaded = DocumentJson.Load(DocumentJson.Save(document));
            if (loaded.Find("vector-curves")!.Contours![0].Points[1].ControlOut is null) throw new Exception("Cubic handles disappeared.");
        });
        foreach (var t in new[] { .2, .5, .8 })
        test("bulk subdivision matches individual exact subdivision across all contours " + t, () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, Contours = [] };
            for (var c = 0; c < 4; c++) n.Contours.Add(new() { Closed = c % 2 == 0, Points = [
                new() { Position = new(c * 150, 0), ControlOut = new(c * 150 + 20, -40) },
                new() { Position = new(c * 150 + 100, 100), ControlIn = new(c * 150 + 60, 0), ControlOut = new(c * 150 + 120, 130) },
                new() { Position = new(c * 150, 100), ControlOut = new(c * 150 - 20, 50) }] });
            var reference = DocumentJson.CloneNode(n); var segments = new PathTopology(n).Segments.Select(s => s.Start).ToArray();
            foreach (var s in segments.Reverse()) PathEditing.Insert(reference, s, t);
            var inserted = PathEditing.Subdivide(n, segments.Concat(segments), t);
            string Save(DesignNode value) => DocumentJson.Save(new() { Id = "test-document", Pages = [new() { Id = "test-page", Nodes = [value] }] });
            if (Save(n) != Save(reference))
                throw new Exception("Bulk subdivision changed the exact de Casteljau result.");
            if (inserted.Count != segments.Length || !inserted.SequenceEqual(inserted.Order())) throw new Exception("Inserted anchor identities are not in resulting order.");
        });
        test("bulk subdivision rejects any open endpoint before mutating an earlier contour", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, Contours = [
                new() { Closed = true, Points = [new() { Position = new(0,0) }, new() { Position = new(50,0) }, new() { Position = new(50,50) }] },
                new() { Points = [new() { Position = new(100,0) }, new() { Position = new(150,0) }] }] };
            var first = n.Contours[0].Points;
            try { PathEditing.Subdivide(n, [0,4]); } catch (ArgumentOutOfRangeException) { if (!ReferenceEquals(first, n.Contours[0].Points) || first.Count != 3) throw new Exception("Partial mutation"); return; }
            throw new Exception("Accepted an open endpoint as a segment.");
        });
        test("bulk subdivision retains untouched contour anchors and reports global indices", () =>
        {
            PathContour Line(double x) => new() { Points = [new() { Position = new(x,0) },new() { Position = new(x+100,0) }] };
            var untouched = Line(200); var n = new DesignNode { Kind = NodeKind.Path, Contours = [Line(0),untouched,Line(400)] };
            var indices = PathEditing.Subdivide(n, [4,0]);
            if (!indices.SequenceEqual(new[] {1,6}) || !ReferenceEquals(n.Contours![1].Points, untouched.Points)) throw new Exception("Untouched geometry or global indices changed.");
        });
    }
}
