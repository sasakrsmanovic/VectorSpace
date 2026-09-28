using VectorSpace.Skia;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private async Task OpenAppearancePlaygroundAsync()
    {
        if (!await ConfirmAsync("Open appearance playground?", "This replaces the current document with editable paint studies. Save a copy first to keep your current document.")) return;
        Session.Load(AppearanceSample.Create()); Surface.Fit(firstFrame: true); _prototype = false; RefreshInspector();
        ShowStatus("Select an image study to edit its fill, crop and effects.");
    }
    private void ChangeAppearance(string label, Action<DesignNode> edit, bool fills = true, bool effects = false) => Change(label, n =>
    {
        edit(n); ComponentService.SetAppearanceOverride(n, fills, effects);
    });
    private async Task PlaceImageAsync(string? nodeId = null, int fillIndex = -1)
    {
        var document = Session.Document;
        var selectedFile = await _storage.OpenImageAsync(); if (selectedFile is not { } file) return;
        if (!ReferenceEquals(document, Session.Document)) throw new InvalidOperationException("The document changed while the image picker was open.");
        var image = RasterImageCodec.Import(file.Bytes);
        Session.Edit(nodeId is null ? "Place image" : "Replace image fill", () =>
        {
            if (nodeId is null)
            {
                var center = Session.Viewport.ScreenToWorld(new(Surface.ActualWidth / 2, Surface.ActualHeight / 2));
                var scale = Math.Min(1, 640d / Math.Max(image.Width, image.Height));
                var node = new DesignNode { Name = Path.GetFileNameWithoutExtension(file.Name), Width = image.Width * scale, Height = image.Height * scale,
                    X = center.X - image.Width * scale / 2, Y = center.Y - image.Height * scale / 2,
                    Fills = [new() { Kind = FillKind.Image, ImageData = image.DataUri }] };
                Session.AddNode(node); Session.Select(node);
            }
            else
            {
                var node = Session.Document.Find(nodeId) ?? throw new InvalidOperationException("The target layer no longer exists.");
                if (node.IsEffectivelyLocked) throw new InvalidOperationException("Unlock this layer before replacing its image.");
                if (fillIndex < 0 || fillIndex >= node.Fills.Count) throw new InvalidOperationException("The target paint no longer exists.");
                node.Fills[fillIndex].Kind = FillKind.Image; node.Fills[fillIndex].ImageData = image.DataUri;
                ComponentService.SetAppearanceOverride(node, fills: true, effects: false);
            }
        });
        Surface.FocusCanvas();
    }
    private void BuildImageFill(InspectorSection section, DesignNode node, int index)
    {
        var fill = node.Fills[index]; var id = node.Id;
        void Edit(string label, Action<FillStyle> change) => ChangeAppearance(label, n => { if (n.Fills.Count > index) change(n.Fills[index]); });
        section.Body.Children.Add(new StudioButton(fill.ImageData is null ? "Choose image…" : "Replace image…", () => RunAsync(() => PlaceImageAsync(id, index))) { HorizontalAlignment = HorizontalAlignment.Stretch });
        section.Body.Children.Add(Studio.Choice(Enum.GetNames<ImageScaleMode>(), fill.ImageMode.ToString(), v => Edit("Image scale mode", f => f.ImageMode = Enum.Parse<ImageScaleMode>(v)), "Image scale mode"));
        var zoomField = Number("Zoom", fill.ImageScale * 100, v => Edit("Image scale", f => f.ImageScale = v / 100), .1, 100000);
        zoomField.IsEnabled = fill.ImageMode is ImageScaleMode.Crop or ImageScaleMode.Tile;
        section.Body.Children.Add(Studio.Columns((zoomField, -1),
            (Number("Angle", fill.ImageRotation, v => Edit("Image rotation", f => f.ImageRotation = v), -360000, 360000), -1)));
        if (fill.ImageMode is ImageScaleMode.Crop or ImageScaleMode.Tile)
            section.Body.Children.Add(Studio.Columns((Number("Crop X", fill.ImageOffset.X * 100, v => Edit("Image position", f => f.ImageOffset = f.ImageOffset with { X = v / 100 }), -100000, 100000), -1),
                (Number("Crop Y", fill.ImageOffset.Y * 100, v => Edit("Image position", f => f.ImageOffset = f.ImageOffset with { Y = v / 100 }), -100000, 100000), -1)));
        section.Body.Children.Add(Number("EV", fill.Exposure, v => Edit("Image exposure", f => f.Exposure = v), -1, 1));
        section.Body.Children.Add(Studio.Columns((Number("Contrast", fill.Contrast * 100, v => Edit("Image contrast", f => f.Contrast = v / 100), -100, 100), -1),
            (Number("Saturate", fill.Saturation * 100, v => Edit("Image saturation", f => f.Saturation = v / 100), -100, 100), -1)));
        section.Body.Children.Add(new StudioButton("Reset image adjustments", () => Edit("Reset image adjustments", f => { f.Exposure = f.Contrast = f.Saturation = 0; })));
        section.Body.Children.Add(new StudioButton("Edit image crop", () => Run(() => Surface.BeginImageCrop(id, index))) { IsEnabled = fill.ImageData is not null });
        if (Surface.IsImageCropping) section.Body.Children.Add(new StudioButton("Done cropping", () => Surface.EndImageCrop()));
    }
    private void BuildAppearanceEffects(DesignNode node)
    {
        var effects = AddSection("Effects", "plus", () => ChangeAppearance("Add effect", n => n.Shadows.Add(new()), false, true));
        for (var index = 0; index < node.Shadows.Count; index++)
        {
            var i = index; var effect = node.Shadows[i];
            void Edit(string label, Action<ShadowStyle> action) => ChangeAppearance(label, n => { if (n.Shadows.Count > i) action(n.Shadows[i]); }, false, true);
            effects.Body.Children.Add(Studio.Columns((Studio.Choice(Enum.GetNames<EffectKind>(), effect.Kind.ToString(), v => Edit("Effect type", f => f.Kind = Enum.Parse<EffectKind>(v)), "Effect type " + (i + 1)), -1),
                (new IconButton(effect.Visible ? "eye" : "eye-off", "Toggle effect", () => Edit("Toggle effect", f => f.Visible = !f.Visible)) { Width = 23 }, 23),
                (new IconButton("minus", "Remove effect", () => ChangeAppearance("Remove effect", n => { if (n.Shadows.Count > i) n.Shadows.RemoveAt(i); }, false, true)) { Width = 23 }, 23)));
            effects.Body.Children.Add(Number("Blur", effect.Blur, v => Edit("Effect blur", f => f.Blur = v), 0, 512));
            if (effect.Kind != EffectKind.LayerBlur)
            {
                effects.Body.Children.Add(Studio.Columns((Number("X", effect.X, v => Edit("Effect X", f => f.X = v), -100000, 100000), -1), (Number("Y", effect.Y, v => Edit("Effect Y", f => f.Y = v), -100000, 100000), -1)));
                effects.Body.Children.Add(Studio.Columns((Number("Spread", effect.Spread, v => Edit("Effect spread", f => f.Spread = v), -512, 512), -1), (Number("%", effect.Opacity * 100, v => Edit("Effect opacity", f => f.Opacity = v / 100), 0, 100), -1)));
                effects.Body.Children.Add(new ColorField(effect.Color, c => Edit("Effect color", f => f.Color = c)));
            }
            effects.Body.Children.Add(new StudioButton("Move effect up", () => ChangeAppearance("Reorder effects", n => { if (i > 0 && n.Shadows.Count > i) (n.Shadows[i - 1], n.Shadows[i]) = (n.Shadows[i], n.Shadows[i - 1]); }, false, true)) { IsEnabled = i > 0 });
        }
    }
}
