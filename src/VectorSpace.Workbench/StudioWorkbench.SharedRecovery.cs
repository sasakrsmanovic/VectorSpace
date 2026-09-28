using System.Text;
using System.Text.Json;
using VectorSpace.Collaboration;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private string _sharedRecoveryWriter = Guid.NewGuid().ToString("N");
    private string _sharedRecoverySignature = "";
    private string? _sharedRecoveryJson;
    private bool _sharedRecoverySaved = true;
    private readonly SemaphoreSlim _sharedRecoveryLock = new(1, 1);
    private List<RecoveryEdit> CurrentRecovery()
    {
        if (_collaboration is not { } connection) return [];
        var result = connection.Replica.Recovery.ToList();
        if (connection.Replica.PendingDocument is { } document)
            result.Add(new("Unacknowledged local edits", "The server had not acknowledged these edits when this recovery copy was saved. Rejoin the file before deciding which changes to reapply.", document, DateTimeOffset.UtcNow));
        return result;
    }
    private void QueueSharedRecovery()
    {
        if (_collaboration is not { } connection) return;
        var r = connection.Replica;
        var signature = $"{r.Confirmed.Revision}:{r.LastSequence}:{r.PendingCount}:{r.Recovery.Count}";
        if (_sharedRecoverySignature == signature) return; _sharedRecoverySignature = signature;
        var entries = CurrentRecovery();
        _sharedRecoveryJson = entries.Count == 0 ? null : JsonSerializer.Serialize(entries, CollaborationJson.Default.ListRecoveryEdit);
        _sharedRecoverySaved = false;
        _ = PersistSharedRecoveryAsync();
    }
    private async Task PersistSharedRecoveryAsync()
    {
        var writer = _sharedRecoveryWriter; var json = _sharedRecoveryJson;
        if (_storage is not ISharedRecoveryStorage storage)
        {
            _sharedRecoverySaved = json is null;
            if (json is not null) ShowStatus("This host cannot persist shared-edit recovery. Download a local copy before closing.", true);
            return;
        }
        await _sharedRecoveryLock.WaitAsync();
        try
        {
            await storage.WriteSharedRecoveryAsync(writer, json);
            if (writer == _sharedRecoveryWriter && json == _sharedRecoveryJson) _sharedRecoverySaved = true;
        }
        catch (Exception error)
        {
            _sharedRecoverySaved = false;
            if (!_disposed) ShowStatus("Shared recovery could not be saved: " + error.Message + ". Download a copy before closing.", true);
        }
        finally { _sharedRecoveryLock.Release(); }
    }
    private async Task ExportCurrentSharedRecoveryAsync()
    {
        var entries = CurrentRecovery();
        if (entries.Count == 0) entries.Add(new("Local copy", "Explicit local copy before leaving collaboration.", DocumentJson.Save(Session.Document), DateTimeOffset.UtcNow));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entries, CollaborationJson.Default.ListRecoveryEdit);
        await _storage.SaveAsync("VectorSpace-collaboration-recovery.json", bytes, "application/json");
    }
    private async Task ShowSharedRecoveryAsync()
    {
        var saved = _storage is ISharedRecoveryStorage storage ? await storage.ReadSharedRecoveryAsync() : new Dictionary<string, string>();
        var entries = new List<(string Writer, RecoveryEdit Edit)>();
        foreach (var (writer, json) in saved)
        {
            if (_collaboration is not null && writer == _sharedRecoveryWriter) continue;
            try
            {
                var records = JsonSerializer.Deserialize(json, CollaborationJson.Default.ListRecoveryEdit) ?? [];
                foreach (var record in records.Take(16)) entries.Add((writer, record));
            }
            catch (JsonException) { ShowStatus("One stored recovery journal is invalid; it has not been deleted.", true); }
        }
        entries.AddRange(CurrentRecovery().Select(e => (_sharedRecoveryWriter, e)));
        var root = new StackPanel { Spacing = 12, Width = 420 };
        root.Children.Add(Wrapped("Recovery copies are local and never contain invitation credentials. They are not replayed automatically after reload, because doing so could overwrite later shared edits. Download a copy, rejoin the shared file, and compare first."));
        var dialog = Dialog("Local collaboration recovery", Studio.Scroll(root)); RecoveryEdit? download = null; var discard = false;
        foreach (var (_, entry) in entries)
        {
            var button = new StudioButton("Download copy", () => { download = entry; dialog.Hide(); });
            AutomationProperties.SetName(button, "Download recovery " + entry.Label);
            root.Children.Add(Studio.Columns((Wrapped(entry.Label + "\n" + entry.Time.ToLocalTime().ToString("g"), 12, Studio.Ink), -1), (button, 120)));
            root.Children.Add(Wrapped(entry.Reason, 10)); root.Children.Add(Studio.Rule());
        }
        if (entries.Count == 0) root.Children.Add(Wrapped("No unacknowledged edits or conflict copies were found."));
        else
        {
            var clear = new StudioButton("Discard saved recovery copies", () => { discard = true; dialog.Hide(); });
            root.Children.Add(clear);
        }
        await dialog.ShowAsync();
        if (download is not null)
            await _storage.SaveAsync(SafeName(download.Label) + "-recovery.vectorspace", Encoding.UTF8.GetBytes(download.Document), "application/json");
        if (discard && await ConfirmAsync("Discard recovery copies?", "This deletes the listed local recovery files. Unsynchronized current edits will still retain their latest safety copy."))
        {
            if (_storage is ISharedRecoveryStorage persistent)
                foreach (var writer in saved.Keys.Where(w => w != _sharedRecoveryWriter)) await persistent.WriteSharedRecoveryAsync(writer, null);
            _collaboration?.Replica.Recovery.Clear(); _sharedRecoverySignature = ""; QueueSharedRecovery();
        }
    }
}
