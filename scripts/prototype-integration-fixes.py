from pathlib import Path
p = Path('tests/VectorSpace.Tests/PrototypeTests.cs')
s = p.read_text().replace('new() { TargetId', 'new PrototypeAction { TargetId').replace('new() { Kind = PrototypeActionKind', 'new PrototypeAction { Kind = PrototypeActionKind')
s = s.replace('        test("prototype action edits participate', '''        test("prototype playground is valid and its smart interactions render", () =>
        {
            var doc = PrototypeSample.Create(); var player = new PrototypeSession(doc);
            var target = player.View.Frame.Children.First(n => n.Name.StartsWith("Explore the details"));
            player.Dispatch(PrototypeTrigger.Click, target.Id); Check(player.View.FrameId == "prototype-detail");
            using var renderer = new SceneRenderer(); var compositor = new PrototypeSceneRenderer(renderer);
            using var bitmap = new SKBitmap(640, 600); using var canvas = new SKCanvas(bitmap);
            player.AdvanceTo(200); compositor.Draw(canvas, player); Check(bitmap.GetPixel(620, 580).Alpha > 0);
        });
        test("prototype action edits participate''')
p.write_text(s)
p = Path('src/VectorSpace.Editor/PrototypePlayer.cs'); p.write_text(p.read_text().replace('Studio.Brush(Studio.Panel)', 'Studio.Brush("#FFFFFF")'))
p = Path('src/VectorSpace.Workbench/StudioWorkbench.Commands.cs'); s = p.read_text()
s = s.replace('        yield return new("Present prototype",', '        yield return new("Prototype playground", "", () => RunAsync(OpenPrototypePlaygroundAsync));\n        yield return new("Present prototype",')
s = s.replace('        AddMenu(menu, "Reset to sample",', '        AddMenu(menu, "Prototype playground", () => RunAsync(OpenPrototypePlaygroundAsync));\n        AddMenu(menu, "Reset to sample",')
p.write_text(s)
p = Path('src/VectorSpace.Workbench/StudioWorkbench.Prototyping.cs'); s = p.read_text()
s = s.replace('    private async Task OpenPrototypeLinkAsync', '''    private async Task OpenPrototypePlaygroundAsync()
    {
        if (!await ConfirmAsync("Open prototype playground?", "This replaces the current document with an editable interaction sample. Save a copy first to retain your current document.")) return;
        Session.Load(PrototypeSample.Create()); Surface.Fit(firstFrame: true); _prototype = true; RefreshInspector();
        ShowStatus("Choose Present to explore navigation, overlays, hover effects and interactive variants.");
    }
    private async Task OpenPrototypeLinkAsync''')
p.write_text(s)
p = Path('src/VectorSpace.Workbench/StudioWorkbench.cs'); s = p.read_text()
s = s.replace('''            _leftPanel.Visibility = _rightPanel.Visibility = _palette.Visibility = presenting ? Visibility.Collapsed : Visibility.Visible;
            _leftColumn.Width = presenting ? new(0) : new(248); _rightColumn.Width = presenting ? new(0) : new(288);
            reveal.Visibility = presenting ? Visibility.Collapsed : Visibility.Visible;''', '''            var hidden = presenting || !_uiVisible;
            _rightPanel.Visibility = _palette.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            _leftPanel.Visibility = hidden || ActualWidth < 700 ? Visibility.Collapsed : Visibility.Visible;
            _leftColumn.Width = hidden || ActualWidth < 700 ? new(0) : new(ActualWidth < 950 ? 216 : 248);
            _rightColumn.Width = hidden ? new(0) : new(ActualWidth < 950 ? 264 : 288);
            reveal.Visibility = !presenting && (!_uiVisible || ActualWidth < 700) ? Visibility.Visible : Visibility.Collapsed;''')
p.write_text(s)
