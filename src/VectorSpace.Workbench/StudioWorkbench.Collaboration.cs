using Microsoft.UI.Xaml.Input;
using VectorSpace.Collaboration;
using VectorSpace.Editor;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private readonly ParticipantStrip _participants = new();
    private readonly RemotePresenceLayer _presenceLayer = new();
    private readonly DispatcherTimer _presenceTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Queue<(Action Apply, TaskCompletionSource Done)> _remoteDeliveries = new();
    private CollaborationConnection? _collaboration;
    private Vec2? _cursorPosition;
    private string _participantName = "You", _presenceSignature = "";
    private string? _following;
    private int _sharedAsyncDepth, _sharedDialogDepth;
    private bool _flushingRemote, _applyingRemote;
    public bool IsCollaborating => _collaboration is not null;
    public string? PendingInvitation { get; set; }
    public string CollaborationApplicationUrl { get; set; } = "https://wieslawsoltes.github.io/VectorSpace/";

    private bool SharedBoundaryBlocked => Session.IsInteracting || Surface.IsTextEditing || _sharedAsyncDepth > 0 || _sharedDialogDepth > 0;
    private void InitializeCollaboration(Grid canvasArea)
    {
        _presenceLayer.Session = Session; canvasArea.Children.Insert(1, _presenceLayer);
        _participants.Update([new("self", "You", "#AF7C2D")]);
        _participants.ParticipantInvoked += id =>
        {
            if (id is "" or "self") RunAsync(ShowParticipantsAsync);
            else { _following = _following == id ? null : id; RefreshCollaboration(false); }
        };
        Surface.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, e) =>
        {
            var p = e.GetCurrentPoint(Surface).Position; _cursorPosition = new(p.X, p.Y);
        }), true);
        Surface.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => _following = null), true);
        Surface.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler((_, _) => _following = null), true);
        Surface.PointerExited += (_, _) => _cursorPosition = null;
        _presenceTimer.Tick += (_, _) => { FlushRemoteDeliveries(); PublishPresence(); };
        Session.Changed += SharedSessionChanged;
    }
    private Task DispatchShared(Action action)
    {
        if (_disposed) return Task.CompletedTask;
        if (DispatcherQueue.HasThreadAccess) { action(); return Task.CompletedTask; }
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            try { if (!_disposed) action(); done.TrySetResult(); }
            catch (Exception error) { done.TrySetException(error); }
        })) done.TrySetCanceled();
        return done.Task;
    }
    private async Task ReceiveShared(Action action, bool changesModel)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await DispatchShared(() =>
        {
            if (_disposed || _collaboration is null) { done.TrySetResult(); return; }
            if (changesModel && SharedBoundaryBlocked)
            {
                _remoteDeliveries.Enqueue((action, done));
                _status.Text = "Remote changes are waiting for the active edit to finish";
            }
            else { try { action(); done.TrySetResult(); } catch (Exception e) { done.TrySetException(e); } }
        });
        if (_disposed || _collaboration is null) done.TrySetResult();
        await done.Task;
    }
    private void FlushRemoteDeliveries()
    {
        if (_flushingRemote || SharedBoundaryBlocked) return;
        _flushingRemote = true;
        try
        {
            while (!SharedBoundaryBlocked && _remoteDeliveries.TryDequeue(out var item))
            {
                try { if (!_disposed && _collaboration is not null) item.Apply(); item.Done.TrySetResult(); }
                catch (Exception error) { item.Done.TrySetException(error); }
            }
        }
        finally { _flushingRemote = false; }
    }
    private void SharedSessionChanged(object? sender, EditorChangedEventArgs e)
    {
        if (_collaboration is null) return;
        if (e.Kind == EditorChangeKind.Document && !_applyingRemote)
        {
            _presenceLayer.DocumentChanged();
            DispatcherQueue.TryEnqueue(FlushRemoteDeliveries);
        }
        if (e.Kind == EditorChangeKind.Viewport) _presenceLayer.Invalidate();
        if (e.Kind is EditorChangeKind.Document or EditorChangeKind.Selection or EditorChangeKind.Viewport) PublishPresence();
    }
    private async Task ConnectSharedAsync(RoomAddress address, string name)
    {
        if (_collaboration is not null) throw new InvalidOperationException("Leave the current shared file first.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || name.Any(char.IsControl)) throw new ArgumentException("Use a display name of 1–64 characters.");
        Surface.FinishTextEdit(true); Surface.FinishPath(false);
        if (Session.IsInteracting) throw new InvalidOperationException("Finish the current pointer gesture before connecting.");
        ShowStatus("Connecting to " + address.Endpoint().Host);
        var connection = await CollaborationConnection.JoinAsync(address, DispatchShared);
        if (_disposed) { connection.Dispose(); return; }
        try
        {
            Session.Load(DocumentProjection.ToDocument(connection.Replica.Visible));
            _participantName = name.Trim(); _collaboration = connection;
            _sharedRecoveryWriter = Guid.NewGuid().ToString("N"); _sharedRecoverySignature = ""; _sharedRecoveryJson = null; _sharedRecoverySaved = true;
            connection.ReceiveDispatcher = ReceiveShared;
            Session.AttachSharedHistory(new SharedHistoryAdapter(this, connection));
            connection.Changed += RefreshCollaboration;
            PendingInvitation = null; _following = null; _presenceSignature = "";
            Surface.Fit(firstFrame: true); RefreshCollaboration(true); PublishPresence();
            _presenceTimer.Start(); connection.Start();
        }
        catch { _collaboration = null; Session.DetachSharedHistory(); connection.Dispose(); throw; }
    }
    private void RefreshCollaboration(bool documentChanged)
    {
        if (_disposed || _collaboration is not { } connection) return;
        if (documentChanged)
        {
            if (Session.IsInteracting || Surface.IsTextEditing) throw new InvalidOperationException("Remote edit crossed a local transaction boundary.");
            _applyingRemote = true;
            try { Session.ApplySharedDocument(DocumentProjection.ToDocument(connection.Replica.Visible)); }
            finally { _applyingRemote = false; }
            _presenceLayer.DocumentChanged();
        }
        if (_following is { } id)
        {
            var peer = connection.Participants.FirstOrDefault(p => p.ClientId == id);
            if (peer is null) _following = null;
            else if (!SharedBoundaryBlocked)
            {
                var changedPage = Session.Page.Id != peer.PageId;
                if (changedPage) Session.SetPage(peer.PageId);
                var pan = new Vec2(peer.PanX, peer.PanY);
                if (changedPage || Session.Viewport.Zoom != peer.Zoom || Session.Viewport.Pan != pan)
                {
                    Session.Viewport.ZoomAt(peer.Zoom, Vec2.Zero); Session.Viewport.Pan = pan;
                    Session.Notify(EditorChangeKind.Viewport);
                }
            }
        }
        var mayEdit = connection.Role is RoomRole.Owner or RoomRole.Editor && connection.CanEdit;
        _inspector.IsHitTestVisible = mayEdit;
        foreach (var (tool, button) in _toolButtons)
            button.IsEnabled = mayEdit || tool is EditorTool.Move or EditorTool.Hand || tool == EditorTool.Comment && connection.Role == RoomRole.Commenter && connection.CanEdit;
        _status.Text = connection.Status + (connection.Role is RoomRole.Viewer or RoomRole.Commenter ? " · " + connection.Role : "");
        _participants.Update([new("self", _participantName, "#AF7C2D"), .. connection.Participants.Select(p => new ParticipantIdentity(p.ClientId, p.Name, p.Color))], _following);
        _presenceLayer.Visibility = Surface.IsPresenting ? Visibility.Collapsed : Visibility.Visible;
        _presenceLayer.Update(connection.Participants.Select(p => new RemotePeer(p.ClientId, p.Name, p.Color, p.PageId, p.HasCursor ? new Vec2(p.X, p.Y) : null, p.Selection)).ToArray());
        QueueSharedRecovery();
    }
    private void PublishPresence()
    {
        if (_disposed || _collaboration is not { } connection) return;
        var world = _cursorPosition is { } cursor ? Session.Viewport.ScreenToWorld(cursor) : Vec2.Zero;
        var selected = Session.SelectedIds.Take(100).ToList();
        var signature = FormattableString.Invariant($"{Session.Page.Id}|{world.X:0.##}|{world.Y:0.##}|{_cursorPosition.HasValue}|{Surface.IsPresenting}|{Session.Viewport.Zoom:R}|{Session.Viewport.Pan.X:R}|{Session.Viewport.Pan.Y:R}|") + string.Join(',', selected);
        if (_presenceSignature == signature) return; _presenceSignature = signature;
        connection.SetPresence(new(connection.Replica.ClientId, _participantName, "", Session.Page.Id, world.X, world.Y,
            _cursorPosition.HasValue && !Surface.IsPresenting, Session.Viewport.Zoom, Session.Viewport.Pan.X, Session.Viewport.Pan.Y, selected, DateTimeOffset.UtcNow));
    }
    private void DisposeCollaboration()
    {
        _presenceTimer.Stop(); Session.Changed -= SharedSessionChanged;
        var connection = _collaboration; _collaboration = null;
        while (_remoteDeliveries.TryDequeue(out var item)) item.Done.TrySetResult();
        connection?.Dispose(); _presenceLayer.Dispose();
    }
    private sealed class SharedHistoryAdapter(StudioWorkbench host, CollaborationConnection connection) : ISharedEditorHistory
    {
        public bool CanEdit(string label) => connection.CanEdit && (connection.Role != RoomRole.Commenter || label is "Add comment" or "Reply to comment" or "Resolve comment");
        public bool CanUndo => connection.CanEdit && connection.Replica.CanUndo;
        public bool CanRedo => connection.CanEdit && connection.Replica.CanRedo;
        public string UndoLabel => connection.Replica.UndoLabel;
        public string RedoLabel => connection.Replica.RedoLabel;
        public IReadOnlyList<string> History => connection.Replica.History;
        public void Commit(string label, string beforeJson, string afterJson) { connection.Submit(afterJson, label); host.QueueSharedRecovery(); }
        public void Undo() => connection.Undo();
        public void Redo() => connection.Redo();
    }
}
