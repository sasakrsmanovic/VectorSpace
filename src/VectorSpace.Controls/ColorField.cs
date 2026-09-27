using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace VectorSpace.Controls;

public sealed class ColorField : UserControl
{
    private readonly TextBox _text;
    private readonly StudioButton _swatch;
    private string _value;
    public event Action<string>? ColorCommitted;
    public string Value
    {
        get => _value;
        set { _value = value; _text.Text = value.TrimStart('#').ToUpperInvariant(); _swatch.Background = Studio.Brush(value); _swatch.RestBackground = value; }
    }
    public ColorField(string color, Action<string> commit)
    {
        _value = color; _text = Studio.Input(color.TrimStart('#'), "Hex color");
        _swatch = new StudioButton { Width = 24, Height = 24, Padding = new(0), CornerRadius = new(4), BorderThickness = new(1), BorderBrush = Studio.Brush("#22000000") };
        AutomationProperties.SetName(_swatch, "Choose color");
        Content = Studio.Columns((_swatch, 24), (_text, -1)); Value = color; ColorCommitted += commit;
        _text.LostFocus += (_, _) => CommitText();
        _text.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { CommitText(); e.Handled = true; } };
        _swatch.Click += (_, _) =>
        {
            var spectrum = new ColorSpectrum { Width = 248, Height = 166 }; spectrum.SetColor(Value);
            var flyout = new Flyout(); var root = new StackPanel { Spacing = 12 };
            root.Children.Add(Studio.Text("Custom color", 12, Studio.Ink, true)); root.Children.Add(spectrum);
            var palette = new Grid { ColumnSpacing = 6 };
            var colors = new[] { "#FFFFFF", "#1E1E1E", "#0D99FF", "#7B61FF", "#F24822", "#FFCD29", "#14AE5C", "#FFA6D5" };
            for (var i = 0; i < colors.Length; i++)
            {
                var c = colors[i]; var b = new StudioButton("", () => { Set(c); flyout.Hide(); }) { Width = 25, Height = 25, RestBackground = c, Background = Studio.Brush(c), BorderThickness = new(1), BorderBrush = Studio.Brush("#22000000") };
                AutomationProperties.SetName(b, c); palette.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); Grid.SetColumn(b, i); palette.Children.Add(b);
            }
            root.Children.Add(palette); spectrum.ColorCommitted += c => { Set(c); }; flyout.Content = root; flyout.ShowAt(_swatch);
        };
    }
    private void Set(string color) { if (_value == color) return; Value = color; ColorCommitted?.Invoke(color); }
    private void CommitText()
    {
        var candidate = "#" + _text.Text.Trim().TrimStart('#');
        if (candidate.Length is 7 or 9 && SKColor.TryParse(candidate, out _)) Set(candidate.ToUpperInvariant()); else Value = _value;
    }
}

/// <summary>Native Skia HSV picker; no browser HTML or system color-dialog dependency.</summary>
public sealed class ColorSpectrum : SKCanvasElement
{
    private float _hue = 260, _saturation = .7f, _value = .95f;
    private bool _dragging, _hueDrag;
    public event Action<string>? ColorCommitted;
    public ColorSpectrum()
    {
        PointerPressed += (_, e) => { _dragging = true; _hueDrag = e.GetCurrentPoint(this).Position.X > ActualWidth - 24; CapturePointer(e.Pointer); Update(e.GetCurrentPoint(this).Position); e.Handled = true; };
        PointerMoved += (_, e) => { if (_dragging) Update(e.GetCurrentPoint(this).Position); };
        PointerReleased += (_, e) => { if (!_dragging) return; _dragging = false; ReleasePointerCapture(e.Pointer); ColorCommitted?.Invoke(Hex()); e.Handled = true; };
        PointerCanceled += (_, _) => _dragging = false;
    }
    public void SetColor(string hex)
    {
        if (SKColor.TryParse(hex, out var c)) { c.ToHsv(out _hue, out var s, out var v); _saturation = s / 100; _value = v / 100; } Invalidate();
    }
    private string Hex() { var c = SKColor.FromHsv(_hue, _saturation * 100, _value * 100); return $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}"; }
    private void Update(Point point)
    {
        if (_hueDrag) _hue = (float)Math.Clamp(point.Y / Math.Max(1, ActualHeight) * 360, 0, 360);
        else { _saturation = (float)Math.Clamp(point.X / Math.Max(1, ActualWidth - 36), 0, 1); _value = 1 - (float)Math.Clamp(point.Y / Math.Max(1, ActualHeight), 0, 1); } Invalidate();
    }
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var w = (float)area.Width - 36; var h = (float)area.Height;
        using var paint = new SKPaint { IsAntialias = true };
        using var horizontal = SKShader.CreateLinearGradient(new(0, 0), new(w, 0), [SKColors.White, SKColor.FromHsv(_hue, 100, 100)], null, SKShaderTileMode.Clamp);
        paint.Shader = horizontal; canvas.DrawRoundRect(new SKRect(0, 0, w, h), 4, 4, paint);
        using var vertical = SKShader.CreateLinearGradient(new(0, 0), new(0, h), [SKColors.Transparent, SKColors.Black], null, SKShaderTileMode.Clamp);
        paint.Shader = vertical; canvas.DrawRoundRect(new SKRect(0, 0, w, h), 4, 4, paint);
        using var hue = SKShader.CreateLinearGradient(new(0, 0), new(0, h), Enumerable.Range(0, 7).Select(i => SKColor.FromHsv(i * 60, 100, 100)).ToArray(), null, SKShaderTileMode.Clamp);
        paint.Shader = hue; canvas.DrawRoundRect(new SKRect(w + 12, 0, w + 32, h), 4, 4, paint);
        paint.Shader = null; paint.Color = SKColors.White; paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 2;
        canvas.DrawCircle(_saturation * w, (1 - _value) * h, 5, paint);
        canvas.DrawRoundRect(new SKRect(w + 10, _hue / 360 * h - 3, w + 34, _hue / 360 * h + 3), 2, 2, paint);
    }
}
