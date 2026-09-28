using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using SkiaSharp;
using VectorSpace.Documents;

namespace VectorSpace.Skia;

/// <summary>Single-render-thread LRU cache. Get returns a borrowed immutable image; callers must not
/// dispose it or retain it across cache mutation. Duplicate encoded payloads share decoded storage.</summary>
public sealed class ImageAssetCache : IDisposable
{
    private sealed record Key(string Digest);
    private sealed record Entry(SKImage? Image, long Bytes, string? Error, LinkedListNode<string> Recency);
    private ConditionalWeakTable<string, Key> _keys = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private int _capacity = 128;
    private long _byteBudget = 128L * 1024 * 1024;
    public int Capacity
    {
        get => _capacity;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _capacity = value; Trim(); }
    }
    public long ByteBudget
    {
        get => _byteBudget;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (value > _byteBudget)
                foreach (var key in _entries.Where(p => p.Value.Image is null).Select(p => p.Key).ToArray()) Remove(key);
            _byteBudget = value; Trim();
        }
    }
    private void Trim()
    {
        while ((_entries.Count > _capacity || DecodedBytes > _byteBudget) && _lru.First is { } oldest) Remove(oldest.Value);
    }
    public long DecodedBytes { get; private set; }
    public long DecodeCount { get; private set; }
    public long DecodeAttemptCount { get; private set; }
    public long HitCount { get; private set; }
    public int Count => _entries.Count;
    public string? LastError { get; private set; }

    public SKImage? Get(string dataUri)
    {
        ArgumentNullException.ThrowIfNull(dataUri);
        if (dataUri.Length > EmbeddedImage.MaxDataUriCharacters)
        {
            LastError = "An embedded image exceeds 8 MiB.";
            return null; // Do not hash or retain arbitrarily large rejected strings.
        }
        // This digest is an internal, process-local content key, not a persisted interchange hash.
        // Hashing the existing UTF-16 storage avoids an image-sized UTF-8 allocation after cloning.
        var key = _keys.GetValue(dataUri, static value => new(Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(value.AsSpan()))))).Digest;
        if (_entries.TryGetValue(key, out var known))
        {
            _lru.Remove(known.Recency); _lru.AddLast(known.Recency); HitCount++; LastError = known.Error; return known.Image;
        }
        SKImage? image = null; long bytes = 0;
        try
        {
            bytes = EmbeddedImage.Inspect(dataUri).DecodedBytes;
            if (bytes > ByteBudget) throw new InvalidDataException("Image exceeds the configured decoded-image cache budget.");
            // Evict before allocating new pixels, not after briefly retaining both full budgets.
            while ((_entries.Count >= Capacity || DecodedBytes > ByteBudget - bytes) && _lru.First is { } oldest)
                Remove(oldest.Value);
            var encoded = EmbeddedImage.Decode(dataUri).Bytes;
            DecodeAttemptCount++;
            image = RasterImageCodec.Decode(encoded, ByteBudget); DecodeCount++;
            bytes = (long)image.Width * image.Height * 4;
            LastError = null;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException)
        {
            image?.Dispose(); image = null; bytes = 0; LastError = e.Message;
        }
        while (_entries.Count >= Math.Max(1, Capacity) || DecodedBytes + bytes > Math.Max(0, ByteBudget))
        {
            if (_lru.First is not { } oldest) break;
            Remove(oldest.Value);
        }
        _entries.Add(key, new(image, bytes, LastError, _lru.AddLast(key))); DecodedBytes += bytes;
        return image;
    }
    private void Remove(string key)
    {
        if (!_entries.Remove(key, out var entry)) return;
        entry.Image?.Dispose(); DecodedBytes -= entry.Bytes; _lru.Remove(entry.Recency);
    }
    public void Clear()
    {
        foreach (var entry in _entries.Values) entry.Image?.Dispose();
        _entries.Clear(); _lru.Clear(); _keys = new(); DecodedBytes = 0; LastError = null;
    }
    public void Dispose() => Clear();
}
