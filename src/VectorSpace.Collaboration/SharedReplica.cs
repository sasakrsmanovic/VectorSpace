using VectorSpace.Documents;

namespace VectorSpace.Collaboration;

/// <summary>Single-thread-owned optimistic replica. Transport must serialize access. Pending edits
/// retain a recoverable native file and never overwrite a server conflict silently.</summary>
public sealed class SharedReplica
{
    private sealed class Entry(Commit commit)
    {
        public Commit Commit { get; } = commit;
        public Dictionary<string, long> Guards { get; } = commit.Changes.ToDictionary(c => c.Key, _ => commit.Revision, StringComparer.Ordinal);
    }
    private sealed record Pending(EditBatch Batch, string Document, int HistoryDirection, Entry? Source);
    private readonly List<Pending> _pending = [];
    private readonly List<Entry> _undo = [], _redo = [];
    private long _sequence;
    public string ClientId { get; }
    public SharedSnapshot Confirmed { get; private set; }
    public SharedSnapshot Visible { get; private set; }
    public List<RecoveryEdit> Recovery { get; } = [];
    public string? LastError { get; private set; }
    public int PendingCount => _pending.Count;
    public bool CanEdit => _pending.Count < 8 && Recovery.Count < 8;
    public bool CanUndo => _pending.Count == 0 && _undo.Count != 0;
    public bool CanRedo => _pending.Count == 0 && _redo.Count != 0;
    public string UndoLabel => _undo.LastOrDefault()?.Commit.Label ?? "";
    public string RedoLabel => _redo.LastOrDefault()?.Commit.Label ?? "";
    public IReadOnlyList<string> History => _undo.Select(e => e.Commit.Label).ToArray();
    public SharedReplica(SharedSnapshot initial, string? clientId = null)
    {
        _ = DocumentProjection.ToDocument(initial);
        ClientId = clientId ?? Guid.NewGuid().ToString("N"); Confirmed = initial.Clone(); Visible = initial.Clone();
    }
    public void Submit(string documentJson, string label)
    {
        if (!CanEdit) throw new InvalidOperationException("Wait for synchronization or save the conflict recovery files before editing further.");
        var next = DocumentProjection.FromJson(documentJson, Visible);
        var changes = DocumentProjection.Diff(Visible, next);
        if (changes.Count == 0) return;
        if (changes.Count > 50_000) throw new InvalidOperationException("This gesture exceeds the shared transaction limit. Save a copy and split the edit.");
        var batch = new EditBatch(Guid.NewGuid().ToString("N"), ClientId, ++_sequence, label[..Math.Min(label.Length, 160)], Confirmed.Revision, changes);
        _pending.Add(new(batch, documentJson, 0, null));
        _redo.Clear(); DocumentProjection.Apply(Visible, changes, -batch.Sequence); LastError = null;
    }
    public bool Undo() => EnqueueHistory(_undo, -1);
    public bool Redo() => EnqueueHistory(_redo, 1);
    private bool EnqueueHistory(List<Entry> stack, int direction)
    {
        if (_pending.Count != 0 || stack.Count == 0) return false;
        var source = stack[^1];
        var changes = source.Commit.Changes.Select(c => new CellChange(c.Key, c.After, c.Before, source.Guards[c.Key])).ToList();
        // Do not optimistically restore any part of a known conflicting history entry.
        if (changes.Any(c => Confirmed.Version(c.Key) != c.ExpectedVersion || Confirmed.Value(c.Key) != c.Before))
        {
            stack.RemoveAt(stack.Count - 1); LastError = "Skipped an undo/redo entry because a collaborator changed its properties."; return false;
        }
        var next = Confirmed.Clone(); DocumentProjection.Apply(next, changes, -(_sequence + 1));
        string json;
        try { json = DocumentProjection.ToJson(next); _ = DocumentJson.Load(json); }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
        {
            stack.RemoveAt(stack.Count - 1); LastError = "Undo/redo would invalidate a collaborator's hierarchy: " + ex.Message; return false;
        }
        stack.RemoveAt(stack.Count - 1);
        var label = (direction < 0 ? "Undo " : "Redo ") + source.Commit.Label;
        var batch = new EditBatch(Guid.NewGuid().ToString("N"), ClientId, ++_sequence, label[..Math.Min(label.Length, 160)], Confirmed.Revision, changes);
        _pending.Add(new(batch, json, direction, source)); Visible = next; return true;
    }
    public EditBatch? NextBatch() => _pending.Count == 0 ? null : _pending[0].Batch with
    {
        BaseRevision = Confirmed.Revision,
        // A negative guard refers to an earlier local pending edit. An unresolved dependency
        // follows a rejected edit and must conflict, never be silently restamped.
        Changes = _pending[0].Batch.Changes.Select(c => c.ExpectedVersion < 0 ? c with { ExpectedVersion = long.MaxValue } : c).ToList()
    };
    public void Receive(SyncReply reply)
    {
        if (reply.Snapshot is { } snapshot && snapshot.Revision >= Confirmed.Revision)
        {
            _ = DocumentProjection.ToDocument(snapshot); Confirmed = snapshot.Clone();
        }
        foreach (var commit in reply.Commits.OrderBy(c => c.Revision))
        {
            if (commit.Revision > Confirmed.Revision)
            {
                if (commit.Revision != Confirmed.Revision + 1) throw new InvalidDataException("Missing collaboration revision; a fresh snapshot is required.");
                DocumentProjection.Apply(Confirmed, commit.Changes, commit.Revision); Confirmed.Revision = commit.Revision;
            }
            Complete(commit);
        }
        if (reply.Acknowledged is { } acknowledged) Complete(acknowledged);
        if (reply.Receipt is { Accepted: false } receipt)
        {
            var index = _pending.FindIndex(p => p.Batch.Id == receipt.Id);
            if (index >= 0)
            {
                var pending = _pending[index]; _pending.RemoveAt(index);
                LastError = receipt.Error ?? "The server rejected this edit.";
                Recovery.Add(new(pending.Batch.Label, LastError, pending.Document, DateTimeOffset.UtcNow));
            }
        }
        Visible = Confirmed.Clone();
        foreach (var pending in _pending) DocumentProjection.Apply(Visible, pending.Batch.Changes, -pending.Batch.Sequence);
    }
    private void Complete(Commit commit)
    {
        if (commit.ClientId != ClientId) return;
        var index = _pending.FindIndex(p => p.Batch.Id == commit.Id);
        if (index < 0) return;
        var pending = _pending[index]; _pending.RemoveAt(index);
        for (var i = 0; i < _pending.Count; i++)
        {
            var item = _pending[i];
            _pending[i] = item with { Batch = item.Batch with { Changes = item.Batch.Changes.Select(c => c.ExpectedVersion == -commit.Sequence ? c with { ExpectedVersion = commit.Revision } : c).ToList() } };
        }
        if (pending.Source is { } source)
        {
            // Retarget only a guard whose precise previous version was exposed by this inverse.
            // This permits progressive own undo/redo without weakening guards against peer edits.
            foreach (var original in source.Commit.Changes)
                foreach (var entry in _undo.Concat(_redo))
                    if (entry.Guards.TryGetValue(original.Key, out var guard) && guard == original.ExpectedVersion &&
                        entry.Commit.Changes.Any(c => c.Key == original.Key && c.After == original.Before))
                        entry.Guards[original.Key] = commit.Revision;
        }
        var destination = pending.HistoryDirection < 0 ? _redo : _undo;
        destination.Add(new(commit));
        while (destination.Count > 150) destination.RemoveAt(0);
    }
    public string VisibleJson() => DocumentProjection.ToJson(Visible);
}
