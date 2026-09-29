namespace VectorSpace.Core;

/// <summary>Deep copies mutable style data without serializing a scene/subtree or duplicating
/// immutable embedded raster strings. No caches or references to owning document nodes.</summary>
public static class StyleCloner
{
    public static List<FillStyle> Fills(IEnumerable<FillStyle> values) => values.Select(Fill).ToList();
    public static FillStyle Fill(FillStyle f) => new()
    {
        Kind = f.Kind, Color = f.Color, Opacity = f.Opacity, Visible = f.Visible, Blend = f.Blend,
        Start = f.Start, End = f.End, Stops = f.Stops.Select(s => new GradientStop { Offset = s.Offset, Color = s.Color, Opacity = s.Opacity }).ToList(),
        ImageData = f.ImageData, ImageMode = f.ImageMode, ImageScale = f.ImageScale, ImageOffset = f.ImageOffset,
        ImageRotation = f.ImageRotation, Exposure = f.Exposure, Contrast = f.Contrast, Saturation = f.Saturation,
        GradientTransform = f.GradientTransform, GradientUserSpace = f.GradientUserSpace, Spread = f.Spread,
        GradientRadius = f.GradientRadius, GradientFocal = f.GradientFocal
    };
    public static List<StrokeStyle> Strokes(IEnumerable<StrokeStyle> values) => values.Select(s => new StrokeStyle
    { Color = s.Color, Width = s.Width, Opacity = s.Opacity, Visible = s.Visible, Dashes = [.. s.Dashes], Alignment = s.Alignment, Cap = s.Cap, Join = s.Join, MiterLimit = s.MiterLimit, DashOffset = s.DashOffset }).ToList();
    public static List<ShadowStyle> Effects(IEnumerable<ShadowStyle> values) => values.Select(s => new ShadowStyle
    { Kind = s.Kind, Color = s.Color, X = s.X, Y = s.Y, Blur = s.Blur, Spread = s.Spread, Visible = s.Visible, Opacity = s.Opacity }).ToList();
    public static bool SameFills(IReadOnlyList<FillStyle> a, IReadOnlyList<FillStyle> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            var x = a[i]; var y = b[i];
            if (x.Kind != y.Kind || x.Color != y.Color || x.Opacity != y.Opacity || x.Visible != y.Visible || x.Blend != y.Blend ||
                x.Start != y.Start || x.End != y.End || x.ImageData != y.ImageData || x.ImageMode != y.ImageMode || x.ImageScale != y.ImageScale ||
                x.ImageOffset != y.ImageOffset || x.ImageRotation != y.ImageRotation || x.Exposure != y.Exposure || x.Contrast != y.Contrast || x.Saturation != y.Saturation ||
                x.GradientTransform != y.GradientTransform || x.GradientUserSpace != y.GradientUserSpace || x.Spread != y.Spread || x.GradientRadius != y.GradientRadius || x.GradientFocal != y.GradientFocal || x.Stops.Count != y.Stops.Count) return false;
            for (var j = 0; j < x.Stops.Count; j++)
                if (x.Stops[j].Color != y.Stops[j].Color || x.Stops[j].Opacity != y.Stops[j].Opacity || x.Stops[j].Offset != y.Stops[j].Offset) return false;
        }
        return true;
    }
    public static bool SameStrokes(IReadOnlyList<StrokeStyle> a, IReadOnlyList<StrokeStyle> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i].Alignment != b[i].Alignment || a[i].Cap != b[i].Cap || a[i].Join != b[i].Join || a[i].MiterLimit != b[i].MiterLimit || a[i].DashOffset != b[i].DashOffset || a[i].Color != b[i].Color || a[i].Width != b[i].Width || a[i].Opacity != b[i].Opacity || a[i].Visible != b[i].Visible || !a[i].Dashes.SequenceEqual(b[i].Dashes)) return false;
        return true;
    }
    public static bool SameEffects(IReadOnlyList<ShadowStyle> a, IReadOnlyList<ShadowStyle> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            var x = a[i]; var y = b[i];
            if (x.Kind != y.Kind || x.Color != y.Color || x.X != y.X || x.Y != y.Y || x.Blur != y.Blur || x.Spread != y.Spread || x.Opacity != y.Opacity || x.Visible != y.Visible) return false;
        }
        return true;
    }
}
