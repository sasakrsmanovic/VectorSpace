using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using VectorSpace.Documents;

namespace VectorSpace.App;

internal sealed partial class BrowserWorkspaceStorage : ISharedRecoveryStorage
{
    public async Task<IReadOnlyDictionary<string, string>> ReadSharedRecoveryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var data = JsonDocument.Parse(await BrowserSharedFiles.ReadRecovery());
        return data.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
    }
    public async Task WriteSharedRecoveryAsync(string writerId, string? journal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); await BrowserSharedFiles.WriteRecovery(writerId, journal ?? "");
    }
}
internal static partial class BrowserSharedFiles
{
    [JSImport("globalThis.vectorSpaceCollaborationRecovery.read")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> ReadRecovery();
    [JSImport("globalThis.vectorSpaceCollaborationRecovery.write")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> WriteRecovery(string id, string journal);
    [JSImport("globalThis.vectorSpaceCollaborationRecovery.takeInvitation")]
    internal static partial string TakeInvitation();
    [JSImport("globalThis.vectorSpaceCollaborationRecovery.applicationUrl")]
    internal static partial string ApplicationUrl();
}
