using System.Globalization;
using Windows.ApplicationModel.DataTransfer;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private string? _propertyClipboard;
    private IEnumerable<QuickAction> PropertyActions()
    {
        yield return new("Copy properties", "Ctrl Alt C", () => RunAsync(() => CopyPropertiesAsync(PropertyGroups.All)));
        yield return new("Paste properties", "Ctrl Alt V", () => RunAsync(() => PastePropertiesAsync()));
        yield return new("Paste selected properties", "", () => RunAsync(() => PastePropertiesAsync(choose: true)));
        foreach (var (name, group) in new[] { ("fills", PropertyGroups.Fills), ("strokes", PropertyGroups.Strokes), ("effects", PropertyGroups.Effects), ("typography", PropertyGroups.Typography) })
        {
            yield return new("Copy " + name, "", () => RunAsync(() => CopyPropertiesAsync(group)));
            yield return new("Paste " + name, "", () => RunAsync(() => PastePropertiesAsync(group)));
        }
        yield return new("Rename layers", "Ctrl R / F2", () => RunAsync(RenameSelectionAsync));
        yield return new("Bring to front", "Ctrl Shift ]", () => Run(() => Session.Reorder(1, true)));
        yield return new("Bring forward", "Ctrl ]", () => Run(() => Session.Reorder(1)));
        yield return new("Send backward", "Ctrl [", () => Run(() => Session.Reorder(-1)));
        yield return new("Send to back", "Ctrl Shift [", () => Run(() => Session.Reorder(-1, true)));
    }
    private Task CopyPropertiesAsync(PropertyGroups groups)
    {
        if (Session.Primary is not { } source) { ShowStatus("Select a source layer to copy properties."); return Task.CompletedTask; }
        var text = PropertyClipboard.Copy(source, groups);
        if (PropertyClipboard.Read(text).Properties.Groups == PropertyGroups.None) { ShowStatus("The source has no supported properties in that group."); return Task.CompletedTask; }
        _propertyClipboard = text;
        try { var data = new DataPackage(); data.SetText(text); Clipboard.SetContent(data); ShowStatus("Copied properties from " + source.Name); }
        catch { ShowStatus("Properties copied within this editor; system clipboard access is unavailable."); }
        Surface.FocusCanvas(); return Task.CompletedTask;
    }
    private async Task PastePropertiesAsync(PropertyGroups groups = PropertyGroups.All, bool choose = false)
    {
        string? text;
        try { var data = Clipboard.GetContent(); text = data.Contains(StandardDataFormats.Text) ? await data.GetTextAsync() : null; }
        catch { text = _propertyClipboard; }
        if (string.IsNullOrEmpty(text)) { ShowStatus("Copy properties from a layer first."); return; }
        // A readable unrelated clipboard must not silently fall back to an old property snapshot.
        var packet = PropertyClipboard.Read(text);
        if (choose)
        {
            var p = packet.Properties;
            var panel = new PropertyTransferPanel([
                new("Fills", "Fills", p.Fills is { } f ? f.Count + " paint(s), including image and gradient settings" : "Not copied", p.Fills is not null),
                new("Strokes", "Strokes", p.Strokes is { } s ? s.Count + " stroke(s), including dash patterns" : "Not copied", p.Strokes is not null),
                new("Effects", "Effects", p.Effects is { } e ? e.Count + " shadow/blur effect(s)" : "Not copied", p.Effects is not null),
                new("Typography", "Typography", p.Typography is { } t ? t.FontFamily + " · " + t.FontSize.ToString("0.##", CultureInfo.InvariantCulture) : "Source is not text", p.Typography is not null),
                new("Appearance", "Appearance", "Opacity, blending and supported corner radius", p.Groups.HasFlag(PropertyGroups.Appearance))
            ]);
            var root = new StackPanel { Spacing = 12, Width = 400 };
            root.Children.Add(Wrapped("From " + packet.SourceName + " to " + Session.Selection.Count + " selected layer(s). Locked layers are left unchanged.", 12, Studio.Ink));
            root.Children.Add(panel);
            root.Children.Add(Wrapped("Position, dimensions, text content, hierarchy and prototype links are preserved. Copied values replace variable bindings only for the transferred properties.", 10));
            var dialog = Dialog("Paste properties", root, "Apply properties", "Cancel");
            panel.SelectionChanged += () => dialog.IsPrimaryButtonEnabled = panel.SelectedKeys.Count > 0;
            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
            groups = PropertyGroups.None; foreach (var key in panel.SelectedKeys) groups |= Enum.Parse<PropertyGroups>(key);
        }
        var count = PropertyTransfer.Paste(Session, packet.Properties, groups);
        Surface.FocusCanvas(); ShowStatus(count == 0 ? "No editable layers support the selected properties." : "Applied properties to " + count + " layer(s)");
    }
    private async Task RenameSelectionAsync()
    {
        var targets = LayerRename.SelectedTargets(Session);
        if (targets.Count == 0) { ShowStatus("Select unlocked layers to rename."); return; }
        using var panel = new BatchRenamePanel();
        var dialog = Dialog("Rename " + targets.Count + (targets.Count == 1 ? " layer" : " layers"), panel, "Rename", "Cancel");
        IReadOnlyList<LayerNameChange> plan = [];
        void Refresh()
        {
            try
            {
                if (!int.TryParse(panel.Start, NumberStyles.None, CultureInfo.InvariantCulture, out var start)) throw new ArgumentException("Enter a nonnegative start number.");
                plan = LayerRename.Plan(targets, new(panel.Pattern, panel.Match, panel.UseRegularExpression, start));
                var byId = plan.ToDictionary(p => p.Id, StringComparer.Ordinal);
                panel.SetPreview(targets.Take(8).Select(t => new RenamePreviewRow(t.Name, byId.TryGetValue(t.Id, out var change) ? change.After : t.Name)),
                    plan.Count + " of " + targets.Count + " editable layer(s) will change · Front-to-back layer-panel order");
                dialog.IsPrimaryButtonEnabled = plan.Count > 0;
            }
            catch (Exception error) when (error is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException or OverflowException)
            { plan = []; panel.SetPreview([], error.Message, true); dialog.IsPrimaryButtonEnabled = false; }
        }
        panel.InputChanged += () => dialog.IsPrimaryButtonEnabled = false;
        panel.PreviewRequested += Refresh; dialog.Opened += (_, _) => panel.FocusPattern(); Refresh();
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary)
        {
            Refresh(); var count = LayerRename.Apply(Session, plan); ShowStatus("Renamed " + count + " layer(s)");
        }
        Surface.FocusCanvas();
    }
}
