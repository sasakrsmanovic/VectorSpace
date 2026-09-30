using System.Globalization;

namespace VectorSpace.Controls;

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
