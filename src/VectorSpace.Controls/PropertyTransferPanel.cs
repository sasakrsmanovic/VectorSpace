using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace VectorSpace.Controls;

public sealed record PropertyTransferOption(string Key, string Title, string Description, bool Available = true);

/// <summary>A reusable compact property-group chooser. Presentation-only: no clipboard, scene,
/// networking or document dependency. Keys are supplied by the host.</summary>
public sealed class PropertyTransferPanel : StackPanel
{
    private readonly Dictionary<string, CheckBox> _choices = new(StringComparer.Ordinal);
    public IReadOnlySet<string> SelectedKeys => _choices.Where(p => p.Value.IsChecked == true && p.Value.IsEnabled).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
    public event Action? SelectionChanged;
    public PropertyTransferPanel(IEnumerable<PropertyTransferOption> options)
    {
        Spacing = 6;
        foreach (var option in options)
        {
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(Studio.Text(option.Title, 12, Studio.Ink, true));
            var detail = Studio.Text(option.Description, 10, Studio.Muted); detail.TextWrapping = TextWrapping.Wrap; detail.TextTrimming = TextTrimming.None;
            content.Children.Add(detail);
            var check = new CheckBox { Content = content, IsChecked = option.Available, IsEnabled = option.Available, FontFamily = Studio.Font, MinHeight = 44, HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(check, "Transfer " + option.Title);
            check.Checked += (_, _) => SelectionChanged?.Invoke(); check.Unchecked += (_, _) => SelectionChanged?.Invoke();
            _choices.Add(option.Key, check); Children.Add(check);
        }
    }
}
