namespace VectorSpace.Server;

internal sealed partial class Room
{
    private bool _presencePulseQueued;
    private void QueuePresenceNotification()
    {
        // Called under Gate. At most one delayed broadcast is retained per room.
        if (_presencePulseQueued) return;
        _presencePulseQueued = true;
        _ = PublishPresenceAsync();
    }
    private async Task PublishPresenceAsync()
    {
        await Task.Delay(100);
        await Gate.WaitAsync();
        try { _presencePulseQueued = false; Pulse(); }
        finally { Gate.Release(); }
    }
}
