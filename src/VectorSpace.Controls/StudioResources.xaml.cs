namespace VectorSpace.Controls;

public sealed partial class StudioResources : ResourceDictionary
{
    private static StudioResources? _current;
    public static StudioResources Current => _current ??= new();
    public StudioResources() => InitializeComponent();
}

/// <summary>Shared design tokens and layout helpers. No editor or document dependency.</summary>
public static class Studio
{
    public const string Accent = "#0D99FF";
    public const string Ink = "#242424";
    public const string Muted = "#777777";
    public const string Line = "#E7E7E7";
    public const string Field = "#F3F3F3";
    public static FontFamily Font { get; set; } = new("Arial");
    public static SolidColorBrush Brush(string hex)
    {
        var color = SkiaSharp.SKColor.TryParse(hex, out var c) ? c : SkiaSharp.SKColors.Transparent;
        return new(Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
    }
    public static TextBlock Text(string text, double size = 11, string color = Ink, bool bold = false) => new()
    {
        Text = text, FontSize = size, Foreground = Brush(color), FontFamily = Font,
        FontWeight = new Windows.UI.Text.FontWeight { Weight = (ushort)(bold ? 600 : 400) },
        VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
    };
    public static TextBox Input(string text = "", string? name = null)
    {
        var box = new TextBox { Text = text, FontFamily = Font, Style = (Style)StudioResources.Current["VS.TextBox"], Height = 30 };
        if (name is not null) AutomationProperties.SetName(box, name); return box;
    }
    public static ComboBox Choice(IEnumerable<string> items, string selected, Action<string> changed, string? label = null)
    {
        var box = new ComboBox { Style = (Style)StudioResources.Current["VS.ComboBox"], FontFamily = Font, ItemsSource = items.ToArray(), SelectedItem = selected, Height = 30 };
        AutomationProperties.SetName(box, label ?? selected);
        box.SelectionChanged += (_, _) => { if (box.SelectedItem is string value) changed(value); }; return box;
    }
    public static Grid Columns(params (UIElement Element, double Width)[] columns)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        for (var i = 0; i < columns.Length; i++)
        {
            grid.ColumnDefinitions.Add(new() { Width = columns[i].Width < 0 ? new GridLength(-columns[i].Width, GridUnitType.Star) : columns[i].Width == 0 ? GridLength.Auto : new(columns[i].Width) });
            Grid.SetColumn(columns[i].Element, i); grid.Children.Add(columns[i].Element);
        }
        return grid;
    }
    public static Border Rule() => new() { Height = 1, Background = Brush(Line) };
    public static Border Surface(UIElement content, double radius = 12) => new() { Background = Brush("#FFFFFF"), BorderBrush = Brush("#DADADA"), BorderThickness = new(1), CornerRadius = new(radius), Child = content };
    public static ScrollViewer Scroll(UIElement content) => new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled };
}
