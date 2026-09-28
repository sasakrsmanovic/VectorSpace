using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Editing;

/// <summary>Host-supplied collaborative history. Commit runs before the local rollback snapshot is
/// released. A failure must leave the remote queue unchanged so the editor can cancel the gesture.</summary>
public interface ISharedEditorHistory
{
    bool CanEdit(string label);
    bool CanUndo { get; }
    bool CanRedo { get; }
    string UndoLabel { get; }
    string RedoLabel { get; }
    IReadOnlyList<string> History { get; }
    void Commit(string label, string beforeJson, string afterJson);
    void Undo();
    void Redo();
}

public sealed partial class EditorSession
{
    public ISharedEditorHistory? SharedHistory { get; private set; }
    public void AttachSharedHistory(ISharedEditorHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (IsInteracting) throw new InvalidOperationException("Finish the active edit before joining a shared file.");
        if (SharedHistory is not null) throw new InvalidOperationException("Leave the current shared file first.");
        _undo.Clear(); _redo.Clear(); SharedHistory = history; Notify(EditorChangeKind.Selection);
    }
    public void DetachSharedHistory()
    {
        if (IsInteracting) CancelInteraction();
        SharedHistory = null; _undo.Clear(); _redo.Clear(); Notify(EditorChangeKind.Selection);
    }
    /// <summary>Applies an authoritative/optimistic merged state without adding local undo entries.
    /// Selection, current page, viewport and local layer expansion are preserved where still valid.</summary>
    public void ApplySharedDocument(DesignDocument document)
    {
        if (IsInteracting) throw new InvalidOperationException("Remote application must wait for the active transaction boundary.");
        DocumentJson.Validate(document); document.RebuildParents();
        var expansion = Document.AllNodes().ToDictionary(n => n.Id, n => n.Expanded, StringComparer.Ordinal);
        foreach (var node in document.AllNodes()) if (expansion.TryGetValue(node.Id, out var expanded)) node.Expanded = expanded;
        var page = document.Pages.FirstOrDefault(p => p.Id == Page.Id) ?? document.Pages[0];
        Document = document; Page = page;
        var ids = page.AllNodes().Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        _selected.IntersectWith(ids);
        if (_primaryId is not null && !_selected.Contains(_primaryId)) _primaryId = _selected.LastOrDefault();
        InvalidateSelection(); IsDirty = true; Notify(EditorChangeKind.Document, "Shared update");
    }
}
