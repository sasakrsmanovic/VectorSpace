using VectorSpace.Core;
using VectorSpace.Layout;

namespace VectorSpace.Editing;

public sealed partial class EditorSession
{
    /// <summary>Reflect selected roots around their shared world-space bounds without double-transforming descendants.</summary>
    public void FlipSelection(bool horizontal)
    {
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (nodes.Length == 0) return;
        var center = nodes.Select(n => n.WorldBounds).Aggregate(RectD.Union).Center;
        var mirror = Matrix2D.Translation(-center.X, -center.Y) * Matrix2D.Scale(horizontal ? -1 : 1, horizontal ? 1 : -1) * Matrix2D.Translation(center.X, center.Y);
        Edit(horizontal ? "Flip horizontal" : "Flip vertical", () =>
        {
            foreach (var n in nodes)
                NodeGeometry.SetLocalMatrix(n, n.WorldMatrix * mirror * (n.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity));
        });
    }
    public void RotateSelection(double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (nodes.Length == 0) return;
        var center = nodes.Select(n => n.WorldBounds).Aggregate(RectD.Union).Center;
        Edit("Rotate selection", () =>
        {
            var rotation = Matrix2D.Translation(-center.X, -center.Y) * Matrix2D.Rotation(degrees) * Matrix2D.Translation(center.X, center.Y);
            foreach (var n in nodes) NodeGeometry.SetLocalMatrix(n, n.WorldMatrix * rotation * (n.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity));
        });
    }
    /// <summary>Changes only requested axes, preserving the other hug/fill sizing contract.</summary>
    public void ResizeSelectionBy(double widthDelta, double heightDelta)
    {
        if (!double.IsFinite(widthDelta) || !double.IsFinite(heightDelta)) throw new ArgumentException("Size deltas must be finite.");
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (nodes.Length == 0 || widthDelta == 0 && heightDelta == 0) return;
        Edit("Resize layers", () =>
        {
            foreach (var n in nodes)
            {
                if (widthDelta != 0) { n.Layout.HugWidth = false; n.FillWidth = false; }
                if (heightDelta != 0) { n.Layout.HugHeight = false; n.FillHeight = false; }
                LayoutEngine.Resize(n, Math.Clamp(n.Width + widthDelta, n.MinWidth, n.MaxWidth), Math.Clamp(n.Height + heightDelta, n.MinHeight, n.MaxHeight));
            }
        });
    }
    /// <summary>Set a fixed gap between free-positioned roots, retaining the leading item's world placement.</summary>
    public void SpaceSelection(bool horizontal, double gap)
    {
        if (!double.IsFinite(gap) || Math.Abs(gap) > 1e6) throw new ArgumentOutOfRangeException(nameof(gap));
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).OrderBy(n => horizontal ? n.WorldBounds.X : n.WorldBounds.Y).ToArray();
        if (nodes.Length < 2) return;
        if (nodes.Any(n => n.Parent?.Layout.Direction is { } direction && direction != LayoutDirection.None && !n.AbsolutePosition))
            throw new InvalidOperationException("Use the parent's auto-layout gap for flow children.");
        Edit("Space layers", () =>
        {
            var cursor = horizontal ? nodes[0].WorldBounds.X : nodes[0].WorldBounds.Y;
            foreach (var n in nodes)
            {
                var bounds = n.WorldBounds;
                var delta = horizontal ? new Vec2(cursor - bounds.X, 0) : new Vec2(0, cursor - bounds.Y);
                var inverse = n.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity;
                var local = inverse.Map(delta) - inverse.Map(Vec2.Zero); n.X += local.X; n.Y += local.Y;
                cursor += (horizontal ? bounds.Width : bounds.Height) + gap;
            }
        });
    }
}
