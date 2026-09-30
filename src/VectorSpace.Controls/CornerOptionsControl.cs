using System.Globalization;

namespace VectorSpace.Controls;

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

