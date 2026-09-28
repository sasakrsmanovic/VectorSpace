using SkiaSharp;
using VectorSpace.Core;

namespace VectorSpace.Skia;

public sealed partial class SceneRenderer
{
    public ImageAssetCache Images { get; } = new();
    private int _gradientCacheCapacity = 512;
    private int _effectCacheCapacity = 256;
    public int GradientCacheCapacity
    {
        get => _gradientCacheCapacity;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _gradientCacheCapacity = value; TrimGradients(); }
    }
    public int EffectCacheCapacity
    {
        get => _effectCacheCapacity;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _effectCacheCapacity = value; TrimEffects(); }
    }
    public int CachedGradientCount => _gradients.Count;
    public int CachedEffectCount => _effects.Count;
    public long GradientBuilds { get; private set; }
    public long EffectBuilds { get; private set; }
    private readonly record struct StopKey(double Offset, string Color, double Opacity);
    private readonly record struct GradientKey(FillKind Kind, double Width, double Height, Vec2 Start, Vec2 End, Matrix2D Matrix, bool UserSpace, GradientSpread Spread, double? Radius, Vec2? Focal);
    private sealed record GradientEntry(GradientKey Key, StopKey[] Stops, SKShader Shader);
    private readonly Dictionary<(string NodeId, int FillIndex), GradientEntry> _gradients = [];
    private readonly record struct EffectKey(EffectKind Kind, bool Visible, string Color, double X, double Y, double Blur, double Spread, double Opacity);
    private sealed record EffectEntry(RectD? Bounds, EffectKey[] Keys, SKImageFilter? Filter);
    private readonly Dictionary<string, EffectEntry> _effects = new(StringComparer.Ordinal);

    private void TrimGradients()
    {
        while (_gradients.Count > _gradientCacheCapacity)
        {
            var first = _gradients.First(); first.Value.Shader.Dispose(); _gradients.Remove(first.Key);
        }
    }
    private void TrimEffects()
    {
        while (_effects.Count > _effectCacheCapacity)
        {
            var first = _effects.First(); first.Value.Filter?.Dispose(); _effects.Remove(first.Key);
        }
    }
    private void ClearAppearance()
    {
        foreach (var entry in _gradients.Values) entry.Shader.Dispose(); _gradients.Clear();
        foreach (var entry in _effects.Values) entry.Filter?.Dispose(); _effects.Clear(); Images.Clear();
    }
    private SKShader? Gradient(FillStyle fill, double width, double height, (string, int) identity)
    {
        if (fill.Stops.Count == 0) return null;
        var key = new GradientKey(fill.Kind, width, height, fill.Start, fill.End, fill.GradientTransform, fill.GradientUserSpace, fill.Spread, fill.GradientRadius, fill.GradientFocal);
        if (_gradients.TryGetValue(identity, out var old) && old.Key == key && SameStops(old.Stops, fill.Stops)) return old.Shader;
        if (old is not null) { old.Shader.Dispose(); _gradients.Remove(identity); }
        if (_gradients.Count >= Math.Max(1, GradientCacheCapacity))
        {
            // Bounded insertion-order eviction, never unbounded native resources.
            var first = _gradients.First(); first.Value.Shader.Dispose(); _gradients.Remove(first.Key);
        }
        var sorted = fill.Stops.OrderBy(s => s.Offset).ToArray();
        var colors = sorted.Select(s => Color(s.Color, s.Opacity)).ToArray();
        var positions = sorted.Select(s => (float)s.Offset).ToArray();
        if (colors.Length == 1) { colors = [colors[0], colors[0]]; positions = [0, 1]; }
        var tile = fill.Spread switch { GradientSpread.Repeat => SKShaderTileMode.Repeat, GradientSpread.Reflect => SKShaderTileMode.Mirror, _ => SKShaderTileMode.Clamp };
        var mapping = fill.GradientTransform * (fill.GradientUserSpace ? Matrix2D.Identity : Matrix2D.Scale(Math.Max(width, 1e-9), Math.Max(height, 1e-9)));
        var a = new SKPoint((float)fill.Start.X, (float)fill.Start.Y); var b = new SKPoint((float)fill.End.X, (float)fill.End.Y);
        SKShader shader;
        if (fill.Kind == FillKind.LinearGradient)
            shader = SKShader.CreateLinearGradient(a, b, colors, positions, tile, Matrix(mapping));
        else if (fill.GradientRadius is { } radius)
        {
            var focal = fill.GradientFocal ?? fill.Start;
            // A focal point on/outside the circle is pulled just inside to avoid a singular cone.
            var delta = focal - fill.Start;
            if (delta.Length >= radius) focal = fill.Start + delta * (radius * .999999 / Math.Max(delta.Length, 1e-9));
            shader = SKShader.CreateTwoPointConicalGradient(new((float)focal.X, (float)focal.Y), 0, a, (float)radius, colors, positions, tile, Matrix(mapping));
        }
        else
        {
            // Preserve the old native Start/End circular gradient behavior.
            var start = new Vec2(fill.Start.X * width, fill.Start.Y * height); var end = new Vec2(fill.End.X * width, fill.End.Y * height);
            shader = SKShader.CreateRadialGradient(new((float)start.X, (float)start.Y), Math.Max(.0001f, (float)start.DistanceTo(end)), colors, positions, tile, Matrix(fill.GradientTransform));
        }
        _gradients[identity] = new(key, fill.Stops.Select(s => new StopKey(s.Offset, s.Color, s.Opacity)).ToArray(), shader); GradientBuilds++; return shader;
    }
    private static bool SameStops(StopKey[] keys, List<GradientStop> stops)
    {
        if (keys.Length != stops.Count) return false;
        for (var i = 0; i < keys.Length; i++) if (keys[i] != new StopKey(stops[i].Offset, stops[i].Color, stops[i].Opacity)) return false;
        return true;
    }
    private void DrawFill(SKCanvas canvas, DesignNode node, FillStyle fill, int fillIndex)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, BlendMode = Blend(fill.Blend) };
        if (fill.Kind == FillKind.Solid) paint.Color = Color(fill.Color, fill.Opacity);
        else
        {
            // Shader colors supply their own alpha. Apply fill opacity exactly once here.
            paint.Color = SKColors.White.WithAlpha((byte)Math.Clamp(Math.Round(fill.Opacity * 255), 0, 255));
            if (fill.Kind == FillKind.Image)
            {
                if (fill.ImageData is null || Images.Get(fill.ImageData) is not { } image) return;
                var tile = fill.ImageMode == ImageScaleMode.Tile ? SKShaderTileMode.Repeat : SKShaderTileMode.Decal;
                using var shader = image.ToShader(tile, tile, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), Matrix(ImagePlacement.Calculate(fill, node.Width, node.Height, image.Width, image.Height)));
                using var adjustment = ImageAdjustment(fill); paint.Shader = shader; paint.ColorFilter = adjustment;
                DrawPaint(canvas, node, paint); return;
            }
            paint.Shader = Gradient(fill, node.Width, node.Height, (node.Id, fillIndex));
            if (paint.Shader is null) return;
        }
        DrawPaint(canvas, node, paint);
    }
    private void DrawPaint(SKCanvas canvas, DesignNode node, SKPaint paint)
    {
        if (node.Kind == NodeKind.Text) DrawText(canvas, node, paint); else canvas.DrawPath(Geometry(node), paint);
    }
    private static SKColorFilter? ImageAdjustment(FillStyle fill)
    {
        if (fill.Exposure == 0 && fill.Contrast == 0 && fill.Saturation == 0) return null;
        var saturation = (float)(1 + fill.Saturation); var k = (float)((1 + fill.Contrast) * Math.Pow(2, fill.Exposure));
        var r = (1 - saturation) * .2126f; var g = (1 - saturation) * .7152f; var b = (1 - saturation) * .0722f;
        var offset = (float)(-.5 * fill.Contrast);
        return SKColorFilter.CreateColorMatrix([
            k * (r + saturation), k * g, k * b, 0, offset,
            k * r, k * (g + saturation), k * b, 0, offset,
            k * r, k * g, k * (b + saturation), 0, offset,
            0, 0, 0, 1, 0]);
    }
    private SKImageFilter? EffectFilter(DesignNode node)
    {
        var nodeId = node.Id; var effects = node.Shadows;
        RectD? sourceBounds = node.Kind is not NodeKind.Text and not NodeKind.Path && (node.ClipContent || node.Children.Count == 0)
            ? node.LocalBounds.Inflate(node.Strokes.Where(s => s.Visible).Select(s => s.Width / 2).DefaultIfEmpty(0).Max() + 1) : null;
        if (effects.Count == 0) return null;
        if (_effects.TryGetValue(nodeId, out var prior) && prior.Bounds == sourceBounds && SameEffects(prior.Keys, effects)) return prior.Filter;
        if (prior is not null) { prior.Filter?.Dispose(); _effects.Remove(nodeId); }
        if (_effects.Count >= Math.Max(1, EffectCacheCapacity)) { var first = _effects.First(); first.Value.Filter?.Dispose(); _effects.Remove(first.Key); }
        var owned = new List<SKImageFilter>();
        T Own<T>(T filter) where T : SKImageFilter { owned.Add(filter); return filter; }
        SKImageFilter? result = null;
        try
        {
            // Every outer shadow samples the original source, never a previously generated shadow.
            foreach (var effect in effects.Where(e => e.Visible && e.Kind == EffectKind.DropShadow).Reverse())
            {
                var spread = (float)effect.Spread;
                var mask = spread > 0 ? Own(SKImageFilter.CreateDilate(spread, spread)) : spread < 0 ? Own(SKImageFilter.CreateErode(-spread, -spread)) : null;
                var shadow = Own(SKImageFilter.CreateDropShadowOnly((float)effect.X, (float)effect.Y, (float)effect.Blur / 2, (float)effect.Blur / 2, Color(effect.Color, effect.Opacity), mask));
                result = result is null ? shadow : Own(SKImageFilter.CreateMerge(result, shadow));
            }
            SKImageFilter? content = null; // null is the unfiltered original content
            foreach (var effect in effects.Where(e => e.Visible && e.Kind == EffectKind.InnerShadow).Reverse())
            {
                var spread = (float)effect.Spread;
                var mask = spread > 0 ? Own(SKImageFilter.CreateErode(spread, spread)) : spread < 0 ? Own(SKImageFilter.CreateDilate(-spread, -spread)) : null;
                var shifted = Own(SKImageFilter.CreateDropShadowOnly((float)effect.X, (float)effect.Y, (float)effect.Blur / 2, (float)effect.Blur / 2, SKColors.Black, mask));
                using var color = SKColorFilter.CreateBlendMode(Color(effect.Color, effect.Opacity), SKBlendMode.Src);
                // Src recoloring affects transparent black. Explicitly bound its processing
                // where the source silhouette has a known finite footprint; SaveLayer bounds
                // alone are only a hint and do not constrain a flood-like image filter.
                var coloredSource = Own(sourceBounds is { } bounds
                    ? SKImageFilter.CreateColorFilter(color, null, Rect(bounds))
                    : SKImageFilter.CreateColorFilter(color));
                var inner = Own(SKImageFilter.CreateBlendMode(SKBlendMode.SrcOut, shifted, coloredSource));
                // Source-atop preserves translucent source alpha; inner effects never tint outer shadows.
                content = Own(SKImageFilter.CreateBlendMode(SKBlendMode.SrcATop, content, inner));
            }
            result = result is null ? content : Own(SKImageFilter.CreateMerge(result, content));
            foreach (var effect in effects.Where(e => e.Visible && e.Kind == EffectKind.LayerBlur && e.Blur > 0))
                result = Own(SKImageFilter.CreateBlur((float)effect.Blur / 2, (float)effect.Blur / 2, result));
            _effects[nodeId] = new(sourceBounds, effects.Select(Key).ToArray(), result); EffectBuilds++;
            owned.Remove(result!); return result;
        }
        finally { foreach (var filter in owned) filter.Dispose(); }
    }
    private static EffectKey Key(ShadowStyle e) => new(e.Kind, e.Visible, e.Color, e.X, e.Y, e.Blur, e.Spread, e.Opacity);
    private static bool SameEffects(EffectKey[] keys, List<ShadowStyle> effects)
    {
        if (keys.Length != effects.Count) return false;
        for (var i = 0; i < keys.Length; i++) if (keys[i] != Key(effects[i])) return false;
        return true;
    }
    private static double EffectPadding(DesignNode node)
    {
        double shadow = 0, blur = 0;
        foreach (var e in node.Shadows)
        {
            if (!e.Visible) continue;
            if (e.Kind == EffectKind.LayerBlur) blur += e.Blur * 3;
            else if (e.Kind == EffectKind.DropShadow) shadow = Math.Max(shadow, Math.Max(Math.Abs(e.X), Math.Abs(e.Y)) + Math.Max(0, e.Spread) + e.Blur * 3);
        }
        return shadow + blur;
    }
}
