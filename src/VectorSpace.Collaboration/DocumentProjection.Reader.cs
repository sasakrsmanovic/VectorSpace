using System.Buffers;
using System.Text;
using System.Text.Json;

namespace VectorSpace.Collaboration;

public static partial class DocumentProjection
{
    /// <summary>Per-call scratch state. A read-only JSON DOM avoids duplicating the document
    /// into mutable JSON nodes, and alternate lookup retains unchanged keys and values.</summary>
    private sealed class ProjectionReader : IDisposable
    {
        private readonly SharedSnapshot? _baseline;
        private readonly SharedSnapshot _result;
        private readonly ArrayBufferWriter<byte> _buffer = new(256);
        private readonly Utf8JsonWriter _writer;
        public ProjectionReader(SharedSnapshot? baseline)
        {
            _baseline = baseline;
            _result = new() { Cells = new(Math.Min(baseline?.Cells.Count ?? 128, MaxCells), StringComparer.Ordinal) };
            _writer = new(_buffer);
        }
        public SharedSnapshot Read(JsonElement root)
        {
            Flatten(root, "$root", null, null, 0);
            return _result;
        }
        private void Flatten(JsonElement value, string entity, string? parent, string? slot, decimal rank)
        {
            if (entity.Contains(Separator) || entity.Length > 512) throw new InvalidDataException("Invalid shared entity identifier.");
            if (parent is not null)
            {
                PutString(entity, "@parent", parent); PutString(entity, "@slot", slot);
                var (key, prior) = Prepare(entity, "@rank"); _writer.WriteNumberValue(rank); Store(key, prior);
            }
            foreach (var property in value.EnumerateObject())
            {
                var name = property.Name;
                if (name.Length == 0 || name.StartsWith('@') || name.Contains(Separator)) throw new InvalidDataException("Reserved collaboration property name.");
                if (entity.StartsWith("node:", StringComparison.Ordinal) && name == "expanded") continue;
                if (property.Value.ValueKind == JsonValueKind.Array && Lists.TryGetValue(name, out var kind) && IsIdentityList(entity, name))
                {
                    var objects = property.Value.EnumerateArray().ToArray();
                    if (objects.Length == 0) continue;
                    var ids = new string[objects.Length]; var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (var i = 0; i < objects.Length; i++)
                    {
                        if (objects[i].ValueKind != JsonValueKind.Object || !objects[i].TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
                            throw new InvalidDataException("Missing entity identity.");
                        ids[i] = kind + ":" + id.GetString();
                        if (!seen.Add(ids[i])) throw new InvalidDataException("Duplicate identity.");
                    }
                    var ranks = Ranks(ids, entity, name, _baseline);
                    for (var i = 0; i < objects.Length; i++) Flatten(objects[i], ids[i], entity, name, ranks[i]);
                }
                else
                {
                    var (key, prior) = Prepare(entity, name); property.Value.WriteTo(_writer); Store(key, prior);
                }
            }
        }
        private void PutString(string entity, string property, string? value)
        {
            var (key, prior) = Prepare(entity, property); _writer.WriteStringValue(value); Store(key, prior);
        }
        private (string Key, string? Prior) Prepare(string entity, string property)
        {
            if (entity.Length + property.Length + 1 > 1024) throw new InvalidDataException("Shared property address is too long.");
            _buffer.Clear(); _writer.Reset(_buffer);
            Span<char> address = stackalloc char[entity.Length + property.Length + 1];
            entity.AsSpan().CopyTo(address); address[entity.Length] = Separator; property.AsSpan().CopyTo(address[(entity.Length + 1)..]);
            if (_baseline is not null && _baseline.Cells.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup) &&
                lookup.TryGetValue(address, out var actualKey, out var prior)) return (actualKey, prior);
            return (new string(address), null);
        }
        private void Store(string key, string? prior)
        {
            _writer.Flush();
            // The default encoder escapes non-ASCII strings. Unequal byte/char sequences
            // safely fall back to UTF-8 decoding rather than assuming equivalent strings.
            var bytes = _buffer.WrittenSpan;
            var unchanged = prior is not null && prior.Length == bytes.Length;
            if (unchanged)
                for (var i = 0; i < bytes.Length; i++)
                    if (prior![i] != bytes[i]) { unchanged = false; break; }
            var value = unchanged ? prior! : Encoding.UTF8.GetString(bytes);
            if (!_result.Cells.TryAdd(key, value)) throw new InvalidDataException("Duplicate shared entity.");
            if (_result.Cells.Count > MaxCells) throw new InvalidDataException("Shared document cell limit exceeded.");
        }
        public void Dispose() => _writer.Dispose();
    }
}
