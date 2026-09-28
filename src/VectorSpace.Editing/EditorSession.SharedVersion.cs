using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Editing;

public sealed partial class EditorSession
{
    public void RestoreSharedVersion(DesignDocument version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (SharedHistory is null) throw new InvalidOperationException("Version restoration requires a shared file.");
        if (version.Id != Document.Id) throw new InvalidOperationException("The revision belongs to a different document.");
        DocumentJson.Validate(version); version.RebuildParents();
        Edit("Restore shared version", () =>
        {
            var pageId = Page.Id; Document = version;
            Page = version.Pages.FirstOrDefault(p => p.Id == pageId) ?? version.Pages[0];
            _selected.Clear(); _primaryId = null; InvalidateSelection();
        });
    }
}
