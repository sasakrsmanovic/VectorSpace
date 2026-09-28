using VectorSpace.Core;
using VectorSpace.Editing;

internal static class DrawingTargetTests
{
    private static DesignNode Frame() => new() { Kind = NodeKind.Frame, Width = 100, Height = 100 };
    private static void Check(bool value) { if (!value) throw new Exception("Unexpected drawing parent."); }
    public static void Register(Action<string, Action> test)
    {
        test("point transforms preserve incomplete legacy canonical dimensions", () =>
        {
            var n = Frame(); n.Kind = NodeKind.Path; n.PathWidth = 200; n.PathHeight = 0;
            Check(PathEditing.PointToWorld(n) == n.WorldMatrix);
            n.PathWidth = 0; n.PathHeight = 200;
            Check(PathEditing.PointToWorld(n) == n.WorldMatrix);
        });
        test("constrained cardinal directions contain no floating-point trig residue", () =>
        {
            Check(DrawingGeometry.ConstrainAngle(new(3, 120)).X == 0);
            Check(DrawingGeometry.ConstrainAngle(new(3, -120)).X == 0);
            Check(DrawingGeometry.ConstrainAngle(new(-120, 3)).Y == 0);
        });
        test("drawing parent uses local rotated bounds, not the world bounding rectangle", () =>
        {
            var frame = Frame(); frame.Rotation = 45;
            Check(DrawingTargetQuery.FindFrame([frame], frame.WorldBounds.Center) == frame);
            Check(DrawingTargetQuery.FindFrame([frame], new(frame.WorldBounds.X + 1, frame.WorldBounds.Y + 1)) is null);
        });
        test("drawing parent excludes locked hidden and transparent frame subtrees", () =>
        {
            var backdrop = Frame(); var group = new DesignNode { Kind = NodeKind.Group }; group.Add(Frame());
            group.Visible = false; Check(DrawingTargetQuery.FindFrame([backdrop, group], new(50, 50)) == backdrop);
            group.Visible = true; group.Locked = true; Check(DrawingTargetQuery.FindFrame([backdrop, group], new(50, 50)) == backdrop);
            group.Locked = false; group.Opacity = 0; Check(DrawingTargetQuery.FindFrame([backdrop, group], new(50, 50)) == backdrop);
        });
        test("drawing parent never inserts into an instance or its nested frames", () =>
        {
            var backdrop = Frame(); var instance = Frame(); instance.Kind = NodeKind.Instance; instance.Add(Frame());
            Check(DrawingTargetQuery.FindFrame([backdrop, instance], new(50, 50)) == backdrop);
        });
        test("drawing parent honors ancestor clipping but permits visible overflow", () =>
        {
            var outer = Frame(); var nested = outer.Add(Frame()); nested.X = 150;
            Check(DrawingTargetQuery.FindFrame([outer], new(175, 50)) == nested);
            outer.ClipContent = true; Check(DrawingTargetQuery.FindFrame([outer], new(175, 50)) is null);
        });
        test("drawing parent excludes rounded clipped corners", () =>
        {
            var outer = Frame(); outer.ClipContent = true; outer.CornerRadius = 40; outer.Add(Frame());
            Check(DrawingTargetQuery.FindFrame([outer], new(1, 1)) is null);
            Check(DrawingTargetQuery.FindFrame([outer], new(50, 50)) == outer.Children[0]);
        });
        test("drawing parent preserves reverse painter order and nested reflection", () =>
        {
            var back = Frame(); var front = Frame(); front.X = 10; front.FlipX = true;
            var inner = front.Add(Frame()); inner.Width = 20; inner.Height = 20;
            Check(DrawingTargetQuery.FindFrame([back, front], inner.WorldMatrix.Map(new Vec2(10, 10))) == inner);
            Check(DrawingTargetQuery.FindFrame([front, back], new(50, 50)) == back);
        });
        test("drawing query is allocation-free for a retained list", () =>
        {
            var outer = Frame(); outer.Add(Frame()); var roots = new[] { outer };
            for (var i = 0; i < 20; i++) DrawingTargetQuery.FindFrame(roots, new(50, 50));
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++) DrawingTargetQuery.FindFrame(roots, new(50, 50));
            Check(GC.GetAllocatedBytesForCurrentThread() == start);
        });
    }
}
