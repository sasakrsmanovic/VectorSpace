using System.Text;
using System.Text.Json;
using System.ComponentModel;
using VectorSpace.Collaboration;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private async Task ShowParticipantsAsync()
    {
        if (_collaboration is not { } connection) { await ShowShareAsync(); return; }
        var root = new StackPanel { Spacing = 10, Width = 370 };
        root.Children.Add(Wrapped(connection.Status, 11));
        root.Children.Add(Studio.Text(_participantName + " · You · " + connection.Role, 12, Studio.Ink, true));
        var dialog = Dialog("People in this file", Studio.Scroll(root)); string? follow = null;
        foreach (var peer in connection.Participants)
        {
            var button = new StudioButton(_following == peer.ClientId ? "Stop following" : "Follow", () => { follow = peer.ClientId; dialog.Hide(); });
            AutomationProperties.SetName(button, "Follow participant " + peer.Name);
            var page = Session.Document.Pages.FirstOrDefault(p => p.Id == peer.PageId)?.Name ?? "Another page";
            root.Children.Add(Studio.Columns((Wrapped(peer.Name + "\n" + page, 12, Studio.Ink), -1), (button, 110)));
        }
        if (connection.Participants.Count == 0) root.Children.Add(Wrapped("No other participants are connected. Create a guest invitation from Share."));
        await dialog.ShowAsync();
        if (follow is not null) { _following = _following == follow ? null : follow; DispatcherQueue.TryEnqueue(() => RefreshCollaboration(false)); }
    }
    private async Task ShowSharedHistoryAsync()
    {
        if (_collaboration is not { } connection) { ShowStatus("Join a shared file to browse its server history."); return; }
        var versions = await connection.Transport.HistoryAsync(); var root = new StackPanel { Spacing = 10, Width = 430 };
        var dialog = Dialog("Version history", Studio.Scroll(root)); long? chosen = null; var restore = false;
        root.Children.Add(Wrapped("Committed server revisions survive a service restart. Restoring creates a new shared edit; it does not rewrite or delete earlier history. Concurrent changes can reject a restore."));
        foreach (var version in versions)
        {
            var download = new StudioButton("Download", () => { chosen = version.Revision; dialog.Hide(); });
            AutomationProperties.SetName(download, "Download revision " + version.Revision);
            var replace = new StudioButton("Restore", () => { chosen = version.Revision; restore = true; dialog.Hide(); }) { IsEnabled = connection.Role is RoomRole.Owner or RoomRole.Editor && connection.Replica.PendingCount == 0 };
            AutomationProperties.SetName(replace, "Restore revision " + version.Revision);
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(Wrapped($"r{version.Revision} · {version.Label}", 12, Studio.Ink));
            row.Children.Add(Studio.Columns((Wrapped(version.Author + " · " + version.Time.ToLocalTime().ToString("g"), 10), -1), (download, 78), (replace, 70)));
            row.Children.Add(Studio.Rule()); root.Children.Add(row);
        }
        if (versions.Count == 0) root.Children.Add(Wrapped("No shared edits have been committed yet."));
        await dialog.ShowAsync();
        if (chosen is not { } revision) return;
        var json = await connection.Transport.VersionAsync(revision);
        if (restore)
        {
            if (await ConfirmAsync("Restore revision " + revision + "?", "This will change the shared design for every participant. It is a new conditional transaction, not a force overwrite."))
                Session.RestoreSharedVersion(DocumentJson.Load(json));
        }
        else await _storage.SaveAsync(SafeName(Session.Document.Name) + "-r" + revision + ".vectorspace", Encoding.UTF8.GetBytes(json), "application/json");
    }
    private async Task LeaveSharedAsync()
    {
        if (_collaboration is not { } connection) return;
        Surface.FinishTextEdit(true); Surface.FinishPath(false);
        if (connection.Replica.PendingCount > 0 || connection.Replica.Recovery.Count > 0)
        {
            if (!await ConfirmAsync("Leave with unsynchronized work?", "A recovery bundle will be downloaded before disconnecting. A request already accepted by the server may still become visible to peers. This window retains its local design.")) return;
            await ExportCurrentSharedRecoveryAsync();
        }
        else if (!await ConfirmAsync("Leave shared file?", "Keep a local copy in this window and stop receiving remote edits? Keep your private access link to rejoin later.")) return;
        await PersistSharedRecoveryAsync();
        _collaboration = null; connection.Changed -= RefreshCollaboration;
        Session.DetachSharedHistory(); _presenceTimer.Stop();
        while (_remoteDeliveries.TryDequeue(out var item)) item.Done.TrySetResult();
        connection.Dispose(); _following = null; _presenceSignature = "";
        _presenceLayer.Update([]); _participants.Update([new("self", "You", "#AF7C2D")]);
        _status.Text = "Local file · Disconnected"; await AutosaveAsync();
    }
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void WriteCollaborationDiagnostics(Utf8JsonWriter json)
    {
        json.WriteStartObject("collaboration"); json.WriteBoolean("connected", IsCollaborating);
        if (_collaboration is { } c)
        {
            json.WriteString("role", c.Role.ToString()); json.WriteString("status", c.Status); json.WriteString("client", c.Replica.ClientId);
            json.WriteBoolean("online", c.Online); json.WriteBoolean("denied", c.AccessDenied); json.WriteNumber("revision", c.Replica.Confirmed.Revision);
            json.WriteNumber("pending", c.Replica.PendingCount); json.WriteNumber("recovery", c.Replica.Recovery.Count);
            json.WriteNumber("participants", c.Participants.Count); json.WriteString("following", _following);
            json.WriteStartArray("names"); foreach (var peer in c.Participants) json.WriteStringValue(peer.Name); json.WriteEndArray();
        }
        json.WriteBoolean("recoverySaved", _sharedRecoverySaved); json.WriteEndObject();
    }
}
