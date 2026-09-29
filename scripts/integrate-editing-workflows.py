"""One-time authored integration: every existing-source change is asserted before application."""
from pathlib import Path

def replace(path, old, new, count=1):
    p = Path(path); text = p.read_text(); assert text.count(old) == count, (path, old[:120], text.count(old), count)
    p.write_text(text.replace(old, new))

def section(path, start, end, new):
    p = Path(path); text = p.read_text(); assert text.count(start) == 1 and text.count(end) == 1, path
    a = text.index(start); b = text.index(end, a); p.write_text(text[:a] + new + text[b:])

core = 'src/VectorSpace.Core/Document.cs'
replace(core, 'public int FormatVersion { get; set; } = 4;', 'public const int CurrentFormatVersion = 5;\n    public int FormatVersion { get; set; } = CurrentFormatVersion;')
replace(core, '    public bool? Visible { get; set; }\n}', '''    public bool? Visible { get; set; }
    public string? Name { get; set; }
    public List<StrokeStyle>? Strokes { get; set; }
    public TypographyStyle? Typography { get; set; }
    public double? Opacity { get; set; }
    public BlendKind? Blend { get; set; }
    public double? CornerRadius { get; set; }
}''')
json = 'src/VectorSpace.Documents/DocumentJson.cs'
replace(json, '[JsonSerializable(typeof(DesignNode))]', '[JsonSerializable(typeof(DesignNode))]\n[JsonSerializable(typeof(InstanceOverride))]')
replace(json, 'document.FormatVersion = 4;', 'document.FormatVersion = DesignDocument.CurrentFormatVersion;')
replace(json, 'document.FormatVersion is not (1 or 2 or 3 or 4)', 'document.FormatVersion is < 1 or > DesignDocument.CurrentFormatVersion')
validation = 'src/VectorSpace.Documents/AppearanceValidation.cs'
replace(validation, 'CheckFills(node.Fills, ref imageCharacters);', 'CheckFills(node.Fills, ref imageCharacters);\n        PropertyClipboard.ValidateStrokes(node.Strokes);')
replace(validation, 'if (entry.Effects is { } effects) CheckEffects(effects);', '''if (entry.Effects is { } effects) CheckEffects(effects);
            if (entry.Strokes is { } strokes) PropertyClipboard.ValidateStrokes(strokes);
            PropertyClipboard.ValidateTypography(entry.Typography);
            PropertyClipboard.ValidateScalars(entry.Opacity, entry.Blend, entry.CornerRadius);
            if (entry.Name is { } name && (name.Length > 4096 || string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl))) throw new InvalidDataException("Invalid instance layer name.");''')
replace('src/VectorSpace.Documents/PropertyClipboard.cs', 'd <= 0 || d > 1e5', 'd < 0 || d > 1e5')
replace('src/VectorSpace.Documents/PropertyClipboard.cs', 's.Dashes.Any(d => !double.IsFinite(d) || d < 0 || d > 1e5))', 's.Dashes.Any(d => !double.IsFinite(d) || d < 0 || d > 1e5) || s.Dashes.Count > 0 && s.Dashes.All(d => d == 0))')

component = 'src/VectorSpace.Editing/ComponentService.cs'
replace(component, 'public static class ComponentService', 'public static partial class ComponentService')
section(component, '    private static List<FillStyle> CloneFills', '    /// <summary>Resolve acyclic', '''    private static List<FillStyle> CloneFills(List<FillStyle> values) => StyleCloner.Fills(values);
    private static List<ShadowStyle> CloneEffects(List<ShadowStyle> values) => StyleCloner.Effects(values);
''')
replace(component, '            if (o.Text is not null) node.Text = o.Text;', '            ApplyPropertyOverrides(node, o);\n            if (o.Text is not null) node.Text = o.Text;')
replace(component, '            instance.SourceId ??= definition.Id;', '''            if (instance.Overrides.TryGetValue(definition.Id, out var rootProperties))
            {
                if (rootProperties.Name is not null) instance.Name = copy.Name;
                if (rootProperties.Opacity.HasValue) instance.Opacity = copy.Opacity;
                if (rootProperties.Blend.HasValue) instance.Blend = copy.Blend;
            }
            instance.SourceId ??= definition.Id;''')
replace(component, '''                Add(node.SourceId); Add(pair.Key); Add(pair.Value.Text); Add(pair.Value.Fill); Add(pair.Value.Visible?.ToString());
                if (pair.Value.Fills is not null) Add(System.Text.Json.JsonSerializer.Serialize(pair.Value.Fills, VectorSpaceJsonContext.Default.ListFillStyle));
                if (pair.Value.Effects is not null) Add(System.Text.Json.JsonSerializer.Serialize(pair.Value.Effects, VectorSpaceJsonContext.Default.ListShadowStyle));''', '''                Add(node.SourceId); Add(pair.Key);
                Add(System.Text.Json.JsonSerializer.Serialize(pair.Value, VectorSpaceJsonContext.Default.InstanceOverride));''')
