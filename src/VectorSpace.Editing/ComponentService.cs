using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Editing;

/// <summary>Reusable local components with linked instances and explicit text/fill overrides.</summary>
public static partial class ComponentService
{
    private sealed record SyncStamp(string Source, string Overrides);
    private static readonly ConditionalWeakTable<DesignNode, SyncStamp> Stamps = new();
    public readonly record struct SynchronizationStatistics(int InstancesVisited, int InstancesRebuilt);
    public static void MakeComponent(EditorSession editor)
    {
        if (editor.SelectionRoots.Count != 1 || editor.Primary is not { } node || node.IsEffectivelyLocked) return;
        editor.Edit("Create component", () =>
        {
            if (node.IsContainer && !node.IsBoolean && node.Kind != NodeKind.Instance)
            {
                node.Kind = NodeKind.Component; node.ComponentId = null; return;
            }
            var siblings = node.Parent?.Children ?? editor.Page.Nodes;
            var index = siblings.IndexOf(node);
            var bounds = node.LocalMatrix.Map(node.LocalBounds);
            var component = new DesignNode { Kind = NodeKind.Component, Name = node.Name + " / Component", X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, Fills = [], Parent = node.Parent };
            siblings.RemoveAt(index); node.X -= bounds.X; node.Y -= bounds.Y; component.Add(node); siblings.Insert(index, component); editor.Select(component);
        });
    }

