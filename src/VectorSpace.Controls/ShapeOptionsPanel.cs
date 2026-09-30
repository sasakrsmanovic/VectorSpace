namespace VectorSpace.Controls;

/// <summary>Control boundary for dense shape editors. IsEnabled is inherited by every child,
/// including keyboard-focusable fields; a hit-test-only disabled panel is insufficient.</summary>
public abstract class ShapeOptionsPanel : UserControl
{
    private readonly StackPanel _body = new();
    protected UIElementCollection Children => _body.Children;
    protected double Spacing { get => _body.Spacing; set => _body.Spacing = value; }
    protected ShapeOptionsPanel()
    {
        Content = _body;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }
}
