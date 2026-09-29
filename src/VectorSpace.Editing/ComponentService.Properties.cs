using VectorSpace.Core;

namespace VectorSpace.Editing;

public static partial class ComponentService
{
    public static void SetPropertyOverrides(DesignNode node, PropertyGroups groups)
    {
        var value = PropertiesFor(node); if (value is null) return;
        if (groups.HasFlag(PropertyGroups.Fills)) { value.Fill = null; value.Fills = StyleCloner.Fills(node.Fills); }
        if (groups.HasFlag(PropertyGroups.Strokes)) value.Strokes = StyleCloner.Strokes(node.Strokes);
        if (groups.HasFlag(PropertyGroups.Effects)) value.Effects = StyleCloner.Effects(node.Shadows);
        if (groups.HasFlag(PropertyGroups.Typography) && node.Kind == NodeKind.Text) value.Typography = TypographyStyle.Capture(node);
        if (groups.HasFlag(PropertyGroups.Appearance))
        {
            value.Opacity = node.Opacity; value.Blend = node.Blend;
            if (LayerProperties.SupportsCorners(node)) value.CornerRadius = node.CornerRadius;
        }
    }
    public static void SetNameOverride(DesignNode node)
    {
        if (PropertiesFor(node) is { } value) value.Name = node.Name;
    }
    private static InstanceOverride? PropertiesFor(DesignNode node)
    {
        var instance = node;
        while (instance is not null && instance.Kind != NodeKind.Instance) instance = instance.Parent;
        if (instance is null || node.SourceId is null) return null;
        if (!instance.Overrides.TryGetValue(node.SourceId, out var value)) instance.Overrides[node.SourceId] = value = new();
        return value;
    }
    private static void ApplyPropertyOverrides(DesignNode node, InstanceOverride value)
    {
        if (value.Name is { } name) node.Name = name;
        if (value.Strokes is { } strokes) node.Strokes = StyleCloner.Strokes(strokes);
        if (value.Typography is { } type && node.Kind == NodeKind.Text) type.Apply(node);
        if (value.Opacity is { } opacity) node.Opacity = opacity;
        if (value.Blend is { } blend) node.Blend = blend;
        if (value.CornerRadius is { } radius) node.CornerRadius = radius;
    }
}
