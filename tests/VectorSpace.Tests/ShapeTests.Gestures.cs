using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Skia;

internal static partial class ShapeTests
{
    private static void RegisterGestures(Action<string, Action> test)
    {
        test("corner drag samples use a stable baseline", () => {
            var n = Node(); n.CornerRadius = 10; var gesture = new ShapeGesture(n, 0, n.WorldMatrix.Map(new Vec2(10, 10)));
            var point = n.WorldMatrix.Map(new Vec2(30, 30)); for (var i = 0; i < 100; i++) gesture.Apply(n, point);
            Near(n.CornerRadius, 30); Check(n.Corners is null && n.X == 0 && n.Y == 0);
        });
        test("Alt corner dragging preserves the other corners", () => {
            var n = Node(); n.Corners = new(5, 10, 15, 20); var gesture = new ShapeGesture(n, 1, n.WorldMatrix.Map(new Vec2(90, 10)));
            gesture.Apply(n, n.WorldMatrix.Map(new Vec2(75, 25)), true); Check(n.Corners == new CornerRadii(5, 25, 15, 20));
        });
        foreach (var flip in new[] { false, true })
            test("shape drag respects a rotated ancestor with reflection " + flip, () => {
                var parent = new DesignNode { X = 200, Y = 300, Width = 300, Height = 250, Rotation = 33, FlipX = flip };
                var n = parent.Add(Node()); n.X = 20; n.Y = 40; n.Rotation = -18; n.CornerRadius = 10;
                var gesture = new ShapeGesture(n, 2, n.WorldMatrix.Map(new Vec2(90, 70)));
                gesture.Apply(n, n.WorldMatrix.Map(new Vec2(70, 50))); Near(n.CornerRadius, 30); Check(n.X == 20 && n.Y == 40);
            });
        test("corner gesture clamps at half the shorter dimension", () => {
            var n = Node(); var gesture = new ShapeGesture(n, 0, Vec2.Zero); gesture.Apply(n, new(1000, 1000)); Near(n.CornerRadius, 40);
            gesture.Apply(n, new(-100, -100)); Near(n.CornerRadius, 0);
        });
        test("arc end drag unwraps the angular discontinuity", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(140, 30);
            var gesture = new ShapeGesture(n, 1, n.WorldMatrix.Map(ShapeGeometry.ArcPoint(n, 170)));
            gesture.Apply(n, n.WorldMatrix.Map(ShapeGeometry.ArcPoint(n, -170))); Near(n.Arc.SweepDegrees, 50);
            gesture.Apply(n, n.WorldMatrix.Map(ShapeGeometry.ArcPoint(n, -160))); Near(n.Arc.SweepDegrees, 60);
        });
        test("arc start dragging preserves the end angle", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(10, 90);
            var gesture = new ShapeGesture(n, 0, n.WorldMatrix.Map(ShapeGeometry.ArcPoint(n, 10)));
            gesture.Apply(n, n.WorldMatrix.Map(ShapeGeometry.ArcPoint(n, 40))); Near(n.Arc.StartDegrees, 40); Near(n.Arc.StartDegrees + n.Arc.SweepDegrees, 100);
        });
        test("Shift arc gesture snaps changes to fifteen degrees", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(0, 90);
            var gesture = new ShapeGesture(n, 1, n.WorldMatrix.Map(ShapeGeometry.ArcPoint(n, 90)));
            gesture.Apply(n, n.WorldMatrix.Map(ShapeGeometry.ArcPoint(n, 113)), constrainAngle: true); Near(n.Arc.SweepDegrees, 120);
        });
        test("ellipse inner-radius dragging uses normalized axes", () => {
            var n = new DesignNode { Kind = NodeKind.Ellipse, Width = 200, Height = 100, Rotation = 27, Arc = new(0, 270, .2) };
            var gesture = new ShapeGesture(n, 2, n.WorldMatrix.Map(new Vec2(120, 50)));
            gesture.Apply(n, n.WorldMatrix.Map(new Vec2(150, 50))); Near(n.Arc.InnerRadius, .5);
            gesture.Apply(n, n.WorldMatrix.Map(new Vec2(100, 75))); Near(n.Arc.InnerRadius, .5);
        });
        test("shape transactions undo restore and cancel without layer motion", () => {
            var n = Node(); n.CornerRadius = 4; var e = Editor(n); e.Select(n); var gesture = new ShapeGesture(n, 0, new(10, 10));
            e.BeginInteraction("Corners"); gesture.Apply(n, new(25, 25)); e.Preview(false); e.CommitInteraction(); Near(e.Primary!.CornerRadius, 19);
            e.Undo(); Near(e.Primary!.CornerRadius, 4); e.Redo(); Near(e.Primary!.CornerRadius, 19);
            e.BeginInteraction("Cancel corners"); e.Primary!.Corners = new(30, 20, 10, 0); e.Preview(false); e.CancelInteraction(); Check(e.Primary!.Corners is null && e.Primary.X == 0);
        });
        test("corner SVG preserves asymmetric geometry and native path precision", () => {
            var n = Node(); n.Corners = new(11.123456789, 7, 25, 0); var svg = VectorPath.Build(n); Check(svg.Contains("11.123456789"));
            using var parsed = SKPath.ParseSvgPathData(svg); using var native = NativeShapeGeometry.Build(n);
            var random = new Random(392); for (var i = 0; i < 200; i++) { var x = (float)(random.NextDouble() * 100); var y = (float)(random.NextDouble() * 80); Check(parsed.Contains(x, y) == native.Contains(x, y)); }
        });
        test("native conics have bounded accurate SVG export without quantizing the document", () => {
            var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = new(23, 267, .35); using var original = NativeShapeGeometry.Build(n);
            var commands = NativeShapeGeometry.Capture(original); var saved = commands.ToArray(); var svg = ShapePathSvg.Commands(commands);
            using var parsed = SKPath.ParseSvgPathData(svg); using var before = new SKPathMeasure(original, false, 1000); using var after = new SKPathMeasure(parsed, false, 1000);
            Near(before.Length, after.Length, .03); Check(commands.SequenceEqual(saved) && svg.Contains('C'));
            // Compare equal high-resolution measurements; the default coarse estimator
            // samples a long native conic differently from many exported cubic segments.
            for (var i = 0; i <= 1000; i++)
            {
                var a = before.GetPosition(before.Length * i / 1000); var b = after.GetPosition(after.Length * i / 1000);
                Check(new Vec2(a.X - b.X, a.Y - b.Y).Length < .01, "Exported conic deviated at arc-length sample " + i);
            }
        });
    }
    private static void RegisterValidation(Action<string, Action> test)
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1, 1e7 })
            test("invalid corner radius rejected " + invalid, () => {
                var n = Node(); n.Corners = new(invalid, 0, 0, 0); Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
            });
        foreach (var arc in new[] { new EllipseArc(double.NaN), new EllipseArc(0, 361), new EllipseArc(0, -361), new EllipseArc(0, 90, -1), new EllipseArc(0, 90, 2) })
            test("invalid ellipse arc rejected " + arc, () => {
                var n = Node(); n.Kind = NodeKind.Ellipse; n.Arc = arc; Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
            });
        test("invalid stroke enum and miter rejected through native document validation", () => {
            var n = Node(); n.Strokes = [new() { Alignment = (StrokeAlignment)99 }]; Throws<InvalidDataException>(() => Editor(n));
            n.Strokes[0].Alignment = StrokeAlignment.Center; n.Strokes[0].MiterLimit = double.NaN; Throws<InvalidDataException>(() => Editor(n));
        });
        test("native commands cannot silently replace another path representation", () => {
            var n = new DesignNode { Kind = NodeKind.Path, PathData = "M0 0L100 100", Commands = [new(PathVerb.Move), new(PathVerb.Line, new(10, 10))] };
            Throws<InvalidDataException>(() => ShapeValidation.Validate(n)); n.PathData = null; n.Points.Add(new()); Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
        });
        test("invalid native command sequences and conic weights are rejected", () => {
            var n = new DesignNode { Kind = NodeKind.Path, Commands = [new(PathVerb.Line, new(10, 10))] }; Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
            n.Commands = [new(PathVerb.Move), new(PathVerb.Conic, new(10, 10), new(4, 5), default, 0)]; Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
            n.Commands = [new(PathVerb.Move), new(PathVerb.Line, new(double.PositiveInfinity, 0))]; Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
        });
        test("Boolean operands cannot include an ordinary text or container layer", () => {
            var n = new DesignNode { Kind = NodeKind.Group, Boolean = BooleanKind.Union, Children = [new() { Kind = NodeKind.Text }] }; Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
            n.Children = [new() { Kind = NodeKind.Frame }]; Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
        });
        test("Boolean groups cannot silently receive auto-layout", () => {
            var n = new DesignNode { Kind = NodeKind.Group, Boolean = BooleanKind.Union }; n.Layout.Direction = LayoutDirection.Horizontal;
            Throws<InvalidDataException>(() => ShapeValidation.Validate(n));
        });
        test("schema-five documents retain legacy round centered stroke defaults", () => {
            var d = DocumentJson.Load("{\"formatVersion\":5,\"pages\":[{\"nodes\":[{\"strokes\":[{\"width\":3}]}]}]}"); var n = d.Pages[0].Nodes[0];
            Check(d.FormatVersion == 6 && n.Corners is null && n.Arc is null && n.Commands is null && n.Strokes[0].Alignment == StrokeAlignment.Center && n.Strokes[0].Cap == StrokeCap.Round && n.Strokes[0].Join == StrokeJoin.Round);
        });
    }
}
