using System.Net;

namespace VectorSpace.Collaboration;

/// <summary>Network lifecycle with explicit UI dispatch. All replica calls and Changed callbacks run
/// through the supplied dispatcher; no network thread mutates the host's scene.</summary>
public sealed partial class CollaborationConnection : IDisposable
{
    private readonly Func<Action, Task> _dispatch;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _sendSignal = new(0, 1);
    private Task[] _loops = [];
    private Participant? _presence;
    private long _presenceVersion, _event;
    private bool _disposed, _started;
    public RoomTransport Transport { get; }
    public SharedReplica Replica { get; }
    public RoomRole Role { get; private set; }
    public bool Online { get; private set; } = true;
    public bool AccessDenied { get; private set; }
    public string Status { get; private set; } = "Connected";
    public IReadOnlyList<Participant> Participants { get; private set; } = [];
    public event Action<bool>? Changed;
    private CollaborationConnection(RoomTransport transport, SyncReply initial, Func<Action, Task> dispatch)
    {
        Transport = transport; _dispatch = dispatch; Role = initial.Role; _event = initial.Event;
        Replica = new(initial.Snapshot ?? throw new InvalidDataException("The server did not provide an initial snapshot."));
        Participants = initial.Participants;
    }
    public static async Task<CollaborationConnection> JoinAsync(RoomAddress address, Func<Action, Task> dispatch, CancellationToken cancellationToken = default)
    {
        var transport = new RoomTransport(address);
        try { return new(transport, await transport.SyncAsync(-1, -1, false, cancellationToken), dispatch); }
        catch { transport.Dispose(); throw; }
    }
    public void Start()
    {
        if (_started || _disposed) return; _started = true;
        _loops = [PollLoop(_stop.Token), SendLoop(_stop.Token), PresenceLoop(_stop.Token)];
    }
    public bool CanEdit => !_disposed && !AccessDenied && Role != RoomRole.Viewer && Replica.CanEdit;
    public void Submit(string document, string label)
    {
        if (!CanEdit) throw new InvalidOperationException("This shared file is read-only or is waiting for queued edits to synchronize.");
        Replica.Submit(document, label); Wake(); Changed?.Invoke(false);
    }
    public void Undo() { if (CanEdit) { var changed = Replica.Undo(); Wake(); Changed?.Invoke(changed); } }
    public void Redo() { if (CanEdit) { var changed = Replica.Redo(); Wake(); Changed?.Invoke(changed); } }
    public void SetPresence(Participant presence) { if (_disposed) return; _presence = presence with { ClientId = Replica.ClientId }; _presenceVersion++; }
    private void Wake() { if (_disposed) return; try { _sendSignal.Release(); } catch (SemaphoreFullException) { } }
    private void Receive(SyncReply reply)
    {
        if (_disposed) return;
        var hasData = reply.Snapshot is not null || reply.Commits.Count != 0 || reply.Acknowledged is not null || reply.Receipt is not null;
        var changed = false;
        if (hasData)
        {
            var before = Replica.Visible; Replica.Receive(reply);
            if (reply.Snapshot is not null || reply.Receipt is { Accepted: false })
                changed = before.Cells.Count != Replica.Visible.Cells.Count || before.Cells.Any(p => Replica.Visible.Value(p.Key) != p.Value);
            else
                changed = reply.Commits.Concat(reply.Acknowledged is { } ack ? new[] { ack } : []).SelectMany(c => c.Changes).Any(c => before.Value(c.Key) != Replica.Visible.Value(c.Key));
        }
        Role = reply.Role; _event = reply.Event; Online = true;
        Status = Replica.LastError ?? (Replica.PendingCount == 0 ? "All changes synchronized" : $"Synchronizing {Replica.PendingCount} edit(s)");
        Participants = reply.Participants.Where(p => p.ClientId != Replica.ClientId).ToArray();
        Changed?.Invoke(changed); if (Replica.PendingCount > 0) Wake();
    }
    private async Task Fault(Exception error, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        await _dispatch(() => {
            if (_disposed) return;
            Online = false;
            AccessDenied = error is HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound };
            Status = AccessDenied ? "Access revoked or room unavailable. Save a local copy before leaving." : "Reconnecting; unsent edits remain in this window. " + error.Message;
            Changed?.Invoke(false);
            if (AccessDenied) _stop.Cancel();
        });
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _stop.Cancel(); Changed = null;
        _ = FinishDisposeAsync();
    }
    private async Task FinishDisposeAsync()
    {
        try { await Task.WhenAll(_loops); } catch (OperationCanceledException) { }
        try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await Transport.LeaveAsync(Replica.ClientId, timeout.Token); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException) { }
        finally { Transport.Dispose(); _stop.Dispose(); _sendSignal.Dispose(); }
    }
}
