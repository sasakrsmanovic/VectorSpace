using VectorSpace.Core;
using VectorSpace.Editing;
using VectorSpace.Layout;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    private void ResizeSelection(Vec2 world, bool aspect, bool center)
    {
        if (Session is not { } editor || _originals.Count == 0) return;
        var single = editor.SelectionRoots.Count == 1;
        if (single)
        {
            var node = editor.SelectionRoots[0];
            GestureGeometry.ResizeFromHandle(node, _originals[node.Id], (ResizeHandle)_resizeHandle,
                _resizeMatrix.Inverse.Map(world), aspect, center);
        }
        else
        {
            var width = Math.Max(1, _startBounds.Width); var height = Math.Max(1, _startBounds.Height);
            var plan = ResizeGeometry.Calculate(new(width, height), (ResizeHandle)_resizeHandle,
                world - new Vec2(_startBounds.X, _startBounds.Y), new(1, 1), new(1e7, 1e7), aspect, center);
            var (left, top, newW, newH) = plan.Bounds;
            foreach (var node in editor.SelectionRoots)
            {
                if (!_originals.TryGetValue(node.Id, out var old)) continue;
                GestureGeometry.Restore(node, old);
                var parent = node.Parent?.WorldMatrix ?? Matrix2D.Identity; var oldCenter = parent.Map(new Vec2(old.X + old.Width / 2, old.Y + old.Height / 2));
                var transformed = new Vec2(_startBounds.X + left + (oldCenter.X - _startBounds.X) * newW / width, _startBounds.Y + top + (oldCenter.Y - _startBounds.Y) * newH / height);
                var localCenter = parent.Inverse.Map(transformed); if (plan.ChangesWidth) { node.Layout.HugWidth = false; node.FillWidth = false; }
                if (plan.ChangesHeight) { node.Layout.HugHeight = false; node.FillHeight = false; }
                LayoutEngine.Resize(node, old.Width * newW / width, old.Height * newH / height); node.X = localCenter.X - node.Width / 2; node.Y = localCenter.Y - node.Height / 2;
            }
        }
    }
}
