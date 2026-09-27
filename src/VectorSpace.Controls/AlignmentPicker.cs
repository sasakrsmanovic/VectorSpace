namespace VectorSpace.Controls;

/// <summary>Reusable keyboard-accessible nine-position alignment control.</summary>
public sealed class AlignmentPicker : UserControl
{
    public AlignmentPicker(int column, int row, Action<int, int> changed)
    {
        var grid = new Grid { Width = 86, Height = 86, ColumnSpacing = 2, RowSpacing = 2, Padding = new(3), Background = Studio.Brush(Studio.Field), CornerRadius = new(6) };
        for (var i = 0; i < 3; i++) { grid.ColumnDefinitions.Add(new()); grid.RowDefinitions.Add(new()); }
        var xNames = new[] { "Left", "Center", "Right" }; var yNames = new[] { "Top", "Center", "Bottom" };
        for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++)
        {
            var cx = x; var cy = y;
            var button = new StudioButton("·", () => changed(cx, cy)) { Width = 24, Height = 24, Padding = new(0), IsSelected = x == column && y == row };
            AutomationProperties.SetName(button, "Align content " + yNames[y] + " " + xNames[x]);
            button.KeyDown += (_, e) =>
            {
                var nx = cx; var ny = cy;
                switch (e.Key) { case VirtualKey.Left: nx--; break; case VirtualKey.Right: nx++; break; case VirtualKey.Up: ny--; break; case VirtualKey.Down: ny++; break; default: return; }
                changed(Math.Clamp(nx, 0, 2), Math.Clamp(ny, 0, 2)); e.Handled = true;
            };
            Grid.SetColumn(button, x); Grid.SetRow(button, y); grid.Children.Add(button);
        }
        Content = grid;
    }
}
