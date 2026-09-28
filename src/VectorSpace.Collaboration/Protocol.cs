using System.Text.Json.Serialization;

namespace VectorSpace.Collaboration;

public enum RoomRole { Viewer, Commenter, Editor, Owner }

/// <summary>Null means an absent cell. The string "null" is an explicit JSON null.</summary>
public sealed record CellChange(string Key, string? Before, string? After, long ExpectedVersion = 0);
public sealed record EditBatch(string Id, string ClientId, long Sequence, string Label, long BaseRevision, List<CellChange> Changes);
public sealed record Commit(long Revision, string Id, string ClientId, long Sequence, string Author, string Label, DateTimeOffset Time, List<CellChange> Changes);
public sealed record Receipt(string Id, long Sequence, bool Accepted, long Revision, string? Error);
public sealed record Participant(string ClientId, string Name, string Color, string PageId, double X, double Y, bool HasCursor,
    double Zoom, double PanX, double PanY, List<string> Selection, DateTimeOffset SeenAt);
public sealed record HistoryItem(long Revision, string Author, string Label, DateTimeOffset Time);
public sealed record RoomGrant(string Id, string Label, RoomRole Role, DateTimeOffset CreatedAt);
public sealed record Invitation(string RoomId, string Token, RoomRole Role, string GrantId);
public sealed record CreateRoom(string Document, string Name);
public sealed record CreateInvitation(string Label, RoomRole Role);
public sealed record RoomInfo(string RoomId, RoomRole Role, string Name);
public sealed class SharedSnapshot
{
    public long Revision { get; set; }
    public Dictionary<string, string> Cells { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> Versions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, Receipt> Receipts { get; set; } = new(StringComparer.Ordinal);
    public SharedSnapshot Clone() => new() { Revision = Revision, Cells = new(Cells, StringComparer.Ordinal), Versions = new(Versions, StringComparer.Ordinal), Receipts = new(Receipts, StringComparer.Ordinal) };
    public string? Value(string key) => Cells.GetValueOrDefault(key);
    public long Version(string key) => Versions.GetValueOrDefault(key);
}
public sealed class SyncReply
{
    public RoomRole Role { get; set; }
    public long Event { get; set; }
    public long Revision { get; set; }
    public SharedSnapshot? Snapshot { get; set; }
    public List<Commit> Commits { get; set; } = [];
    public List<Participant> Participants { get; set; } = [];
    public Receipt? Receipt { get; set; }
}
public sealed record RecoveryEdit(string Label, string Reason, string Document, DateTimeOffset Time);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SharedSnapshot))]
[JsonSerializable(typeof(SyncReply))]
[JsonSerializable(typeof(EditBatch))]
[JsonSerializable(typeof(Commit))]
[JsonSerializable(typeof(Receipt))]
[JsonSerializable(typeof(CreateRoom))]
[JsonSerializable(typeof(Invitation))]
[JsonSerializable(typeof(CreateInvitation))]
[JsonSerializable(typeof(RoomInfo))]
[JsonSerializable(typeof(Participant))]
[JsonSerializable(typeof(List<HistoryItem>))]
[JsonSerializable(typeof(List<RoomGrant>))]
[JsonSerializable(typeof(List<RecoveryEdit>))]
public partial class CollaborationJson : JsonSerializerContext;
