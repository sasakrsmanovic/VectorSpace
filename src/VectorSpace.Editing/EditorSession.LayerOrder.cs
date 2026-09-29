using VectorSpace.Core;

namespace VectorSpace.Editing;

public sealed partial class EditorSession
{
    private void ReorderCore(int direction, bool extreme)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (nodes.Any(n => Ancestors(n).Any(p => p.Kind == NodeKind.Instance)))
            throw new InvalidOperationException("Change the main component or detach the instance before reordering its children.");
        var groups = nodes.GroupBy(n => n.Parent).Select(group =>
        {
            var layers = group.Key?.Children ?? Page.Nodes;
            return (Layers: layers, Selected: (IReadOnlySet<DesignNode>)group.ToHashSet());
        }).Where(g => LayerOrdering.CanMove(g.Layers, g.Selected, direction)).ToArray();
        if (groups.Length == 0) return;
        var label = direction > 0 ? (extreme ? "Bring to front" : "Bring forward") : (extreme ? "Send to back" : "Send backward");
        Edit(label, () =>
        {
            foreach (var group in groups) LayerOrdering.Move(group.Layers, group.Selected, direction, extreme);
            InvalidateSelection();
        });
    }
}
