using System.Globalization;

namespace VectorSpace.Controls;

public sealed record StrokeOptions(int Alignment, int Cap, int Join, double MiterLimit, double DashOffset, string Dashes);

/// <summary>Reusable compact stroke geometry editor. Values describe behavior, not renderer objects;
/// hosts map the selection indices to their own model and own the undo transaction.</summary>
public sealed class StrokeOptionsControl : ShapeOptionsPanel
{
    private StrokeOptions _value;
    public event Action<StrokeOptions>? ValueCommitted;
    public StrokeOptionsControl(StrokeOptions value, bool allowAlignment = true)
    {
        _value = value; Spacing = 6;
        AutomationProperties.SetName(this, "Stroke geometry options");
        var alignment = new SegmentedControl(["Center", "Inside", "Outside"], value.Alignment) { IsEnabled = allowAlignment };
        alignment.SelectionChanged += i => Commit(_value with { Alignment = i }); Children.Add(alignment);
        var cap = Studio.Choice(["Butt", "Round", "Square"], new[] { "Butt", "Round", "Square" }[value.Cap], s => Commit(_value with { Cap = Array.IndexOf(new[] { "Butt", "Round", "Square" }, s) }), "Stroke cap");
        var join = Studio.Choice(["Miter", "Round", "Bevel"], new[] { "Miter", "Round", "Bevel" }[value.Join], s => Commit(_value with { Join = Array.IndexOf(new[] { "Miter", "Round", "Bevel" }, s) }), "Stroke join");
        Children.Add(Studio.Columns((cap, -1), (join, -1)));
        Children.Add(Studio.Columns((new NumericField("Miter", value.MiterLimit, v => Commit(_value with { MiterLimit = v })) { Minimum = 1, Maximum = 128 }, -1),
            (new NumericField("Phase", value.DashOffset, v => Commit(_value with { DashOffset = v })) { Minimum = -1e6, Maximum = 1e6 }, -1)));
        var dashes = Studio.Input(value.Dashes, "Dash pattern"); dashes.PlaceholderText = "Dash, gap — e.g. 8, 6";
        void SaveDashes()
        {
            if (dashes.Text == _value.Dashes) return;
            var values = dashes.Text.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length > 128 || values.Any(s => !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v) || v < 0 || v > 1e5)) { dashes.Text = _value.Dashes; return; }
            Commit(_value with { Dashes = dashes.Text });
        }
        dashes.LostFocus += (_, _) => SaveDashes();
        dashes.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { SaveDashes(); e.Handled = true; } else if (e.Key == VirtualKey.Escape) { dashes.Text = _value.Dashes; e.Handled = true; } };
        Children.Add(dashes);
        if (!allowAlignment) Children.Add(Studio.Text("Open contours and text use centered strokes.", 10, Studio.Muted));
    }
    private void Commit(StrokeOptions value) { if (_value == value) return; _value = value; ValueCommitted?.Invoke(value); }
}

public sealed record CornerOptions(bool Independent, double TopLeft, double TopRight, double BottomRight, double BottomLeft);

/// <summary>Independent-corner editor with a compact spatial 2×2 layout.</summary>
public sealed class CornerOptionsControl : ShapeOptionsPanel
{
    private CornerOptions _value;
    public event Action<CornerOptions>? ValueCommitted;
    public CornerOptionsControl(CornerOptions value)
    {
        _value = value; Spacing = 6;
        var mode = new SegmentedControl(["Linked corners", "Independent"], value.Independent ? 1 : 0);
        mode.SelectionChanged += i => Commit(i == 0 ? new(false, _value.TopLeft, _value.TopLeft, _value.TopLeft, _value.TopLeft) : _value with { Independent = true });
        Children.Add(mode);
        NumericField Field(string name, double v, Func<CornerOptions, double, CornerOptions> update) => new(name, v, n => Commit(_value.Independent ? update(_value, n) : new(false, n, n, n, n))) { Minimum = 0, Maximum = 1e6 };
        if (value.Independent)
        {
            Children.Add(Studio.Columns((Field("Top L", value.TopLeft, (v, n) => v with { TopLeft = n }), -1), (Field("Top R", value.TopRight, (v, n) => v with { TopRight = n }), -1)));
            Children.Add(Studio.Columns((Field("Bottom L", value.BottomLeft, (v, n) => v with { BottomLeft = n }), -1), (Field("Bottom R", value.BottomRight, (v, n) => v with { BottomRight = n }), -1)));
        }
        else Children.Add(Field("Radius", value.TopLeft, (v, n) => v with { TopLeft = n }));
    }
    private void Commit(CornerOptions value) { if (_value == value) return; _value = value; ValueCommitted?.Invoke(value); }
}

public sealed record ArcOptions(double Start, double Sweep, double InnerRadius, bool Open);

/// <summary>Arc angles are clockwise degrees; the radius field is a percentage of the ellipse.</summary>
public sealed class ArcOptionsControl : ShapeOptionsPanel
{
    private ArcOptions _value;
    public event Action<ArcOptions>? ValueCommitted;
    public ArcOptionsControl(ArcOptions value)
    {
        _value = value; Spacing = 6;
        var mode = new SegmentedControl(["Sector", "Ring", "Open arc"], value.Open ? 2 : value.InnerRadius > 0 ? 1 : 0);
        mode.SelectionChanged += i => Commit(_value with { Open = i == 2, InnerRadius = i == 1 ? Math.Max(.5, _value.InnerRadius) : i == 0 ? 0 : _value.InnerRadius });
        Children.Add(mode);
        Children.Add(Studio.Columns((new NumericField("Start", value.Start, v => Commit(_value with { Start = v })) { Minimum = -360000, Maximum = 360000 }, -1),
            (new NumericField("Sweep", value.Sweep, v => Commit(_value with { Sweep = v })) { Minimum = -360, Maximum = 360 }, -1)));
        Children.Add(new NumericField("Inner %", value.InnerRadius * 100, v => Commit(_value with { InnerRadius = v / 100 })) { Minimum = 0, Maximum = 100, IsEnabled = !value.Open });
    }
    private void Commit(ArcOptions value) { if (_value == value) return; _value = value; ValueCommitted?.Invoke(value); }
}
