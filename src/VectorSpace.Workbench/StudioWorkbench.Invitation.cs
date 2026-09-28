namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    /// <summary>Reviews an invitation without automatically contacting its server.</summary>
    public void OpenCollaborationInvitation(string link)
    {
        if (string.IsNullOrWhiteSpace(link) || link.Length > 8192) return;
        PendingInvitation = link; RunAsync(ShowShareAsync);
    }
    /// <summary>The await owns the modal boundary. Presentation Opened/Closed events
    /// are not a balanced lifetime contract and must not retain a remote-delivery lock.</summary>
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        _sharedDialogDepth++;
        try { return await dialog.ShowAsync(); }
        finally
        {
            _sharedDialogDepth--;
            DispatcherQueue.TryEnqueue(FlushRemoteDeliveries);
        }
    }
}
