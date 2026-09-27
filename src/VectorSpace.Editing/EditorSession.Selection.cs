using VectorSpace.Core;
using VectorSpace.Layout;

namespace VectorSpace.Editing;

public sealed partial class EditorSession
{
    public DesignNode? ResolveSelection(DesignNode? hit, bool deep = false) => SelectionQuery.Resolve(hit, SelectedIds, Primary?.Parent, deep);
    public void SelectChild()
    {
        if (Primary is not { } parent) return;
        var child = parent.Children.LastOrDefault(n => n.Visible && !n.IsEffectivelyLocked);
        if (child is not null) Select(child);
    }
    public void SelectParent() { if (Primary?.Parent is { } parent) Select(parent); else Select((DesignNode?)null); }
    public void SelectSibling(bool previous = false)
    {
        var primary = Primary;
        var siblings = (primary?.Parent?.Children ?? Page.Nodes).Where(n => n.Visible && !n.IsEffectivelyLocked).ToArray();
        if (siblings.Length == 0) return;
        var index = Array.IndexOf(siblings, primary);
        Select(siblings[(index + (previous ? -1 : 1) + siblings.Length) % siblings.Length]);
    }
    public void SelectMatching(string property)
    {
        if (Primary is not { } source) return;
        Select(Page.AllNodes().Where(n => n.IsEffectivelyVisible && !n.IsEffectivelyLocked && property switch
        {
            "fill" => n.Fill == source.Fill,
            "font" => n.Kind == NodeKind.Text && n.FontFamily == source.FontFamily,
            "instance" => n.Kind == NodeKind.Instance && n.ComponentId == source.ComponentId,
            _ => n.Kind == source.Kind && n.Name == source.Name
        }).Select(n => n.Id));
    }
    public void AddAutoLayout()
    {
        var roots = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (roots.Length == 0 || roots.Any(n => n.Parent != roots[0].Parent)) return;
        Edit("Add auto layout", () =>
        {
            DesignNode frame;
            if (roots.Length == 1 && roots[0].IsContainer && roots[0].Kind != NodeKind.Instance)
            {
                frame = roots[0]; if (frame.Kind == NodeKind.Group) frame.Kind = NodeKind.Frame;
            }
            else
            {
                var bounds = roots.Select(n => n.LocalMatrix.Map(n.LocalBounds)).Aggregate(RectD.Union);
                var parent = roots[0].Parent; var siblings = parent?.Children ?? Page.Nodes;
                var index = roots.Min(siblings.IndexOf);
                frame = new() { Kind = NodeKind.Frame, Name = "Auto layout", X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, Fills = [], Parent = parent };
                foreach (var n in roots) { siblings.Remove(n); n.X -= bounds.X; n.Y -= bounds.Y; frame.Add(n); }
                siblings.Insert(index, frame);
            }
            var children = frame.Children.Where(n => n.Visible && !n.AbsolutePosition).ToArray();
            var horizontal = children.Length < 2 || children.Max(n => n.Bounds.Center.X) - children.Min(n => n.Bounds.Center.X) >= children.Max(n => n.Bounds.Center.Y) - children.Min(n => n.Bounds.Center.Y);
            var ordered = children.OrderBy(n => horizontal ? n.X : n.Y).ToArray();
            var gaps = ordered.Zip(ordered.Skip(1), (a, b) => horizontal ? b.X - a.Bounds.Right : b.Y - a.Bounds.Bottom).Order().ToArray();
            frame.Layout.Direction = horizontal ? LayoutDirection.Horizontal : LayoutDirection.Vertical;
            frame.Layout.Gap = gaps.Length == 0 ? 16 : gaps[gaps.Length / 2];
            frame.Layout.PaddingLeft = frame.Layout.PaddingTop = frame.Layout.PaddingRight = frame.Layout.PaddingBottom = 0;
            frame.Layout.HugWidth = frame.Layout.HugHeight = true;
            var queue = new Queue<DesignNode>(ordered);
            for (var i = 0; i < frame.Children.Count; i++) if (frame.Children[i].Visible && !frame.Children[i].AbsolutePosition) frame.Children[i] = queue.Dequeue();
            InvalidateSelection(); Select(frame);
        });
    }
    /// <summary>Reorder a flow selection at a local pointer position. Preserves selected order,
    /// hidden/absolute slots and parentage; callers can coalesce a whole drag into one transaction.</summary>
    public bool ReorderAutoLayout(Vec2 worldPoint)
    {
        var roots = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (roots.Length == 0 || roots[0].Parent is not { } parent || parent.Layout.Direction == LayoutDirection.None || roots.Any(n => n.Parent != parent || n.AbsolutePosition)) return false;
        var selected = roots.ToHashSet(); var flow = parent.Children.Where(n => n.Visible && !n.AbsolutePosition).ToList();
        var moving = flow.Where(selected.Contains).ToArray(); var rest = flow.Where(n => !selected.Contains(n)).ToList();
        var local = parent.WorldMatrix.Inverse.Map(worldPoint);
        var horizontal = parent.Layout.Direction == LayoutDirection.Horizontal;
        var index = rest.FindIndex(n => parent.Layout.Direction == LayoutDirection.Grid || parent.Layout.Wrap
            ? (horizontal ? local.Y < n.Y || local.Y <= n.Bounds.Bottom && local.X < n.Bounds.Center.X : local.X < n.X || local.X <= n.Bounds.Right && local.Y < n.Bounds.Center.Y)
            : (horizontal ? local.X < n.Bounds.Center.X : local.Y < n.Bounds.Center.Y));
        if (index < 0) index = rest.Count;
        rest.InsertRange(index, moving);
        if (flow.SequenceEqual(rest)) return false;
        var cursor = 0;
        for (var i = 0; i < parent.Children.Count; i++) if (parent.Children[i].Visible && !parent.Children[i].AbsolutePosition) parent.Children[i] = rest[cursor++];
        InvalidateSelection(); LayoutEngine.Arrange(parent); return true;
    }
}
