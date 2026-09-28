using System.Text.Json;

namespace VectorSpace.Collaboration;

public sealed partial class SharedReplica
{
    private bool _projectionConflict;
    private void ValidateOptimisticProjection()
    {
        _projectionConflict = false;
        if (_pending.Count == 0) return;
        try { _ = DocumentProjection.ToDocument(Visible); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or JsonException)
        {
            // Keep all pending batches and their native recovery documents. The server still
            // decides their guarded outcomes in order. Never expose an orphaned half-node to UI.
            Visible = Confirmed.Clone(); _projectionConflict = true;
            LastError = "A remote structural edit conflicts with queued work. Resolving queued transactions; local copies are retained.";
        }
    }
    private void CheckPendingBudget(string document)
    {
        if (_pending.Sum(p => (long)p.Document.Length) + document.Length > 64L * 1024 * 1024)
            throw new InvalidOperationException("The unsynchronized edit buffer reached 64 MiB of document text. Wait for synchronization before continuing.");
    }
    private void TrimSharedHistory()
    {
        static long Cost(Entry e) => e.Commit.Changes.Sum(c => (long)(c.Before?.Length ?? 0) + (c.After?.Length ?? 0) + c.Key.Length);
        var cost = _undo.Sum(Cost) + _redo.Sum(Cost);
        while (cost > 32L * 1024 * 1024 && _undo.Count + _redo.Count > 1)
        {
            var list = _undo.Count > 1 || _redo.Count == 0 ? _undo : _redo;
            cost -= Cost(list[0]); list.RemoveAt(0);
        }
    }
    public void AcknowledgeRecovery()
    {
        Recovery.Clear(); LastError = null;
    }
}
