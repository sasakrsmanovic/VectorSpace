using System.Text.Json;
using System.Text.Json.Nodes;

namespace VectorSpace.Collaboration;

/// <summary>Single-owner authoritative state. Prepare is side-effect free; hosts persist its event
/// before Accept. A conflict rejects the entire gesture rather than silently overwriting a peer.</summary>
public sealed class TransactionEngine
{
    public SharedSnapshot State { get; private set; }
    public TransactionEngine(SharedSnapshot state)
    {
        _ = DocumentProjection.ToDocument(state);
        if (state.Revision < 0 || state.Versions.Values.Any(v => v < 0 || v > state.Revision)) throw new InvalidDataException("Invalid shared versions.");
        State = state.Clone();
    }
    public Commit Prepare(EditBatch batch, RoomRole role, string author)
    {
        if (role == RoomRole.Viewer) throw new UnauthorizedAccessException("This invitation can view but cannot edit.");
        if (!Enum.IsDefined(role) || string.IsNullOrWhiteSpace(batch.Id) || batch.Id.Length > 96 || string.IsNullOrWhiteSpace(batch.ClientId) || batch.ClientId.Length > 96 || batch.Sequence <= 0 || batch.BaseRevision < 0 || batch.BaseRevision > State.Revision)
            throw new InvalidDataException("Invalid transaction identity or revision.");
        if (batch.Label is null || batch.Label.Length > 160 || batch.Changes is null || batch.Changes.Count is 0 or > 50_000 || batch.Changes.Any(c => c is null) || batch.Changes.Sum(c => (long)(c.Before?.Length ?? 0) + (c.After?.Length ?? 0)) > 40L * 1024 * 1024)
            throw new InvalidDataException("Transaction limits exceeded.");
        var seen = new HashSet<string>(StringComparer.Ordinal); var actual = new List<CellChange>(batch.Changes.Count);
        foreach (var change in batch.Changes)
        {
            if (change.Key is null || change.Key.Length > 1024 || !seen.Add(change.Key)) throw new InvalidDataException("Duplicate or invalid changed cell.");
            var (entity, property) = DocumentProjection.Address(change.Key);
            if (role == RoomRole.Commenter && !entity.StartsWith("comment:", StringComparison.Ordinal)) throw new UnauthorizedAccessException("This invitation can comment but cannot change the design.");
            if (change.Before == change.After || change.ExpectedVersion < 0) throw new InvalidDataException("No-op or invalid transaction cell.");
            var value = State.Value(change.Key);
            if (State.Version(change.Key) != change.ExpectedVersion || value != change.Before)
            {
                if (entity.StartsWith("comment:", StringComparison.Ordinal) && property == "replies" && TryAppend(change.Before, change.After, value, out var appended))
                    actual.Add(new(change.Key, value, appended, State.Version(change.Key)));
                else throw new CollaborationConflictException(change.Key);
            }
            else actual.Add(change);
        }
        var candidate = State.Clone(); var revision = checked(State.Revision + 1);
        DocumentProjection.Apply(candidate, actual, revision); candidate.Revision = revision;
        if (candidate.Versions.Count > DocumentProjection.MaxCells * 2) throw new InvalidDataException("Room tombstone budget reached; export to a new room.");
        _ = DocumentProjection.ToDocument(candidate);
        return new(revision, batch.Id, batch.ClientId, batch.Sequence, author, batch.Label, DateTimeOffset.UtcNow, actual);
    }
    public void Accept(Commit commit)
    {
        if (commit.Revision != State.Revision + 1) throw new InvalidOperationException("Noncontiguous shared commit.");
        DocumentProjection.Apply(State, commit.Changes, commit.Revision); State.Revision = commit.Revision;
    }
    private static bool TryAppend(string? before, string? after, string? current, out string? merged)
    {
        merged = null;
        if (before is null || after is null || current is null) return false;
        try
        {
            if (JsonNode.Parse(before) is not JsonArray b || JsonNode.Parse(after) is not JsonArray a || JsonNode.Parse(current) is not JsonArray c || a.Count <= b.Count || c.Count < b.Count) return false;
            for (var i = 0; i < b.Count; i++) if (!JsonNode.DeepEquals(b[i], a[i]) || !JsonNode.DeepEquals(b[i], c[i])) return false;
            for (var i = b.Count; i < a.Count; i++) c.Add(a[i]?.DeepClone());
            merged = c.ToJsonString(); return true;
        }
        catch (JsonException) { return false; }
    }
}
public sealed class CollaborationConflictException(string key) : InvalidOperationException("Another participant changed " + DocumentProjection.Address(key).Property + "; this complete edit was kept for recovery.")
{
    public string Key { get; } = key;
}
