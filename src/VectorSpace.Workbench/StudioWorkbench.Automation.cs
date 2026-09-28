using System.Text;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    /// <summary>Read-only named-control geometry for opt-in browser acceptance tests of the Skia UI.
    /// Does not execute commands, change focus or mutate the document.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string CaptureAutomationState()
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartArray();
            if (XamlRoot is { Content: FrameworkElement root })
            {
                var visited = new HashSet<DependencyObject>(); var viewport = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
                Visit(root, viewport);
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot)) if (popup.Child is { } child) Visit(child, viewport);
                void Visit(DependencyObject element, Rect clip)
                {
                    if (!visited.Add(element) || visited.Count > 10000) return;
                    if (element is FrameworkElement view)
                    {
                        if (view.Visibility != Visibility.Visible || view.Opacity <= 0) return;
                        try
                        {
                            var bounds = view.TransformToVisual(root).TransformBounds(new Rect(0, 0, view.ActualWidth, view.ActualHeight));
                            var visible = bounds; visible.Intersect(clip);
                            if (view is ScrollViewer) clip = visible;
                            var name = AutomationProperties.GetName(view);
                            if (string.IsNullOrEmpty(name)) name = view switch
                            {
                                MenuFlyoutItem menu => menu.Text,
                                ButtonBase { Content: string text } => text,
                                ComboBoxItem { Content: string text } => text,
                                _ => ""
                            };
                            if (view == Surface) name = "Design canvas";
                            if (name.Length > 0 && visible.Width > 1 && visible.Height > 1)
                            {
                                json.WriteStartObject(); json.WriteString("name", name); json.WriteString("type", view.GetType().Name);
                                json.WriteNumber("x", visible.X); json.WriteNumber("y", visible.Y); json.WriteNumber("width", visible.Width); json.WriteNumber("height", visible.Height);
                                json.WriteBoolean("enabled", view is not Control control || control.IsEnabled);
                                if (view is TextBox textBox && textBox.Tag as string != "Sensitive") json.WriteString("value", textBox.Text);
                                if (view is ComboBox combo) json.WriteString("value", combo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() : combo.SelectedItem?.ToString());
                                json.WriteEndObject();
                            }
                        }
                        catch (ArgumentException) { return; } // Popup closing during a diagnostics tick.
                    }
                    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) Visit(VisualTreeHelper.GetChild(element, i), clip);
                }
            }
            json.WriteEndArray();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
