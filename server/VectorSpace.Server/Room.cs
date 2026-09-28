using VectorSpace.Collaboration;
using VectorSpace.Documents;

namespace VectorSpace.Server;

internal sealed partial class Room
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    private readonly string _metadataPath, _journalPath;
    private RoomMetadata _metadata;
    private readonly TransactionEngine _engine;
    private readonly Dictionary<string, JournalRecord> _receipts = new(StringComparer.Ordinal);
    private readonly List<Commit> _events = [];
    private readonly List<HistoryItem> _history = [];
    private readonly Dictionary<string, (string Grant, Participant Presence)> _participants = new(StringComparer.Ordinal);
    private TaskCompletionSource _changed = NewSignal();
    private long _event, _eventBytes;
    private bool _faulted;
    public string Id => _metadata.Id;
    public string Name => _metadata.Name;
    public long Revision => _engine.State.Revision;
    public Task Changed => _changed.Task;
    public long Event => _event;
    public Room(string metadataPath, RoomMetadata metadata)
    {
        _metadataPath = metadataPath; _journalPath = Path.ChangeExtension(metadataPath, ".journal");
        _metadata = metadata; _engine = new(metadata.Initial);
        foreach (var record in RoomJournal.Read(_journalPath))
        {
            if (record.Commit is { } commit) { _engine.Accept(commit); Remember(commit); }
            _receipts[record.Actor] = record;
        }
    }
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void Pulse() { _event++; var previous = _changed; _changed = NewSignal(); previous.TrySetResult(); }
    public SyncReply Sync(GrantRecord grant, long since)
    {
        PurgePresence();
        var stale = since < 0 || since > Revision || since < Revision && (_events.Count == 0 || since < _events[0].Revision - 1);
        return new()
        {
            Role = grant.Role, Event = _event, Revision = Revision,
            Snapshot = stale ? _engine.State.Clone() : null,
            Commits = stale ? [] : _events.Where(c => c.Revision > since).ToList(),
            Participants = _participants.Values.Select(v => v.Presence).ToList()
        };
    }
    private void Remember(Commit commit)
    {
        _events.Add(commit); _eventBytes += Cost(commit);
        while (_events.Count > 256 || _eventBytes > 8 * 1024 * 1024)
        { _eventBytes -= Cost(_events[0]); _events.RemoveAt(0); }
        _history.Add(new(commit.Revision, commit.Author, commit.Label, commit.Time));
        if (_history.Count > 1000) _history.RemoveAt(0);
        static long Cost(Commit c) => c.Changes.Sum(p => (long)p.Key.Length + (p.Before?.Length ?? 0) + (p.After?.Length ?? 0));
    }
    public void Leave(GrantRecord grant, string client)
    {
        if (_participants.TryGetValue(client, out var value) && value.Grant == grant.Id) { _participants.Remove(client); Pulse(); }
    }
    private void PurgePresence()
    {
        var expired = _participants.Where(p => p.Value.Presence.SeenAt < DateTimeOffset.UtcNow.AddSeconds(-30)).Select(p => p.Key).ToArray();
        foreach (var id in expired) _participants.Remove(id);
        if (expired.Length > 0) Pulse();
    }
    public List<HistoryItem> History() => _history.TakeLast(100).Reverse().ToList();
    public string Version(long revision)
    {
        if (revision < 0 || revision > Revision) throw new InvalidDataException("Unknown room revision.");
        var snapshot = _metadata.Initial.Clone();
        foreach (var record in RoomJournal.Read(_journalPath))
        {
            if (record.Commit is not { } c) continue;
            if (c.Revision > revision) break;
            DocumentProjection.Apply(snapshot, c.Changes, c.Revision); snapshot.Revision = c.Revision;
        }
        return DocumentJson.Save(DocumentProjection.ToDocument(snapshot));
    }
}
