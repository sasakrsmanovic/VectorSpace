using System.Text.Json.Serialization;

namespace VectorSpace.Core;

[Flags]
public enum PropertyGroups { None = 0, Fills = 1, Strokes = 2, Effects = 4, Typography = 8, Appearance = 16, All = 31 }

/// <summary>Whole-layer typography, without text content, layout dimensions or identity.</summary>
public sealed record TypographyStyle
{
    public string FontFamily { get; init; } = "Inter";
    public double FontSize { get; init; } = 24;
    public int FontWeight { get; init; } = 400;
    public double LineHeight { get; init; } = 1.25;
    public double LetterSpacing { get; init; }
    public TextAlignment Alignment { get; init; }
    [JsonConstructor]
    public TypographyStyle(string fontFamily = "Inter", double fontSize = 24, int fontWeight = 400,
        double lineHeight = 1.25, double letterSpacing = 0, TextAlignment alignment = TextAlignment.Left)
    {
        FontFamily = fontFamily; FontSize = fontSize; FontWeight = fontWeight;
        LineHeight = lineHeight; LetterSpacing = letterSpacing; Alignment = alignment;
    }
    public static TypographyStyle Capture(DesignNode node) => new()
    {
        FontFamily = node.FontFamily, FontSize = node.FontSize, FontWeight = node.FontWeight,
        LineHeight = node.LineHeight, LetterSpacing = node.LetterSpacing, Alignment = node.TextAlign
    };
    public void Apply(DesignNode node)
    {
        node.FontFamily = FontFamily; node.FontSize = FontSize; node.FontWeight = FontWeight;
        node.LineHeight = LineHeight; node.LetterSpacing = LetterSpacing; node.TextAlign = Alignment;
    }
}

/// <summary>A detached property snapshot. Null omits a group; an empty list intentionally clears it.
/// Mutable paints/collections are owned by the snapshot; immutable strings/raster data are shared.</summary>
public sealed class LayerProperties
{
    public List<FillStyle>? Fills { get; set; }
    public List<StrokeStyle>? Strokes { get; set; }
    public List<ShadowStyle>? Effects { get; set; }
    public TypographyStyle? Typography { get; set; }
    public double? Opacity { get; set; }
    public BlendKind? Blend { get; set; }
    public double? CornerRadius { get; set; }
    [JsonIgnore] public PropertyGroups Groups => (Fills is null ? 0 : PropertyGroups.Fills) | (Strokes is null ? 0 : PropertyGroups.Strokes)
        | (Effects is null ? 0 : PropertyGroups.Effects) | (Typography is null ? 0 : PropertyGroups.Typography)
        | (Opacity is null && Blend is null && CornerRadius is null ? 0 : PropertyGroups.Appearance);
    public static bool SupportsCorners(DesignNode node) => node.Kind is NodeKind.Rectangle or NodeKind.Frame or NodeKind.Component or NodeKind.Instance or NodeKind.Section;
    public static LayerProperties Capture(DesignNode node, PropertyGroups groups = PropertyGroups.All)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new()
        {
            Fills = groups.HasFlag(PropertyGroups.Fills) ? StyleCloner.Fills(node.Fills) : null,
            Strokes = groups.HasFlag(PropertyGroups.Strokes) ? StyleCloner.Strokes(node.Strokes) : null,
            Effects = groups.HasFlag(PropertyGroups.Effects) ? StyleCloner.Effects(node.Shadows) : null,
            Typography = groups.HasFlag(PropertyGroups.Typography) && node.Kind == NodeKind.Text ? TypographyStyle.Capture(node) : null,
            Opacity = groups.HasFlag(PropertyGroups.Appearance) ? node.Opacity : null,
            Blend = groups.HasFlag(PropertyGroups.Appearance) ? node.Blend : null,
            CornerRadius = groups.HasFlag(PropertyGroups.Appearance) && SupportsCorners(node) ? node.CornerRadius : null
        };
    }
    public PropertyGroups Apply(DesignNode node, PropertyGroups groups = PropertyGroups.All)
    {
        ArgumentNullException.ThrowIfNull(node); var applied = PropertyGroups.None;
        if (groups.HasFlag(PropertyGroups.Fills) && Fills is { } fills) { node.Fills = StyleCloner.Fills(fills); applied |= PropertyGroups.Fills; }
        if (groups.HasFlag(PropertyGroups.Strokes) && Strokes is { } strokes) { node.Strokes = StyleCloner.Strokes(strokes); applied |= PropertyGroups.Strokes; }
        if (groups.HasFlag(PropertyGroups.Effects) && Effects is { } effects) { node.Shadows = StyleCloner.Effects(effects); applied |= PropertyGroups.Effects; }
        if (groups.HasFlag(PropertyGroups.Typography) && Typography is { } text && node.Kind == NodeKind.Text) { text.Apply(node); applied |= PropertyGroups.Typography; }
        if (groups.HasFlag(PropertyGroups.Appearance))
        {
            if (Opacity is { } opacity) { node.Opacity = opacity; applied |= PropertyGroups.Appearance; }
            if (Blend is { } blend) { node.Blend = blend; applied |= PropertyGroups.Appearance; }
            if (CornerRadius is { } radius && SupportsCorners(node)) { node.CornerRadius = radius; applied |= PropertyGroups.Appearance; }
        }
        return applied;
    }
    public PropertyGroups Difference(DesignNode node)
    {
        var groups = PropertyGroups.None;
        if (Fills is { } f && !StyleCloner.SameFills(f, node.Fills)) groups |= PropertyGroups.Fills;
        if (Strokes is { } s && !StyleCloner.SameStrokes(s, node.Strokes)) groups |= PropertyGroups.Strokes;
        if (Effects is { } e && !StyleCloner.SameEffects(e, node.Shadows)) groups |= PropertyGroups.Effects;
        if (Typography is { } t && node.Kind == NodeKind.Text && t != TypographyStyle.Capture(node)) groups |= PropertyGroups.Typography;
        if (Opacity is { } o && o != node.Opacity || Blend is { } b && b != node.Blend || CornerRadius is { } r && r != node.CornerRadius) groups |= PropertyGroups.Appearance;
        return groups;
    }
}
