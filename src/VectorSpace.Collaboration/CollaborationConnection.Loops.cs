using System.Text.Json;

namespace VectorSpace.Collaboration;

public sealed partial class CollaborationConnection
{
    private async Task PollLoop(CancellationToken token)
    {
        var failures = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                long revision = -1, afterEvent = -1;
                await _dispatch(() => { revision = Replica.Confirmed.Revision; afterEvent = _event; });
                var reply = await Transport.SyncAsync(revision, afterEvent, true, token);
                await Deliver(reply); failures = 0;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException or InvalidOperationException)
            { await Fault(ex, token); await Delay(++failures, token); }
        }
    }
    private async Task SendLoop(CancellationToken token)
    {
        var failures = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                EditBatch? batch = null;
                await _dispatch(() => { batch = Replica.NextBatch(); });
                if (batch is null) { await _sendSignal.WaitAsync(token); continue; }
                var reply = await Transport.SubmitAsync(batch, token);
                await Deliver(reply); failures = 0;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException or InvalidOperationException)
            { await Fault(ex, token); await Delay(++failures, token); }
        }
    }
    private async Task PresenceLoop(CancellationToken token)
    {
        long sentVersion = -1; var sentAt = DateTimeOffset.MinValue;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(120, token);
                Participant? presence = null; long version = 0;
                await _dispatch(() => { presence = _presence; version = _presenceVersion; });
                if (presence is null || version == sentVersion && DateTimeOffset.UtcNow - sentAt < TimeSpan.FromSeconds(5)) continue;
                await Transport.PresenceAsync(presence, token); sentVersion = version; sentAt = DateTimeOffset.UtcNow;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException or InvalidOperationException)
            { await Fault(ex, token); await Delay(2, token); }
        }
    }
    private static async Task Delay(int failures, CancellationToken token)
    {
        try { await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(8000, 250 * Math.Pow(2, Math.Min(5, failures))) + Random.Shared.Next(100)), token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
