using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

internal static partial class CompoundPathTests
{
    private static void RegisterTopology(Action<string, Action> test)
    {
        test("topology has independent closed seams and no Move-boundary segments", () =>
        {
            var n = Compound(Loop(), Line(new(150, 0), new(180, 20), new(220, 0))); var t = new PathTopology(n);
            Check(t.Points.Count == 7 && t.Contours.Count == 2 && t.Segments.Count == 6);
            Check(t.Next(3) == 0 && t.Next(6) == -1 && t.Previous(4) == -1 && t.Previous(0) == 3);
            Check(t.ContourIndex(3) == 0 && t.ContourIndex(4) == 1 && t.IsEndpoint(4) && !t.IsEndpoint(5));
            Throws<ArgumentOutOfRangeException>(() => t.Curve(6)); Throws<ArgumentOutOfRangeException>(() => t.ContourIndex(7));
        });
        test("retained topology sees position edits and detects structural replacement", () =>
        {
            var n = Compound(Loop(), Loop(150)); var t = new PathTopology(n); n.Contours![1].Points[0].Position = new(151, 0);
            Check(t.Matches(n) && t.Points[4].Position.X == 151); n.Contours[1].Closed = false; Check(!t.Matches(n));
            t = new(n); n.Contours[1].Points = n.Contours[1].Points.ToList(); Check(!t.Matches(n));
            Check(!new PathTopology(n).Matches(DocumentJson.CloneNode(n)));
        });
        foreach (var c in new[] { 0, 1 }) foreach (var fraction in new[] { .2, .5, .85 })
            test($"cubic subdivision respects compound seam {c} at {fraction}", () =>
            {
                var n = Compound(Loop(), Loop(150)); var t = new PathTopology(n); var start = c * 4 + 3;
                t.Points[start].ControlOut = t.Points[start].Position + new Vec2(-20, 10); t.Points[c * 4].ControlIn = t.Points[c * 4].Position + new Vec2(-20, -10);
                var before = t.Curve(start); var inserted = PathEditing.Insert(n, start, fraction); var after = new PathTopology(n);
                Check(inserted == start + 1 && after.Contours[c].Count == 5 && after.Contours[1 - c].Count == 4);
                for (var i = 0; i <= 100; i++) { var u = i / 100d; Near(after.Curve(start).Evaluate(u), before.Evaluate(fraction * u)); Near(after.Curve(inserted).Evaluate(u), before.Evaluate(fraction + (1 - fraction) * u)); }
            });
        test("moving anchors across contours does not move unselected endpoints", () =>
        {
            var n = Compound(Loop(), Loop(150)); PathEditing.Move(n, [0, 5], new(8, -3)); var t = new PathTopology(n);
            Near(t.Points[0].Position, new(8, -3)); Near(t.Points[5].Position, new(258, -3)); Near(t.Points[4].Position, new(150, 0));
        });
        test("tangent generation never borrows neighbors from another contour", () =>
        {
            var n = Compound(Line(new(0, 0), new(100, 0)), Line(new(400, 300), new(500, 300)));
            PathEditing.SetTangents(n, [1, 2], TangentMode.Smooth); var t = new PathTopology(n);
            Check(t.Points[1].ControlOut is null && t.Points[2].ControlIn is null);
            Near(t.Points[1].ControlIn!.Value, new(200d / 3, 0)); Near(t.Points[2].ControlOut!.Value, new(400 + 100d / 3, 300));
        });
        test("deletion validates all affected contours before mutating any", () =>
        {
            var n = Compound(Loop(), Line(new(150, 0), new(200, 0))); var before = DocumentJson.SaveNodes([n]);
            Throws<InvalidOperationException>(() => PathEditing.Delete(n, [0, 4])); Check(DocumentJson.SaveNodes([n]) == before);
            PathEditing.Delete(n, [0]); Check(n.Contours![0].Points.Count == 3 && n.Contours[1].Points.Count == 2);
        });
        test("reverse changes only the requested contour and swaps handles", () =>
        {
            var n = Compound(Loop(), Loop(150)); n.Contours![1].Points[0].ControlOut = new(170, -15);
            var outer = n.Contours[0].Points.Select(p => p.Position).ToArray(); ContourEditing.Reverse(n, [1]);
            Check(n.Contours[0].Points.Select(p => p.Position).SequenceEqual(outer)); Near(n.Contours[1].Points[^1].ControlIn!.Value, new(170, -15));
        });
        test("cutting a closed contour preserves every cubic and changes only its seam", () =>
        {
            var n = Compound(Loop(), Loop(150)); n.Contours![1].Points[2].ControlOut = new(200, 130);
            var old = new PathTopology(n); var curves = Enumerable.Range(4, 4).Select(old.Curve).ToArray();
            ContourEditing.Cut(n, 6); var t = new PathTopology(n);
            Check(t.Contours[0].Closed && !t.Contours[1].Closed && t.Contours[1].Count == 5);
            for (var i = 0; i < 4; i++) Check(t.Curve(4 + i) == curves[(2 + i) % 4]);
            Check(t.Points[4].ControlIn is null && t.Points[8].ControlOut is null);
        });
        test("cutting an open interior anchor promotes single path into two independent contours", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, Points = Line(new(0, 0), new(50, 30), new(100, 0)).Points };
            n.Points[0].ControlOut = new(10, 30); n.Points[1].ControlIn = new(20, 40); n.Points[1].ControlOut = new(70, 40);
            var first = PathEditing.Segment(n, 0); var second = PathEditing.Segment(n, 1);
            ContourEditing.Cut(n, 1); var t = new PathTopology(n);
            Check(n.Points.Count == 0 && t.Contours.Count == 2 && t.Points.Count == 4 && t.Next(1) == -1);
            Check(t.Curve(0) == first && t.Curve(2) == second && !ReferenceEquals(t.Points[1], t.Points[2]));
        });
        foreach (var firstStart in new[] { false, true }) foreach (var secondEnd in new[] { false, true })
            test($"join orients endpoints without moving anchors {firstStart}/{secondEnd}", () =>
            {
                var n = Compound(Line(new(0, 0), new(50, 10)), Line(new(150, 0), new(200, 10)), Loop(250, 0, 20));
                var a = firstStart ? 0 : 1; var b = secondEnd ? 3 : 2; var t = new PathTopology(n); var pa = t.Points[a].Position; var pb = t.Points[b].Position;
                ContourEditing.Join(n, a, b); t = new(n); Check(t.Contours.Count == 2 && t.Contours[0].Count == 4 && t.Contours[1].Closed);
                Near(t.Points[1].Position, pa); Near(t.Points[2].Position, pb); Check(t.Points[1].ControlOut is null && t.Points[2].ControlIn is null);
            });
        test("joining one contour endpoints creates a straight seam and keeps layer identity", () =>
        {
            var n = Compound(Line(new(0, 0), new(50, 40), new(100, 0))); var id = n.Id; ContourEditing.Join(n, 0, 2);
            Check(n.Id == id && n.Contours is null && n.Closed && n.Points.Count == 3); Near(PathEditing.Segment(n, 2).Evaluate(.5), new(50, 0));
        });
        test("invalid cut join and removal are atomic", () =>
        {
            var n = Compound(Line(new(0, 0), new(50, 30), new(100, 0)), Loop(150)); var before = DocumentJson.SaveNodes([n]);
            Throws<InvalidOperationException>(() => ContourEditing.Join(n, 1, 3)); Throws<InvalidOperationException>(() => ContourEditing.Cut(n, 0));
            Throws<ArgumentOutOfRangeException>(() => ContourEditing.Remove(n, 8)); Check(DocumentJson.SaveNodes([n]) == before);
        });
        test("removing a contour preserves the other contour references and final contour cannot disappear", () =>
        {
            var n = Compound(Loop(), Loop(150)); var other = n.Contours![0].Points; ContourEditing.Remove(n, 1);
            Check(n.Contours is null && ReferenceEquals(n.Points, other) && n.Closed);
            Throws<InvalidOperationException>(() => ContourEditing.Remove(n, 0));
        });
        test("compound simplification respects endpoints and leaves curved inputs unchanged", () =>
        {
            var n = Compound(Line(new(0, 0), new(50, 0), new(100, 0)), Line(new(150, 20), new(200, 20), new(250, 20)));
            Check(PathEditing.Simplify(n, 1) == 2 && n.Contours!.All(c => c.Points.Count == 2));
            n.Contours![1].Points[0].ControlOut = new(170, 50); var before = DocumentJson.SaveNodes([n]);
            Throws<InvalidOperationException>(() => PathEditing.Simplify(n, 2)); Check(DocumentJson.SaveNodes([n]) == before);
        });
        test("closure validates all selected contours before updating any", () =>
        {
            var n = Compound(Line(new(0, 0), new(50, 0), new(100, 0)), Line(new(150, 0), new(200, 0)));
            Throws<InvalidOperationException>(() => ContourEditing.SetClosed(n, [0, 1], true)); Check(n.Contours!.All(c => !c.Closed));
        });
    }
}
