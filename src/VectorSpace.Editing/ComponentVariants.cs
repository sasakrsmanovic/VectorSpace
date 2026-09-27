using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Editing;

/// <summary>Local component-set variants. Definitions retain their identifiers; instances preserve matched layer overrides when swapped.</summary>
public static class ComponentVariants
{
    public static DesignNode? SetFor(DesignDocument document, DesignNode node)
    {
        if (node.Kind == NodeKind.ComponentSet) return node;
        if (node.Kind == NodeKind.Instance) node = document.Find(node.ComponentId) ?? node;
        return node.Parent?.Kind == NodeKind.ComponentSet ? node.Parent : null;
    }
    public static DesignNode Combine(EditorSession editor, string name = "Component set")
    {
        var roots = editor.SelectionRoots.ToArray();
        if (roots.Length < 2 || roots.Any(n => n.Kind != NodeKind.Component || n.IsEffectivelyLocked || n.Parent != roots[0].Parent) || roots[0].Parent?.Kind == NodeKind.ComponentSet)
            throw new InvalidOperationException("Select at least two unlocked sibling components.");
        DesignNode set = null!;
        editor.Edit("Combine as variants", () =>
        {
            var bounds = roots.Select(n => n.LocalMatrix.Map(n.LocalBounds)).Aggregate(RectD.Union).Inflate(24);
            set = new() { Kind = NodeKind.ComponentSet, Name = name, X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, Parent = roots[0].Parent, Fills = [], Strokes = [new() { Color = "#9747FF", Dashes = [6, 4] }], CornerRadius = 8 };
            var siblings = roots[0].Parent?.Children ?? editor.Page.Nodes;
            var index = roots.Min(siblings.IndexOf);
            for (var i = 0; i < roots.Length; i++)
            {
                var node = roots[i]; siblings.Remove(node); node.X -= bounds.X; node.Y -= bounds.Y; set.Add(node);
                node.VariantProperties = new() { ["Variant"] = "Variant " + (i + 1) };
                node.Name = "Variant=Variant " + (i + 1);
            }
            siblings.Insert(index, set); editor.Select(set);
        }); return set;
    }
    public static DesignNode Add(EditorSession editor, string componentOrSetId)
    {
        var node = editor.Document.Find(componentOrSetId) ?? throw new InvalidOperationException("Component no longer exists.");
        var set = SetFor(editor.Document, node);
        if (node.IsEffectivelyLocked || node.Kind is not (NodeKind.Component or NodeKind.ComponentSet)) throw new InvalidOperationException("Select an unlocked component or set.");
        DesignNode created = null!;
        editor.Edit("Add variant", () =>
        {
            var source = node.Kind == NodeKind.ComponentSet ? node.Children.FirstOrDefault() ?? throw new InvalidOperationException("The component set is empty.") : node;
            if (set is null)
            {
                var siblings = source.Parent?.Children ?? editor.Page.Nodes; var index = siblings.IndexOf(source);
                set = new() { Kind = NodeKind.ComponentSet, Name = source.Name, X = source.X - 24, Y = source.Y - 24, Width = source.Width + 48, Height = source.Height + 48, Parent = source.Parent, Fills = [], Strokes = [new() { Color = "#9747FF", Dashes = [6, 4] }], CornerRadius = 8 };
                siblings.RemoveAt(index); source.X = source.Y = 24; set.Add(source); siblings.Insert(index, set);
                source.VariantProperties = new() { ["Variant"] = "Default" }; source.Name = "Variant=Default";
            }
            created = DocumentJson.CloneNode(source, true); created.Parent = set;
            created.X = source.X; created.Y = set.Children.Select(c => c.Bounds.Bottom).DefaultIfEmpty(0).Max() + 24;
            var key = source.VariantProperties.Keys.FirstOrDefault() ?? "Variant";
            var suffix = 2; string value;
            do value = "Variant " + suffix++; while (set.Children.Any(c => c.VariantProperties.GetValueOrDefault(key) == value));
            created.VariantProperties[key] = value; created.Name = DisplayName(created); set.Add(created);
            set.Width = Math.Max(set.Width, created.Bounds.Right + 24); set.Height = Math.Max(set.Height, created.Bounds.Bottom + 24); editor.Select(created);
        }); return created;
    }
    public static void SetProperty(EditorSession editor, string componentId, string property, string value) => editor.Edit("Edit variant property", () =>
    {
        var component = editor.Document.Find(componentId) ?? throw new InvalidOperationException("Component no longer exists.");
        var set = SetFor(editor.Document, component) ?? throw new InvalidOperationException("The component is not a variant.");
        if (component.IsEffectivelyLocked || string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Use nonempty properties on unlocked variants.");
        foreach (var variant in set.Children) if (!variant.VariantProperties.ContainsKey(property)) variant.VariantProperties[property] = "Default";
        component.VariantProperties[property] = value.Trim();
        if (set.Children.Any(c => c != component && SameProperties(c, component))) throw new InvalidOperationException("This combination already exists in the component set.");
        foreach (var variant in set.Children) variant.Name = DisplayName(variant);
    });
    public static void SwitchProperty(EditorSession editor, string instanceId, string property, string value)
    {
        var instance = editor.Document.Find(instanceId) ?? throw new InvalidOperationException("Instance no longer exists.");
        var set = SetFor(editor.Document, instance) ?? throw new InvalidOperationException("The instance is not linked to a variant set.");
        var current = editor.Document.Find(instance.ComponentId)!;
        var target = set.Children.Where(c => c.VariantProperties.GetValueOrDefault(property) == value)
            .OrderByDescending(c => current.VariantProperties.Count(p => p.Key != property && c.VariantProperties.GetValueOrDefault(p.Key) == p.Value)).FirstOrDefault()
            ?? throw new InvalidOperationException("No variant provides that value.");
        Switch(editor, instanceId, target.Id);
    }
    public static void Switch(EditorSession editor, string instanceId, string componentId) =>
        editor.Edit("Swap component variant", () => SwitchInDocument(editor.Document, instanceId, componentId));

    /// <summary>Change variant identity within an existing transaction. The caller must synchronize,
    /// validate and arrange once the action batch completes; this method does not create editor history.</summary>
    public static void SwitchInDocument(DesignDocument document, string instanceId, string componentId, bool allowLocked = false)
    {
        var instance = document.Find(instanceId) ?? throw new InvalidOperationException("Instance no longer exists.");
        var definition = document.Find(componentId) ?? throw new InvalidOperationException("Component no longer exists.");
        if (instance.Kind != NodeKind.Instance || definition.Kind != NodeKind.Component || (!allowLocked && instance.IsEffectivelyLocked)) throw new InvalidOperationException("Select an unlocked instance and a component definition.");
        var old = document.Find(instance.ComponentId);
        var oldPaths = old is null ? [] : NamedPaths(old);
        var newPaths = NamedPaths(definition);
        var map = oldPaths.Where(p => newPaths.ContainsKey(p.Key)).ToDictionary(p => p.Value.Id, p => newPaths[p.Key].Id, StringComparer.Ordinal);
        var overrides = new Dictionary<string, InstanceOverride>(StringComparer.Ordinal);
        foreach (var (source, value) in instance.Overrides) if (map.TryGetValue(source, out var replacement)) overrides[replacement] = value;
        // Remapping SourceId before synchronization lets matched layers retain their runtime identities and selections.
        foreach (var node in instance.DescendantsAndSelf()) if (node.SourceId is { } id && map.TryGetValue(id, out var replacement)) node.SourceId = replacement;
        instance.Overrides = overrides; instance.ComponentId = definition.Id; instance.SourceId = definition.Id;
        if (old is null || Math.Abs(instance.Width - old.Width) < .001) instance.Width = definition.Width;
        if (old is null || Math.Abs(instance.Height - old.Height) < .001) instance.Height = definition.Height;
    }
    private static bool SameProperties(DesignNode a, DesignNode b) => a.VariantProperties.Count == b.VariantProperties.Count && a.VariantProperties.All(p => b.VariantProperties.GetValueOrDefault(p.Key) == p.Value);
    public static string DisplayName(DesignNode node) => string.Join(", ", node.VariantProperties.Select(p => p.Key + "=" + p.Value));
    private static Dictionary<string, DesignNode> NamedPaths(DesignNode root)
    {
        var result = new Dictionary<string, DesignNode>(StringComparer.Ordinal) { [""] = root }; Visit(root, ""); return result;
        void Visit(DesignNode parent, string path)
        {
            var counts = new Dictionary<(string, NodeKind), int>();
            foreach (var child in parent.Children)
            {
                var key = (child.Name, child.Kind); var index = counts.GetValueOrDefault(key); counts[key] = index + 1;
                var next = path + "/" + child.Name.Length + ":" + child.Name + ":" + child.Kind + ":" + index;
                result[next] = child;
                if (child.Kind != NodeKind.Instance) Visit(child, next);
            }
        }
    }
}
