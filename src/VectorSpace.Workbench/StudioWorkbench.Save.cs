using System.Text;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private int _saveDepth;
    public bool IsSaving => _saveDepth != 0;
    private async Task SaveAsync()
    {
        _saveDepth++;
        try
        {
            Surface.CommitPendingEdits();
            // Inspector text fields commit on focus loss. Complete that boundary before
            // serializing, and do not leave keyboard focus on the detached inline editor.
            Surface.FocusCanvas();
            var json = DocumentJson.Save(Session.Document);
            await _storage.SaveAsync(SafeName(Session.Document.Name) + ".vectorspace", Encoding.UTF8.GetBytes(json), "application/json");
            Session.MarkSaved(json); ShowStatus("Downloaded editable document");
        }
        finally
        {
            _saveDepth--;
            // A browser download/native picker and the save-state inspector refresh may
            // move focus after inline teardown. Restore the authoring surface at completion,
            // but never steal it from a subsequently started text or pointer transaction.
            if (!_disposed && !Surface.IsTextEditing && !Session.IsInteracting && _sharedDialogDepth == 0) Surface.FocusCanvas();
        }
    }
}
