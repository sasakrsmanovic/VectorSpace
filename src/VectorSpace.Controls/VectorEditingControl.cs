namespace VectorSpace.Controls;

public enum VectorEditCommand
{
    SelectAll, Done, Corner, Smooth, Mirrored, Subdivide, DeleteAnchors, Reverse, ToggleClosed,
    Simplify, PreviousContour, NextContour, SelectContour, Cut, Join, DeleteContour
}
public sealed record VectorEditState(int Selected, int Anchors, int Contours, int ActiveContour,
    bool Closed, bool EvenOdd, bool CanCut, bool CanJoin);

/// <summary>Compact reusable vector-edit inspector. Model and history are supplied by the host;
/// control labels have stable accessible names independent of their compact visible captions.</summary>
public sealed class VectorEditingControl : ShapeOptionsPanel
{
    public event Action<VectorEditCommand>? Command;
    public event Action<bool>? FillRuleChanged;
    public VectorEditingControl(VectorEditState state)
    {
        ArgumentNullException.ThrowIfNull(state); Spacing = 6;
        Children.Add(Studio.Text($"{state.Selected} / {state.Anchors} anchors · {state.Contours} contour(s)", 11, Studio.Muted));
        Children.Add(Studio.Columns((Button("Select all", "Select all points", VectorEditCommand.SelectAll), -1),
            (Button("Done", "Done editing", VectorEditCommand.Done), -1)));
        Children.Add(Studio.Columns((Button("Corner", "Corner", VectorEditCommand.Corner, state.Selected > 0), -1),
            (Button("Smooth", "Smooth", VectorEditCommand.Smooth, state.Selected > 0), -1),
            (Button("Mirrored", "Mirrored", VectorEditCommand.Mirrored, state.Selected > 0), -1)));
        Children.Add(Studio.Columns((Button("Subdivide", "Split selected segments", VectorEditCommand.Subdivide, state.Selected > 1), -1),
            (Button("Delete anchors", "Delete selected anchors", VectorEditCommand.DeleteAnchors, state.Selected > 0), -1)));
        Children.Add(Studio.Rule());
        Children.Add(Studio.Columns((Button("‹", "Previous contour", VectorEditCommand.PreviousContour, state.Contours > 1), 26),
            (Studio.Text($"Contour {state.ActiveContour + 1} / {state.Contours}", 11), -1),
            (Button("›", "Next contour", VectorEditCommand.NextContour, state.Contours > 1), 26)));
        Children.Add(Studio.Columns((Button("Select contour", "Select contour", VectorEditCommand.SelectContour), -1),
            (Button(state.Closed ? "Open" : "Close", state.Closed ? "Open path" : "Close path", VectorEditCommand.ToggleClosed), -1)));
        Children.Add(Studio.Columns((Button("Cut anchor", "Cut at selected anchor", VectorEditCommand.Cut, state.CanCut), -1),
            (Button("Join ends", "Join selected endpoints", VectorEditCommand.Join, state.CanJoin), -1)));
        Children.Add(Studio.Columns((Button("Reverse", "Reverse path", VectorEditCommand.Reverse), -1),
            (Button("Delete contour", "Delete contour", VectorEditCommand.DeleteContour, state.Contours > 1), -1)));
        var rule = new SegmentedControl(["Non-zero", "Even-odd"], state.EvenOdd ? 1 : 0);
        rule.SelectionChanged += i => FillRuleChanged?.Invoke(i == 1); Children.Add(rule);
        Children.Add(Button("Simplify freehand", "Simplify freehand", VectorEditCommand.Simplify));
    }
    private StudioButton Button(string caption, string name, VectorEditCommand command, bool enabled = true)
    {
        var button = new StudioButton(caption, () => Command?.Invoke(command)) { IsEnabled = enabled, HorizontalContentAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetName(button, name); return button;
    }
}
