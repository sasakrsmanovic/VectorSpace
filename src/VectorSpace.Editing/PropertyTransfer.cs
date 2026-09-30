using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Editing;

/// <summary>Transactional style authoring shared by clipboard and inspector. Explicit property paste
/// materializes resolved values and leaves unrelated sizing/content variable bindings untouched.</summary>
public static class PropertyTransfer
{
    public static int Paste(EditorSession editor, LayerProperties properties, PropertyGroups groups = PropertyGroups.All)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(properties);
        if ((groups & ~PropertyGroups.All) != 0) throw new ArgumentOutOfRangeException(nameof(groups));
        PropertyClipboard.Validate(properties);
        var targets = editor.Selection.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (targets.Length == 0 || (groups & properties.Groups) == PropertyGroups.None) return 0;
        var count = 0;
        editor.Edit("Paste properties", () =>
        {
            foreach (var node in targets)
            {
                var applied = properties.Apply(node, groups);
                if (applied == PropertyGroups.None) continue;
                if (applied.HasFlag(PropertyGroups.Fills)) Freeze(node, VariableTarget.Fill);
                if (applied.HasFlag(PropertyGroups.Strokes)) Freeze(node, VariableTarget.Stroke);
                if (applied.HasFlag(PropertyGroups.Typography))
                { Freeze(node, VariableTarget.FontFamily); Freeze(node, VariableTarget.FontSize); Freeze(node, VariableTarget.LetterSpacing); }
                if (applied.HasFlag(PropertyGroups.Appearance))
                {
                    if (properties.Opacity.HasValue) Freeze(node, VariableTarget.Opacity);
                    if (properties.CornerRadius.HasValue && LayerProperties.SupportsCorners(node)) Freeze(node, VariableTarget.CornerRadius);
                }
                ComponentService.SetPropertyOverrides(node, applied, captureCornerRadius: properties.CornerRadius.HasValue);
                count++;
            }
        });
        return count;
    }
    public static void Update(EditorSession editor, string label, Action<DesignNode> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        editor.UpdateSelection(label, node =>
        {
            var before = LayerProperties.Capture(node);
            var beforeCorners = node.Corners; var beforeRadius = node.CornerRadius;
            change(node);
            var changed = before.Difference(node);
            // Clearing independent radii to the already-stored uniform scalar is still
            // an authored geometry change, even when the scalar itself did not change.
            var cornersChanged = LayerProperties.SupportsCorners(node) && (beforeCorners != node.Corners || beforeRadius != node.CornerRadius);
            if (cornersChanged) changed |= PropertyGroups.Appearance;
            if (changed.HasFlag(PropertyGroups.Fills)) Freeze(node, VariableTarget.Fill);
            if (changed.HasFlag(PropertyGroups.Strokes)) Freeze(node, VariableTarget.Stroke);
            if (before.Opacity is { } opacity && opacity != node.Opacity) Freeze(node, VariableTarget.Opacity);
            if (cornersChanged) Freeze(node, VariableTarget.CornerRadius);
            if (before.Typography is { } text)
            {
                if (text.FontFamily != node.FontFamily) Freeze(node, VariableTarget.FontFamily);
                if (text.FontSize != node.FontSize) Freeze(node, VariableTarget.FontSize);
                if (text.LetterSpacing != node.LetterSpacing) Freeze(node, VariableTarget.LetterSpacing);
            }
            if (changed != PropertyGroups.None) ComponentService.SetPropertyOverrides(node, changed, captureCornerRadius: cornersChanged);
        });
    }
    private static void Freeze(DesignNode node, VariableTarget target)
    {
        var inInstance = false;
        for (var parent = node; parent is not null; parent = parent.Parent) if (parent.Kind == NodeKind.Instance) { inInstance = true; break; }
        if (inInstance) node.VariableBindings[target] = new() { IsOverride = true, Disabled = true, Fallback = VariableResolver.Read(node, target) };
        else node.VariableBindings.Remove(target);
    }
}
