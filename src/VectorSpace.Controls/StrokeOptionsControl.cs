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

