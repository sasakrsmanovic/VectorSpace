namespace VectorSpace.Collaboration;

public sealed partial class CollaborationConnection
{
    /// <summary>Optional host dispatcher. The Boolean is true when model state may change.
    /// Hosts defer those deliveries until active pointer/text/modal transactions complete.
    /// Presence-only responses may proceed immediately. Every returned task must complete
    /// or cancel during host disposal; network loops apply bounded backpressure by awaiting it.</summary>
    public Func<Action, bool, Task>? ReceiveDispatcher { get; set; }
    private Task Deliver(SyncReply reply)
    {
        var changesModel = reply.Snapshot is not null || reply.Commits.Count != 0 || reply.Acknowledged is not null || reply.Receipt is not null;
        return ReceiveDispatcher is { } receive ? receive(() => Receive(reply), changesModel) : _dispatch(() => Receive(reply));
    }
}
