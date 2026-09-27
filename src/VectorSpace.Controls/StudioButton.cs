namespace VectorSpace.Controls;

public class StudioButton : Button
{
    private bool _selected;
    private bool _primary;
    private bool _hover;
    public bool IsSelected { get => _selected; set { _selected = value; Refresh(); } }
    public bool IsPrimary { get => _primary; set { _primary = value; Refresh(); } }
    public string RestBackground { get; set; } = "#00FFFFFF";
    public StudioButton()
    {
        Style = (Style)StudioResources.Current["VS.Button"]; FontFamily = Studio.Font;
        PointerEntered += (_, _) => { _hover = true; Refresh(); };
        PointerExited += (_, _) => { _hover = false; Refresh(); };
        Refresh();
    }
    public StudioButton(string text, Action clicked) : this()
    {
        Content = text; AutomationProperties.SetName(this, text); Click += (_, _) => clicked();
    }
    protected virtual void Refresh()
    {
        Background = Studio.Brush(_selected || _primary ? Studio.Accent : _hover ? "#EEEEEE" : RestBackground);
        Foreground = Studio.Brush(_selected || _primary ? "#FFFFFF" : Studio.Ink);
        if (Content is IconView icon) icon.Color = _selected || _primary ? "#FFFFFF" : Studio.Ink;
    }
}

public sealed class IconButton : StudioButton
{
    private readonly IconView _icon;
    public string Glyph { get => _icon.Glyph; set => _icon.Glyph = value; }
    public IconButton(string glyph, string tooltip, Action clicked)
    {
        _icon = new() { Glyph = glyph }; Content = _icon; Width = Height = 30; Padding = new(6);
        AutomationProperties.SetName(this, tooltip); ToolTipService.SetToolTip(this, tooltip); Click += (_, _) => clicked();
    }
}

public sealed class SegmentedControl : UserControl
{
    private readonly List<StudioButton> _buttons = [];
    public event Action<int>? SelectionChanged;
    private int _selected;
    public int SelectedIndex
    {
        get => _selected;
        set { _selected = Math.Clamp(value, 0, Math.Max(0, _buttons.Count - 1)); for (var i = 0; i < _buttons.Count; i++) { _buttons[i].IsSelected = false; _buttons[i].Background = Studio.Brush(i == _selected ? "#FFFFFF" : "#00FFFFFF"); _buttons[i].FontWeight = new() { Weight = (ushort)(i == _selected ? 600 : 400) }; } }
    }
    public SegmentedControl(IEnumerable<string> labels, int selected = 0)
    {
        var grid = new Grid { Padding = new(3), ColumnSpacing = 2, Background = Studio.Brush(Studio.Field), CornerRadius = new(6) };
        foreach (var label in labels)
        {
            var index = _buttons.Count; var button = new StudioButton(label, () => { SelectedIndex = index; SelectionChanged?.Invoke(index); }) { Height = 26, Padding = new(8, 3) };
            grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); Grid.SetColumn(button, index); grid.Children.Add(button); _buttons.Add(button);
        }
        Content = grid; SelectedIndex = selected;
    }
}

public sealed class InspectorSection : UserControl
{
    public StackPanel Body { get; } = new() { Spacing = 8, Margin = new(16, 0, 16, 14) };
    public bool IsExpanded { get => Body.Visibility == Visibility.Visible; set => Body.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    public InspectorSection(string title, string? actionGlyph = null, Action? action = null)
    {
        var header = new StudioButton(title, () => IsExpanded = !IsExpanded) { HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = new() { Weight = 600 }, Height = 40, Padding = new(16, 8) };
        var grid = Studio.Columns((header, -1));
        if (actionGlyph is not null)
        {
            var button = new IconButton(actionGlyph, title + " options", () => action?.Invoke()) { Margin = new(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); Grid.SetColumn(button, 1); grid.Children.Add(button);
        }
        var root = new StackPanel(); root.Children.Add(Studio.Rule()); root.Children.Add(grid); root.Children.Add(Body); Content = root;
    }
}
