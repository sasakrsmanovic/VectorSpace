namespace VectorSpace.Controls;

public sealed class LayerEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Glyph { get; init; } = "rectangle";
    public int Depth { get; init; }
    public bool HasChildren { get; init; }
    public bool Expanded { get; init; }
    public bool Visible { get; init; } = true;
    public bool Locked { get; init; }
    public bool IsComponent { get; init; }
    public Action? ToggleExpanded { get; init; }
    public Action? ToggleVisibility { get; init; }
    public Action? ToggleLocked { get; init; }
    public Action? Rename { get; init; }
}

/// <summary>A recycling-friendly layer row. All actions are supplied by the host.</summary>
public sealed class LayerRow : UserControl
{
    public LayerRow()
    {
        DataContextChanged += (_, _) => Refresh(); Loaded += (_, _) => Refresh();
        DoubleTapped += (_, e) => { if (DataContext is LayerEntry entry) { entry.Rename?.Invoke(); e.Handled = true; } };
    }
    private void Refresh()
    {
        if (DataContext is not LayerEntry entry) return;
        var expander = new IconButton(entry.Expanded ? "chevron-down" : "chevron-right", "Expand " + entry.Name, () => entry.ToggleExpanded?.Invoke()) { Width = 20, Height = 28, Padding = new(3), Opacity = entry.HasChildren ? 1 : 0, IsHitTestVisible = entry.HasChildren, IsTabStop = false };
        var icon = new IconView { Glyph = entry.Glyph, Width = 14, Height = 14, Color = entry.IsComponent ? "#9747FF" : "#777777", VerticalAlignment = VerticalAlignment.Center };
        var title = Studio.Text(entry.Name, 11, entry.IsComponent ? "#9747FF" : Studio.Ink);
        title.Opacity = entry.Visible ? 1 : .4;
        var visibility = new IconButton(entry.Visible ? "eye" : "eye-off", entry.Visible ? "Hide " + entry.Name : "Show " + entry.Name, () => entry.ToggleVisibility?.Invoke()) { Width = 22, Height = 28, Padding = new(4), Opacity = entry.Visible ? 0 : .7, IsTabStop = false };
        var locked = new IconButton(entry.Locked ? "lock" : "unlock", entry.Locked ? "Unlock " + entry.Name : "Lock " + entry.Name, () => entry.ToggleLocked?.Invoke()) { Width = 22, Height = 28, Padding = new(4), Opacity = entry.Locked ? .7 : 0, IsTabStop = false };
        var grid = Studio.Columns((expander, 20), (icon, 14), (title, -1), (locked, 22), (visibility, 22)); grid.ColumnSpacing = 5; grid.Height = 30; grid.Margin = new(Math.Min(96, entry.Depth * 14), 0, 4, 0);
        PointerEntered += ShowActions; PointerExited += HideActions;
        void ShowActions(object sender, PointerRoutedEventArgs e) { visibility.Opacity = locked.Opacity = .65; }
        void HideActions(object sender, PointerRoutedEventArgs e) { visibility.Opacity = entry.Visible ? 0 : .7; locked.Opacity = entry.Locked ? .7 : 0; }
        AutomationProperties.SetName(this, entry.Name); Content = grid;
    }
}
