using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Collaboration;

/// <summary>Identity-addressed projection. Values without stable identities are atomic properties.
/// Page/layer/comment/variable/mode membership and ordering are independent cells.</summary>
public static partial class DocumentProjection
{
    private const char Separator = '\u001f';
    public const int MaxCells = 500_000;
    public const int MaxCharacters = 32 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private static readonly Dictionary<string, string> Lists = new(StringComparer.Ordinal)
    {
        ["pages"] = "page", ["nodes"] = "node", ["children"] = "node", ["comments"] = "comment",
        ["variableCollections"] = "collection", ["variables"] = "variable", ["modes"] = "mode"
    };
    public static string Key(string entity, string property) => entity + Separator + property;
    public static (string Entity, string Property) Address(string key)
    {
        var i = key.IndexOf(Separator);
        if (i < 1 || i != key.LastIndexOf(Separator) || i == key.Length - 1) throw new InvalidDataException("Invalid collaborative cell address.");
        return (key[..i], key[(i + 1)..]);
    }
    public static SharedSnapshot FromDocument(DesignDocument document, SharedSnapshot? baseline = null) => FromJson(DocumentJson.Save(document), baseline);
    public static SharedSnapshot FromJson(string json, SharedSnapshot? baseline = null)
    {
        if (json.Length > MaxCharacters) throw new InvalidDataException("Shared documents are limited to 32 MiB of text.");
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 128 }) as JsonObject ?? throw new InvalidDataException("Expected a document object.");
        var result = new SharedSnapshot();
        Flatten(root, "$root", null, null, 0);
        if (result.Cells.Count > MaxCells) throw new InvalidDataException("Shared document cell limit exceeded.");
        return result;

        void Flatten(JsonObject value, string entity, string? parent, string? slot, decimal rank)
        {
            if (entity.Contains(Separator) || entity.Length > 512) throw new InvalidDataException("Invalid shared entity identifier.");
            if (parent is not null)
            {
                Put("@parent", JsonValue.Create(parent)); Put("@slot", JsonValue.Create(slot)); Put("@rank", JsonValue.Create(rank));
            }
            foreach (var property in value)
            {
                if (property.Key.StartsWith('@') || property.Key.Contains(Separator)) throw new InvalidDataException("Reserved collaboration property name.");
                if (entity.StartsWith("node:", StringComparison.Ordinal) && property.Key == "expanded") continue;
                if (property.Value is JsonArray array && Lists.TryGetValue(property.Key, out var kind) && IsIdentityList(entity, property.Key))
                {
                    var objects = array.Select(n => n as JsonObject ?? throw new InvalidDataException("Invalid identity array.")).ToArray();
                    var ids = objects.Select(o => kind + ":" + (o["id"]?.GetValue<string>() ?? throw new InvalidDataException("Missing entity identity."))).ToArray();
                    if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new InvalidDataException("Duplicate identity.");
                    var ranks = Ranks(ids, entity, property.Key, baseline);
                    for (var i = 0; i < objects.Length; i++) Flatten(objects[i], ids[i], entity, property.Key, ranks[i]);
                }
                else Put(property.Key, property.Value);
            }
            void Put(string name, JsonNode? node)
            {
                if (!result.Cells.TryAdd(Key(entity, name), node?.ToJsonString(Json) ?? "null")) throw new InvalidDataException("Duplicate shared entity.");
            }
        }
    }
    private static bool IsIdentityList(string entity, string slot) => entity == "$root"
        ? slot is "pages" or "comments" or "variableCollections" or "variables"
        : entity.StartsWith("page:", StringComparison.Ordinal) ? slot == "nodes"
        : entity.StartsWith("node:", StringComparison.Ordinal) ? slot == "children"
        : entity.StartsWith("collection:", StringComparison.Ordinal) && slot == "modes";

    // Preserve unchanged ranks. Concurrent insertion at the same gap is ordered by identity.
    // Explicit reorder/decimal exhaustion reranks that sibling list under ordinary cell guards.
    private static decimal[] Ranks(string[] ids, string parent, string slot, SharedSnapshot? baseline)
    {
        var values = new decimal?[ids.Length]; decimal? last = null; string? lastId = null;
        var parentJson = JsonValue.Create(parent)!.ToJsonString(); var slotJson = JsonValue.Create(slot)!.ToJsonString();
        for (var i = 0; i < ids.Length; i++)
        {
            if (baseline is null || baseline.Value(Key(ids[i], "@parent")) != parentJson || baseline.Value(Key(ids[i], "@slot")) != slotJson ||
                !decimal.TryParse(baseline.Value(Key(ids[i], "@rank")), NumberStyles.Float, CultureInfo.InvariantCulture, out var rank)) continue;
            if (last is { } prior && (rank < prior || rank == prior && string.CompareOrdinal(ids[i], lastId) <= 0)) return Sequential(ids.Length);
            values[i] = rank; last = rank; lastId = ids[i];
        }
        try
        {
            var result = new decimal[ids.Length]; var start = 0; decimal left = 0;
            while (start < ids.Length)
            {
                if (values[start] is { } known) { result[start++] = known; left = known; continue; }
                var end = start; while (end < ids.Length && values[end] is null) end++;
                var count = end - start;
                if (start == 0) left = end < ids.Length ? values[end]!.Value - count - 1 : 0;
                var step = end < ids.Length ? (values[end]!.Value - left) / (count + 1) : 1;
                if (step <= 0) return Sequential(ids.Length);
                for (var i = start; i < end; i++)
                {
                    var next = left + step;
                    if (next <= left || end < ids.Length && next >= values[end]!.Value) return Sequential(ids.Length);
                    result[i] = next; left = next;
                }
                start = end;
            }
            return result;
        }
        catch (OverflowException) { return Sequential(ids.Length); }
        static decimal[] Sequential(int count) => Enumerable.Range(1, count).Select(i => (decimal)i).ToArray();
    }
}
