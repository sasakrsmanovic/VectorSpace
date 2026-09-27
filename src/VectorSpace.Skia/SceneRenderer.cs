using SkiaSharp;
using VectorSpace.Core;

namespace VectorSpace.Skia;

/// <summary>Retained geometry cache with explicit native-resource ownership. No Uno dependency.</summary>
public sealed class SceneRenderer : IDisposable
{
    private sealed record CachedPath(string Signature, SKPath Path);
    private readonly Dictionary<string, CachedPath> _paths = [];
    private readonly Dictionary<string, SKTypeface> _typefaces = [];
    private SKTypeface? _customTypeface;
    public bool Outlines { get; set; }
    public long RenderedNodes { get; private set; }
    public void SetTypeface(SKTypeface typeface) { _customTypeface?.Dispose(); _customTypeface = typeface; }
    public void ClearCache()
    {
        foreach (var p in _paths.Values) p.Path.Dispose(); _paths.Clear();
    }
    public static SKColor Color(string? hex, double opacity = 1)
    {
        if (!SKColor.TryParse(hex, out var color)) color = hex?.ToLowerInvariant() switch { "white" => SKColors.White, "black" => SKColors.Black, "red" => SKColors.Red, "blue" => SKColors.Blue, "green" => SKColors.Green, "transparent" => SKColors.Transparent, _ => new SKColor(217, 217, 217) };
        return color.WithAlpha((byte)Math.Clamp(Math.Round(color.Alpha * opacity), 0, 255));
    }
    public static SKMatrix Matrix(Matrix2D m) => new((float)m.M11, (float)m.M21, (float)m.DX, (float)m.M12, (float)m.M22, (float)m.DY, 0, 0, 1);
    public static SKRect Rect(RectD r) => new((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
    public SKPath Geometry(DesignNode node)
    {
        var signature = VectorPath.Build(node);
        if (node.Kind == NodeKind.Path && node.PathWidth > 0 && node.PathHeight > 0) signature += $"|{node.Width:R}|{node.Height:R}|{node.PathWidth:R}|{node.PathHeight:R}";
        if (_paths.TryGetValue(node.Id, out var cache) && cache.Signature == signature) return cache.Path;
        var path = SKPath.ParseSvgPathData(VectorPath.Build(node)) ?? new SKPath();
        if (node.Kind == NodeKind.Path && node.PathWidth > 0 && node.PathHeight > 0) path.Transform(SKMatrix.CreateScale((float)(node.Width / node.PathWidth), (float)(node.Height / node.PathHeight)));
        if (cache is not null) cache.Path.Dispose();
        if (_paths.Count > 100_000) ClearCache();
        _paths[node.Id] = new(signature, path); return path;
    }
    public void Draw(SKCanvas canvas, IEnumerable<DesignNode> nodes, RectD? worldViewport = null)
    {
        RenderedNodes = 0;
        foreach (var node in nodes)
        {
            if (worldViewport.HasValue && !node.WorldBounds.Inflate(100).Intersects(worldViewport.Value) && (node.Children.Count == 0 || node.ClipContent)) continue;
            DrawNode(canvas, node);
        }
    }
    public void DrawWorldNode(SKCanvas canvas, DesignNode node)
    {
        canvas.Save(); if (node.Parent is not null) canvas.Concat(Matrix(node.Parent.WorldMatrix)); DrawNode(canvas, node); canvas.Restore();
    }
    private void DrawNode(SKCanvas canvas, DesignNode node)
    {
        if (!node.Visible || node.Opacity <= 0 || node.Kind == NodeKind.Slice) return;
        RenderedNodes++;
        canvas.Save(); canvas.Concat(Matrix(node.LocalMatrix));
        var layer = node.Opacity < .999 || node.Blend != BlendKind.Normal || node.Shadows.Any(s => s.Visible);
        if (layer)
        {
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Clamp(node.Opacity * 255, 0, 255)), BlendMode = Blend(node.Blend) };
            var shadow = node.Shadows.FirstOrDefault(s => s.Visible);
            using var filter = shadow is null ? null : SKImageFilter.CreateDropShadow((float)shadow.X, (float)shadow.Y, (float)Math.Clamp(shadow.Blur / 2, 0, 256), (float)Math.Clamp(shadow.Blur / 2, 0, 256), Color(shadow.Color, shadow.Opacity));
            paint.ImageFilter = filter; canvas.SaveLayer(paint);
        }
        if (Outlines && node.Kind != NodeKind.Text)
        {
            using var outline = new SKPaint { IsAntialias = true, Color = new SKColor(80, 80, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            canvas.DrawPath(Geometry(node), outline);
        }
        else
        {
            foreach (var fill in node.Fills.Where(f => f.Visible))
            {
                using var paint = new SKPaint { IsAntialias = true, Color = Color(fill.Color, fill.Opacity), Style = SKPaintStyle.Fill };
                using var shader = Shader(fill, node.Width, node.Height); paint.Shader = shader;
                if (node.Kind == NodeKind.Text) DrawText(canvas, node, paint); else canvas.DrawPath(Geometry(node), paint);
            }
            foreach (var stroke in node.Strokes.Where(s => s.Visible && s.Width > 0))
            {
                using var paint = new SKPaint { IsAntialias = true, Color = Color(stroke.Color, stroke.Opacity), Style = SKPaintStyle.Stroke, StrokeWidth = (float)stroke.Width, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
                using var dash = stroke.Dashes.Count >= 2 && stroke.Dashes.All(d => d > 0) ? SKPathEffect.CreateDash(stroke.Dashes.Select(d => (float)d).ToArray(), 0) : null; paint.PathEffect = dash;
                if (node.Kind == NodeKind.Text) DrawText(canvas, node, paint); else canvas.DrawPath(Geometry(node), paint);
            }
        }
        if (node.ClipContent)
        {
            using var clip = new SKPath(); clip.AddRoundRect(new SKRect(0, 0, (float)node.Width, (float)node.Height), (float)node.CornerRadius, (float)node.CornerRadius); canvas.ClipPath(clip, SKClipOperation.Intersect, true);
        }
        foreach (var child in node.Children) DrawNode(canvas, child);
        if (layer) canvas.Restore(); canvas.Restore();
    }
    private static SKShader? Shader(FillStyle fill, double width, double height)
    {
        if (fill.Kind == FillKind.Solid || fill.Stops.Count < 2) return null;
        var stops = fill.Stops.OrderBy(s => s.Offset).ToArray(); var colors = stops.Select(s => Color(s.Color, fill.Opacity)).ToArray(); var positions = stops.Select(s => (float)Math.Clamp(s.Offset, 0, 1)).ToArray();
        var start = new SKPoint((float)(width * fill.Start.X), (float)(height * fill.Start.Y)); var end = new SKPoint((float)(width * fill.End.X), (float)(height * fill.End.Y));
        return fill.Kind == FillKind.LinearGradient ? SKShader.CreateLinearGradient(start, end, colors, positions, SKShaderTileMode.Clamp) : SKShader.CreateRadialGradient(start, Math.Max(1, (float)Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2))), colors, positions, SKShaderTileMode.Clamp);
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
        foreach (var line in Wrap(node.Text, font, (float)node.Width, paint))
        {
            var length = font.MeasureText(line, paint) + Math.Max(0, line.Length - 1) * (float)node.LetterSpacing;
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
            if (!node.ClipContent || inside)
            {
                var child = HitTest(node.Children, point, deep, tolerance);
                if (child is not null) return deep || node.Kind == NodeKind.Frame || node.Kind == NodeKind.Section ? child : node;
            }
            if (node.Kind == NodeKind.Text && inside) return node;
            if (node.Kind == NodeKind.Group && node.Children.Count > 0) continue;
            if (node.Kind == NodeKind.Frame && inside && node.Fills.Count > 0) return node;
            var path = Geometry(node);
            if (node.Fills.Any(f => f.Visible) && path.Contains((float)local.X, (float)local.Y)) return node;
            if (node.Strokes.Count > 0)
            {
                using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = (float)Math.Max(tolerance * 2, node.Strokes.Max(s => s.Width)), StrokeCap = SKStrokeCap.Round };
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
