using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Layout;
using VectorSpace.Skia;

internal static class ParityTests
{
    private static DesignNode Node(double x = 0, double y = 0, double w = 100, double h = 40) => new() { X = x, Y = y, Width = w, Height = h };
    private static DesignNode Flow(LayoutDirection direction = LayoutDirection.Horizontal, double w = 300, double h = 200)
    {
        var n = Node(0, 0, w, h); n.Kind = NodeKind.Frame;
        n.Layout = new() { Direction = direction, Gap = 10, CrossGap = 8, PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0 };
        return n;
    }
    private static EditorSession Editor(params DesignNode[] nodes)
    {
        var d = new DesignDocument(); d.Pages.Clear(); d.Pages.Add(new() { Nodes = [.. nodes] }); return new(d);
    }
    private static void Equal(double a, double b, double tolerance = 1e-6) { if (Math.Abs(a - b) > tolerance || !double.IsFinite(a)) throw new Exception($"Expected {b:R}, got {a:R}"); }
    private static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected an exception"); }
    private static void Stable(DesignNode node)
    {
        LayoutEngine.Arrange(node); var first = node.DescendantsAndSelf().Select(n => (n.X, n.Y, n.Width, n.Height)).ToArray();
        LayoutEngine.Arrange(node); var second = node.DescendantsAndSelf().Select(n => (n.X, n.Y, n.Width, n.Height)).ToArray();
        Check(first.SequenceEqual(second));
    }
    public static void Register(Action<string, Action> test)
    {
        test("layout horizontal wrap and hug height", () =>
        {
            var p = Flow(w: 220); p.Layout.Wrap = p.Layout.HugHeight = true;
            var a = p.Add(Node()); var b = p.Add(Node()); var c = p.Add(Node()); LayoutEngine.Arrange(p);
            Equal(a.X, 0); Equal(b.X, 110); Equal(c.X, 0); Equal(c.Y, 48); Equal(p.Height, 88); Stable(p);
        });
        test("layout vertical wrap and hug width", () =>
        {
            var p = Flow(LayoutDirection.Vertical, h: 90); p.Layout.Wrap = p.Layout.HugWidth = true;
            var a = p.Add(Node()); var b = p.Add(Node()); var c = p.Add(Node()); LayoutEngine.Arrange(p);
            Equal(a.Y, 0); Equal(b.Y, 50); Equal(c.X, 108); Equal(c.Y, 0); Equal(p.Width, 208); Stable(p);
        });
        test("layout exact wrap boundary fits", () =>
        {
            var p = Flow(w: 210); p.Layout.Wrap = p.Layout.HugHeight = true; p.Add(Node()); p.Add(Node()); LayoutEngine.Arrange(p); Equal(p.Height, 40);
            p.Width = 209; LayoutEngine.Arrange(p); Equal(p.Height, 88);
        });
        test("layout negative gaps overlap", () =>
        {
            var p = Flow(); p.Layout.Gap = -20; p.Layout.HugWidth = true; p.Add(Node()); var b = p.Add(Node()); LayoutEngine.Arrange(p); Equal(b.X, 80); Equal(p.Width, 180); Stable(p);
        });
        test("layout absolute and hidden children do not consume flow", () =>
        {
            var p = Flow(); p.Layout.HugWidth = p.Layout.HugHeight = true; p.Add(Node()); var a = p.Add(Node(500, 600)); a.AbsolutePosition = true;
            p.Add(Node(w: 999)).Visible = false; LayoutEngine.Arrange(p); Equal(p.Width, 100); Equal(p.Height, 40); Equal(a.X, 500); Equal(a.Y, 600);
        });
        test("layout absolute end constraint follows resize", () =>
        {
            var p = Flow(); var a = p.Add(Node(250, 160, 30, 20)); a.AbsolutePosition = true; a.HorizontalConstraint = a.VerticalConstraint = AxisConstraint.End;
            LayoutEngine.Resize(p, 500, 400); Equal(a.X, 450); Equal(a.Y, 360);
        });
        test("layout empty hug collapses to padding", () =>
        {
            var p = Flow(); p.Layout.HugWidth = p.Layout.HugHeight = true; p.Layout.PaddingLeft = 12; p.Layout.PaddingRight = 8; p.Layout.PaddingTop = 7; p.Layout.PaddingBottom = 3;
            LayoutEngine.Arrange(p); Equal(p.Width, 20); Equal(p.Height, 10);
        });
        test("layout fill redistributes maximum", () =>
        {
            var p = Flow(w: 310); var a = p.Add(Node()); var b = p.Add(Node()); a.FillWidth = b.FillWidth = true; a.MaxWidth = 80;
            LayoutEngine.Arrange(p); Equal(a.Width, 80); Equal(b.Width, 220); Stable(p);
        });
        test("layout fill redistributes minimum", () =>
        {
            var p = Flow(w: 310); var a = p.Add(Node()); var b = p.Add(Node()); a.FillWidth = b.FillWidth = true; a.MinWidth = 220;
            LayoutEngine.Arrange(p); Equal(a.Width, 220); Equal(b.Width, 80); Stable(p);
        });
        test("layout fill preserves minimum on overflow", () =>
        {
            var p = Flow(w: 100); var a = p.Add(Node()); var b = p.Add(Node()); a.FillWidth = b.FillWidth = true; a.MinWidth = b.MinWidth = 70;
            LayoutEngine.Arrange(p); Equal(a.Width, 70); Equal(b.Width, 70); Equal(b.X, 80);
        });
        test("layout fill allocation overrides child hug", () =>
        {
            var p = Flow(w: 400); var child = p.Add(Flow(w: 100)); child.FillWidth = true; child.Layout.HugWidth = true; child.Add(Node(w: 30));
            LayoutEngine.Arrange(p); Equal(child.Width, 400); Stable(p);
        });
        test("layout nested wrapping reflows after fill", () =>
        {
            var p = Flow(LayoutDirection.Vertical, w: 210); p.Layout.HugHeight = true;
            var wrap = p.Add(Flow(w: 500)); wrap.FillWidth = true; wrap.Layout.Wrap = wrap.Layout.HugHeight = true;
            wrap.Add(Node()); wrap.Add(Node()); wrap.Add(Node()); var next = p.Add(Node()); LayoutEngine.Arrange(p);
            Equal(wrap.Width, 210); Equal(wrap.Height, 88); Equal(next.Y, 98); Equal(p.Height, 138); Stable(p);
        });
        test("layout horizontal and vertical cross fill", () =>
        {
            var p = Flow(w: 300, h: 80); var child = p.Add(Node()); child.FillHeight = true; child.MaxHeight = 60;
            p.Layout.Alignment = LayoutAlignment.End; LayoutEngine.Arrange(p); Equal(child.Height, 60); Equal(child.Y, 20);
        });
        foreach (var (distribution, expected, gap) in new[] {
            (LayoutDistribution.SpaceBetween, 0d, 200d), (LayoutDistribution.SpaceAround, 25d, 150d), (LayoutDistribution.SpaceEvenly, 100d / 3, 400d / 3) })
            test($"layout {distribution} spacing", () =>
            {
                var p = Flow(); p.Layout.Gap = 0; p.Layout.Distribution = distribution; var a = p.Add(Node()); var b = p.Add(Node()); LayoutEngine.Arrange(p);
                Equal(a.X, expected); Equal(b.X - a.X, gap);
            });
        test("layout packed end alignment", () =>
        {
            var p = Flow(); p.Layout.PrimaryAlignment = LayoutAlignment.End; p.Layout.Alignment = LayoutAlignment.Center; var a = p.Add(Node()); LayoutEngine.Arrange(p);
            Equal(a.X, 200); Equal(a.Y, 80);
        });
        test("grid fractional tracks and automatic placement", () =>
        {
            var p = Flow(LayoutDirection.Grid, 320, 200); p.Layout.GridColumns = 3; p.Layout.HugHeight = true;
            for (var i = 0; i < 4; i++) p.Add(Node(w: 20)).FillWidth = true;
            LayoutEngine.Arrange(p); Equal(p.Children[0].Width, 100); Equal(p.Children[2].X, 220); Equal(p.Children[3].X, 0); Equal(p.Children[3].Y, 48); Equal(p.Height, 88); Stable(p);
        });
        test("grid fixed and weighted fractional tracks", () =>
        {
            var p = Flow(LayoutDirection.Grid, 400); p.Layout.Columns = [new() { Sizing = GridTrackSizing.Fixed, Value = 80 }, new() { Value = 1 }, new() { Value = 2 }];
            for (var i = 0; i < 3; i++) p.Add(Node()).FillWidth = true;
            LayoutEngine.Arrange(p); Equal(p.Children[0].Width, 80); Equal(p.Children[1].Width, 100); Equal(p.Children[2].Width, 200); Stable(p);
        });
        test("grid hug tracks preserve content width", () =>
        {
            var p = Flow(LayoutDirection.Grid, 300); p.Layout.Columns = [new() { Sizing = GridTrackSizing.Hug }, new()];
            p.Add(Node(w: 80)); var b = p.Add(Node(w: 20)); b.FillWidth = true; LayoutEngine.Arrange(p); Equal(b.X, 90); Equal(b.Width, 210); Stable(p);
        });
        test("grid span consumes occupied cells", () =>
        {
            var p = Flow(LayoutDirection.Grid, 320); p.Layout.HugHeight = true;
            var a = p.Add(Node()); a.ColumnSpan = 2; a.FillWidth = true; var b = p.Add(Node()); var c = p.Add(Node());
            LayoutEngine.Arrange(p); Equal(a.Width, 210); Equal(b.X, 220); Equal(c.X, 0); Equal(c.Y, 48); Stable(p);
        });
        test("grid explicit placement respects row and column", () =>
        {
            var p = Flow(LayoutDirection.Grid, 320); p.Layout.HugHeight = true; var a = p.Add(Node()); a.GridRow = 2; a.GridColumn = 1;
            LayoutEngine.Arrange(p); Equal(a.X, 110); Equal(a.Y, 16); Equal(p.Height, 56); Stable(p);
        });
        test("grid min max track redistribution", () =>
        {
            var p = Flow(LayoutDirection.Grid, 400); p.Layout.Columns = [new() { Max = 50 }, new() { Min = 200 }, new()];
            for (var i = 0; i < 3; i++) p.Add(Node()).FillWidth = true;
            LayoutEngine.Arrange(p); Equal(p.Children[0].Width, 50); Equal(p.Children[1].Width, 200); Equal(p.Children[2].Width, 130); Stable(p);
        });
        test("grid nested wrapping updates hug row height", () =>
        {
            var p = Flow(LayoutDirection.Grid, 210); p.Layout.GridColumns = 1; p.Layout.HugHeight = true;
            var a = p.Add(Flow(w: 500)); a.FillWidth = true; a.Layout.HugHeight = a.Layout.Wrap = true;
            a.Add(Node()); a.Add(Node()); a.Add(Node()); var b = p.Add(Node()); LayoutEngine.Arrange(p);
            Equal(a.Width, 210); Equal(a.Height, 88); Equal(b.Y, 96); Equal(p.Height, 136); Stable(p);
        });
        test("layout properties JSON round trip", () =>
        {
            var p = Flow(LayoutDirection.Grid); p.Layout.Wrap = true; p.Layout.Distribution = LayoutDistribution.SpaceAround;
            p.Layout.Columns = [new() { Sizing = GridTrackSizing.Fixed, Value = 42 }]; var a = p.Add(Node()); a.AbsolutePosition = true; a.MinWidth = 12; a.ColumnSpan = 2;
            var d = DocumentJson.Load(DocumentJson.Save(Editor(p).Document)); var n = d.Pages[0].Nodes[0];
            Check(n.Layout.Direction == LayoutDirection.Grid && n.Layout.Wrap && n.Children[0].AbsolutePosition); Equal(n.Layout.Columns[0].Value, 42); Equal(n.Children[0].MinWidth, 12);
        });
        test("invalid layout bounds rejected atomically", () =>
        {
            var a = Node(); var e = Editor(a); e.Select(a); Throws(() => e.UpdateSelection("bad bounds", n => n.MinWidth = n.MaxWidth + 1));
            Equal(e.Primary!.MinWidth, 1); Check(!e.CanUndo);
        });
        test("nonfinite grid track rejected", () =>
        {
            var p = Flow(LayoutDirection.Grid); var e = Editor(p); Throws(() => e.Edit("bad track", () => p.Layout.Columns.Add(new() { Value = double.NaN })));
            Equal(e.Page.Nodes[0].Layout.Columns.Count, 0);
        });
        test("parent first selection and explicit deep selection", () =>
        {
            var p = Flow(); var g = p.Add(Flow()); var a = g.Add(Node()); var selected = new HashSet<string>();
            Check(SelectionQuery.Resolve(a, selected) == p); Check(SelectionQuery.Resolve(a, selected, p) == g); Check(SelectionQuery.Resolve(a, selected, deep: true) == a);
        });
        test("selected ancestor preserved for multiselection drag", () =>
        {
            var p = Flow(); var g = p.Add(Flow()); var a = g.Add(Node()); Check(SelectionQuery.Resolve(a, new HashSet<string> { g.Id }) == g);
        });
        test("selection scope stops at section boundary", () =>
        {
            var section = Flow(); section.Kind = NodeKind.Section; var p = section.Add(Flow()); var a = p.Add(Node()); Check(SelectionQuery.Resolve(a, new HashSet<string>()) == p);
        });
        test("locked ancestor and invisible ancestors exclude selection", () =>
        {
            var p = Flow(); var a = p.Add(Node()); p.Locked = true; Check(SelectionQuery.Resolve(a, new HashSet<string>(), deep: true) is null);
            p.Locked = false; p.Visible = false; Check(SelectionQuery.Resolve(a, new HashSet<string>(), deep: true) is null);
        });
        test("marquee rotated corners avoid AABB false positive", () =>
        {
            var a = Node(w: 100, h: 100); a.Rotation = 45; var bounds = a.WorldBounds;
            Check(!SelectionQuery.Intersects(a, new(bounds.X, bounds.Y, 4, 4), false)); Check(SelectionQuery.Intersects(a, new(48, 48, 4, 4), false));
            Check(SelectionQuery.Intersects(a, bounds.Inflate(1), true)); Check(!SelectionQuery.Intersects(a, new(45, 45, 10, 10), true));
        });
        test("marquee parent first and deep leaf results", () =>
        {
            var p = Flow(); var a = p.Add(Node()); Check(SelectionQuery.Marquee([p], new(0, 0, 150, 100)).SequenceEqual([p]));
            Check(SelectionQuery.Marquee([p], new(0, 0, 150, 100), deep: true).SequenceEqual([a]));
        });
        test("marquee skips clipped outside descendants", () =>
        {
            var p = Flow(w: 100, h: 100); p.ClipContent = true; p.Add(Node(200, 200)); Equal(SelectionQuery.Marquee([p], new(200, 200, 10, 10), true).Count, 0);
        });
        test("selection cache survives repeated queries", () =>
        {
            var a = Node(); var e = Editor(a); e.Select(a); Check(ReferenceEquals(e.Selection, e.Selection)); Check(ReferenceEquals(e.SelectionRoots, e.SelectionRoots));
            var before = e.Selection; e.Select(e.SelectedIds); Check(ReferenceEquals(before, e.Selection));
        });
        test("last clicked node is primary irrespective of z order", () =>
        {
            var a = Node(); var b = Node(); var e = Editor(a, b); e.Select([b.Id, a.Id]); Check(e.Primary == a); e.Select(a, true); Check(e.Primary == b);
        });
        test("selection undo invalidates cached scene references", () =>
        {
            var a = Node(); var e = Editor(a); e.Select(a); var before = e.Selection;
            e.UpdateSelection("move", n => n.X = 100); e.Undo(); Check(!ReferenceEquals(e.Selection[0], before[0])); Equal(e.Primary!.X, 0);
        });
        test("selection hierarchy keyboard navigation", () =>
        {
            var p = Flow(); var a = p.Add(Node()); var b = p.Add(Node()); var e = Editor(p); e.Select(p); e.SelectChild(); Check(e.Primary == b);
            e.SelectSibling(true); Check(e.Primary == a); e.SelectSibling(true); Check(e.Primary == b); e.SelectParent(); Check(e.Primary == p);
        });
        test("auto layout wrapping is one undo entry", () =>
        {
            var a = Node(30, 40, 100, 40); var b = Node(150, 40, 80, 40); var e = Editor(a, b); e.Select([a.Id, b.Id]); e.AddAutoLayout();
            Equal(e.Page.Nodes.Count, 1); Equal(e.History.Count, 1); Check(e.Primary!.Layout.Direction == LayoutDirection.Horizontal); Equal(e.Primary.Layout.Gap, 20);
            Equal(a.WorldBounds.X, 30); Equal(b.WorldBounds.X, 150); e.Undo(); Equal(e.Page.Nodes.Count, 2); Check(!e.CanUndo); e.Redo(); Equal(e.Page.Nodes.Count, 1);
        });
        test("auto layout detects vertical selection", () =>
        {
            var a = Node(30, 40); var b = Node(30, 100); var e = Editor(a, b); e.Select([b.Id, a.Id]); e.AddAutoLayout();
            Check(e.Primary!.Layout.Direction == LayoutDirection.Vertical); Equal(e.Primary.Layout.Gap, 20); Equal(b.WorldBounds.Y, 100);
        });
        test("auto layout reorder drag cancels atomically", () =>
        {
            var p = Flow(w: 330); var a = p.Add(Node()); var b = p.Add(Node()); var c = p.Add(Node()); LayoutEngine.Arrange(p);
            var e = Editor(p); e.Select(a); e.BeginInteraction("reorder"); Check(e.ReorderAutoLayout(new(500, 10))); Check(p.Children[^1] == a); e.CancelInteraction();
            Check(e.Page.Nodes[0].Children[0].Id == a.Id); Check(!e.CanUndo);
        });
        test("auto layout reorder commits one history item", () =>
        {
            var p = Flow(w: 330); var a = p.Add(Node()); var b = p.Add(Node()); var c = p.Add(Node()); LayoutEngine.Arrange(p);
            var e = Editor(p); e.Select(a); e.BeginInteraction("reorder"); e.ReorderAutoLayout(new(500, 10)); e.CommitInteraction(); Equal(e.History.Count, 1);
            e.Undo(); Check(e.Page.Nodes[0].Children[0].Id == a.Id); e.Redo(); Check(e.Page.Nodes[0].Children[^1].Id == a.Id);
        });
        test("baseline resize does not compound stretch clamping", () =>
        {
            var p = Flow(); p.Layout.Direction = LayoutDirection.None; var a = p.Add(Node(10, 10, 280, 180)); a.HorizontalConstraint = a.VerticalConstraint = AxisConstraint.Stretch;
            var baseline = DocumentJson.CloneNode(p); GestureGeometry.Resize(p, baseline, 0, 0, 5, 5); GestureGeometry.Resize(p, baseline, 0, 0, 300, 200);
            Equal(a.Width, 280); Equal(a.Height, 180); Equal(a.X, 10);
        });
        test("rotated resize preserves opposite corner", () =>
        {
            var a = Node(50, 60, 100, 40); a.Rotation = 35; var baseline = DocumentJson.CloneNode(a); var corner = a.WorldMatrix.Map(new Vec2(a.Width, a.Height));
            GestureGeometry.Resize(a, baseline, -20, -10, 120, 50); var after = a.WorldMatrix.Map(new Vec2(a.Width, a.Height)); Equal(after.X, corner.X); Equal(after.Y, corner.Y);
        });
        test("multi rotation uses shared pivot", () =>
        {
            var a = Node(0, 0, 20, 20); var b = Node(100, 0, 20, 20); var pivot = new Vec2(60, 10);
            GestureGeometry.Rotate(a, DocumentJson.CloneNode(a), pivot, 90); GestureGeometry.Rotate(b, DocumentJson.CloneNode(b), pivot, 90);
            Equal(a.Bounds.Center.X, 60); Equal(a.Bounds.Center.Y, -40); Equal(b.Bounds.Center.Y, 60); Equal(a.Rotation, 90);
        });
        test("reflected parent rotation preserves world orientation", () =>
        {
            var p = Flow(); p.FlipX = true; p.Rotation = 25; var a = p.Add(Node(20, 30)); var baseline = DocumentJson.CloneNode(a); var pivot = new Vec2(150, 80);
            var before = a.WorldMatrix.Map(Vec2.Zero); var rotation = Matrix2D.Translation(-pivot.X, -pivot.Y) * Matrix2D.Rotation(40) * Matrix2D.Translation(pivot.X, pivot.Y);
            GestureGeometry.Rotate(a, baseline, pivot, 40); var after = a.WorldMatrix.Map(Vec2.Zero); var expected = rotation.Map(before);
            Equal(after.X, expected.X); Equal(after.Y, expected.Y);
        });
        test("retained geometry survives fill and position changes", () =>
        {
            using var r = new SceneRenderer(); var a = Node(); var path = r.Geometry(a); a.X = 44; a.Fill = "#FF0000";
            Check(ReferenceEquals(path, r.Geometry(a))); Equal(r.GeometryBuilds, 1); Equal(r.GeometryCacheHits, 1);
        });
        test("retained geometry invalidates path control handles", () =>
        {
            using var r = new SceneRenderer(); var a = Node(); a.Kind = NodeKind.Path; a.Points = [new() { Position = new(0, 0) }, new() { Position = new(100, 40) }];
            r.Geometry(a); a.Points[0].ControlOut = new Vec2(30, 50); r.Geometry(a); Equal(r.GeometryBuilds, 2);
        });
        test("retained geometry invalidates dimensions", () =>
        {
            using var r = new SceneRenderer(); var a = Node(); r.Geometry(a); a.Width = 220; var p = r.Geometry(a); Equal(r.GeometryBuilds, 2); Equal(p.Bounds.Width, 220);
        });
        test("geometry cache is bounded and LRU", () =>
        {
            using var r = new SceneRenderer { GeometryCacheCapacity = 2 }; var a = Node(); var b = Node(); var c = Node(); r.Geometry(a); r.Geometry(b); r.Geometry(a); r.Geometry(c);
            Equal(r.CachedGeometryCount, 2); r.Geometry(a); Equal(r.GeometryBuilds, 3); r.Geometry(b); Equal(r.GeometryBuilds, 4);
        });
        test("geometry trim removes deleted nodes without rebuilding survivors", () =>
        {
            using var r = new SceneRenderer(); var a = Node(); var b = Node(); r.Geometry(a); r.Geometry(b); r.TrimCache([a.Id]); Equal(r.CachedGeometryCount, 1); r.Geometry(a); Equal(r.GeometryBuilds, 2);
        });
        test("viewport culls nested offscreen children", () =>
        {
            using var r = new SceneRenderer(); using var bitmap = new SKBitmap(200, 200); using var canvas = new SKCanvas(bitmap); var p = Flow(); p.Layout.Direction = LayoutDirection.None;
            p.Add(Node(20, 20)); p.Add(Node(5000, 5000)); r.Draw(canvas, [p], new(0, 0, 200, 200)); Equal(r.RenderedNodes, 2); Equal(r.CulledNodes, 1);
        });
        test("viewport preserves overflow of unclipped containers", () =>
        {
            using var r = new SceneRenderer(); using var bitmap = new SKBitmap(200, 200); using var canvas = new SKCanvas(bitmap); var p = Flow(); p.X = 5000; p.Add(Node(-4950, 20));
            r.Draw(canvas, [p], new(0, 0, 200, 200)); Equal(r.RenderedNodes, 2);
        });
        test("rounded frame clips child hit tests", () =>
        {
            using var r = new SceneRenderer(); var p = Flow(w: 100, h: 100); p.CornerRadius = 40; p.ClipContent = true; var c = p.Add(Node(w: 100, h: 100));
            Check(r.HitTest([p], new(1, 1), true) != c); Check(r.HitTest([p], new(50, 50), true) == c);
        });
        test("large selection repeated queries allocate no collections", () =>
        {
            var e = Editor(Enumerable.Range(0, 2000).Select(i => Node(i * 10)).ToArray()); e.SelectAll(); _ = e.SelectionRoots;
            var before = GC.GetAllocatedBytesForCurrentThread(); var total = 0;
            for (var i = 0; i < 1000; i++) total += e.SelectionRoots.Count + e.Selection.Count;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before; Equal(total, 4_000_000); Check(allocated < 1024);
        });
    }
}
