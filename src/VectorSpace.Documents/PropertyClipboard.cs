using System.Text.Json;
using System.Text.Json.Serialization;
using VectorSpace.Core;

namespace VectorSpace.Documents;

public sealed class PropertyClipboardPacket
{
    public int Version { get; set; } = 1;
    public string SourceName { get; set; } = "Layer";
    public LayerProperties Properties { get; set; } = new();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PropertyClipboardPacket))]
[JsonSerializable(typeof(LayerProperties))]
public partial class PropertyClipboardJson : JsonSerializerContext;

/// <summary>Portable style-only clipboard. Never evaluates expressions, resolves external URLs,
/// captures children/identity, or imports foreign variable definitions.</summary>
public static class PropertyClipboard
{
    public const string Prefix = "VectorSpace.Properties/1\n";
    public static string Copy(DesignNode source, PropertyGroups groups = PropertyGroups.All)
    {
        var packet = new PropertyClipboardPacket { SourceName = source.Name[..Math.Min(source.Name.Length, 256)], Properties = LayerProperties.Capture(source, groups) };
        Validate(packet.Properties);
        var result = Prefix + JsonSerializer.Serialize(packet, PropertyClipboardJson.Default.PropertyClipboardPacket);
        if (result.Length > DocumentJson.MaxDocumentCharacters) throw new InvalidDataException("Property clipboard exceeds the 32 MiB text limit.");
        return result;
    }
    public static PropertyClipboardPacket Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > DocumentJson.MaxDocumentCharacters || !text.StartsWith(Prefix, StringComparison.Ordinal)) throw new InvalidDataException("The clipboard does not contain supported VectorSpace properties.");
        var packet = JsonSerializer.Deserialize(text.AsSpan(Prefix.Length), PropertyClipboardJson.Default.PropertyClipboardPacket) ?? throw new InvalidDataException("Empty property clipboard.");
        if (packet.Version != 1 || packet.SourceName is null || packet.SourceName.Length > 256 || packet.Properties is null) throw new InvalidDataException("Invalid property clipboard version or metadata.");
        Validate(packet.Properties); return packet;
    }
    public static void Validate(LayerProperties p)
    {
        ArgumentNullException.ThrowIfNull(p);
        long characters = 0;
        // Reuse the portable native paint/image guards without creating a document or cloning a subtree.
        var node = new DesignNode { Fills = p.Fills ?? [], Strokes = p.Strokes ?? [], Shadows = p.Effects ?? [] };
        AppearanceValidation.Validate(node, ref characters);
        ValidateTypography(p.Typography); ValidateScalars(p.Opacity, p.Blend, p.CornerRadius);
    }
    internal static void ValidateTypography(TypographyStyle? t)
    {
        if (t is null) return;
        if (string.IsNullOrWhiteSpace(t.FontFamily) || t.FontFamily.Length > 256 || t.FontFamily.Any(char.IsControl) ||
            !double.IsFinite(t.FontSize) || t.FontSize is < 1 or > 4096 || t.FontWeight is < 1 or > 1000 ||
            !double.IsFinite(t.LineHeight) || t.LineHeight is < .2 or > 10 || !double.IsFinite(t.LetterSpacing) || Math.Abs(t.LetterSpacing) > 1e5 || !Enum.IsDefined(t.Alignment))
            throw new InvalidDataException("Invalid transferred typography.");
    }
    internal static void ValidateScalars(double? opacity, BlendKind? blend, double? radius)
    {
        if (opacity is { } o && (!double.IsFinite(o) || o is < 0 or > 1) || blend is { } b && !Enum.IsDefined(b) || radius is { } r && (!double.IsFinite(r) || r is < 0 or > 1e6))
            throw new InvalidDataException("Invalid transferred appearance.");
    }
    internal static void ValidateStrokes(List<StrokeStyle> strokes)
    {
        if (strokes.Count > 64) throw new InvalidDataException("A layer is limited to 64 strokes.");
        foreach (var s in strokes)
            if (s is null || s.Color is null || !double.IsFinite(s.Width) || s.Width is < 0 or > 1e5 || !double.IsFinite(s.Opacity) || s.Opacity is < 0 or > 1 ||
                s.Dashes is null || s.Dashes.Count > 128 || s.Dashes.Any(d => !double.IsFinite(d) || d < 0 || d > 1e5) || s.Dashes.Count > 0 && s.Dashes.All(d => d == 0))
                throw new InvalidDataException("Invalid stroke or dash pattern.");
    }
}
