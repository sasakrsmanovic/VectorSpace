using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace VectorSpace.Controls;

public sealed record RenamePreviewRow(string Before, string After);

/// <summary>Two-column batch-rename editor with bounded preview presentation and debounced requests.
/// A host validates names and applies transactions; this control never mutates a document.</summary>
public sealed class BatchRenamePanel : Grid, IDisposable
{
    private readonly TextBox _match = Studio.Input("", "Match layer names");
    private readonly TextBox _pattern = Studio.Input("$&", "Rename to");
    private readonly TextBox _start = Studio.Input("1", "Start numbering at");
    private readonly CheckBox _regex = new() { Content = "Regular expression", FontFamily = Studio.Font, FontSize = 11 };
    private readonly StackPanel _preview = new() { Spacing = 9 };
    private readonly TextBlock _summary = Studio.Text("", 11, Studio.Muted);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private bool _disposed;
    public string Match => _match.Text;
    public string Pattern => _pattern.Text;
    public string Start => _start.Text;
    public bool UseRegularExpression => _regex.IsChecked == true;
    public event Action? InputChanged;
    public event Action? PreviewRequested;
    public BatchRenamePanel()
    {
        Width = 500; ColumnSpacing = 18;
        ColumnDefinitions.Add(new() { Width = new(215) }); ColumnDefinitions.Add(new() { Width = new(267) });
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new() { Height = GridLength.Auto });
        var left = new StackPanel { Spacing = 12 };
        left.Children.Add(Studio.Text("Preview", 12, Studio.Ink, true));
        var scroll = Studio.Scroll(_preview); scroll.Height = 282; left.Children.Add(scroll); Children.Add(left);
        var right = new StackPanel { Spacing = 8 }; SetColumn(right, 1); Children.Add(right);
        _match.MaxLength = 512; _pattern.MaxLength = 4096; _start.MaxLength = 9;
        _match.PlaceholderText = "All of each name";
        right.Children.Add(Studio.Text("Match", 11, Studio.Ink, true)); right.Children.Add(_match); right.Children.Add(_regex);
        right.Children.Add(Studio.Text("Rename to", 11, Studio.Ink, true)); right.Children.Add(_pattern);
        var tokens = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var (label, token) in new[] { ("Current name", "$&"), ("Number ↑", "$nn"), ("Number ↓", "$NN") })
        {
            var button = new StudioButton(label, () => Insert(token)) { FontSize = 10, Padding = new(7, 3, 7, 3), MinWidth = 0 };
            AutomationProperties.SetName(button, "Insert " + label); tokens.Children.Add(button);
        }
        right.Children.Add(tokens);
        right.Children.Add(Studio.Text("Start ascending / stop descending at", 10, Studio.Muted)); right.Children.Add(_start);
        var hint = Studio.Text("$& current match · $nn padded number · $NN reverse number · $1 capture group. Regex uses linear-time matching; lookarounds and backreferences are not supported.", 10, Studio.Muted);
        hint.TextWrapping = TextWrapping.Wrap; hint.TextTrimming = TextTrimming.None; right.Children.Add(hint);
        _summary.TextWrapping = TextWrapping.Wrap; _summary.TextTrimming = TextTrimming.None; _summary.Margin = new(0, 14, 0, 0);
        SetRow(_summary, 1); SetColumnSpan(_summary, 2); Children.Add(_summary);
        _match.TextChanged += Changed; _pattern.TextChanged += Changed; _start.TextChanged += Changed;
        _regex.Checked += (_, _) => Request(); _regex.Unchecked += (_, _) => Request();
        _timer.Tick += (_, _) => FlushPreview();
    }
    private void Changed(object sender, TextChangedEventArgs e) => Request();
    private void Request() { if (_disposed) return; InputChanged?.Invoke(); _timer.Stop(); _timer.Start(); }
    private void Insert(string token)
    {
        var start = _pattern.SelectionStart; var length = _pattern.SelectionLength;
        _pattern.Text = _pattern.Text.Remove(start, length).Insert(start, token);
        _pattern.Focus(FocusState.Programmatic); _pattern.SelectionStart = start + token.Length; _pattern.SelectionLength = 0;
    }
    public void FocusPattern() { _pattern.Focus(FocusState.Programmatic); _pattern.SelectAll(); }
    public void FlushPreview() { _timer.Stop(); if (!_disposed) PreviewRequested?.Invoke(); }
    public void SetPreview(IEnumerable<RenamePreviewRow> rows, string summary, bool error = false)
    {
        _preview.Children.Clear();
        foreach (var row in rows.Take(8))
        {
            var item = new StackPanel { Spacing = 2 }; item.Children.Add(Studio.Text(row.Before, 10, Studio.Muted));
            item.Children.Add(Studio.Text(row.After, 11, Studio.Ink, true)); _preview.Children.Add(item);
        }
        _summary.Text = summary; _summary.Foreground = Studio.Brush(error ? "#B3261E" : Studio.Muted);
        AutomationProperties.SetName(_summary, "Rename preview status");
    }
    public new void Dispose() { _disposed = true; _timer.Stop(); PreviewRequested = null; InputChanged = null; }
}
