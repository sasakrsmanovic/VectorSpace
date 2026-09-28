using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Layout;
using VectorSpace.Skia;

internal static class ToolEditingTests
{
    private static void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Equal(double actual, double expected, double epsilon = 1e-6) => Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= epsilon, $"Expected {expected}, got {actual}.");
    private static void Equal(Vec2 actual, Vec2 expected, double epsilon = 1e-6) { Equal(actual.X, expected.X, epsilon); Equal(actual.Y, expected.Y, epsilon); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static DesignNode Path(bool curved = false, bool closed = false) => new()
    {
        Kind = NodeKind.Path, Width = 200, Height = 100, PathWidth = 200, PathHeight = 100, Closed = closed,
        Points = [new() { Position = new(0, 0), ControlOut = curved ? new Vec2(40, 80) : null },
            new() { Position = new(100, 20), ControlIn = curved ? new Vec2(70, -30) : null },
            new() { Position = new(200, 100) }]
    };
    private static EditorSession Editor(params DesignNode[] nodes) => new(new() { Pages = [new() { Nodes = [.. nodes] }] });

    public static void Register(Action<string, Action> test)
    {
        foreach (var t in new[] { .01, .2, .5, .9, .99 })
        {
            test("de Casteljau insertion preserves both cubic portions at " + t, () =>
            {
                var node = Path(true); var original = PathEditing.Segment(node, 0);
                var inserted = PathEditing.Insert(node, 0, t); Check(inserted == 1 && node.Points.Count == 4);
                var left = PathEditing.Segment(node, 0); var right = PathEditing.Segment(node, 1);
                for (var i = 0; i <= 100; i++)
                {
                    var s = i / 100d; Equal(left.Evaluate(s), original.Evaluate(s * t));
                    Equal(right.Evaluate(s), original.Evaluate(t + s * (1 - t)));
                }
            });
        }
        test("closed closing-segment insertion retains topology", () =>
        {
            var n = Path(false, true); var original = PathEditing.Segment(n, 2);
            Check(PathEditing.Insert(n, 2, .5) == 3 && n.Closed); Equal(n.Points[^1].Position, original.Evaluate(.5));
        });
        test("straight segment insertion uses linear distance without artificial tangents", () =>
        {
            var n = Path(); PathEditing.Insert(n, 0, .25); Equal(n.Points[1].Position, new(25, 5));
            Check(n.Points.All(p => p.ControlIn is null && p.ControlOut is null));
        });
        test("invalid insertion is atomic", () =>
        {
            var n = Path(true); var before = DocumentJson.SaveNodes([n]);
            foreach (var t in new[] { 0, 1, double.NaN, double.PositiveInfinity }) Throws<ArgumentOutOfRangeException>(() => PathEditing.Insert(n, 0, t));
            Throws<ArgumentOutOfRangeException>(() => PathEditing.Insert(n, 2, .5)); Check(DocumentJson.SaveNodes([n]) == before);
        });
        test("SVG path construction retains subpixel precision", () =>
        {
            var node = Path(); node.Points[0].Position = new(.000125, .123456789);
            var data = VectorPath.Build(node); Check(data.Contains("0.000125") && data.Contains("0.123456789"));
        });
        test("point movement translates tangents exactly once and deduplicates indices", () =>
        {
            var n = Path(true); var origin = n.Points[0].ControlOut!.Value;
            PathEditing.Move(n, [0, 0, 1], new(7, -3)); Equal(n.Points[0].Position, new(7, -3)); Equal(n.Points[0].ControlOut!.Value, origin + new Vec2(7, -3));
            Equal(n.Points[2].Position, new(200, 100));
        });
        test("invalid point selection never partially mutates", () =>
        {
            var n = Path(); var before = DocumentJson.SaveNodes([n]);
            Throws<ArgumentOutOfRangeException>(() => PathEditing.Move(n, [0, 99], new(5, 5))); Check(DocumentJson.SaveNodes([n]) == before);
        });
        test("delete anchors enforces viable contour and removes dead endpoint handles", () =>
        {
            var n = Path(true); PathEditing.Delete(n, [1]); Check(n.Points.Count == 2 && n.Points[0].ControlIn is null && n.Points[^1].ControlOut is null);
            var before = DocumentJson.SaveNodes([n]); Throws<InvalidOperationException>(() => PathEditing.Delete(n, [0])); Check(DocumentJson.SaveNodes([n]) == before);
        });
        test("deleting to two anchors opens a closed contour", () => { var n = Path(false, true); PathEditing.Delete(n, [1]); Check(!n.Closed); });
        test("reversing twice preserves exact anchor and tangent data", () =>
        {
            var n = Path(true, true); var before = DocumentJson.SaveNodes([n]); PathEditing.Reverse(n); PathEditing.Reverse(n); Check(DocumentJson.SaveNodes([n]) == before);
        });
        test("reverse direction preserves sampled cubic locus", () =>
        {
            var n = Path(true); var original = PathEditing.Segment(n, 0); PathEditing.Reverse(n); var reversed = PathEditing.Segment(n, 1);
            for (var i = 0; i <= 100; i++) Equal(original.Evaluate(i / 100d), reversed.Evaluate(1 - i / 100d));
        });
        foreach (var mode in Enum.GetValues<TangentMode>())
            test("point tangent mode " + mode, () =>
            {
                var n = Path(true); PathEditing.SetTangents(n, [1], mode); var p = n.Points[1];
                if (mode == TangentMode.Corner) { Check(p.ControlIn is null && p.ControlOut is null); return; }
                var a = p.ControlIn!.Value - p.Position; var b = p.ControlOut!.Value - p.Position;
                Equal(a.X * b.Y - a.Y * b.X, 0); Check(a.X * b.X + a.Y * b.Y < 0);
                if (mode == TangentMode.Mirrored) Equal(a.Length, b.Length);
            });
        test("open-end smoothing never creates a phantom segment", () =>
        {
            var n = Path(); PathEditing.SetTangents(n, [0, 2], TangentMode.Smooth); Check(n.Points[0].ControlIn is null && n.Points[^1].ControlOut is null);
        });
        test("linked handle retains opposite length; Alt-like independent motion does not move it", () =>
        {
            var p = new PathPoint { Position = new(10, 10), ControlIn = new(0, 10), ControlOut = new(30, 10) };
            PathEditing.MoveHandle(p, false, new(10, 40), false); Equal(p.ControlIn!.Value, new(10, 0));
            PathEditing.MoveHandle(p, false, new(20, 40), true); Equal(p.ControlIn!.Value, new(10, 0));
            PathEditing.MoveHandle(p, false, new(20, 40), false, true); Equal(p.ControlIn!.Value, new(0, -20));
        });
        test("simplification preserves endpoints and reduces a collinear freehand stroke", () =>
        {
            var n = Path(); n.Points = Enumerable.Range(0, 2000).Select(i => new PathPoint { Position = new(i, i * 2) }).ToList();
            Check(PathEditing.Simplify(n, .5) == 1998 && n.Points.Count == 2); Equal(n.Points[^1].Position, new(1999, 3998));
        });
        test("simplification retains a real corner", () =>
        {
            var n = Path(); n.Points = [new() { Position = new(0, 0) }, new() { Position = new(10, 0) }, new() { Position = new(20, 0) }, new() { Position = new(20, 10) }, new() { Position = new(20, 20) }];
            Check(PathEditing.Simplify(n, .1) == 2); Equal(n.Points[1].Position, new(20, 0));
        });
        test("simplification validates curve semantics and tolerance", () =>
        {
            Throws<InvalidOperationException>(() => PathEditing.Simplify(Path(true), 1));
            Throws<InvalidOperationException>(() => PathEditing.Simplify(Path(false, true), 1));
            Throws<ArgumentOutOfRangeException>(() => PathEditing.Simplify(Path(), double.NaN));
            var n = Path(); Check(PathEditing.Simplify(n, 0) == 0 && n.Points.Count == 3);
        });
        test("closest segment recovers a known curve parameter", () =>
        {
            var curve = PathEditing.Segment(Path(true), 0);
            foreach (var t in new[] { .1, .4, .6, .9 }) { var closest = curve.Closest(curve.Evaluate(t)); Equal(closest.T, t, 1e-5); Check(closest.Distance < .001); }
        });
        test("closest degenerate cubic is finite", () =>
        {
            var curve = new CubicSegment(new(7, 3), new(7, 3), new(7, 3), new(7, 3)); var hit = curve.Closest(new(10, 7)); Equal(hit.Distance, 5);
        });
        foreach (var angle in new[] { 0d, 27, 90, 185 })
        foreach (var flipped in new[] { false, true })
            test($"point world transform round trip angle {angle} reflected {flipped}", () =>
            {
                var parent = new DesignNode { X = 70, Y = 90, Rotation = angle, FlipX = flipped };
                var n = parent.Add(Path()); n.Width = 400; n.Height = 50; n.Rotation = 31;
                var transform = PathEditing.PointToWorld(n); var point = new Vec2(70, 20); Equal(transform.Inverse.Map(transform.Map(point)), point);
                var worldDelta = new Vec2(1, -7); var local = transform.Inverse.Map(worldDelta) - transform.Inverse.Map(Vec2.Zero);
                Equal(transform.Map(point + local) - transform.Map(point), worldDelta);
            });
        foreach (var kind in new[] { NodeKind.Rectangle, NodeKind.Ellipse, NodeKind.Frame, NodeKind.Section, NodeKind.Polygon, NodeKind.Star, NodeKind.Slice, NodeKind.Text })
            test("drawing constraints cover tool " + kind, () =>
            {
                var n = new DesignNode { Kind = kind }; DrawingGeometry.Apply(n, new(100, 100), new(70, 120), true, true);
                Equal(n.Width, 60); Equal(n.Height, 60); Equal(n.Bounds.Center, new(100, 100));
                DrawingGeometry.Apply(n, new(100, 100), new(130, 100), true, false); Equal(n.Width, 30); Equal(n.Height, 30);
            });
        foreach (var end in new[] { new Vec2(140, 0), new Vec2(0, 120), new Vec2(-180, 0), new Vec2(0, -160), new Vec2(-120, -90) })
            test("line endpoint fidelity " + end, () =>
            {
                var n = new DesignNode { Kind = NodeKind.Line }; DrawingGeometry.Apply(n, Vec2.Zero, end, false, false);
                using var r = new SceneRenderer(); using var it = r.Geometry(n).CreateRawIterator(); var p = new SKPoint[4];
                Check(it.Next(p) == SKPathVerb.Move); Equal(n.WorldMatrix.Map(new Vec2(p[0].X, p[0].Y)), Vec2.Zero, .002);
                Check(it.Next(p) == SKPathVerb.Line); Equal(n.WorldMatrix.Map(new Vec2(p[1].X, p[1].Y)), end, .002);
            });
        test("line Shift locks 45-degree angle rather than forcing a square", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Line }; DrawingGeometry.Apply(n, new(50, 50), new(160, 55), true, false);
            using var path = SKPath.ParseSvgPathData(n.PathData); Equal(path.TightBounds.Height, 0); Equal(path.TightBounds.Width, Math.Sqrt(110 * 110 + 25), .002);
        });
        test("canonical line geometry resizes and exports at its new extent", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Line }; DrawingGeometry.Apply(n, Vec2.Zero, new(100, 0), false, false); n.Width = 200;
            using var r = new SceneRenderer(); Equal(r.Geometry(n).TightBounds.Width, 200); Check(SvgFormat.Export([n], n.LocalBounds).Contains("scale(2 1)"));
        });
        test("arrow keeps endpoint and two head branches", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Arrow }; DrawingGeometry.Apply(n, new(50, 50), new(-50, 50), false, true);
            Check(n.PathData!.Count(c => c == 'M') == 2 && n.PathData.Count(c => c == 'L') == 3); Equal(n.Bounds.Center.X, 50);
        });
        foreach (var kind in new[] { NodeKind.Rectangle, NodeKind.Ellipse, NodeKind.Line, NodeKind.Polygon, NodeKind.Star })
            test("convert primitive to undoable editable points " + kind, () =>
            {
                var n = new DesignNode { Kind = kind, X = 30, Y = 50, Rotation = 25, Width = 200, Height = 120 }; var e = Editor(n); e.Select(n);
                var matrix = n.WorldMatrix; using var r = new SceneRenderer(); EditablePathConversion.Convert(e, r, n);
                Check(n.Kind == NodeKind.Path && n.Points.Count >= 2 && n.PathData is null); Check(n.WorldMatrix == matrix);
                var id = n.Id; e.Undo(); Check(e.Document.Find(id)!.Kind == kind); e.Redo(); Check(PathEditing.CanEdit(e.Document.Find(id)!));
            });
        test("SVG quadratic conversion preserves cubic-equivalent geometry", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, PathData = "M0 0 Q50 100 100 0", Width = 100, Height = 100 }; var e = Editor(n);
            using var r = new SceneRenderer(); EditablePathConversion.Convert(e, r, n);
            Equal(n.Points[0].ControlOut!.Value, new(100d / 3, 200d / 3)); Equal(n.Points[1].ControlIn!.Value, new(200d / 3, 200d / 3));
        });
        test("multi-contour conversion leaves compound holes untouched", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, PathData = "M0 0H100V100H0Z M20 20V80H80V20Z" }; var e = Editor(n); using var r = new SceneRenderer();
            var before = DocumentJson.Save(e.Document); Throws<InvalidOperationException>(() => EditablePathConversion.Convert(e, r, n)); Check(DocumentJson.Save(e.Document) == before && !e.CanUndo);
        });
        test("editable geometry equals SVG reference for integer cubic controls", () =>
        {
            var n = Path(true, true); using var r = new SceneRenderer(); using var original = SKPath.ParseSvgPathData(VectorPath.Build(n)); var direct = r.Geometry(n);
            Equal(direct.TightBounds.Width, original.TightBounds.Width); Equal(direct.TightBounds.Height, original.TightBounds.Height);
            for (var x = 0; x < 200; x += 3) for (var y = 0; y < 100; y += 3) Check(direct.Contains(x, y) == original.Contains(x, y));
        });
        test("editing path points invalidates geometry once and movement retains it", () =>
        {
            var n = Path(); using var r = new SceneRenderer(); r.Geometry(n); PathEditing.Move(n, [1], new(1, 0)); r.Geometry(n); Check(r.GeometryBuilds == 2);
            n.X++; r.Geometry(n); Check(r.GeometryBuilds == 2);
        });
        test("point command history is atomic and cancellable", () =>
        {
            var n = Path(true); var e = Editor(n); e.Select(n); e.Edit("Insert", () => PathEditing.Insert(n, 0, .5)); Check(e.History.Count == 1);
            e.Undo(); Check(e.Primary!.Points.Count == 3); e.Redo(); Check(e.Primary!.Points.Count == 4);
            e.BeginInteraction("Points"); PathEditing.Move(e.Primary!, [0, 1], new(40, 50)); e.CancelInteraction(); Equal(e.Primary!.Points[0].Position, Vec2.Zero);
        });
        test("paste in place preserves nested world placement and existing paste offset", () =>
        {
            var p = new DesignNode { X = 120, Y = 80, Rotation = 30 }; var n = p.Add(new() { X = 20, Y = 35 }); var e = Editor(p); e.Select(n);
            var origin = n.WorldMatrix.Map(Vec2.Zero); var copy = e.CopySelection(); e.Paste(copy, true); Equal(e.Primary!.WorldMatrix.Map(Vec2.Zero), origin);
            e.Paste(copy); Equal(e.Primary!.WorldMatrix.Map(Vec2.Zero), origin + new Vec2(24, 24));
        });
        test("flip selection reflects shared world bounds and twice is identity", () =>
        {
            var a = new DesignNode { X = 0, Width = 20 }; var b = new DesignNode { X = 80, Width = 40 }; var e = Editor(a, b); e.Select([a.Id, b.Id]);
            e.FlipSelection(true); Equal(a.WorldBounds.X, 100); Equal(b.WorldBounds.X, 0); e.FlipSelection(true); Equal(a.WorldBounds.X, 0); Equal(b.WorldBounds.X, 80);
        });
        test("flip skips locked roots and never double-transforms selected descendants", () =>
        {
            var p = new DesignNode(); var c = p.Add(new() { X = 10 }); var locked = new DesignNode { X = 500, Locked = true };
            var e = Editor(p, locked); e.Select([p.Id, c.Id, locked.Id]); var childLocal = c.LocalMatrix; e.FlipSelection(false);
            Check(c.LocalMatrix == childLocal && locked.X == 500 && !locked.FlipY);
        });
        test("quarter-turn selection uses a shared pivot and is undoable", () =>
        {
            var n = new DesignNode { X = 20, Y = 40, Width = 80, Height = 30 }; var e = Editor(n); e.Select(n); var center = n.WorldBounds.Center;
            e.RotateSelection(90); Equal(n.WorldBounds.Center, center); Equal(n.WorldBounds.Width, 30); e.Undo(); Equal(e.Primary!.Rotation, 0);
        });
        test("keyboard resize leaves untouched hug axis available for reflow", () =>
        {
            var n = new DesignNode { Width = 100, Height = 20, Layout = new() { Direction = LayoutDirection.Horizontal, HugHeight = true, PaddingTop = 0, PaddingBottom = 0 }, Children = [new() { Height = 20 }] };
            var e = Editor(n); e.Select(n); e.ResizeSelectionBy(10, 0); Equal(n.Width, 110); Check(n.Layout.HugHeight); Equal(n.Height, 20);
        });
        test("explicit spacing honors world-space widths under different parents", () =>
        {
            var a = new DesignNode { Width = 20 }; var p = new DesignNode { X = 100, Rotation = 90 }; var b = p.Add(new() { Width = 50, Height = 30 });
            var e = Editor(a, p); e.Select([a.Id, b.Id]); e.SpaceSelection(true, 16); Equal(b.WorldBounds.X - a.WorldBounds.Right, 16);
        });
        test("spacing rejects auto-layout children before opening history", () =>
        {
            var p = new DesignNode { Layout = new() { Direction = LayoutDirection.Horizontal } }; var a = p.Add(new()); var b = p.Add(new()); var e = Editor(p); e.Select([a.Id, b.Id]);
            Throws<InvalidOperationException>(() => e.SpaceSelection(true, 10)); Check(!e.CanUndo && !e.IsInteracting);
        });
    }
}
