using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace VectorSpace.Controls;

/// <summary>Original 24-unit vector icon vocabulary, rendered at the display's device scale.</summary>
public sealed class IconView : SKCanvasElement
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(IconView), new PropertyMetadata("move", Changed));
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(string), typeof(IconView), new PropertyMetadata(Studio.Ink, Changed));
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string Color { get => (string)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public IconView() { Width = Height = 18; IsHitTestVisible = false; }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((IconView)sender).Invalidate();
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        canvas.Save(); canvas.Scale((float)(area.Width / 24), (float)(area.Height / 24));
        using var path = SKPath.ParseSvgPathData(Paths.GetValueOrDefault(Glyph) ?? Paths["rectangle"]);
        using var paint = new SKPaint { IsAntialias = true, Color = SKColor.TryParse(Color, out var c) ? c : SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
        canvas.DrawPath(path, paint); canvas.Restore();
    }
    public static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        ["move"] = "M5 3 L19 13 L12 14 L9 21 Z M12 14 L17 21",
        ["scale"] = "M4 4 H13 M4 4 V13 M4 4 L20 20 M13 20 H20 V13",
        ["frame"] = "M8 3 V21 M16 3 V21 M3 8 H21 M3 16 H21",
        ["section"] = "M3 6 V20 H21 V9 H12 L9 6 Z",
        ["rectangle"] = "M5 4 H19 Q20 4 20 5 V19 Q20 20 19 20 H5 Q4 20 4 19 V5 Q4 4 5 4 Z",
        ["ellipse"] = "M21 12 A9 9 0 1 1 3 12 A9 9 0 1 1 21 12 Z",
        ["line"] = "M4 20 L20 4",
        ["arrow"] = "M4 20 L20 4 M10 4 H20 V14",
        ["polygon"] = "M12 3 L22 20 H2 Z",
        ["star"] = "M12 2 L15 9 L22 10 L17 15 L18 22 L12 18 L6 22 L7 15 L2 10 L9 9 Z",
        ["pen"] = "M4 20 L6 10 L16 3 L21 8 L14 18 Z M6 10 L14 18 M4 20 L11 13 M16 3 L18 1 L23 6 L21 8",
        ["pencil"] = "M4 16 L16 4 L20 8 L8 20 L3 21 Z M13 7 L17 11",
        ["text"] = "M4 5 V3 H20 V5 M12 3 V21 M8 21 H16",
        ["hand"] = "M7 12 V6 Q7 3 10 5 V11 V3 Q12 1 13 4 V11 V5 Q15 3 16 6 V12 V9 Q19 6 19 11 V16 Q19 22 12 22 Q8 22 5 17 L2 12 Q3 9 5 11 L7 13",
        ["comment"] = "M5 4 H19 Q21 4 21 6 V15 Q21 17 19 17 H10 L5 21 V17 H5 Q3 17 3 15 V6 Q3 4 5 4 Z",
        ["slice"] = "M4 8 V4 H8 M16 4 H20 V8 M20 16 V20 H16 M8 20 H4 V16 M8 8 H16 V16 H8 Z",
        ["chevron-down"] = "M7 10 L12 15 L17 10",
        ["chevron-right"] = "M10 7 L15 12 L10 17",
        ["chevron-left"] = "M14 7 L9 12 L14 17",
        ["plus"] = "M12 5 V19 M5 12 H19",
        ["minus"] = "M5 12 H19",
        ["close"] = "M6 6 L18 18 M18 6 L6 18",
        ["search"] = "M17 10 A7 7 0 1 1 3 10 A7 7 0 1 1 17 10 Z M15 15 L21 21",
        ["more"] = "M5 12 H5.1 M12 12 H12.1 M19 12 H19.1",
        ["layers"] = "M12 3 L22 8 L12 13 L2 8 Z M3 13 L12 18 L21 13 M3 18 L12 23 L21 18",
        ["component"] = "M12 2 L17 7 L12 12 L7 7 Z M7 7 L12 12 L7 17 L2 12 Z M17 7 L22 12 L17 17 L12 12 Z M12 12 L17 17 L12 22 L7 17 Z",
        ["instance"] = "M12 3 L21 12 L12 21 L3 12 Z",
        ["group"] = "M4 4 H8 M16 4 H20 V8 M20 16 V20 H16 M8 20 H4 V16 M4 8 V4 M8 8 H16 V16 H8 Z",
        ["eye"] = "M2 12 Q12 0 22 12 Q12 24 2 12 Z M15 12 A3 3 0 1 1 9 12 A3 3 0 1 1 15 12 Z",
        ["eye-off"] = "M3 3 L21 21 M3 12 Q7 6 11 6 M14 6 Q18 7 22 12 Q19 17 15 18 M11 18 Q6 17 2 12",
        ["lock"] = "M5 10 H19 V21 H5 Z M8 10 V6 A4 4 0 0 1 16 6 V10",
        ["unlock"] = "M5 10 H19 V21 H5 Z M8 10 V6 A4 4 0 0 1 16 6",
        ["play"] = "M7 4 L21 12 L7 20 Z",
        ["undo"] = "M8 4 L3 9 L8 14 M3 9 H14 Q21 9 21 16 V20",
        ["redo"] = "M16 4 L21 9 L16 14 M21 9 H10 Q3 9 3 16 V20",
        ["download"] = "M12 3 V16 M7 11 L12 16 L17 11 M4 17 V21 H20 V17",
        ["upload"] = "M12 17 V4 M7 9 L12 4 L17 9 M4 17 V21 H20 V17",
        ["save"] = "M4 3 H17 L21 7 V21 H3 V3 Z M7 3 V10 H17 V3 M7 21 V14 H17 V21",
        ["check"] = "M4 12 L9 17 L20 6",
        ["left"] = "M4 3 V21 M8 6 H19 V10 H8 Z M8 14 H15 V18 H8 Z",
        ["center"] = "M12 2 V22 M5 6 H19 V10 H5 Z M8 14 H16 V18 H8 Z",
        ["right"] = "M20 3 V21 M5 6 H16 V10 H5 Z M9 14 H16 V18 H9 Z",
        ["top"] = "M3 4 H21 M6 8 V19 H10 V8 Z M14 8 V15 H18 V8 Z",
        ["middle"] = "M2 12 H22 M6 5 V19 H10 V5 Z M14 8 V16 H18 V8 Z",
        ["bottom"] = "M3 20 H21 M6 5 V16 H10 V5 Z M14 9 V16 H18 V9 Z",
        ["horizontal"] = "M3 5 H8 V19 H3 Z M16 5 H21 V19 H16 Z M10 12 H14 M12 10 L14 12 L12 14",
        ["vertical"] = "M5 3 H19 V8 H5 Z M5 16 H19 V21 H5 Z M12 10 V14 M10 12 L12 14 L14 12",
        ["grid"] = "M3 3 H9 V9 H3 Z M15 3 H21 V9 H15 Z M3 15 H9 V21 H3 Z M15 15 H21 V21 H15 Z",
        ["link"] = "M9 15 L15 9 M7 13 L5 15 A4 4 0 0 0 11 21 L14 18 M10 6 L13 3 A4 4 0 0 1 19 9 L17 11",
        ["rotate"] = "M18 6 A8 8 0 1 0 20 14 M18 2 V7 H13",
        ["radius"] = "M5 20 V12 Q5 5 12 5 H20 M2 12 H8 M12 2 V8",
        ["opacity"] = "M12 3 Q20 12 20 16 A8 8 0 1 1 4 16 Q4 12 12 3 Z M12 7 V21",
        ["flip"] = "M12 2 V22 M3 7 L9 17 H3 Z M21 7 L15 17 H21 Z",
        ["union"] = "M3 3 H14 V8 H21 V21 H8 V14 H3 Z",
        ["subtract"] = "M3 3 H14 V8 H8 V14 H3 Z M8 8 H21 V21 H8 Z",
        ["intersect"] = "M3 3 H14 V14 H3 Z M8 8 H21 V21 H8 Z M8 8 H14 V14 H8 Z",
        ["exclude"] = "M3 3 H14 V8 H8 V14 H3 Z M14 8 H21 V21 H8 V14 H14 Z",
        ["code"] = "M8 6 L2 12 L8 18 M16 6 L22 12 L16 18 M14 3 L10 21",
        ["help"] = "M21 12 A9 9 0 1 1 3 12 A9 9 0 1 1 21 12 Z M9 8 Q9 4 13 6 Q17 8 12 12 V14 M12 18 H12.1",
        ["sidebar"] = "M3 4 H21 V20 H3 Z M9 4 V20 M5 8 H7 M5 12 H7",
        ["trash"] = "M3 6 H21 M9 6 V3 H15 V6 M6 6 L7 21 H17 L18 6 M10 10 V17 M14 10 V17",
        ["logo"] = "M3 4 L12 21 L21 4 L12 10 Z M3 4 L12 10 L21 4",
        ["sparkle"] = "M12 2 L15 9 L22 12 L15 15 L12 22 L9 15 L2 12 L9 9 Z",
        ["ruler"] = "M3 16 L16 3 L22 9 L9 22 Z M7 12 L10 15 M10 9 L13 12 M13 6 L16 9",
        ["page"] = "M5 3 H14 L20 9 V21 H5 Z M14 3 V9 H20 M8 13 H16 M8 17 H14"
    };
}
