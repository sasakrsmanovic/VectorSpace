using SkiaSharp;
using System.Diagnostics;
using VectorSpace.Core;

namespace VectorSpace.Skia;

/// <summary>Retained geometry cache with explicit native-resource ownership. No Uno dependency.</summary>
public sealed partial class SceneRenderer : IDisposable
{
    private readonly record struct GeometryKey(NodeKind Kind, double Width, double Height, double Radius, int Sides, double Ratio, string? Data, double PathWidth, double PathHeight, bool Closed);
    private readonly record struct PointKey(Vec2 Position, Vec2? In, Vec2? Out);
    private sealed record CachedPath(GeometryKey Key, PointKey[] Points, SKPath Path, LinkedListNode<string> Recency);
    private readonly LinkedList<string> _recency = new();
    private readonly record struct TextKey(string Text, string Family, int Weight, double Size, double Width, double Spacing);
    private sealed record TextLine(string Text, float Width);
    private readonly Dictionary<TextKey, TextLine[]> _textLines = [];
    public int GeometryCacheCapacity { get; set; } = 8192;
    public long GeometryBuilds { get; private set; }
    public long GeometryCacheHits { get; private set; }
    public long CulledNodes { get; private set; }
    public double LastDrawMilliseconds { get; private set; }
    public int CachedGeometryCount => _paths.Count;
    private readonly Dictionary<string, CachedPath> _paths = [];
    private readonly Dictionary<string, SKTypeface> _typefaces = [];
    private SKTypeface? _customTypeface;
    public bool Outlines { get; set; }
    public long RenderedNodes { get; private set; }
    public void SetTypeface(SKTypeface typeface) { _textLines.Clear(); _customTypeface?.Dispose(); _customTypeface = typeface; }
    public void ClearCache()
    {
        ClearAppearance(); foreach (var p in _paths.Values) p.Path.Dispose(); _paths.Clear(); _recency.Clear(); _textLines.Clear();
    }
    public static SKColor Color(string? hex, double opacity = 1)
    {
        if (!SKColor.TryParse(hex, out var color)) color = hex?.ToLowerInvariant() switch { "white" => SKColors.White, "black" => SKColors.Black, "red" => SKColors.Red, "blue" => SKColors.Blue, "green" => SKColors.Green, "transparent" => SKColors.Transparent, _ => new SKColor(217, 217, 217) };
        return color.WithAlpha((byte)Math.Clamp(Math.Round(color.Alpha * opacity), 0, 255));
    }
    public static SKMatrix Matrix(Matrix2D m) => new((float)m.M11, (float)m.M21, (float)m.DX, (float)m.M12, (float)m.M22, (float)m.DY, 0, 0, 1);
    public static SKRect Rect(RectD r) => new((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
    public void TrimCache(IEnumerable<string> activeIds)
    {
        var active = activeIds.ToHashSet(StringComparer.Ordinal);
        foreach (var id in _paths.Keys.Where(id => !active.Contains(id)).ToArray()) RemoveCached(id);
        foreach (var key in _gradients.Keys.Where(k => !active.Contains(k.NodeId)).ToArray()) { _gradients[key].Shader.Dispose(); _gradients.Remove(key); }
        foreach (var id in _effects.Keys.Where(id => !active.Contains(id)).ToArray()) { _effects[id].Filter?.Dispose(); _effects.Remove(id); }
    }
    private void RemoveCached(string id)
    {
        if (!_paths.Remove(id, out var entry)) return;
        _recency.Remove(entry.Recency); entry.Path.Dispose();
    }
    public SKPath Geometry(DesignNode node)
    {
        var key = new GeometryKey(node.Kind, node.Width, node.Height, node.CornerRadius, node.Sides, node.StarRatio, node.PathData, node.PathWidth, node.PathHeight, node.Closed);
        if (_paths.TryGetValue(node.Id, out var cache) && cache.Key == key && PointsEqual(cache.Points, node))
        {
            GeometryCacheHits++; _recency.Remove(cache.Recency); _recency.AddLast(cache.Recency); return cache.Path;
        }
        var path = node.Kind == NodeKind.Path && node.PathData is null && node.Points.Count > 0 ? BuildEditableGeometry(node) : SKPath.ParseSvgPathData(VectorPath.Build(node)) ?? new SKPath();
        if ((node.Kind == NodeKind.Path || node.PathData is not null) && node.PathWidth > 0 && node.PathHeight > 0) path.Transform(SKMatrix.CreateScale((float)(node.Width / node.PathWidth), (float)(node.Height / node.PathHeight)));
        RemoveCached(node.Id);
        while (_paths.Count >= Math.Max(1, GeometryCacheCapacity) && _recency.First is { } oldest) RemoveCached(oldest.Value);
        var points = node.Kind == NodeKind.Path ? node.Points.Select(p => new PointKey(p.Position, p.ControlIn, p.ControlOut)).ToArray() : [];
        var recency = _recency.AddLast(node.Id); _paths[node.Id] = new(key, points, path, recency); GeometryBuilds++;
        return path;
    }
    private static SKPath BuildEditableGeometry(DesignNode node)
    {
        var path = new SKPath(); var points = node.Points;
        path.MoveTo((float)points[0].Position.X, (float)points[0].Position.Y);
        for (var i = 1; i < points.Count; i++) Segment(points[i - 1], points[i]);
        if (node.Closed) { Segment(points[^1], points[0]); path.Close(); }
        return path;
        void Segment(PathPoint a, PathPoint b)
        {
            if (a.ControlOut.HasValue || b.ControlIn.HasValue)
            {
                var first = a.ControlOut ?? a.Position; var second = b.ControlIn ?? b.Position;
                path.CubicTo((float)first.X, (float)first.Y, (float)second.X, (float)second.Y, (float)b.Position.X, (float)b.Position.Y);
            }
            else path.LineTo((float)b.Position.X, (float)b.Position.Y);
        }
    }
    private static bool PointsEqual(PointKey[] cached, DesignNode node)
    {
        if (node.Kind != NodeKind.Path) return true;
        if (cached.Length != node.Points.Count) return false;
        for (var i = 0; i < cached.Length; i++) { var p = node.Points[i]; if (cached[i] != new PointKey(p.Position, p.ControlIn, p.ControlOut)) return false; }
        return true;
    }
    public void Draw(SKCanvas canvas, IEnumerable<DesignNode> nodes, RectD? worldViewport = null)
    {
        var started = Stopwatch.GetTimestamp(); RenderedNodes = 0; CulledNodes = 0;
        foreach (var node in nodes) DrawNode(canvas, node, Matrix2D.Identity, worldViewport);
        LastDrawMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }
    public void DrawWorldNode(SKCanvas canvas, DesignNode node)
    {
        canvas.Save(); var parent = node.Parent?.WorldMatrix ?? Matrix2D.Identity;
        if (node.Parent is not null) canvas.Concat(Matrix(parent));
        DrawNode(canvas, node, parent, null); canvas.Restore();
    }
    private void DrawNode(SKCanvas canvas, DesignNode node, Matrix2D parent, RectD? viewport, Vec2? rootScroll = null)
    {
        if (!node.Visible || node.Opacity <= 0 || node.Kind == NodeKind.Slice) return;
        var world = node.LocalMatrix * parent;
        // Unclipped containers, text overflow and user-edited paths are conservative: never
        // reject them using only their nominal frame. Descendants are still culled individually.
        if (rootScroll is null && viewport is { } view && (node.ClipContent || node.Children.Count == 0 && node.Kind is not NodeKind.Text and not NodeKind.Path and not NodeKind.Arrow))
        {
            var padding = node.Strokes.Where(s => s.Visible).Select(s => s.Width / 2).DefaultIfEmpty(0).Max();
            padding += EffectPadding(node);
            if (!world.Map(node.LocalBounds.Inflate(padding + 1)).Intersects(view)) { CulledNodes++; return; }
        }
        RenderedNodes++;
        canvas.Save(); canvas.Concat(Matrix(node.LocalMatrix));
        var layer = node.Opacity < .999 || node.Blend != BlendKind.Normal || node.Shadows.Any(s => s.Visible);
        if (layer)
        {
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Clamp(node.Opacity * 255, 0, 255)), BlendMode = Blend(node.Blend) };
            paint.ImageFilter = EffectFilter(node);
            if (node.Children.Count == 0 && node.Kind is not NodeKind.Text and not NodeKind.Path and not NodeKind.Arrow)
            {
                // Bound the offscreen to a conservative simple-leaf footprint. An unbounded
                // save layer turns each tiny filtered shape into a viewport-sized raster pass.
                var outset = EffectPadding(node) + node.Strokes.Where(s => s.Visible).Select(s => s.Width / 2).DefaultIfEmpty(0).Max() + 1;
                canvas.SaveLayer(Rect(node.LocalBounds.Inflate(outset)), paint);
            }
            else canvas.SaveLayer(paint);
        }
        if (Outlines && node.Kind != NodeKind.Text)
        {
            using var outline = new SKPaint { IsAntialias = true, Color = new SKColor(80, 80, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            canvas.DrawPath(Geometry(node), outline);
        }
        else
        {
            for (var i = 0; i < node.Fills.Count; i++) if (node.Fills[i] is { Visible: true, Opacity: > 0 } fill) DrawFill(canvas, node, fill, i);
            foreach (var stroke in node.Strokes.Where(s => s.Visible && s.Width > 0))
            {
                using var paint = new SKPaint { IsAntialias = true, Color = Color(stroke.Color, stroke.Opacity), Style = SKPaintStyle.Stroke, StrokeWidth = (float)stroke.Width, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
                using var dash = stroke.Dashes.Count >= 2 && stroke.Dashes.All(d => d > 0) ? SKPathEffect.CreateDash(stroke.Dashes.Select(d => (float)d).ToArray(), 0) : null; paint.PathEffect = dash;
                if (node.Kind == NodeKind.Text) DrawText(canvas, node, paint); else canvas.DrawPath(Geometry(node), paint);
            }
        }
        if (node.ClipContent || rootScroll.HasValue)
        {
            using var clip = new SKPath(); clip.AddRoundRect(new SKRect(0, 0, (float)node.Width, (float)node.Height), (float)node.CornerRadius, (float)node.CornerRadius); canvas.ClipPath(clip, SKClipOperation.Intersect, true);
        }
        if (rootScroll is { } scroll) canvas.Translate((float)-scroll.X, (float)-scroll.Y);
        // An ancestor filter can sample offscreen descendant pixels. Do not cull its inputs.
        var childViewport = node.Shadows.Any(s => s.Visible) ? null : viewport;
        foreach (var child in node.Children) DrawNode(canvas, child, world, childViewport);
        if (layer) canvas.Restore(); canvas.Restore();
    }
    private SKTypeface Typeface(DesignNode node)
    {
        if (_customTypeface is not null && node.FontFamily == "Inter") return _customTypeface;
        var key = node.FontFamily + "|" + node.FontWeight;
        if (!_typefaces.TryGetValue(key, out var typeface)) _typefaces[key] = typeface = SKTypeface.FromFamilyName(node.FontFamily, new SKFontStyle(node.FontWeight, 5, SKFontStyleSlant.Upright)) ?? SKTypeface.Default;
        return typeface;
    }
    public void DrawText(SKCanvas canvas, DesignNode node, SKPaint paint)
    {
        using var font = new SKFont(Typeface(node), (float)node.FontSize) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true, Embolden = _customTypeface is not null && node.FontWeight >= 600 };
        var y = (float)node.FontSize;
        var key = new TextKey(node.Text, node.FontFamily, node.FontWeight, node.FontSize, node.Width, node.LetterSpacing);
        if (!_textLines.TryGetValue(key, out var lines))
        {
            if (_textLines.Count >= 2048) _textLines.Clear();
            lines = Wrap(node.Text, font, (float)node.Width, paint).Select(line => new TextLine(line, font.MeasureText(line, paint) + Math.Max(0, line.EnumerateRunes().Count() - 1) * (float)node.LetterSpacing)).ToArray();
            _textLines[key] = lines;
        }
        foreach (var measured in lines)
        {
            var line = measured.Text; var length = measured.Width;
            var x = node.TextAlign == TextAlignment.Center ? ((float)node.Width - length) / 2 : node.TextAlign == TextAlignment.Right ? (float)node.Width - length : 0;
            if (Math.Abs(node.LetterSpacing) < .001) canvas.DrawText(line, x, y, font, paint);
            else foreach (var rune in line.EnumerateRunes()) { var text = rune.ToString(); canvas.DrawText(text, x, y, font, paint); x += font.MeasureText(text, paint) + (float)node.LetterSpacing; }
            y += (float)(node.FontSize * node.LineHeight);
        }
    }
    private static IEnumerable<string> Wrap(string text, SKFont font, float width, SKPaint paint)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            if (font.MeasureText(paragraph, paint) <= width || !paragraph.Contains(' ')) { yield return paragraph; continue; }
            var line = "";
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureText(candidate, paint) > width) { yield return line; line = word; }
                else line = candidate;
            }
            yield return line;
        }
    }
    public DesignNode? HitTest(IEnumerable<DesignNode> roots, Vec2 point, bool deep = false, double tolerance = 4)
    {
        foreach (var node in roots.Reverse())
        {
            if (!node.IsEffectivelyVisible || node.IsEffectivelyLocked || node.Kind == NodeKind.Slice) continue;
            var local = node.WorldMatrix.Inverse.Map(point); var inside = node.LocalBounds.Contains(local);
            if (node.Children.Count == 0 && node.Kind is not NodeKind.Path && !node.LocalBounds.Inflate(tolerance + node.Strokes.Where(s => s.Visible).Select(s => s.Width).DefaultIfEmpty(0).Max()).Contains(local)) continue;
            var insideClip = inside;
            if (node.ClipContent && node.CornerRadius > 0)
            {
                using var clip = new SKPath(); clip.AddRoundRect(Rect(node.LocalBounds), (float)node.CornerRadius, (float)node.CornerRadius);
                insideClip = clip.Contains((float)local.X, (float)local.Y);
            }
            if (!node.ClipContent || insideClip)
            {
                var child = HitTest(node.Children, point, deep, tolerance);
                if (child is not null) return deep || node.Kind == NodeKind.Frame || node.Kind == NodeKind.Section ? child : node;
            }
            if (node.Kind == NodeKind.Text && inside) return node;
            if (node.Kind == NodeKind.Group && node.Children.Count > 0) continue;
            if (node.Kind == NodeKind.Frame && inside && node.Fills.Count > 0) return node;
            var path = Geometry(node);
            if (node.Fills.Any(f => f.Visible) && path.Contains((float)local.X, (float)local.Y)) return node;
            if (node.Strokes.Any(s => s.Visible && s.Width > 0))
            {
                using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = (float)Math.Max(tolerance * 2, node.Strokes.Where(s => s.Visible && s.Width > 0).Max(s => s.Width)), StrokeCap = SKStrokeCap.Round };
                using var outline = new SKPath(); stroke.GetFillPath(path, outline);
                if (outline.Contains((float)local.X, (float)local.Y)) return node;
            }
        }
        return null;
    }
    public byte[] ExportPng(IEnumerable<DesignNode> nodes, RectD bounds, double scale = 1)
    {
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale)); var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
        if (!double.IsFinite(scale) || scale <= 0 || width > 16384 || height > 16384 || (long)width * height > 64_000_000) throw new InvalidOperationException("Export is limited to 16,384 pixels per edge and 64 megapixels.");
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)) ?? throw new InvalidOperationException("Could not allocate export surface.");
        surface.Canvas.Clear(SKColors.Transparent); surface.Canvas.Scale((float)scale); surface.Canvas.Translate((float)-bounds.X, (float)-bounds.Y);
        var outlines = Outlines; Outlines = false;
        try { foreach (var n in nodes) DrawWorldNode(surface.Canvas, n); }
        finally { Outlines = outlines; }
        using var image = surface.Snapshot(); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }
    private static SKBlendMode Blend(BlendKind kind) => kind switch { BlendKind.Multiply => SKBlendMode.Multiply, BlendKind.Screen => SKBlendMode.Screen, BlendKind.Overlay => SKBlendMode.Overlay, BlendKind.Darken => SKBlendMode.Darken, BlendKind.Lighten => SKBlendMode.Lighten, BlendKind.Difference => SKBlendMode.Difference, _ => SKBlendMode.SrcOver };
    public void Dispose()
    {
        ClearCache(); foreach (var face in _typefaces.Values.Distinct()) if (face != SKTypeface.Default) face.Dispose(); _typefaces.Clear(); _customTypeface?.Dispose(); _customTypeface = null;
    }
}
