using System.Text.Json;
using VectorSpace.Collaboration;

namespace VectorSpace.Server;

internal sealed partial class Room
{
    public SyncReply Submit(GrantRecord grant, EditBatch batch)
    {
        if (_faulted) throw new IOException("This room is read-only after a storage failure. Restart after repairing storage.");
        if (batch.ClientId is null || batch.ClientId.Length != 32 || !batch.ClientId.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid client identifier.");
        var actor = grant.Id + "/" + batch.ClientId;
        if (_receipts.TryGetValue(actor, out var previous))
        {
            if (batch.Sequence == previous.Receipt.Sequence && batch.Id == previous.Receipt.Id)
            {
                var duplicate = Sync(grant, batch.BaseRevision); duplicate.Receipt = previous.Receipt; duplicate.Acknowledged = previous.Commit; return duplicate;
            }
            if (batch.Sequence != previous.Receipt.Sequence + 1) throw new InvalidDataException("Client sequence is stale or discontinuous; reopen the room.");
        }
        else if (batch.Sequence != 1 || _receipts.Count >= 2048) throw new InvalidDataException("Invalid first sequence or room client budget reached.");
        Commit? commit = null; string? error = null;
        try { commit = _engine.Prepare(batch, grant.Role, grant.Label); }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or JsonException or UnauthorizedAccessException) { error = ex.Message; }
        var receipt = new Receipt(batch.Id, batch.Sequence, commit is not null, commit?.Revision ?? Revision, error);
        var record = new JournalRecord(actor, receipt, commit);
        try { RoomJournal.Append(_journalPath, record); }
        catch (IOException) { _faulted = true; throw; }
        if (commit is not null) { _engine.Accept(commit); Remember(commit); }
        _receipts[actor] = record; Pulse();
        var reply = Sync(grant, batch.BaseRevision); reply.Receipt = receipt; reply.Acknowledged = commit; return reply;
    }
}