    public static DesignNode InsertInstance(EditorSession editor, DesignNode component, Vec2 position)
    {
        if (component.Kind != NodeKind.Component) throw new ArgumentException("The source must be a component.", nameof(component));
        var clone = DocumentJson.CloneNode(component);
        SetSources(clone); DocumentJson.RegenerateIds([clone]); clone.Kind = NodeKind.Instance; clone.ComponentId = component.Id; clone.X = position.X; clone.Y = position.Y;
        editor.Edit("Insert component instance", () => { editor.AddNode(clone); editor.Select(clone); }); return clone;
    }
    public static void Detach(EditorSession editor) => editor.UpdateSelection("Detach instance", n =>
    {
        if (n.Kind != NodeKind.Instance) return; n.Kind = NodeKind.Frame; n.ComponentId = null; n.Overrides.Clear(); n.SourceId = null; foreach (var c in n.Children) ClearSource(c);
    });
    private static void ClearSource(DesignNode n) { n.SourceId = null; if (n.Kind != NodeKind.Instance) foreach (var c in n.Children) ClearSource(c); }
    public static void ResetOverrides(EditorSession editor)
    {
        editor.Edit("Reset instance overrides", () =>
        {
            foreach (var instance in editor.SelectionRoots.Where(n => n.Kind == NodeKind.Instance && !n.IsEffectivelyLocked))
                foreach (var node in instance.DescendantsAndSelf())
                {
                    node.Overrides.Clear(); node.VariableModes.Clear(); node.PrototypeReactionsOverride = false;
                    foreach (var key in node.VariableBindings.Where(p => p.Value.IsOverride).Select(p => p.Key).ToArray()) node.VariableBindings.Remove(key);
                }
        });
    }
    public static void SetOverride(DesignNode node, string? text = null, string? fill = null)
    {
        var instance = node; while (instance is not null && instance.Kind != NodeKind.Instance) instance = instance.Parent;
        if (instance is null || node.SourceId is null) return;
        if (!instance.Overrides.TryGetValue(node.SourceId, out var value)) instance.Overrides[node.SourceId] = value = new();
        if (text is not null) value.Text = text; if (fill is not null) value.Fill = fill;
    }
    /// <summary>Capture complete appearance overrides instead of reducing an image or gradient to a color.</summary>
    public static void SetAppearanceOverride(DesignNode node, bool fills = true, bool effects = true)
    {
        var instance = node;
        while (instance is not null && instance.Kind != NodeKind.Instance) instance = instance.Parent;
        if (instance is null || node.SourceId is null) return;
        if (!instance.Overrides.TryGetValue(node.SourceId, out var value)) instance.Overrides[node.SourceId] = value = new();
        if (fills) { value.Fill = null; value.Fills = CloneFills(node.Fills); }
        if (effects) value.Effects = CloneEffects(node.Shadows);
    }
    private static List<FillStyle> CloneFills(List<FillStyle> values) => StyleCloner.Fills(values);
    private static List<ShadowStyle> CloneEffects(List<ShadowStyle> values) => StyleCloner.Effects(values);
    /// <summary>Resolve acyclic local component dependencies, preserve scoped descendant IDs, and skip unchanged instances.</summary>
    public static SynchronizationStatistics Synchronize(DesignDocument document)
    {
        var all = document.AllNodes().ToArray();
        var components = all.Where(n => n.Kind == NodeKind.Component && !InsideInstance(n)).ToDictionary(n => n.Id);
        var fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = 0; var rebuilt = 0;
        var expansionSizes = new Dictionary<string, int>(StringComparer.Ordinal);
        var expanded = 0;
        foreach (var page in document.Pages) foreach (var root in page.Nodes)
        {
            expanded = checked(expanded + CountExpanded(root, [], 0));
            if (expanded > DocumentJson.MaxNodes) throw new InvalidOperationException("Component expansion exceeds the document node limit.");
        }
        foreach (var component in components.Values) Fingerprint(component, []);
        foreach (var instance in all.Where(n => n.Kind == NodeKind.Instance && !InsideInstance(n))) Sync(instance, 0);
        return new(visited, rebuilt);

        int CountExpanded(DesignNode node, HashSet<string> chain, int depth)
        {
            if (depth > 60) throw new InvalidOperationException("Component expansion exceeds the nesting limit.");
            if (node.Kind == NodeKind.Instance && node.ComponentId is { } source && components.TryGetValue(source, out var definition))
            {
                if (!chain.Add(source)) throw new InvalidOperationException("Component definitions contain a cycle.");
                if (!expansionSizes.TryGetValue(source, out var size)) expansionSizes[source] = size = CountExpanded(definition, chain, depth + 1);
                chain.Remove(source); return size;
            }
            var total = 1;
            foreach (var child in node.Children)
            {
                total = checked(total + CountExpanded(child, chain, depth + 1));
                if (total > DocumentJson.MaxNodes) throw new InvalidOperationException("Component expansion exceeds the document node limit.");
            }
            return total;
        }
        string Fingerprint(DesignNode component, HashSet<string> chain)
        {
            if (fingerprints.TryGetValue(component.Id, out var known)) return known;
            if (chain.Count >= 48 || !chain.Add(component.Id)) throw new InvalidOperationException("Component definitions contain a cycle or exceed the nesting limit.");
            var text = new StringBuilder(DocumentJson.SaveNodes([component]));
            foreach (var nested in TopInstances(component.Children))
                if (nested.ComponentId is { } id && components.TryGetValue(id, out var definition)) text.Append(Fingerprint(definition, chain));
            chain.Remove(component.Id);
            return fingerprints[component.Id] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
        void Sync(DesignNode instance, int depth)
        {
            if (depth > 48) throw new InvalidOperationException("Component nesting limit exceeded.");
            visited++;
            if (instance.ComponentId is null || !components.TryGetValue(instance.ComponentId, out var definition)) return;
            var stamp = new SyncStamp(fingerprints[definition.Id], OverrideStamp(instance));
            if (Stamps.TryGetValue(instance, out var previous) && previous == stamp) return;
            var copy = DocumentJson.CloneNode(definition); SetSources(copy);
            TransferLocalState(copy, instance);
            ApplyOverrides(copy, instance.Overrides, true);
            foreach (var nested in TopInstances(copy.Children)) Sync(nested, depth + 1);
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            PreserveIds(copy, instance, ids);
            foreach (var n in copy.DescendantsAndSelf()) if (n.PrototypeTargetId is { } target && ids.TryGetValue(target, out var replacement)) n.PrototypeTargetId = replacement;
            foreach (var n in copy.DescendantsAndSelf()) PrototypeValidation.Remap(n, ids);
            instance.Children = copy.Children; foreach (var child in instance.Children) child.Parent = instance;
            instance.Fills = copy.Fills; instance.Strokes = copy.Strokes; instance.Shadows = copy.Shadows;
            instance.Corners = copy.Corners; instance.CornerRadius = copy.CornerRadius; instance.Layout = copy.Layout; instance.ClipContent = copy.ClipContent;
            instance.Text = copy.Text; instance.FontFamily = copy.FontFamily; instance.FontSize = copy.FontSize;
            instance.FontWeight = copy.FontWeight; instance.TextAlign = copy.TextAlign; instance.LineHeight = copy.LineHeight; instance.LetterSpacing = copy.LetterSpacing;
            instance.VariantProperties = copy.VariantProperties;
            instance.Reactions = copy.Reactions; instance.PrototypeTargetId = copy.PrototypeTargetId;
            instance.PrototypeOverflow = copy.PrototypeOverflow; instance.PrototypeReactionsOverride = copy.PrototypeReactionsOverride;
            if (instance.Overrides.TryGetValue(definition.Id, out var rootProperties))
            {
                if (rootProperties.Name is not null) instance.Name = copy.Name;
                if (rootProperties.Opacity.HasValue) instance.Opacity = copy.Opacity;
                if (rootProperties.Blend.HasValue) instance.Blend = copy.Blend;
            }
            instance.SourceId ??= definition.Id;
            // A binding authored on an instance is local. Definition bindings are refreshed unless explicitly overridden.
            foreach (var key in instance.VariableBindings.Where(p => !p.Value.IsOverride).Select(p => p.Key).ToArray()) instance.VariableBindings.Remove(key);
            foreach (var pair in copy.VariableBindings) instance.VariableBindings.TryAdd(pair.Key, pair.Value);
            rebuilt++; Stamps.Remove(instance); Stamps.Add(instance, new(fingerprints[definition.Id], OverrideStamp(instance)));
        }
    }
    private static bool InsideInstance(DesignNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent) if (parent.Kind == NodeKind.Instance) return true;
        return false;
    }
    private static IEnumerable<DesignNode> TopInstances(IEnumerable<DesignNode> roots)
    {
        foreach (var node in roots)
            if (node.Kind == NodeKind.Instance) yield return node;
            else foreach (var instance in TopInstances(node.Children)) yield return instance;
    }
    private static void TransferLocalState(DesignNode fresh, DesignNode old)
    {
        if (old.PrototypeReactionsOverride)
        {
            fresh.PrototypeReactionsOverride = true; fresh.Reactions = DocumentJson.CloneReactions(old.Reactions);
            fresh.PrototypeTargetId = old.PrototypeTargetId; fresh.PrototypeOverflow = old.PrototypeOverflow;
        }
        foreach (var pair in old.VariableBindings.Where(p => p.Value.IsOverride)) fresh.VariableBindings[pair.Key] = pair.Value;
        // Mode overrides on instances intentionally override the source's mode selection.
        foreach (var pair in old.VariableModes) fresh.VariableModes[pair.Key] = pair.Value;
        var priorChildren = old.Children.Where(n => n.SourceId is not null).GroupBy(n => (n.SourceId, n.Kind)).ToDictionary(g => g.Key, g => g.First());
        foreach (var child in fresh.Children)
        {
            if (!priorChildren.TryGetValue((child.SourceId, child.Kind), out var prior)) continue;
            if (child.Kind == NodeKind.Instance) child.Overrides = prior.Overrides;
            TransferLocalState(child, prior);
        }
    }
    private static void ApplyOverrides(DesignNode node, Dictionary<string, InstanceOverride> overrides, bool root)
    {
        if (node.SourceId is { } source && overrides.TryGetValue(source, out var o))
        {
            ApplyPropertyOverrides(node, o);
            if (o.Text is not null) node.Text = o.Text;
            if (o.Fills is not null) node.Fills = CloneFills(o.Fills);
            if (o.Effects is not null) node.Shadows = CloneEffects(o.Effects);
            if (o.Fill is not null) node.Fill = o.Fill;
            if (o.Visible.HasValue) node.Visible = o.Visible.Value;
        }
        if (root || node.Kind != NodeKind.Instance) foreach (var child in node.Children) ApplyOverrides(child, overrides, false);
    }
    private static void PreserveIds(DesignNode fresh, DesignNode? old, Dictionary<string, string> ids)
    {
        var id = fresh.Id; fresh.Id = old?.Id ?? Guid.NewGuid().ToString("N"); ids[id] = fresh.Id;
        var priorChildren = old?.Children.Where(n => n.SourceId is not null).GroupBy(n => n.SourceId!).ToDictionary(g => g.Key, g => g.First());
        foreach (var child in fresh.Children) PreserveIds(child, child.SourceId is { } source && priorChildren?.TryGetValue(source, out var prior) == true ? prior : null, ids);
    }
    private static string OverrideStamp(DesignNode root)
    {
        var text = new StringBuilder();
        foreach (var node in root.DescendantsAndSelf())
        {
            Add(node.PrototypeReactionsOverride.ToString());
            if (node.PrototypeReactionsOverride)
            {
                Add(node.SourceId); Add(node.PrototypeTargetId); Add(node.PrototypeOverflow.ToString());
                Add(System.Text.Json.JsonSerializer.Serialize(node.Reactions, VectorSpaceJsonContext.Default.ListPrototypeReaction));
            }
            foreach (var pair in node.Overrides.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                Add(node.SourceId); Add(pair.Key);
                Add(System.Text.Json.JsonSerializer.Serialize(pair.Value, VectorSpaceJsonContext.Default.InstanceOverride));
            }
            foreach (var pair in node.VariableBindings.Where(p => p.Value.IsOverride).OrderBy(p => p.Key)) { Add(node.SourceId); Add(pair.Key.ToString()); Add(pair.Value.VariableId); Add(pair.Value.Disabled.ToString()); Add(pair.Value.Fallback.ToString()); }
            foreach (var pair in node.VariableModes.OrderBy(p => p.Key, StringComparer.Ordinal)) { Add(node.SourceId); Add(pair.Key); Add(pair.Value); }
        }
        return text.ToString();
        void Add(string? value) { text.Append(value?.Length ?? -1).Append(':').Append(value); }
    }
    private static void SetSources(DesignNode node)
    {
        node.SourceId = node.Id;
        foreach (var child in node.Children) { child.SourceId = child.Id; if (child.Kind != NodeKind.Instance) SetSources(child); }
    }
}
