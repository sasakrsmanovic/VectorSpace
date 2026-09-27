using System.Text;
using Windows.ApplicationModel.DataTransfer;
using VectorSpace.Layout;
using VectorSpace.Skia;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private string? _clipboard;
    private const string ClipboardPrefix = "VectorSpace/1\n";
    private sealed record QuickAction(string Name, string Shortcut, Action Execute);
    private IEnumerable<QuickAction> Actions()
    {
        yield return new("New document", "", () => RunAsync(NewDocumentAsync));
        yield return new("Open document or import SVG", "Ctrl O", () => RunAsync(OpenAsync));
        yield return new("Save document", "Ctrl S", () => RunAsync(SaveAsync));
        yield return new("Export PNG", "", () => RunAsync(() => ExportAsync(false)));
        yield return new("Export SVG", "", () => RunAsync(() => ExportAsync(true)));
        yield return new("Undo", "Ctrl Z", () => Run(Session.Undo));
        yield return new("Redo", "Ctrl Shift Z", () => Run(Session.Redo));
        yield return new("Duplicate", "Ctrl D", () => Run(() => Session.DuplicateSelection()));
        yield return new("Group selection", "Ctrl G", () => Run(() => Session.GroupSelection()));
        yield return new("Frame selection", "Ctrl Alt G", () => Run(() => Session.GroupSelection(true)));
        yield return new("Ungroup", "Ctrl Shift G", () => Run(Session.UngroupSelection));
        yield return new("Create component", "Ctrl Alt K", () => Run(() => ComponentService.MakeComponent(Session)));
        yield return new("Add auto layout", "Shift A", AddAutoLayout);
        yield return new("Fit all", "Shift 1", () => Surface.Fit());
        yield return new("Fit selection", "Shift 2", () => Surface.Fit(true));
        yield return new("Toggle grid", "Ctrl '", () => { Session.GridVisible = !Session.GridVisible; Surface.Invalidate(); });
        yield return new("Toggle rulers", "Shift R", () => { Session.RulersVisible = !Session.RulersVisible; Surface.Invalidate(); });
        yield return new("Toggle outlines", "Ctrl Shift O", () => { Session.OutlinesVisible = !Session.OutlinesVisible; Surface.Invalidate(); });
        yield return new("Toggle snapping", "", () => { Session.SnapEnabled = !Session.SnapEnabled; ShowStatus(Session.SnapEnabled ? "Snapping on" : "Snapping off"); });
        yield return new("Present prototype", "", () => Run(Surface.Present));
        yield return new("Keyboard shortcuts", "?", () => RunAsync(ShowHelpAsync));
    }
    private static void AddMenu(MenuFlyout menu, string text, Action action, bool enabled = true)
    {
        var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled, FontFamily = Studio.Font, FontSize = 12 }; item.Click += (_, _) => action(); menu.Items.Add(item);
    }
    private void ShowFileMenu(FrameworkElement target)
    {
        var menu = new MenuFlyout();
        AddMenu(menu, "New document", () => RunAsync(NewDocumentAsync));
        AddMenu(menu, "Open…                         Ctrl O", () => RunAsync(OpenAsync));
        AddMenu(menu, "Save a local copy…        Ctrl S", () => RunAsync(SaveAsync));
        AddMenu(menu, "Reset to sample", () => RunAsync(async () => { if (await ConfirmAsync("Replace document?", "This restores the editable Aether sample. Download a copy first to keep your current document.")) { Session.Load(SampleDocument.Create()); Surface.Fit(firstFrame: true); } }));
        menu.Items.Add(new MenuFlyoutSeparator());
        AddMenu(menu, "Undo " + Session.UndoLabel, () => Run(Session.Undo), Session.CanUndo);
        AddMenu(menu, "Redo " + Session.RedoLabel, () => Run(Session.Redo), Session.CanRedo);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddMenu(menu, "Export selection as PNG…", () => RunAsync(() => ExportAsync(false)));
        AddMenu(menu, "Export selection as SVG…", () => RunAsync(() => ExportAsync(true)));
        AddMenu(menu, "Frame presets…", () => RunAsync(ShowFramePresetsAsync));
        menu.Items.Add(new MenuFlyoutSeparator());
        AddMenu(menu, (Session.GridVisible ? "✓ " : "") + "Show grid", () => { Session.GridVisible = !Session.GridVisible; Surface.Invalidate(); });
        AddMenu(menu, (Session.RulersVisible ? "✓ " : "") + "Show rulers", () => { Session.RulersVisible = !Session.RulersVisible; Surface.Invalidate(); });
        AddMenu(menu, (Session.SnapEnabled ? "✓ " : "") + "Snap to objects", () => Session.SnapEnabled = !Session.SnapEnabled);
        AddMenu(menu, "Clear page guides", () => Run(() => Session.Edit("Clear guides", () => Session.Page.Guides.Clear())));
        AddMenu(menu, "Keyboard shortcuts and about", () => RunAsync(ShowHelpAsync));
        menu.ShowAt(target);
    }
    private void ShowCanvasMenu(Point position)
    {
        var menu = new MenuFlyout(); var selected = Session.Selection.Count > 0;
        AddMenu(menu, "Copy                           Ctrl C", () => RunAsync(() => CopyAsync(false)), selected);
        AddMenu(menu, "Cut                              Ctrl X", () => RunAsync(() => CopyAsync(true)), selected);
        AddMenu(menu, "Paste                          Ctrl V", () => RunAsync(PasteAsync));
        AddMenu(menu, "Duplicate                   Ctrl D", () => Run(() => Session.DuplicateSelection()), selected);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddMenu(menu, "Group selection           Ctrl G", () => Run(() => Session.GroupSelection()), selected);
        AddMenu(menu, "Frame selection", () => Run(() => Session.GroupSelection(true)), selected);
        AddMenu(menu, "Ungroup                      Ctrl Shift G", () => Run(Session.UngroupSelection), selected);
        AddMenu(menu, "Add auto layout            Shift A", AddAutoLayout, selected);
        AddMenu(menu, "Create component", () => Run(() => ComponentService.MakeComponent(Session)), Session.SelectionRoots.Count == 1);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddMenu(menu, "Bring to front", () => Run(() => Session.Reorder(1, true)), selected);
        AddMenu(menu, "Bring forward", () => Run(() => Session.Reorder(1)), selected);
        AddMenu(menu, "Send backward", () => Run(() => Session.Reorder(-1)), selected);
        AddMenu(menu, "Send to back", () => Run(() => Session.Reorder(-1, true)), selected);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddMenu(menu, "Rename                         F2", () => { if (Session.Primary is { } n) RunAsync(() => RenameLayerAsync(n)); }, selected);
        AddMenu(menu, "Toggle lock", () => Run(() => Session.Edit("Toggle lock", () => { foreach (var n in Session.Selection) n.Locked = !n.Locked; })), selected);
        AddMenu(menu, "Delete                           Delete", () => Run(Session.DeleteSelection), selected);
        menu.ShowAt(Surface, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = position });
    }
    private void ShowZoomMenu(FrameworkElement target)
    {
        var menu = new MenuFlyout();
        foreach (var percent in new[] { 25, 50, 75, 100, 150, 200, 400 }) AddMenu(menu, percent + "%", () => Surface.ZoomTo(percent / 100d));
        menu.Items.Add(new MenuFlyoutSeparator()); AddMenu(menu, "Zoom to fit           Shift 1", () => Surface.Fit()); AddMenu(menu, "Zoom to selection  Shift 2", () => Surface.Fit(true), Session.Selection.Count > 0); menu.ShowAt(target);
    }
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var control = Keyboard.Control; var shift = Keyboard.Shift; var alt = Keyboard.Alt;
        if (Surface.IsPresenting) { if (e.Key == VirtualKey.Escape) { Surface.ExitPresentation(); e.Handled = true; } return; }
        if (control && e.Key == VirtualKey.S) { RunAsync(SaveAsync); e.Handled = true; return; }
        if (Keyboard.IsTextInput(e.OriginalSource as DependencyObject)) return;
        Action? action = null;
        if (control)
        {
            action = e.Key switch
            {
                VirtualKey.Z => shift ? Session.Redo : Session.Undo,
                VirtualKey.Y => Session.Redo,
                VirtualKey.A => Session.SelectAll,
                VirtualKey.D => () => Session.DuplicateSelection(),
                VirtualKey.C => () => RunAsync(() => CopyAsync(false)),
                VirtualKey.X => () => RunAsync(() => CopyAsync(true)),
                VirtualKey.V => () => RunAsync(PasteAsync),
                VirtualKey.O when shift => () => { Session.OutlinesVisible = !Session.OutlinesVisible; Surface.Invalidate(); },
                VirtualKey.O => () => RunAsync(OpenAsync),
                VirtualKey.G when alt => () => Session.GroupSelection(true),
                VirtualKey.G when shift => Session.UngroupSelection,
                VirtualKey.G => () => Session.GroupSelection(),
                VirtualKey.K when alt => () => ComponentService.MakeComponent(Session),
                VirtualKey.K => () => RunAsync(ShowQuickActionsAsync),
                _ => null
            };
            if ((int)e.Key == 219) action = () => Session.Reorder(-1, shift);
            if ((int)e.Key == 221) action = () => Session.Reorder(1, shift);
            if ((int)e.Key == 222) action = () => { Session.GridVisible = !Session.GridVisible; Surface.Invalidate(); };
        }
        else
        {
            var step = shift ? 10d : 1d;
            action = e.Key switch
            {
                VirtualKey.Delete or VirtualKey.Back => Session.DeleteSelection,
                VirtualKey.Left => () => Session.MoveSelection(-step, 0),
                VirtualKey.Right => () => Session.MoveSelection(step, 0),
                VirtualKey.Up => () => Session.MoveSelection(0, -step),
                VirtualKey.Down => () => Session.MoveSelection(0, step),
                VirtualKey.V => () => Session.Tool = EditorTool.Move,
                VirtualKey.K => () => Session.Tool = EditorTool.Scale,
                VirtualKey.F => () => Session.Tool = EditorTool.Frame,
                VirtualKey.R when shift => () => { Session.RulersVisible = !Session.RulersVisible; Surface.Invalidate(); },
                VirtualKey.R => () => Session.Tool = EditorTool.Rectangle,
                VirtualKey.O => () => Session.Tool = EditorTool.Ellipse,
                VirtualKey.L => () => Session.Tool = shift ? EditorTool.Arrow : EditorTool.Line,
                VirtualKey.P => () => Session.Tool = shift ? EditorTool.Pencil : EditorTool.Pen,
                VirtualKey.T => () => Session.Tool = EditorTool.Text,
                VirtualKey.H => () => Session.Tool = EditorTool.Hand,
                VirtualKey.C => () => Session.Tool = EditorTool.Comment,
                VirtualKey.S => () => Session.Tool = shift ? EditorTool.Section : EditorTool.Slice,
                VirtualKey.A when shift => AddAutoLayout,
                VirtualKey.Number1 when shift => () => Surface.Fit(),
                VirtualKey.Number2 when shift => () => Surface.Fit(true),
                VirtualKey.Number0 => () => Surface.ZoomTo(1),
                VirtualKey.F2 => () => { if (Session.Primary is { } n) RunAsync(() => RenameLayerAsync(n)); },
                VirtualKey.Tab => TogglePanels,
                VirtualKey.Escape => () => { Surface.CancelGesture(); Session.Select((DesignNode?)null); Session.Tool = EditorTool.Move; },
                VirtualKey.Enter => () => { Surface.FinishPath(false); if (Session.Primary?.Kind == NodeKind.Text) Surface.BeginTextEdit(Session.Primary); },
                VirtualKey.Space => () => Surface.IsSpaceDown = true,
                _ => null
            };
            if ((int)e.Key is 187 or 107) action = () => Surface.ZoomTo(Session.Viewport.Zoom * 1.25);
            if ((int)e.Key is 189 or 109) action = () => Surface.ZoomTo(Session.Viewport.Zoom / 1.25);
            if ((int)e.Key == 191) action = () => RunAsync(shift ? ShowHelpAsync : ShowQuickActionsAsync);
        }
        if (action is not null)
        {
            try { action(); } catch (Exception ex) { ShowStatus(ex.Message, true); } e.Handled = true;
        }
    }
    private void AddAutoLayout()
    {
        Run(() =>
        {
            if (Session.SelectionRoots.Count == 0) return;
            if (Session.SelectionRoots.Count != 1 || Session.Primary?.IsContainer != true) Session.GroupSelection(true);
            Session.UpdateSelection("Add auto layout", n => { n.Layout.Direction = LayoutDirection.Horizontal; n.Layout.HugWidth = true; n.Layout.HugHeight = true; });
        });
    }
    private async Task CopyAsync(bool cut)
    {
        if (Session.Selection.Count == 0) return;
        var nodes = Session.SelectionRoots.Select(n =>
        {
            var clone = DocumentJson.CloneNode(n); NodeGeometry.SetLocalMatrix(clone, n.WorldMatrix); return clone;
        }).ToArray();
        _clipboard = ClipboardPrefix + DocumentJson.SaveNodes(nodes);
        try { var package = new DataPackage(); package.SetText(_clipboard); Clipboard.SetContent(package); }
        catch { ShowStatus("Copied to this editor's clipboard. Browser clipboard access was unavailable."); }
        if (cut) Session.DeleteSelection();
        await Task.CompletedTask;
    }
    private async Task PasteAsync()
    {
        string? text = null;
        try { var content = Clipboard.GetContent(); if (content.Contains(StandardDataFormats.Text)) text = await content.GetTextAsync(); }
        catch { text = _clipboard; }
        text ??= _clipboard;
        if (string.IsNullOrWhiteSpace(text)) { ShowStatus("The clipboard is empty or clipboard access is unavailable."); return; }
        if (text.StartsWith(ClipboardPrefix, StringComparison.Ordinal)) Session.Paste(text[ClipboardPrefix.Length..]);
        else if (text.TrimStart().StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || text.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)) ImportSvg(text, "Pasted SVG");
        else
        {
            var point = Session.Viewport.ScreenToWorld(new(Surface.ActualWidth / 2, Surface.ActualHeight / 2));
            var node = new DesignNode { Kind = NodeKind.Text, Name = "Pasted text", Text = text, Fill = "#242424", X = point.X, Y = point.Y, Width = 320, Height = 100 };
            Session.Edit("Paste text", () => { Session.AddNode(node); Session.Select(node); });
        }
    }
    private async Task NewDocumentAsync()
    {
        if (Session.IsDirty && !await ConfirmAsync("Create a new document?", "Download a copy first to keep your current work. The local autosave will be replaced.")) return;
        Session.Load(new()); Surface.Fit();
    }
    private async Task OpenAsync()
    {
        var file = await _storage.OpenAsync(); if (!file.HasValue) return;
        if (file.Value.Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) { ImportSvg(file.Value.Text, Path.GetFileNameWithoutExtension(file.Value.Name)); return; }
        var document = DocumentJson.Load(file.Value.Text);
        if (Session.IsDirty && !await ConfirmAsync("Open another document?", "This replaces your current work. Save a local copy first to keep it.")) return;
        Session.Load(document); Surface.Fit(); ShowStatus("Opened " + file.Value.Name);
    }
    private void ImportSvg(string source, string name)
    {
        var result = SvgFormat.Import(source, name); var nodes = result.Document.Pages[0].Nodes;
        var point = Session.Viewport.ScreenToWorld(new(Surface.ActualWidth / 2, Surface.ActualHeight / 2));
        Session.Edit("Import SVG", () =>
        {
            foreach (var node in nodes) { node.X = point.X - node.Width / 2; node.Y = point.Y - node.Height / 2; Session.AddNode(node); } Session.Select(nodes.Select(n => n.Id));
        });
        Surface.Fit(true); ShowStatus(result.Warnings.Count == 0 ? "Imported editable SVG" : "Imported SVG. " + string.Join(" ", result.Warnings));
    }
    private async Task SaveAsync()
    {
        var json = DocumentJson.Save(Session.Document); await _storage.SaveAsync(SafeName(Session.Document.Name) + ".vectorspace", Encoding.UTF8.GetBytes(json), "application/json"); Session.MarkSaved(json); ShowStatus("Downloaded editable document");
    }
    private async Task ExportAsync(bool svg)
    {
        var nodes = Session.SelectionRoots.Count > 0 ? Session.SelectionRoots.Where(n => n.Visible).ToArray() : Session.Page.Nodes.Where(n => n.Visible).ToArray();
        if (nodes.Length == 0) { ShowStatus("There are no visible layers to export."); return; }
        var bounds = nodes.Select(n => n.WorldBounds).Aggregate(RectD.Union);
        if (nodes.Length == 1 && nodes[0].Kind == NodeKind.Slice) nodes = Session.Page.Nodes.Where(n => n.Visible && n.Kind != NodeKind.Slice).ToArray();
        var bytes = svg ? Encoding.UTF8.GetBytes(SvgFormat.Export(nodes, bounds)) : Surface.Renderer.ExportPng(nodes, bounds, _exportScale);
        var name = SafeName(Session.SelectionRoots.Count == 1 ? Session.SelectionRoots[0].Name : Session.Page.Name);
        await _storage.SaveAsync(name + (svg ? ".svg" : ".png"), bytes, svg ? "image/svg+xml" : "image/png"); ShowStatus("Exported " + (svg ? "SVG" : "PNG"));
    }
    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':']).ToHashSet(); var result = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim(); return string.IsNullOrEmpty(result) ? "VectorSpace" : result;
    }
    private ContentDialog Dialog(string title, UIElement content, string primary = "", string close = "Close") => new()
    {
        Title = title, Content = content, PrimaryButtonText = primary, CloseButtonText = close, XamlRoot = XamlRoot,
        FontFamily = Studio.Font, RequestedTheme = ElementTheme.Light, DefaultButton = string.IsNullOrEmpty(primary) ? ContentDialogButton.Close : ContentDialogButton.Primary,
        MinWidth = 320, MaxWidth = 560
    };
    private async Task<string?> PromptAsync(string title, string value, bool multiline = false)
    {
        var input = Studio.Input(value, title); input.Width = 350; input.Height = multiline ? 110 : 34; input.AcceptsReturn = multiline; input.TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
        var dialog = Dialog(title, input, "Save", "Cancel"); dialog.Opened += (_, _) => { input.Focus(FocusState.Programmatic); input.SelectAll(); };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text.Trim() : null;
    }
    private async Task<bool> ConfirmAsync(string title, string description) => await Dialog(title, Wrapped(description, 12, Studio.Ink), "Continue", "Cancel").ShowAsync() == ContentDialogResult.Primary;
    private async Task RenameDocumentAsync()
    {
        var text = await PromptAsync("Rename document", Session.Document.Name); if (!string.IsNullOrWhiteSpace(text)) Session.Edit("Rename document", () => Session.Document.Name = text);
    }
    private async Task RenameLayerAsync(DesignNode node)
    {
        var text = await PromptAsync("Rename layer", node.Name); if (!string.IsNullOrWhiteSpace(text)) Session.Edit("Rename layer", () => node.Name = text);
    }
    private async Task EditCommentAsync(Vec2 anchor, CommentThread? thread)
    {
        if (thread is null)
        {
            var text = await PromptAsync("Leave a comment", "", true);
            if (!string.IsNullOrWhiteSpace(text)) Session.Edit("Add comment", () => Session.Document.Comments.Add(new() { PageId = Session.Page.Id, Anchor = anchor, Text = text })); return;
        }
        var root = new StackPanel { Spacing = 12, Width = 360 };
        root.Children.Add(Studio.Text(thread.Author + " · " + thread.CreatedAt.ToLocalTime().ToString("g"), 11, Studio.Muted)); root.Children.Add(Wrapped(thread.Text, 13, Studio.Ink));
        foreach (var reply in thread.Replies) { root.Children.Add(Studio.Rule()); root.Children.Add(Wrapped("You: " + reply, 12, Studio.Ink)); }
        var input = Studio.Input("", "Reply to comment"); input.PlaceholderText = "Reply…"; input.AcceptsReturn = true; input.Height = 72; root.Children.Add(input);
        var dialog = Dialog("Comment", root, "Reply"); dialog.SecondaryButtonText = thread.Resolved ? "Reopen" : "Resolve";
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text)) Session.Edit("Reply to comment", () => thread.Replies.Add(input.Text.Trim()));
        else if (result == ContentDialogResult.Secondary) Session.Edit("Resolve comment", () => thread.Resolved = !thread.Resolved);
    }
    private async Task ShowShareAsync()
    {
        var root = new StackPanel { Spacing = 14, Width = 360 };
        root.Children.Add(Wrapped("Your work stays on this device. VectorSpace stores a recovery copy in your browser or desktop profile; there is no collaboration server or public document link.", 12, Studio.Ink));
        root.Children.Add(Wrapped("Share an editable .vectorspace file with another person, or export SVG/PNG from the inspector. They can open the file in their own copy of VectorSpace."));
        var dialog = Dialog("Share a local copy", root, "Download document"); if (await dialog.ShowAsync() == ContentDialogResult.Primary) await SaveAsync();
    }
    private async Task ShowHelpAsync()
    {
        var root = new StackPanel { Spacing = 12, Width = 410 };
        root.Children.Add(Studio.Text("VectorSpace", 24, Studio.Ink, true)); root.Children.Add(Wrapped("An independent, local-first vector design editor built with Uno Platform and SkiaSharp. Original implementation and assets; not affiliated with Figma.", 12, Studio.Ink));
        foreach (var (name, shortcut) in new[] { ("Move / Frame / Rectangle / Ellipse", "V / F / R / O"), ("Pen / Pencil / Text / Comment", "P / Shift P / T / C"), ("Pan / Zoom", "Space-drag / Ctrl-wheel"), ("Select multiple / Deep-select", "Shift-click / Ctrl-click"), ("Constrain / Duplicate while dragging", "Shift / Alt"), ("Undo / Redo", "Ctrl Z / Ctrl Shift Z"), ("Group / Ungroup", "Ctrl G / Ctrl Shift G"), ("Nudge / Large nudge", "Arrows / Shift-arrows"), ("Fit all / Fit selection", "Shift 1 / Shift 2"), ("Save / Open / Quick actions", "Ctrl S / Ctrl O / Ctrl K"), ("Finish path / Close path", "Enter / Click first point"), ("Hide panels / Cancel / Rename", "Tab / Esc / F2") })
            root.Children.Add(Studio.Columns((Wrapped(name, 11, Studio.Ink), -1), (Wrapped(shortcut, 10, Studio.Muted), 165)));
        root.Children.Add(Studio.Rule()); root.Children.Add(Wrapped("This alpha does not provide complete Figma compatibility: .fig files, multiplayer, variables/variants, plugin execution and advanced prototyping are not implemented. SVG import reports unsupported elements instead of executing them.", 10));
        await Dialog("Keyboard shortcuts & about", Studio.Scroll(root)).ShowAsync();
    }
    private async Task ShowQuickActionsAsync()
    {
        var root = new StackPanel { Spacing = 10, Width = 410 }; var search = Studio.Input("", "Search quick actions"); search.PlaceholderText = "Search actions…"; search.Height = 38; root.Children.Add(search);
        var results = new StackPanel { Spacing = 3 }; var scroll = Studio.Scroll(results); scroll.MaxHeight = 360; root.Children.Add(scroll);
        var dialog = Dialog("Quick actions", root);
        void Filter()
        {
            results.Children.Clear(); foreach (var item in Actions().Where(a => a.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
            {
                var button = new StudioButton { Content = Studio.Columns((Studio.Text(item.Name, 12), -1), (Studio.Text(item.Shortcut, 10, Studio.Muted), 110)), HorizontalContentAlignment = HorizontalAlignment.Stretch, Height = 36, Padding = new(8) };
                AutomationProperties.SetName(button, item.Name); button.Click += (_, _) => { dialog.Hide(); DispatcherQueue.TryEnqueue(() => item.Execute()); }; results.Children.Add(button);
            }
        }
        search.TextChanged += (_, _) => Filter(); dialog.Opened += (_, _) => search.Focus(FocusState.Programmatic); Filter(); await dialog.ShowAsync();
    }
    private async Task ShowFramePresetsAsync()
    {
        var presets = new (string Name, int Width, int Height)[] { ("Desktop", 1440, 1024), ("Laptop", 1280, 832), ("Tablet", 834, 1194), ("Phone", 390, 844), ("Phone · compact", 360, 800), ("Watch", 198, 242), ("Presentation 16:9", 1920, 1080), ("Social square", 1080, 1080), ("A4 · 96 dpi", 794, 1123) };
        var root = new StackPanel { Spacing = 5, Width = 360 }; var dialog = Dialog("Frame presets", root);
        foreach (var preset in presets)
        {
            var button = new StudioButton { Content = Studio.Columns((Studio.Text(preset.Name, 12), -1), (Studio.Text(preset.Width + " × " + preset.Height, 11, Studio.Muted), 110)), Height = 38, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) =>
            {
                dialog.Hide(); var p = Session.Viewport.ScreenToWorld(new(Surface.ActualWidth / 2, Surface.ActualHeight / 2)); var frame = new DesignNode { Kind = NodeKind.Frame, Name = preset.Name, X = p.X - preset.Width / 2, Y = p.Y - preset.Height / 2, Width = preset.Width, Height = preset.Height, Fill = "#FFFFFF", ClipContent = true };
                Run(() => Session.Edit("Create frame", () => { Session.AddNode(frame); Session.Select(frame); })); Surface.Fit(true);
            }; root.Children.Add(button);
        }
        await dialog.ShowAsync();
    }
}
