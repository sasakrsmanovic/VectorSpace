using System.Text.Json;
using VectorSpace.Collaboration;
using VectorSpace.Documents;

namespace VectorSpace.Server;

internal sealed class RoomRepository : IDisposable
{
    private readonly string _directory;
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.Ordinal);
    private readonly FileStream _processLock;
    private readonly object _gate = new();
    public RoomRepository(string directory)
    {
        _directory = Path.GetFullPath(directory); Directory.CreateDirectory(_directory);
        _processLock = new(Path.Combine(_directory, "service.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        foreach (var path in Directory.GetFiles(_directory, "*.room.json"))
        {
            if (_rooms.Count >= 32) throw new InvalidDataException("The service is limited to 32 resident rooms.");
            if (new FileInfo(path).Length > 96L * 1024 * 1024) throw new InvalidDataException("Room metadata is too large.");
            using var stream = File.OpenRead(path);
            var metadata = JsonSerializer.Deserialize(stream, ServerJson.Default.RoomMetadata) ?? throw new InvalidDataException("Invalid room metadata.");
            _rooms.Add(metadata.Id, new(path, metadata));
        }
    }
    public Room Get(string id)
    {
        if (id.Length != 32 || !id.All(Uri.IsHexDigit)) throw new KeyNotFoundException("Unknown room.");
        lock (_gate) return _rooms.GetValueOrDefault(id) ?? throw new KeyNotFoundException("Unknown room.");
    }
    public Invitation Create(CreateRoom request)
    {
        var document = DocumentJson.Load(request.Document); ServerDocument.Normalize(document);
        var state = DocumentProjection.FromDocument(document);
        _ = DocumentProjection.ToDocument(state);
        var (token, grant) = Room.NewGrant(request.Name, RoomRole.Owner);
        lock (_gate)
        {
            if (_rooms.Count >= 32) throw new InvalidOperationException("This service has reached its 32-room capacity.");
            var id = Guid.NewGuid().ToString("N"); var metadata = new RoomMetadata(id, document.Name, state, [grant]);
            var path = Path.Combine(_directory, id + ".room.json");
            RoomJournal.SaveMetadata(path, metadata);
            _rooms.Add(id, new(path, metadata));
            return new(id, token, RoomRole.Owner, grant.Id);
        }
    }
    public void Dispose() => _processLock.Dispose();
}
