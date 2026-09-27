using System.Globalization;

namespace VectorSpace.Controls;

/// <summary>A numeric editor with finite-value validation, arrow stepping and label-drag scrubbing.</summary>
public sealed class NumericField : UserControl
{
    private readonly TextBox _input;
    private readonly Border _root;
    private double _value;
    private bool _scrubbing;
    private double _startX, _startValue;
    public event Action<double>? ValueCommitted;
    public double Minimum { get; set; } = -1_000_000;
    public double Maximum { get; set; } = 1_000_000;
    public double Step { get; set; } = 1;
    public double Value { get => _value; set { _value = double.IsFinite(value) ? value : 0; _input.Text = _value.ToString("0.##", CultureInfo.InvariantCulture); } }
    public NumericField(string label, double value, Action<double>? commit = null)
    {
        _input = Studio.Input(); _input.Background = Studio.Brush("#00FFFFFF"); _input.Padding = new(1, 4, 5, 4);
        AutomationProperties.SetName(_input, label); ToolTipService.SetToolTip(_input, label);
        var prefix = new Border { Width = 28, Background = Studio.Brush("#00FFFFFF"), Child = Studio.Text(label, 10, Studio.Muted), Padding = new(8, 0, 0, 0) };
        _root = new Border { Background = Studio.Brush(Studio.Field), CornerRadius = new(5), Child = Studio.Columns((prefix, 28), (_input, -1)), Height = 30 };
        ((Grid)_root.Child).ColumnSpacing = 0; Content = _root; Value = value;
        if (commit is not null) ValueCommitted += commit;
        _input.LostFocus += (_, _) => Commit();
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == VirtualKey.Escape) { Value = _value; e.Handled = true; }
            else if (e.Key is VirtualKey.Up or VirtualKey.Down)
            {
                var amount = Step * (e.Key == VirtualKey.Up ? 1 : -1); Value = Math.Clamp(Parse(_input.Text, _value) + amount, Minimum, Maximum); ValueCommitted?.Invoke(Value); e.Handled = true;
            }
        };
        prefix.PointerPressed += (_, e) => { _scrubbing = true; _startX = e.GetCurrentPoint(prefix).Position.X; _startValue = Value; prefix.CapturePointer(e.Pointer); e.Handled = true; };
        prefix.PointerMoved += (_, e) => { if (_scrubbing) Value = Math.Clamp(_startValue + Math.Round(e.GetCurrentPoint(prefix).Position.X - _startX) * Step, Minimum, Maximum); };
        prefix.PointerReleased += (_, e) => { if (!_scrubbing) return; _scrubbing = false; prefix.ReleasePointerCapture(e.Pointer); ValueCommitted?.Invoke(Value); e.Handled = true; };
        prefix.PointerCanceled += (_, _) => { if (_scrubbing) { _scrubbing = false; Value = _startValue; } };
    }
    private void Commit()
    {
        if (!double.TryParse(_input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { Value = _value; return; }
        value = Math.Clamp(value, Minimum, Maximum); if (Math.Abs(value - _value) < 1e-9) return;
        Value = value; ValueCommitted?.Invoke(value);
    }
    private static double Parse(string text, double fallback) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : fallback;
}
