using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using VectorSpace.Collaboration;

namespace VectorSpace.Server;

public sealed record GrantRecord(string Id, string Label, RoomRole Role, string TokenHash, DateTimeOffset CreatedAt);
public sealed record RoomMetadata(string Id, string Name, SharedSnapshot Initial, List<GrantRecord> Grants);
public sealed record JournalRecord(string Actor, Receipt Receipt, Commit? Commit);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(RoomMetadata))]
[JsonSerializable(typeof(JournalRecord))]
internal partial class ServerJson : JsonSerializerContext;

/// <summary>Single-process append-only journal. Length plus SHA-256 frames distinguish incomplete
/// trailing writes from corrupted complete records. No successful receipt precedes Flush(true).</summary>
internal static class RoomJournal
{
    public const long MaxBytes = 256L * 1024 * 1024;
    private const int MaxRecordBytes = 96 * 1024 * 1024;
    public static IEnumerable<JournalRecord> Read(string path)
    {
        if (!File.Exists(path)) yield break;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Room journal exceeds the configured limit.");
        var header = new byte[36];
        while (stream.Position < stream.Length)
        {
            var start = stream.Position;
            if (stream.Length - start < header.Length) { stream.SetLength(start); stream.Flush(true); yield break; }
            stream.ReadExactly(header);
            var size = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (size is < 2 or > MaxRecordBytes) throw new InvalidDataException("Corrupted room journal length.");
            if (stream.Length - stream.Position < size) { stream.SetLength(start); stream.Flush(true); yield break; }
            var payload = new byte[size]; stream.ReadExactly(payload);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), header.AsSpan(4))) throw new InvalidDataException("Corrupted room journal checksum.");
            yield return JsonSerializer.Deserialize(payload, ServerJson.Default.JournalRecord) ?? throw new InvalidDataException("Invalid room journal entry.");
        }
    }
    public static void Append(string path, JournalRecord entry)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry, ServerJson.Default.JournalRecord);
        if (bytes.Length > MaxRecordBytes) throw new InvalidDataException("Room transaction exceeds journal limits.");
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        if (stream.Length > MaxBytes - bytes.Length - 36) throw new InvalidOperationException("Room journal budget reached. Export the design to a new room.");
        stream.Seek(0, SeekOrigin.End);
        Span<byte> header = stackalloc byte[36]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length); SHA256.HashData(bytes, header[4..]);
        stream.Write(header); stream.Write(bytes); stream.Flush(true);
        PrivateFile(path);
    }
    public static void SaveMetadata(string path, RoomMetadata metadata)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, metadata, ServerJson.Default.RoomMetadata); stream.Flush(true);
            }
            PrivateFile(temporary); File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void PrivateFile(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
