using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Layout;

internal static class ResizeTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Resize assertion failed"); }
    private static void Equal(double actual, double expected) { if (!double.IsFinite(actual) || Math.Abs(actual - expected) > 1e-6) throw new Exception($"Expected {expected:R}, got {actual:R}"); }
    private static void EqualPoint(Vec2 actual, Vec2 expected) { Equal(actual.X, expected.X); Equal(actual.Y, expected.Y); }
    private static DesignNode Node() => new() { X = 37, Y = 23, Width = 100, Height = 40 };
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    public static void Register(Action<string, Action> test)
    {
        foreach (var handle in Enum.GetValues<ResizeHandle>())
        {
            test($"resize {handle} retains its opposite anchor under min/max clamping and transforms", () =>
            {
                foreach (var rotation in new[] { 0, 31, 90, -127 })
                foreach (var flip in new[] { false, true })
                foreach (var point in new[] { new Vec2(-500, -300), new Vec2(800, 600), new Vec2(60, 20) })
                {
                    var parent = Node(); parent.Rotation = -23; parent.FlipY = flip;
                    var node = parent.Add(Node()); node.Rotation = rotation; node.FlipX = flip;
                    node.MinWidth = 80; node.MaxWidth = 150; node.MinHeight = 16; node.MaxHeight = 64;
                    var baseline = DocumentJson.CloneNode(node);
                    var plan = ResizeGeometry.Calculate(baseline, handle, point);
                    var before = node.WorldMatrix.Map(new Vec2(baseline.Width * plan.Anchor.X, baseline.Height * plan.Anchor.Y));
                    GestureGeometry.ResizeFromHandle(node, baseline, handle, point);
                    var after = node.WorldMatrix.Map(new Vec2(node.Width * plan.Anchor.X, node.Height * plan.Anchor.Y));
                    EqualPoint(after, before); Equal(node.Width, plan.Bounds.Width); Equal(node.Height, plan.Bounds.Height);
                }
            });
        }
        test("west resize clamps size before positioning the stationary east edge", () =>
        {
            var node = Node(); node.MinWidth = 80;
            GestureGeometry.ResizeFromHandle(node, DocumentJson.CloneNode(node), ResizeHandle.Left, new(99, 20));
            Equal(node.Width, 80); Equal(node.X + node.Width, 137);
        });
        test("explicit local box keeps its anchor after clamping", () =>
        {
            var node = Node(); node.MinWidth = 80;
            GestureGeometry.Resize(node, DocumentJson.CloneNode(node), 99, 0, 1, 40);
            Equal(node.X + node.Width, 137); Equal(node.Width, 80);
        });
        test("right side handle keeps vertical fill and hug state", () =>
        {
            var node = Node(); node.FillWidth = node.FillHeight = true;
            node.Layout.HugWidth = node.Layout.HugHeight = true;
            GestureGeometry.ResizeFromHandle(node, DocumentJson.CloneNode(node), ResizeHandle.Right, new(140, 20));
            Check(!node.FillWidth && !node.Layout.HugWidth && node.FillHeight && node.Layout.HugHeight);
            Equal(node.Width, 140); Equal(node.Height, 40);
        });
        test("bottom side handle keeps horizontal fill and hug state", () =>
        {
            var node = Node(); node.FillWidth = node.FillHeight = true;
            node.Layout.HugWidth = node.Layout.HugHeight = true;
            GestureGeometry.ResizeFromHandle(node, DocumentJson.CloneNode(node), ResizeHandle.Bottom, new(50, 70));
            Check(node.FillWidth && node.Layout.HugWidth && !node.FillHeight && !node.Layout.HugHeight);
            Equal(node.Width, 100); Equal(node.Height, 70);
        });
        foreach (var handle in new[] { ResizeHandle.Left, ResizeHandle.Right, ResizeHandle.Top, ResizeHandle.Bottom })
        {
            test($"Shift {handle} handle can shrink while maintaining aspect", () =>
            {
                var node = Node(); var baseline = DocumentJson.CloneNode(node);
                var pointer = new Vec2(50, 20);
                GestureGeometry.ResizeFromHandle(node, baseline, handle, pointer, true);
                Equal(node.Width, 50); Equal(node.Height, 20);
            });
        }
        test("Alt Shift corner resize keeps center under a size limit", () =>
        {
            var node = Node(); node.MaxWidth = 150;
            var center = node.WorldMatrix.Map(new Vec2(50, 20));
            GestureGeometry.ResizeFromHandle(node, DocumentJson.CloneNode(node), ResizeHandle.TopLeft, new(-200, -300), true, true);
            EqualPoint(node.WorldMatrix.Map(new Vec2(node.Width / 2, node.Height / 2)), center);
            Equal(node.Width, 150); Equal(node.Height, 60);
        });
        test("aspect resize respects both axis limits", () =>
        {
            var node = Node(); node.MaxHeight = 60;
            GestureGeometry.ResizeFromHandle(node, DocumentJson.CloneNode(node), ResizeHandle.Right, new(1000, 20), true);
            Equal(node.Width, 150); Equal(node.Height, 60);
        });
        test("infeasible aspect limits prioritize explicit dimensions", () =>
        {
            var node = Node(); node.MinWidth = 200; node.MaxHeight = 40;
            var plan = ResizeGeometry.Calculate(node, ResizeHandle.BottomRight, new(800, 800), true);
            Check(plan.Bounds.Width >= 200 && plan.Bounds.Height <= 40);
        });
        test("horizontal resize reflows wrapping content and preserves hug height", () =>
        {
            var node = Node(); node.Kind = NodeKind.Frame; node.Width = 210;
            node.Layout = new() { Direction = LayoutDirection.Horizontal, Wrap = true, HugHeight = true,
                Gap = 10, CrossGap = 8, PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0 };
            node.Add(Node()); node.Add(Node()); LayoutEngine.Arrange(node);
            Equal(node.Height, 40); var baseline = DocumentJson.CloneNode(node);
            GestureGeometry.ResizeFromHandle(node, baseline, ResizeHandle.Right, new(100, 20));
            Equal(node.Height, 88); Check(node.Layout.HugHeight); Equal(node.Y, 23);
            LayoutEngine.Arrange(node); Equal(node.Height, 88);
            GestureGeometry.ResizeFromHandle(node, baseline, ResizeHandle.Right, new(210, 20));
            Equal(node.Height, 40); Equal(node.Y, 23);
        });
        test("handle previews are baseline-relative and reversible through size limits", () =>
        {
            var node = Node(); node.MinWidth = 80; node.Rotation = 29;
            var child = node.Add(Node()); child.HorizontalConstraint = AxisConstraint.Scale;
            var baseline = DocumentJson.CloneNode(node);
            GestureGeometry.ResizeFromHandle(node, baseline, ResizeHandle.Left, new(99, 20));
            GestureGeometry.ResizeFromHandle(node, baseline, ResizeHandle.Left, new(-100, 20));
            var once = DocumentJson.CloneNode(baseline);
            GestureGeometry.ResizeFromHandle(once, baseline, ResizeHandle.Left, new(-100, 20));
            EqualPoint(new(node.X, node.Y), new(once.X, once.Y)); Equal(node.Width, once.Width);
            Equal(node.Children[0].Width, once.Children[0].Width);
        });
        test("handle resize commits once and restores sizing modes on undo", () =>
        {
            var node = Node(); node.FillHeight = true; node.Layout.HugWidth = true;
            var session = new EditorSession(new() { Pages = [new() { Nodes = [node] }] }); session.Select(node);
            var baseline = DocumentJson.CloneNode(node); session.BeginInteraction("Resize");
            for (var i = 0; i < 10; i++) { GestureGeometry.ResizeFromHandle(node, baseline, ResizeHandle.Right, new(110 + i, 20)); session.Preview(); }
            session.CommitInteraction(); Equal(session.History.Count, 1);
            session.Undo(); Check(session.Primary!.Layout.HugWidth && session.Primary.FillHeight);
            session.Redo(); Check(!session.Primary!.Layout.HugWidth && session.Primary.FillHeight); Equal(session.Primary.Width, 119);
        });
        test("invalid handle and nonfinite pointer are rejected", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => ResizeGeometry.Calculate(Node(), (ResizeHandle)9, new(2, 3)));
            Throws<ArgumentOutOfRangeException>(() => ResizeGeometry.Calculate(Node(), ResizeHandle.Left, new(double.NaN, 3)));
        });
    }
}
