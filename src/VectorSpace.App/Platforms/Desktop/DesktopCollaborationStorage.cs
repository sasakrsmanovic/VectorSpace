using System.Text;
using VectorSpace.Documents;

namespace VectorSpace.App;

internal sealed partial class DesktopWorkspaceStorage : ISharedRecoveryStorage
{
    private static string SharedDirectory => Path.Combine(DirectoryPath, "shared-recovery");
    public async Task<IReadOnlyDictionary<string, string>> ReadSharedRecoveryAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(SharedDirectory)) return result;
        foreach (var path in Directory.EnumerateFiles(SharedDirectory, "*.json"))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _)) continue;
            if (result.Count >= 64 || new FileInfo(path).Length > 256L * 1024 * 1024) throw new InvalidDataException("Recovery storage exceeds the supported limits; preserve the files manually.");
            result.Add(Path.GetFileNameWithoutExtension(path), await File.ReadAllTextAsync(path, cancellationToken));
        }
        return result;
    }
    public async Task WriteSharedRecoveryAsync(string writerId, string? journal, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(writerId, "N", out _)) throw new ArgumentException("Invalid recovery writer identifier.");
        Directory.CreateDirectory(SharedDirectory);
        var path = Path.Combine(SharedDirectory, writerId + ".json");
        if (journal is null) { File.Delete(path); return; }
        if (journal.Length > 128 * 1024 * 1024 || !File.Exists(path) && Directory.EnumerateFiles(SharedDirectory, "*.json").Take(64).Count() >= 64)
            throw new InvalidDataException("Recovery capacity reached; download existing copies first.");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(journal), cancellationToken); stream.Flush(true);
            }
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
