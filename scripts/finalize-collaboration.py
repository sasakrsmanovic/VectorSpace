"""One-time reviewed fixes applied with the guarded integration, then removed."""
from pathlib import Path

def replace(path, old, new, count=1):
    p=Path(path); text=p.read_text(); assert text.count(old)==count, (path, old[:100], text.count(old), count); p.write_text(text.replace(old,new))

presence='src/VectorSpace.Editor/RemotePresenceLayer.cs'
replace(presence, 'm.Map(new(n.Width, 0))', 'm.Map(new Vec2(n.Width, 0))')
replace(presence, 'm.Map(new(n.Width, n.Height))', 'm.Map(new Vec2(n.Width, n.Height))')
replace(presence, 'm.Map(new(0, n.Height))', 'm.Map(new Vec2(0, n.Height))')
replace(presence, 'public void Dispose()', 'public new void Dispose()')

replica='src/VectorSpace.Collaboration/SharedReplica.cs'
replace(replica, 'public sealed class SharedReplica', 'public sealed partial class SharedReplica')
replace(replica, 'public bool CanEdit => _pending.Count < 8 && Recovery.Count < 8;', 'public bool CanEdit => _pending.Count < 8 && Recovery.Count == 0 && !_projectionConflict;')
replace(replica, 'var next = DocumentProjection.FromJson(documentJson, Visible);', 'CheckPendingBudget(documentJson);\n        var next = DocumentProjection.FromJson(documentJson, Visible);')
replace(replica, 'foreach (var pending in _pending) DocumentProjection.Apply(Visible, pending.Batch.Changes, -pending.Batch.Sequence);', 'foreach (var pending in _pending) DocumentProjection.Apply(Visible, pending.Batch.Changes, -pending.Batch.Sequence);\n        ValidateOptimisticProjection();')
replace(replica, 'while (destination.Count > 150) destination.RemoveAt(0);', 'while (destination.Count > 150) destination.RemoveAt(0);\n        TrimSharedHistory();')

bridge='src/VectorSpace.Workbench/StudioWorkbench.Collaboration.cs'
replace(bridge, '''    private Task ReceiveShared(Action action, bool changesModel)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = DispatchShared(() =>''', '''    private async Task ReceiveShared(Action action, bool changesModel)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await DispatchShared(() =>''')
replace(bridge, '''        });
        return done.Task;
    }
    private void FlushRemoteDeliveries()''', '''        });
        if (_disposed || _collaboration is null) done.TrySetResult();
        await done.Task;
    }
    private void FlushRemoteDeliveries()''')
replace(bridge, '_participantName = name.Trim(); _collaboration = connection;', '''_participantName = name.Trim(); _collaboration = connection;
            _sharedRecoveryWriter = Guid.NewGuid().ToString("N"); _sharedRecoverySignature = ""; _sharedRecoveryJson = null;''')
replace(bridge, '''        _status.Text = connection.Status +''', '''        var mayEdit = connection.Role is RoomRole.Owner or RoomRole.Editor && !connection.AccessDenied;
        _inspector.IsHitTestVisible = mayEdit;
        foreach (var (tool, button) in _toolButtons)
            button.IsEnabled = mayEdit || tool is EditorTool.Move or EditorTool.Hand || tool == EditorTool.Comment && connection.Role == RoomRole.Commenter;
        _status.Text = connection.Status +''')
replace(bridge, 'private readonly ParticipantStrip _participants = new();', 'private readonly ParticipantStrip _participants = new();')

history='src/VectorSpace.Workbench/StudioWorkbench.SharedHistory.cs'
replace(history, 'Session.DetachSharedHistory(); _presenceTimer.Stop();', 'Session.DetachSharedHistory(); _presenceTimer.Stop(); _inspector.IsHitTestVisible = true;\n        foreach (var button in _toolButtons.Values) button.IsEnabled = true;')
recovery='src/VectorSpace.Workbench/StudioWorkbench.SharedRecovery.cs'
replace(recovery, 'saved.Keys.Where(w => w != _sharedRecoveryWriter)', 'saved.Keys.Where(w => _collaboration is null || w != _sharedRecoveryWriter)')
replace(recovery, '_collaboration?.Replica.Recovery.Clear();', '_collaboration?.Replica.AcknowledgeRecovery();')

commands=Path('src/VectorSpace.Workbench/StudioWorkbench.Commands.cs')
s=commands.read_text(); start=s.index('    private async Task ShowLocalShareAsync()'); end=s.index('    private async Task ShowHelpAsync()',start)
commands.write_text(s[:start]+s[end:])

access='server/VectorSpace.Server/Room.Access.cs'
replace(access, '_participants[p.ClientId] = (grant.Id, p with { Color = color, SeenAt = DateTimeOffset.UtcNow }); Pulse();', '_participants[p.ClientId] = (grant.Id, p with { Color = color, SeenAt = DateTimeOffset.UtcNow }); QueuePresenceNotification();')
program='server/VectorSpace.Server/Program.cs'
replace(program, 'PermitLimit = 3000,', 'PermitLimit = 60000,')
replace(program, 'var api = app.MapGroup("/api/rooms").RequireRateLimiting("api");', '''var api = app.MapGroup("/api/rooms").RequireRateLimiting("api");
api.AddEndpointFilter(async (context, next) =>
{
    // Reject unauthorized callers before reading a potentially large JSON body.
    // Mutation handlers recheck the invitation under the room lock after reading.
    var request = context.HttpContext;
    if (request.Request.RouteValues["id"] is string id)
    {
        var room = repository.Get(id); await room.Gate.WaitAsync(request.RequestAborted);
        try { _ = room.Authorize(Token(request)); } finally { room.Gate.Release(); }
    }
    return await next(context);
});''')
engine='src/VectorSpace.Collaboration/TransactionEngine.cs'
replace(engine, 'batch.Label.Length > 160 ||', 'batch.Label.Length > 160 || batch.Label.Any(char.IsControl) ||')
replace(engine, 'if (change.Before == change.After || change.ExpectedVersion < 0)', '''if (entity == "$root" && property is "id" or "formatVersion") throw new InvalidDataException("Shared file identity and schema are immutable.");
            if (change.Before == change.After || change.ExpectedVersion < 0)''')

print('Lifecycle and admission fixes applied.')
