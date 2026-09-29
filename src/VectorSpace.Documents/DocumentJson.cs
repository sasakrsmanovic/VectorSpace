using System.Text.Json;
using System.Text.Json.Serialization;
using VectorSpace.Core;

namespace VectorSpace.Documents;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false, UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DesignDocument))]
[JsonSerializable(typeof(DesignNode))]
[JsonSerializable(typeof(InstanceOverride))]
[JsonSerializable(typeof(List<DesignNode>))]
[JsonSerializable(typeof(List<PrototypeReaction>))]
[JsonSerializable(typeof(List<FillStyle>))]
[JsonSerializable(typeof(List<ShadowStyle>))]
public partial class VectorSpaceJsonContext : JsonSerializerContext;

public static class DocumentJson
{
    public const int MaxDocumentCharacters = 32 * 1024 * 1024;
    public const int MaxNodes = 100_000;
    public static string Save(DesignDocument document) => JsonSerializer.Serialize(document, VectorSpaceJsonContext.Default.DesignDocument);
    public static DesignDocument Load(string json)
    {
        if (json.Length > MaxDocumentCharacters) throw new InvalidDataException("The document exceeds the 32 MiB text limit.");
        var document = JsonSerializer.Deserialize(json, VectorSpaceJsonContext.Default.DesignDocument) ?? throw new InvalidDataException("The file does not contain a VectorSpace document.");
        Validate(document); document.FormatVersion = DesignDocument.CurrentFormatVersion; document.RebuildParents(); return document;
    }
    public static DesignNode CloneNode(DesignNode node, bool newIds = false)
    {
        var clone = JsonSerializer.Deserialize(JsonSerializer.Serialize(node, VectorSpaceJsonContext.Default.DesignNode), VectorSpaceJsonContext.Default.DesignNode)!;
        Attach(clone, null);
        if (newIds) RegenerateIds([clone]);
        return clone;
        static void Attach(DesignNode n, DesignNode? parent) { n.Parent = parent; foreach (var c in n.Children) Attach(c, n); }
    }
    public static List<PrototypeReaction> CloneReactions(List<PrototypeReaction> reactions) => JsonSerializer.Deserialize(JsonSerializer.Serialize(reactions, VectorSpaceJsonContext.Default.ListPrototypeReaction), VectorSpaceJsonContext.Default.ListPrototypeReaction)!;
    public static string SaveNodes(IEnumerable<DesignNode> nodes) => JsonSerializer.Serialize(nodes.ToList(), VectorSpaceJsonContext.Default.ListDesignNode);
    public static List<DesignNode> LoadNodes(string json)
    {
        if (json.Length > MaxDocumentCharacters) throw new InvalidDataException("Clipboard content is too large.");
        var nodes = JsonSerializer.Deserialize(json, VectorSpaceJsonContext.Default.ListDesignNode) ?? [];
        var doc = new DesignDocument { Pages = [new() { Nodes = nodes }] }; Validate(doc); doc.RebuildParents(); RegenerateIds(nodes); return nodes;
    }
    public static void RegenerateIds(IEnumerable<DesignNode> roots)
    {
        var nodes = roots.SelectMany(n => n.DescendantsAndSelf()).ToArray();
        var ids = nodes.ToDictionary(n => n.Id, _ => Guid.NewGuid().ToString("N"));
        foreach (var node in nodes)
        {
            node.Id = ids[node.Id];
            PrototypeValidation.Remap(node, ids);
            if (node.PrototypeTargetId is { } target && ids.TryGetValue(target, out var replacement)) node.PrototypeTargetId = replacement;
            if (node.ComponentId is { } component && ids.TryGetValue(component, out replacement)) node.ComponentId = replacement;
        }
    }
    public static void Validate(DesignDocument document)
    {
        if (document.FormatVersion is < 1 or > DesignDocument.CurrentFormatVersion) throw new InvalidDataException($"Unsupported VectorSpace format version {document.FormatVersion}.");
        if (document.Pages is null || document.Pages.Count is < 1 or > 1000) throw new InvalidDataException("A document must have between 1 and 1000 pages.");
        var ids = new HashSet<string>(StringComparer.Ordinal); var count = 0; long imageCharacters = 0;
        foreach (var page in document.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Id) || !ids.Add(page.Id) || page.Nodes is null) throw new InvalidDataException("Invalid or duplicate page identifier.");
            foreach (var node in page.Nodes) Check(node, 0);
        }
        VariableResolver.Validate(document);
        PrototypeValidation.Validate(document);
        void Check(DesignNode n, int depth)
        {
            if (++count > MaxNodes || depth > 60) throw new InvalidDataException("Document node count or nesting limit exceeded.");
            if (n is null || string.IsNullOrWhiteSpace(n.Id) || !ids.Add(n.Id)) throw new InvalidDataException("Invalid or duplicate layer identifier.");
            if (!double.IsFinite(n.X) || !double.IsFinite(n.Y) || !double.IsFinite(n.Width) || !double.IsFinite(n.Height) || !double.IsFinite(n.Rotation) || n.Width < 0 || n.Height < 0 || n.Width > 1e7 || n.Height > 1e7 || Math.Abs(n.X) > 1e9 || Math.Abs(n.Y) > 1e9) throw new InvalidDataException("A layer has invalid geometry.");
            if (n.Children is null || n.Fills is null || n.Strokes is null || n.Shadows is null || n.Layout is null || n.Points is null || n.Overrides is null || n.VariantProperties is null || n.VariableBindings is null || n.VariableModes is null) throw new InvalidDataException("A layer is missing required data.");
            if (!double.IsFinite(n.MinWidth) || !double.IsFinite(n.MinHeight) || !double.IsFinite(n.MaxWidth) || !double.IsFinite(n.MaxHeight) || n.MinWidth < 0 || n.MinHeight < 0 || n.MaxWidth < n.MinWidth || n.MaxHeight < n.MinHeight || n.MaxWidth > 1e7 || n.MaxHeight > 1e7) throw new InvalidDataException("Invalid size limits.");
            var l = n.Layout;
            if (new[] { l.Gap, l.CrossGap, l.PaddingLeft, l.PaddingRight, l.PaddingTop, l.PaddingBottom }.Any(v => !double.IsFinite(v) || Math.Abs(v) > 1e7)) throw new InvalidDataException("Invalid auto-layout geometry.");
            if (l.GridColumns is < 1 or > 128 || l.Columns is null || l.Rows is null || l.Columns.Count > 128 || l.Rows.Count > 10000 || n.ColumnSpan is < 1 or > 128 || n.RowSpan is < 1 or > 128 || n.GridColumn is < -1 or > 127 || n.GridRow is < -1 or > 10000) throw new InvalidDataException("Invalid grid placement.");
            foreach (var track in l.Columns.Concat(l.Rows))
                if (track is null || !double.IsFinite(track.Value) || !double.IsFinite(track.Min) || !double.IsFinite(track.Max) || track.Value < 0 || track.Min < 0 || track.Max < track.Min || track.Max > 1e7) throw new InvalidDataException("Invalid grid track.");
            ShapeValidation.Validate(n);
            AppearanceValidation.Validate(n, ref imageCharacters);
            n.Opacity = Numbers.Clamp(n.Opacity, 0, 1); n.FontSize = Numbers.Clamp(n.FontSize, 1, 4096);
            n.CornerRadius = Numbers.Clamp(n.CornerRadius, 0, 1e6); n.Sides = Math.Clamp(n.Sides, 3, 128);
            n.StarRatio = Numbers.Clamp(n.StarRatio, .01, 1); n.LineHeight = Numbers.Clamp(n.LineHeight, .2, 10);
            if (n.Points.Any(p => !p.Position.IsFinite || (p.ControlIn.HasValue && !p.ControlIn.Value.IsFinite) || (p.ControlOut.HasValue && !p.ControlOut.Value.IsFinite))) throw new InvalidDataException("A path contains invalid points.");
            if (n.Kind == NodeKind.ComponentSet && n.Children.Any(c => c.Kind != NodeKind.Component)) throw new InvalidDataException("Component sets can contain only component definitions.");
            foreach (var child in n.Children) Check(child, depth + 1);
        }
    }
}

public interface IWorkspaceStorage
{
    Task<string?> ReadAutosaveAsync(CancellationToken cancellationToken = default);
    Task WriteAutosaveAsync(string document, CancellationToken cancellationToken = default);
    Task<(string Name, string Text)?> OpenAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string name, byte[] bytes, string contentType, CancellationToken cancellationToken = default);
    Task<(string Name, byte[] Bytes)?> OpenImageAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This host does not provide an image picker.");
}