editor = 'src/VectorSpace.Editing/EditorSession.cs'
replace(editor, 'DocumentJson.Validate(document); document.RebuildParents(); Document = document;', 'DocumentJson.Validate(document); document.FormatVersion = DesignDocument.CurrentFormatVersion; document.RebuildParents(); Document = document;', 2)
section(editor, '    public void Reorder(int direction, bool extreme = false)', '    public void Align(string alignment)', '    public void Reorder(int direction, bool extreme = false) => ReorderCore(direction, extreme);\n')
replace('src/VectorSpace.Workbench/StudioWorkbench.Inspector.cs', 'Session.UpdateSelection(label, change)', 'PropertyTransfer.Update(Session, label, change)')
commands = 'src/VectorSpace.Workbench/StudioWorkbench.Commands.cs'
replace(commands, 'foreach (var action in EditingActions()) yield return action;', 'foreach (var action in EditingActions()) yield return action;\n        foreach (var action in PropertyActions()) yield return action;')
replace(commands, '                VirtualKey.C => () => RunAsync(() => CopyAsync(false)),', '                VirtualKey.C when alt => () => RunAsync(() => CopyPropertiesAsync(PropertyGroups.All)),\n                VirtualKey.C => () => RunAsync(() => CopyAsync(false)),')
replace(commands, '                VirtualKey.V => () => RunAsync(() => PasteAsync(shift)),', '                VirtualKey.V when alt => () => RunAsync(() => PastePropertiesAsync()),\n                VirtualKey.V => () => RunAsync(() => PasteAsync(shift)),\n                VirtualKey.R => () => RunAsync(RenameSelectionAsync),')
replace(commands, 'VirtualKey.F2 => () => { if (Session.Primary is { } n) RunAsync(() => RenameLayerAsync(n)); },', 'VirtualKey.F2 => () => RunAsync(RenameSelectionAsync),')
replace(commands, 'AddMenu(menu, "Rename                         F2", () => { if (Session.Primary is { } n) RunAsync(() => RenameLayerAsync(n)); }, selected);', 'AddMenu(menu, "Rename layers                 F2", () => RunAsync(RenameSelectionAsync), selected);')
replace(commands, '        AddMenu(menu, "Duplicate                   Ctrl D",', '''        AddMenu(menu, "Copy properties              Ctrl Alt C", () => RunAsync(() => CopyPropertiesAsync(PropertyGroups.All)), selected);
        AddMenu(menu, "Paste properties             Ctrl Alt V", () => RunAsync(() => PastePropertiesAsync()), selected);
        AddMenu(menu, "Paste selected properties…", () => RunAsync(() => PastePropertiesAsync(choose: true)), selected);
        AddMenu(menu, "Duplicate                   Ctrl D",''')
replace(commands, '        if (text.StartsWith(ClipboardPrefix, StringComparison.Ordinal))', '        if (text.StartsWith(PropertyClipboard.Prefix, StringComparison.Ordinal)) { PropertyTransfer.Paste(Session, PropertyClipboard.Read(text).Properties); Surface.FocusCanvas(); }\n        else if (text.StartsWith(ClipboardPrefix, StringComparison.Ordinal))')
section(commands, '    private async Task SaveAsync()', '    private async Task ExportAsync(bool svg)', '')
replace(commands, '        var text = await PromptAsync("Rename layer", node.Name); if (!string.IsNullOrWhiteSpace(text)) Session.Edit("Rename layer", () => node.Name = text);', '''        var id = node.Id; var before = node.Name;
        var text = await PromptAsync("Rename layer", before);
        if (!string.IsNullOrWhiteSpace(text)) LayerRename.Apply(Session, [new(id, before, text)]);
        Surface.FocusCanvas();''')
replace('src/VectorSpace.Controls/BatchRenamePanel.cs', 'public void Dispose()', 'public new void Dispose()')
replace('src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs', 'json.WriteBoolean("ready", true);', 'json.WriteBoolean("ready", true); json.WriteBoolean("saving", workbench.IsSaving); json.WriteBoolean("canvasFocused", workbench.Surface.HasCanvasKeyboardFocus);')
replace('tests/browser/tool-editing.spec.mjs', "  return JSON.parse(await fs.readFile(path, 'utf8'));", "  await expect.poll(async () => (await state(page)).saving).toBe(false);\n  return JSON.parse(await fs.readFile(path, 'utf8'));")
replace('tests/browser/tool-editing.spec.mjs', "  await page.keyboard.press('Enter'); await control(page, 'Edit canvas text');", "  await expect.poll(async () => (await state(page)).canvasFocused).toBe(true);\n  await page.keyboard.press('Enter'); await control(page, 'Edit canvas text');")
replace('tests/VectorSpace.Tests/Program.cs', 'if (args.Contains("--benchmark-editing"))', 'if (args.Contains("--benchmark-workflows")) return EditingWorkflowBenchmarks.Run();\nif (args.Contains("--benchmark-editing"))')
replace('tests/VectorSpace.Tests/Program.cs', 'var failed = 0;', 'EditingWorkflowTests.Register(Test);\n\nvar failed = 0;')
# Retain version-4 input fixtures as migration coverage; update only assertions on newly saved output.
for p in Path('tests').rglob('*'):
    if p.suffix not in ('.cs', '.mjs') or p.name == 'EditingWorkflowTests.cs': continue
    text = p.read_text(); changed = text.replace('.FormatVersion == 4', '.FormatVersion == DesignDocument.CurrentFormatVersion').replace('.formatVersion).toBe(4)', '.formatVersion).toBe(5)')
    if changed != text: p.write_text(changed); print('Updated current-output schema assertions:', p)
replace('Directory.Build.props', '<Version>0.6.0-alpha.1</Version>', '<Version>0.7.0-alpha.1</Version>')
replace('src/VectorSpace.App/VectorSpace.App.csproj', '<ApplicationDisplayVersion>0.6.0</ApplicationDisplayVersion>', '<ApplicationDisplayVersion>0.7.0</ApplicationDisplayVersion>')
replace('src/VectorSpace.App/VectorSpace.App.csproj', '<ApplicationVersion>6</ApplicationVersion>', '<ApplicationVersion>7</ApplicationVersion>')
print('Guarded editing integration applied; historical fixtures were retained.')
