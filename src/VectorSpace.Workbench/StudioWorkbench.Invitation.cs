namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    /// <summary>Shows an invitation for review without automatically contacting its server.
    /// Hosts should remove the credential fragment from browser history after reading it.</summary>
    public void OpenCollaborationInvitation(string link)
    {
        if (string.IsNullOrWhiteSpace(link) || link.Length > 8192) return;
        PendingInvitation = link; RunAsync(ShowShareAsync);
    }
    private ContentDialog TrackSharedDialog(ContentDialog dialog)
    {
        dialog.Opened += (_, _) => _sharedDialogDepth++;
        dialog.Closed += (_, _) =>
        {
            _sharedDialogDepth = Math.Max(0, _sharedDialogDepth - 1);
            DispatcherQueue.TryEnqueue(FlushRemoteDeliveries);
        };
        return dialog;
    }
}
