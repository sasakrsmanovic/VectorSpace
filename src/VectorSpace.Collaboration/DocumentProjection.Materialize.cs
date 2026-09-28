using System.Text.Json;
using System.Text.Json.Nodes;
using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Collaboration;

public static partial class DocumentProjection
{
    public static DesignDocument ToDocument(SharedSnapshot state) => DocumentJson.Load(ToJson(state));
    public static string ToJson(SharedSnapshot state)
    {
        if (state.Cells.Count > MaxCells || state.Cells.Sum(p => (long)p.Key.Length + p.Value.Length) > MaxCharacters) throw new InvalidDataException("Shared document exceeds its limits.");
        var entities = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var (key, value) in state.Cells)
        {
            var (entity, property) = Address(key);
            if (!entities.TryGetValue(entity, out var obj)) entities.Add(entity, obj = new());
            obj.Add(property, JsonNode.Parse(value, documentOptions: new JsonDocumentOptions { MaxDepth = 96 }));
        }
        if (!entities.TryGetValue("$root", out var root)) throw new InvalidDataException("Missing shared document root.");
        var children = new Dictionary<(string, string), List<(string Id, JsonObject Object, decimal Rank)>>();
        foreach (var (id, obj) in entities)
        {
            if (id == "$root") continue;
            var parent = obj["@parent"]?.GetValue<string>() ?? throw new InvalidDataException("Missing entity parent.");
            var slot = obj["@slot"]?.GetValue<string>() ?? throw new InvalidDataException("Missing entity slot.");
            var rank = obj["@rank"]?.GetValue<decimal>() ?? throw new InvalidDataException("Missing entity order.");
            if (!entities.ContainsKey(parent) || !IsIdentityList(parent, slot) || !Lists.TryGetValue(slot, out var kind) || id != kind + ":" + obj["id"]?.GetValue<string>())
                throw new InvalidDataException("Invalid collaborative hierarchy.");
            if (!children.TryGetValue((parent, slot), out var list)) children[(parent, slot)] = list = [];
            list.Add((id, obj, rank));
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Build("$root", root, 0);
        if (visited.Count != entities.Count) throw new InvalidDataException("Orphaned or cyclic shared hierarchy.");
        return root.ToJsonString(Json);

        void Build(string id, JsonObject obj, int depth)
        {
            if (depth > 100 || !visited.Add(id)) throw new InvalidDataException("Shared hierarchy depth or cycle limit exceeded.");
            obj.Remove("@parent"); obj.Remove("@slot"); obj.Remove("@rank");
            foreach (var slot in Lists.Keys.Where(s => IsIdentityList(id, s)))
            {
                var array = new JsonArray();
                if (children.TryGetValue((id, slot), out var list))
                    foreach (var child in list.OrderBy(x => x.Rank).ThenBy(x => x.Id, StringComparer.Ordinal)) { Build(child.Id, child.Object, depth + 1); array.Add((JsonNode)child.Object); }
                obj[slot] = array;
            }
        }
    }
    public static List<CellChange> Diff(SharedSnapshot before, SharedSnapshot after)
    {
        var changes = new List<CellChange>();
        foreach (var (key, value) in after.Cells)
            if (before.Value(key) != value) changes.Add(new(key, before.Value(key), value, before.Version(key)));
        foreach (var (key, value) in before.Cells)
            if (!after.Cells.ContainsKey(key)) changes.Add(new(key, value, null, before.Version(key)));
        return changes;
    }
    public static void Apply(SharedSnapshot target, IEnumerable<CellChange> changes, long revision)
    {
        foreach (var change in changes)
        {
            if (change.After is null) target.Cells.Remove(change.Key); else target.Cells[change.Key] = change.After;
            target.Versions[change.Key] = revision;
        }
    }
}
