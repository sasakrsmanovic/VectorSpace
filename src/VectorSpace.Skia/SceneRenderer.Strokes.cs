using SkiaSharp;
using VectorSpace.Core;

namespace VectorSpace.Skia;

public sealed partial class SceneRenderer
{
    private readonly record struct StrokeKey(double Width, StrokeAlignment Alignment, StrokeCap Cap, StrokeJoin Join, double Miter, double Offset);
    private sealed class StrokeEntry(SKPath source, StrokeKey key, double[] dashes, SKPath region, LinkedListNode<(string, int)> recency) : IDisposable
    {
        public SKPath Source { get; } = source;
        public StrokeKey Key { get; } = key;
        public double[] Dashes { get; } = dashes;
        public SKPath Region { get; } = region;
        public SKPath? HitBand { get; set; }
        public double HitTolerance { get; set; }
        public LinkedListNode<(string, int)> Recency { get; } = recency;
        public void Dispose() { Region.Dispose(); HitBand?.Dispose(); }
    }
    private readonly Dictionary<(string, int), StrokeEntry> _strokeRegions = [];
    private readonly LinkedList<(string, int)> _strokeRecency = new();
    private int _strokeCapacity = 2048;
    public int StrokeCacheCapacity
    {
        get => _strokeCapacity;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _strokeCapacity = value; while (_strokeRegions.Count > value) RemoveStroke(_strokeRecency.First!.Value); }
    }
    public int CachedStrokeCount => _strokeRegions.Count;
    public long StrokeBuilds { get; private set; }
    public long StrokeCacheHits { get; private set; }
    /// <summary>Borrowed immutable stroke fill path, owned by this renderer. A subsequent cache
    /// mutation may evict it. Paint color/opacity and layer transforms do not rebuild its geometry.</summary>
    public SKPath StrokeGeometry(DesignNode node, int index)
    {
        if (index < 0 || index >= node.Strokes.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var source = Geometry(node); var stroke = node.Strokes[index]; var identity = (node.Id, index);
        var key = new StrokeKey(stroke.Width, stroke.Alignment, stroke.Cap, stroke.Join, stroke.MiterLimit, stroke.DashOffset);
        if (_strokeRegions.TryGetValue(identity, out var entry) && ReferenceEquals(entry.Source, source) && entry.Key == key && entry.Dashes.AsSpan().SequenceEqual(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(stroke.Dashes)))
        {
            _strokeRecency.Remove(entry.Recency); _strokeRecency.AddLast(entry.Recency); StrokeCacheHits++; return entry.Region;
        }
        var path = BuildStrokeRegion(source, stroke);
        RemoveStroke(identity);
        while (_strokeRegions.Count >= _strokeCapacity) RemoveStroke(_strokeRecency.First!.Value);
        _strokeRegions.Add(identity, new(source, key, stroke.Dashes.ToArray(), path, _strokeRecency.AddLast(identity))); StrokeBuilds++;
        return path;
    }
    public static SKPath BuildStrokeRegion(SKPath source, StrokeStyle stroke)
    {
        var region = new SKPath();
        if (stroke.Width <= 0 || source.IsEmpty) return region;
        var aligned = stroke.Alignment != StrokeAlignment.Center && NativeShapeGeometry.IsClosed(source);
        using var paint = StrokePaint(stroke, aligned ? 2 : 1);
        try
        {
            if (!paint.GetFillPath(source, region)) throw new InvalidDataException("Unable to construct a finite stroke region.");
            if (aligned)
            {
                using var result = region.Op(source, stroke.Alignment == StrokeAlignment.Inside ? SKPathOp.Intersect : SKPathOp.Difference)
                    ?? throw new InvalidDataException("Unable to align this stroke.");
                region.Reset(); region.AddPath(result); region.FillType = result.FillType;
            }
            return region;
        }
        catch { region.Dispose(); throw; }
    }
    public static SKPaint StrokePaint(StrokeStyle stroke, double widthMultiplier = 1)
    {
        var paint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)(stroke.Width * widthMultiplier),
            Color = Color(stroke.Color, stroke.Opacity), StrokeMiter = (float)stroke.MiterLimit,
            StrokeCap = stroke.Cap switch { StrokeCap.Butt => SKStrokeCap.Butt, StrokeCap.Square => SKStrokeCap.Square, _ => SKStrokeCap.Round },
            StrokeJoin = stroke.Join switch { StrokeJoin.Miter => SKStrokeJoin.Miter, StrokeJoin.Bevel => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Round }
        };
        try
        {
            if (stroke.Dashes.Count != 0)
            {
                var count = stroke.Dashes.Count; var data = new float[count * (count % 2 == 0 ? 1 : 2)];
                for (var i = 0; i < data.Length; i++) data[i] = (float)stroke.Dashes[i % count];
                using var effect = SKPathEffect.CreateDash(data, (float)stroke.DashOffset);
                paint.PathEffect = effect;
            }
            return paint;
        }
        catch { paint.Dispose(); throw; }
    }
    private bool StrokeContains(DesignNode node, int index, Vec2 point, double tolerance)
    {
        var region = StrokeGeometry(node, index);
        if (region.Contains((float)point.X, (float)point.Y)) return true;
        if (tolerance <= 0 || region.IsEmpty) return false;
        var entry = _strokeRegions[(node.Id, index)];
        if (entry.HitBand is null || entry.HitTolerance != tolerance)
        {
            entry.HitBand?.Dispose(); entry.HitBand = new(); entry.HitTolerance = tolerance;
            using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(tolerance * 2), StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
            paint.GetFillPath(region, entry.HitBand);
        }
        return entry.HitBand.Contains((float)point.X, (float)point.Y);
    }
    private void RemoveStroke((string, int) id)
    {
        if (!_strokeRegions.Remove(id, out var entry)) return;
        _strokeRecency.Remove(entry.Recency); entry.Dispose();
    }
    private void ClearStrokeCache()
    {
        foreach (var entry in _strokeRegions.Values) entry.Dispose(); _strokeRegions.Clear(); _strokeRecency.Clear();
    }
    private void TrimStrokeCache(HashSet<string> ids)
    {
        foreach (var key in _strokeRegions.Keys.Where(k => !ids.Contains(k.Item1)).ToArray()) RemoveStroke(key);
    }
}
