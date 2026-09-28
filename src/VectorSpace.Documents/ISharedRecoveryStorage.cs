namespace VectorSpace.Documents;

/// <summary>Optional host persistence for collaborative recovery. Journals contain native design
/// copies, never room tokens. Separate writer IDs prevent two windows overwriting each other's work.</summary>
public interface ISharedRecoveryStorage
{
    Task<IReadOnlyDictionary<string, string>> ReadSharedRecoveryAsync(CancellationToken cancellationToken = default);
    Task WriteSharedRecoveryAsync(string writerId, string? journal, CancellationToken cancellationToken = default);
}
