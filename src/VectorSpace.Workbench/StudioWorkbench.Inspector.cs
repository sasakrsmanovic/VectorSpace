using VectorSpace.Layout;
using VectorSpace.Skia;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private double _exportScale = 1;
    private void Change(string label, Action<DesignNode> change) => Run(() => Session.UpdateSelection(label, change));
    private void RefreshInspector()
    {
        _inspector.Children.Clear();
        if (_prototype) { BuildPrototypeInspector(); return; }
        var node = Session.Primary;
        if (node is null)
        {
            var page = AddSection("Page"); page.Body.Children.Add(Studio.Columns((new ColorField(Session.Page.Background, c => Run(() => Session.Edit("Canvas background", () => Session.Page.Background = c))), -1), (Studio.Text("100%", 11, Studio.Muted), 42)));
            var styles = AddSection("Local styles");
            foreach (var (name, color) in Session.Document.ColorStyles)
            {
                var swatch = new Border { Width = 18, Height = 18, CornerRadius = new(4), Background = Studio.Brush(color), BorderBrush = Studio.Brush("#22000000"), BorderThickness = new(1) };
                styles.Body.Children.Add(Studio.Columns((swatch, 18), (Studio.Text(name, 11), -1), (Studio.Text(color.TrimStart('#'), 10, Studio.Muted), 58)));
            }
            var start = AddSection("Make space for your ideas");
            start.Body.Children.Add(Wrapped("Select a layer to inspect its properties, or choose a tool from the floating toolbar to start drawing."));
            start.Body.Children.Add(new StudioButton("Frame presets", () => RunAsync(ShowFramePresetsAsync)) { RestBackground = Studio.Field, Background = Studio.Brush(Studio.Field), HorizontalAlignment = HorizontalAlignment.Stretch });
            var comments = AddSection("Comments", "comment", () => { Session.Tool = EditorTool.Comment; });
            var threads = Session.Document.Comments.Where(c => c.PageId == Session.Page.Id && !c.Resolved).ToArray();
            if (threads.Length == 0) comments.Body.Children.Add(Wrapped("Press C and click the canvas to leave a local comment."));
            foreach (var thread in threads) comments.Body.Children.Add(new StudioButton(thread.Text, () => RunAsync(() => EditCommentAsync(thread.Anchor, thread))) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left });
            AddExportSection(); return;
        }
        var summary = new StackPanel { Spacing = 5, Margin = new(16, 14, 16, 14) };
        var heading = Session.Selection.Count == 1 ? node.Name : Session.Selection.Count + " layers selected";
        summary.Children.Add(Studio.Columns((new IconView { Glyph = Glyph(node.Kind), Color = node.Kind is NodeKind.Component or NodeKind.Instance ? "#9747FF" : Studio.Ink, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center }, 16), (Studio.Text(heading, 12, Studio.Ink, true), -1), (new IconButton("more", "Layer actions", () => ShowCanvasMenu(new Point(20, 20))), 24)));
        summary.Children.Add(Studio.Text(node.Kind.ToString() + (node.Parent is null ? " · Canvas" : " · " + node.Parent.Name), 10, Studio.Muted));
        _inspector.Children.Add(summary);
        var position = AddSection("Position");
        var align = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        foreach (var name in new[] { "left", "center", "right", "top", "middle", "bottom" }) align.Children.Add(new IconButton(name, "Align " + name, () => Run(() => Session.Align(name))) { Width = 30, Height = 26 });
        position.Body.Children.Add(align);
        position.Body.Children.Add(Studio.Columns((Number("X", node.X, v => Change("Change X", n => n.X = v)), -1), (Number("Y", node.Y, v => Change("Change Y", n => n.Y = v)), -1)));
        var flip = new IconButton("flip", "Flip horizontally", () => Change("Flip horizontal", n => n.FlipX = !n.FlipX));
        var flipY = new IconButton("flip", "Flip vertically", () => Change("Flip vertical", n => n.FlipY = !n.FlipY)) { RenderTransform = new RotateTransform { Angle = 90 }, RenderTransformOrigin = new(.5, .5) };
        position.Body.Children.Add(Studio.Columns((Number("°", node.Rotation, v => Change("Rotate", n => n.Rotation = v)), -1), (flip, 30), (flipY, 30)));
        var size = AddSection("Layout", node.IsContainer ? "plus" : null, () => Change("Add auto layout", n => { if (n.IsContainer) n.Layout.Direction = LayoutDirection.Horizontal; }));
        var chain = new IconButton("link", "Lock aspect ratio", () => _aspectLocked = !_aspectLocked) { IsSelected = _aspectLocked, Width = 24 };
        size.Body.Children.Add(Studio.Columns((Number("W", node.Width, v => Resize(v, null), 1), -1), (Number("H", node.Height, v => Resize(null, v), 1), -1), (chain, 24)));
        if (node.IsContainer)
        {
            var clip = Check("Clip content", node.ClipContent, value => Change("Clip content", n => n.ClipContent = value)); size.Body.Children.Add(clip);
            size.Body.Children.Add(Studio.Choice(Enum.GetNames<LayoutDirection>(), node.Layout.Direction.ToString(), value => Change("Change auto layout", n => n.Layout.Direction = Enum.Parse<LayoutDirection>(value)), "Auto layout direction"));
            if (node.Layout.Direction != LayoutDirection.None)
            {
                size.Body.Children.Add(Studio.Columns((Number("Gap", node.Layout.Gap, v => Change("Item spacing", n => n.Layout.Gap = v), 0), -1), (Studio.Choice(Enum.GetNames<LayoutAlignment>(), node.Layout.Alignment.ToString(), value => Change("Layout alignment", n => n.Layout.Alignment = Enum.Parse<LayoutAlignment>(value)), "Layout alignment"), -1)));
                size.Body.Children.Add(Studio.Columns((Number("L", node.Layout.PaddingLeft, v => Change("Left padding", n => n.Layout.PaddingLeft = v), 0), -1), (Number("R", node.Layout.PaddingRight, v => Change("Right padding", n => n.Layout.PaddingRight = v), 0), -1)));
                size.Body.Children.Add(Studio.Columns((Number("T", node.Layout.PaddingTop, v => Change("Top padding", n => n.Layout.PaddingTop = v), 0), -1), (Number("B", node.Layout.PaddingBottom, v => Change("Bottom padding", n => n.Layout.PaddingBottom = v), 0), -1)));
                size.Body.Children.Add(Studio.Columns((Check("Hug width", node.Layout.HugWidth, v => Change("Hug width", n => n.Layout.HugWidth = v)), -1), (Check("Hug height", node.Layout.HugHeight, v => Change("Hug height", n => n.Layout.HugHeight = v)), -1)));
            }
        }
        if (node.Parent is not null)
        {
            var constraints = AddSection("Constraints");
            constraints.Body.Children.Add(Studio.Columns((Studio.Choice(Enum.GetNames<AxisConstraint>(), node.HorizontalConstraint.ToString(), value => Change("Horizontal constraint", n => n.HorizontalConstraint = Enum.Parse<AxisConstraint>(value)), "Horizontal constraint"), -1), (Studio.Choice(Enum.GetNames<AxisConstraint>(), node.VerticalConstraint.ToString(), value => Change("Vertical constraint", n => n.VerticalConstraint = Enum.Parse<AxisConstraint>(value)), "Vertical constraint"), -1)));
            if (node.Parent.Layout.Direction != LayoutDirection.None) constraints.Body.Children.Add(Studio.Columns((Check("Fill width", node.FillWidth, v => Change("Fill width", n => n.FillWidth = v)), -1), (Check("Fill height", node.FillHeight, v => Change("Fill height", n => n.FillHeight = v)), -1)));
        }
        var appearance = AddSection("Appearance");
        appearance.Body.Children.Add(Studio.Columns((Number("%", node.Opacity * 100, v => Change("Opacity", n => n.Opacity = v / 100), 0, 100), -1), (Number("R", node.CornerRadius, v => Change("Corner radius", n => n.CornerRadius = v), 0), -1)));
        appearance.Body.Children.Add(Studio.Choice(Enum.GetNames<BlendKind>(), node.Blend.ToString(), value => Change("Blend mode", n => n.Blend = Enum.Parse<BlendKind>(value)), "Blend mode"));
        if (node.Kind is NodeKind.Polygon or NodeKind.Star)
        {
            appearance.Body.Children.Add(Number("N", node.Sides, v => Change("Polygon sides", n => n.Sides = (int)v), 3, 128));
            if (node.Kind == NodeKind.Star) appearance.Body.Children.Add(Number("%", node.StarRatio * 100, v => Change("Star ratio", n => n.StarRatio = v / 100), 1, 100));
        }
        if (node.Kind == NodeKind.Text) BuildTypography(node);
        BuildFills(node); BuildStrokes(node); BuildEffects(node);
        if (Session.SelectionRoots.Count >= 2)
        {
            var paths = AddSection("Combine shapes"); var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            foreach (var op in Enum.GetValues<BooleanOperation>()) row.Children.Add(new IconButton(op.ToString().ToLowerInvariant(), op.ToString(), () => Run(() => BooleanOperations.Apply(Session, Surface.Renderer, op)))); paths.Body.Children.Add(row);
            paths.Body.Children.Add(Studio.Columns((new StudioButton("Distribute H", () => Run(() => Session.Distribute(true))), -1), (new StudioButton("Distribute V", () => Run(() => Session.Distribute(false))), -1)));
        }
        if (node.Kind is NodeKind.Instance or NodeKind.Component)
        {
            var component = AddSection(node.Kind == NodeKind.Component ? "Component" : "Instance");
            if (node.Kind == NodeKind.Instance)
            {
                component.Body.Children.Add(new StudioButton("Reset overrides", () => Run(() => ComponentService.ResetOverrides(Session))));
                component.Body.Children.Add(new StudioButton("Detach instance", () => Run(() => ComponentService.Detach(Session))));
            }
            else component.Body.Children.Add(new StudioButton("Update all instances", () => Run(() => Session.Edit("Synchronize components", () => ComponentService.Synchronize(Session.Document)))));
        }
        AddExportSection();
    }
    private InspectorSection AddSection(string title, string? glyph = null, Action? action = null)
    {
        var section = new InspectorSection(title, glyph, action); _inspector.Children.Add(section); return section;
    }
    private static NumericField Number(string label, double value, Action<double> changed, double minimum = -1_000_000, double maximum = 1_000_000) => new(label, value, changed) { Minimum = minimum, Maximum = maximum };
    private static CheckBox Check(string text, bool value, Action<bool> changed)
    {
        var box = new CheckBox { Content = text, IsChecked = value, FontFamily = Studio.Font, FontSize = 11, MinWidth = 0, MinHeight = 28, Padding = new(0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(box, text); box.Checked += (_, _) => changed(true); box.Unchecked += (_, _) => changed(false); return box;
    }
    private void Resize(double? width, double? height)
    {
        Change("Resize layers", node =>
        {
            var w = width ?? node.Width; var h = height ?? node.Height;
            if (_aspectLocked) { if (width.HasValue) h = node.Height * w / Math.Max(1, node.Width); else w = node.Width * h / Math.Max(1, node.Height); }
            node.Layout.HugWidth = node.Layout.HugHeight = false; LayoutEngine.Resize(node, w, h);
        });
    }
    private void BuildTypography(DesignNode node)
    {
        var section = AddSection("Typography");
        var family = Studio.Input(node.FontFamily, "Font family"); family.LostFocus += (_, _) => { if (family.Text != node.FontFamily && !string.IsNullOrWhiteSpace(family.Text)) Change("Font family", n => n.FontFamily = family.Text); }; section.Body.Children.Add(family);
        var weights = new[] { "Regular", "Medium", "Semibold", "Bold" }; var weightValues = new[] { 400, 500, 600, 700 };
        section.Body.Children.Add(Studio.Columns((Studio.Choice(weights, weights[Math.Clamp((node.FontWeight - 400) / 100, 0, 3)], value => Change("Font weight", n => n.FontWeight = weightValues[Array.IndexOf(weights, value)]), "Font weight"), -1), (Number("T", node.FontSize, v => Change("Font size", n => n.FontSize = v), 1, 4096), 92)));
        section.Body.Children.Add(Studio.Columns((Number("↕", node.LineHeight * 100, v => Change("Line height", n => n.LineHeight = v / 100), 20, 1000), -1), (Number("↔", node.LetterSpacing, v => Change("Letter spacing", n => n.LetterSpacing = v), -100, 100), -1)));
        var align = new SegmentedControl(["Left", "Center", "Right"], (int)node.TextAlign); align.SelectionChanged += index => Change("Text alignment", n => n.TextAlign = (TextAlignment)index); section.Body.Children.Add(align);
        var text = Studio.Input(node.Text, "Text content"); text.AcceptsReturn = true; text.TextWrapping = TextWrapping.Wrap; text.Height = 68;
        text.LostFocus += (_, _) => { if (text.Text != node.Text) Change("Change text", n => { n.Text = text.Text; ComponentService.SetOverride(n, text: text.Text); }); }; section.Body.Children.Add(text);
        section.Body.Children.Add(new StudioButton("Edit on canvas", () => Surface.BeginTextEdit(node)));
    }
    private void BuildFills(DesignNode node)
    {
        var fills = AddSection("Fill", "plus", () => Change("Add fill", n => n.Fills.Add(new())));
        for (var index = 0; index < node.Fills.Count; index++)
        {
            var i = index; var fill = node.Fills[i];
            fills.Body.Children.Add(Studio.Columns((new ColorField(fill.Color, c => Change("Fill color", n => { if (n.Fills.Count > i) { n.Fills[i].Color = c; ComponentService.SetOverride(n, fill: c); } })), -1), (Number("%", fill.Opacity * 100, v => Change("Fill opacity", n => { if (n.Fills.Count > i) n.Fills[i].Opacity = v / 100; }), 0, 100), 65), (new IconButton(fill.Visible ? "eye" : "eye-off", "Toggle fill", () => Change("Toggle fill", n => { if (n.Fills.Count > i) n.Fills[i].Visible = !n.Fills[i].Visible; })) { Width = 23 }, 23), (new IconButton("minus", "Remove fill", () => Change("Remove fill", n => { if (n.Fills.Count > i) n.Fills.RemoveAt(i); })) { Width = 23 }, 23)));
            fills.Body.Children.Add(Studio.Choice(Enum.GetNames<FillKind>(), fill.Kind.ToString(), value => Change("Fill type", n => { if (n.Fills.Count > i) n.Fills[i].Kind = Enum.Parse<FillKind>(value); }), "Fill type"));
            if (fill.Kind != FillKind.Solid)
            {
                for (var stopIndex = 0; stopIndex < fill.Stops.Count; stopIndex++)
                {
                    var s = stopIndex; var stop = fill.Stops[s];
                    fills.Body.Children.Add(Studio.Columns((new ColorField(stop.Color, color => Change("Gradient stop color", n => { if (n.Fills.Count > i && n.Fills[i].Stops.Count > s) n.Fills[i].Stops[s].Color = color; })), -1), (Number("%", stop.Offset * 100, value => Change("Gradient stop position", n => { if (n.Fills.Count > i && n.Fills[i].Stops.Count > s) n.Fills[i].Stops[s].Offset = value / 100; }), 0, 100), 75)));
                }
                fills.Body.Children.Add(new StudioButton("Add gradient stop", () => Change("Add gradient stop", n => { if (n.Fills.Count > i) n.Fills[i].Stops.Add(new() { Offset = .5, Color = "#FFFFFF" }); })));
                fills.Body.Children.Add(Studio.Columns((Number("X1", fill.Start.X, v => Change("Gradient start", n => { if (n.Fills.Count > i) n.Fills[i].Start = n.Fills[i].Start with { X = v }; }), 0, 1), -1), (Number("Y1", fill.Start.Y, v => Change("Gradient start", n => { if (n.Fills.Count > i) n.Fills[i].Start = n.Fills[i].Start with { Y = v }; }), 0, 1), -1)));
            }
        }
    }
    private void BuildStrokes(DesignNode node)
    {
        var section = AddSection("Stroke", "plus", () => Change("Add stroke", n => n.Strokes.Add(new())));
        for (var index = 0; index < node.Strokes.Count; index++)
        {
            var i = index; var stroke = node.Strokes[i];
            section.Body.Children.Add(Studio.Columns((new ColorField(stroke.Color, color => Change("Stroke color", n => { if (n.Strokes.Count > i) n.Strokes[i].Color = color; })), -1), (Number("W", stroke.Width, v => Change("Stroke width", n => { if (n.Strokes.Count > i) n.Strokes[i].Width = v; }), 0, 1000), 68), (new IconButton("minus", "Remove stroke", () => Change("Remove stroke", n => { if (n.Strokes.Count > i) n.Strokes.RemoveAt(i); })) { Width = 24 }, 24)));
            section.Body.Children.Add(Studio.Columns((Number("%", stroke.Opacity * 100, v => Change("Stroke opacity", n => { if (n.Strokes.Count > i) n.Strokes[i].Opacity = v / 100; }), 0, 100), -1), (Studio.Choice(["Solid", "Dashed", "Dotted"], stroke.Dashes.Count == 0 ? "Solid" : stroke.Dashes[0] == 1 ? "Dotted" : "Dashed", value => Change("Stroke dash", n => { if (n.Strokes.Count > i) n.Strokes[i].Dashes = value == "Dashed" ? [8, 6] : value == "Dotted" ? [1, 5] : []; }), "Stroke dash"), -1)));
        }
    }
    private void BuildEffects(DesignNode node)
    {
        var effects = AddSection("Effects", "plus", () => Change("Add shadow", n => { if (n.Shadows.Count == 0) n.Shadows.Add(new()); }));
        if (node.Shadows.FirstOrDefault() is not { } shadow) return;
        effects.Body.Children.Add(Studio.Columns((Studio.Text("Drop shadow", 11), -1), (new IconButton(shadow.Visible ? "eye" : "eye-off", "Toggle shadow", () => Change("Toggle shadow", n => { if (n.Shadows.Count > 0) n.Shadows[0].Visible = !n.Shadows[0].Visible; })), 24), (new IconButton("minus", "Remove shadow", () => Change("Remove shadow", n => n.Shadows.Clear())), 24)));
        effects.Body.Children.Add(Studio.Columns((Number("X", shadow.X, v => Change("Shadow X", n => { if (n.Shadows.Count > 0) n.Shadows[0].X = v; })), -1), (Number("Y", shadow.Y, v => Change("Shadow Y", n => { if (n.Shadows.Count > 0) n.Shadows[0].Y = v; })), -1)));
        effects.Body.Children.Add(Studio.Columns((Number("Blur", shadow.Blur, v => Change("Shadow blur", n => { if (n.Shadows.Count > 0) n.Shadows[0].Blur = v; }), 0, 512), -1), (Number("%", shadow.Opacity * 100, v => Change("Shadow opacity", n => { if (n.Shadows.Count > 0) n.Shadows[0].Opacity = v / 100; }), 0, 100), -1)));
        effects.Body.Children.Add(new ColorField(shadow.Color, c => Change("Shadow color", n => { if (n.Shadows.Count > 0) n.Shadows[0].Color = c; })));
    }
    private void AddExportSection()
    {
        var section = AddSection("Export");
        section.Body.Children.Add(Studio.Columns((Studio.Choice(["1×", "2×", "3×", "4×"], ((int)_exportScale) + "×", value => _exportScale = value[0] - '0', "Export scale"), 70), (new StudioButton("PNG", () => RunAsync(() => ExportAsync(false))) { RestBackground = Studio.Field, Background = Studio.Brush(Studio.Field) }, -1), (new StudioButton("SVG", () => RunAsync(() => ExportAsync(true))) { RestBackground = Studio.Field, Background = Studio.Brush(Studio.Field) }, -1)));
        section.Body.Children.Add(Wrapped(Session.Selection.Count > 0 ? "Exports selected layers at their actual document dimensions." : "Exports all visible layers on this page.", 10));
    }
    private void BuildPrototypeInspector()
    {
        var heading = AddSection("Prototype");
        heading.Body.Children.Add(Wrapped("Connect a layer to another frame. Present the design and click the layer to navigate."));
        if (Session.Primary is not { } node) { heading.Body.Children.Add(Wrapped("Select a layer to add an interaction.")); return; }
        var interaction = AddSection("Interactions"); interaction.Body.Children.Add(Studio.Text("On click → Navigate to", 11));
        var frames = Session.Document.AllNodes().Where(n => n.IsFrame && n.Id != node.Id && !n.IsDescendantOf(node)).ToArray();
        var combo = new ComboBox { Style = (Style)StudioResources.Current["VS.ComboBox"], Height = 32, FontFamily = Studio.Font };
        combo.Items.Add(new ComboBoxItem { Content = "None", Tag = "" });
        foreach (var frame in frames) combo.Items.Add(new ComboBoxItem { Content = frame.Name, Tag = frame.Id });
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == node.PrototypeTargetId) ?? combo.Items[0];
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem item) Change("Prototype interaction", n => n.PrototypeTargetId = string.IsNullOrEmpty((string)item.Tag) ? null : (string)item.Tag); };
        AutomationProperties.SetName(combo, "Prototype destination"); interaction.Body.Children.Add(combo);
        interaction.Body.Children.Add(new StudioButton("Present prototype", () => Run(Surface.Present)) { IsPrimary = true });
        var info = AddSection("Preview"); info.Body.Children.Add(Wrapped("Navigation is immediate. Press Escape to return to editing. Prototype links are stored in the document and work without a server."));
    }
}
