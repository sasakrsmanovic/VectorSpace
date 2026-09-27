#!/usr/bin/env python3
"""One-time, reviewable integration of the prototype runtime. Removed before merging."""
from pathlib import Path

def replace(path, old, new, count=1):
    p = Path(path); s = p.read_text()
    assert s.count(old) >= count, f'Missing integration anchor: {path}: {old[:100]}'
    p.write_text(s.replace(old, new, count))

core = 'src/VectorSpace.Core/Document.cs'
replace(core, 'public sealed class DesignNode\n', 'public sealed partial class DesignNode\n')
replace(core, 'public int FormatVersion { get; set; } = 2;', 'public int FormatVersion { get; set; } = 3;')
models = 'src/VectorSpace.Core/Prototyping/PrototypeModels.cs'
replace(models, '    public List<PrototypeReaction> Reactions', '    public bool PrototypeReactionsOverride { get; set; }\n    public List<PrototypeReaction> Reactions')
validation = 'src/VectorSpace.Core/Prototyping/PrototypeValidation.cs'
replace(validation, '            if (depth > MaxBranchDepth', '            if (list.Count == 0) return;\n            if (depth > MaxBranchDepth')
replace(validation, '    private static void Fail(', '    [System.Diagnostics.CodeAnalysis.DoesNotReturn]\n    private static void Fail(')
json = 'src/VectorSpace.Documents/DocumentJson.cs'
replace(json, '[JsonSerializable(typeof(List<DesignNode>))]', '[JsonSerializable(typeof(List<DesignNode>))]\n[JsonSerializable(typeof(List<PrototypeReaction>))]')
replace(json, 'document.FormatVersion = 2;', 'document.FormatVersion = 3;')
replace(json, 'document.FormatVersion is not (1 or 2)', 'document.FormatVersion is not (1 or 2 or 3)')
replace(json, '        VariableResolver.Validate(document);', '        VariableResolver.Validate(document);\n        PrototypeValidation.Validate(document);')
replace(json, '            node.Id = ids[node.Id];', '            node.Id = ids[node.Id];\n            PrototypeValidation.Remap(node, ids);')
replace(json, '    public static string SaveNodes(', '    public static List<PrototypeReaction> CloneReactions(List<PrototypeReaction> reactions) => JsonSerializer.Deserialize(JsonSerializer.Serialize(reactions, VectorSpaceJsonContext.Default.ListPrototypeReaction), VectorSpaceJsonContext.Default.ListPrototypeReaction)!;\n    public static string SaveNodes(')

p = Path('src/VectorSpace.Editing/ComponentVariants.cs'); s = p.read_text()
start = s.index('    public static void Switch(EditorSession'); end = s.index('    private static bool SameProperties', start)
block = s[start:end]
body = block.split('    {\n', 1)[1].rsplit('    });', 1)[0]
assert 'editor.Document' in body
body = body.replace('editor.Document', 'document').replace('|| instance.IsEffectivelyLocked)', '|| (!allowLocked && instance.IsEffectivelyLocked))')
s = s[:start] + '''    public static void Switch(EditorSession editor, string instanceId, string componentId) =>
        editor.Edit("Swap component variant", () => SwitchInDocument(editor.Document, instanceId, componentId));

    /// <summary>Change variant identity within an existing transaction. The caller must synchronize,
    /// validate and arrange once the action batch completes; this method does not create editor history.</summary>
    public static void SwitchInDocument(DesignDocument document, string instanceId, string componentId, bool allowLocked = false)
    {
''' + body + '    }\n' + s[end:]
p.write_text(s)
component = 'src/VectorSpace.Editing/ComponentService.cs'
replace(component, '            instance.VariantProperties = copy.VariantProperties;', '''            instance.VariantProperties = copy.VariantProperties;
            instance.Reactions = copy.Reactions; instance.PrototypeTargetId = copy.PrototypeTargetId;
            instance.PrototypeOverflow = copy.PrototypeOverflow; instance.PrototypeReactionsOverride = copy.PrototypeReactionsOverride;''')
replace(component, '            instance.Children = copy.Children;', '            foreach (var n in copy.DescendantsAndSelf()) PrototypeValidation.Remap(n, ids);\n            instance.Children = copy.Children;')
replace(component, '        foreach (var pair in old.VariableBindings', '''        if (old.PrototypeReactionsOverride)
        {
            fresh.PrototypeReactionsOverride = true; fresh.Reactions = DocumentJson.CloneReactions(old.Reactions);
            fresh.PrototypeTargetId = old.PrototypeTargetId; fresh.PrototypeOverflow = old.PrototypeOverflow;
        }
        foreach (var pair in old.VariableBindings''')
replace(component, '            foreach (var pair in node.Overrides.OrderBy', '''            Add(node.PrototypeReactionsOverride.ToString());
            if (node.PrototypeReactionsOverride)
            {
                Add(node.SourceId); Add(node.PrototypeTargetId); Add(node.PrototypeOverflow.ToString());
                Add(System.Text.Json.JsonSerializer.Serialize(node.Reactions, VectorSpaceJsonContext.Default.ListPrototypeReaction));
            }
            foreach (var pair in node.Overrides.OrderBy''')
replace(component, '                    node.Overrides.Clear(); node.VariableModes.Clear();', '                    node.Overrides.Clear(); node.VariableModes.Clear(); node.PrototypeReactionsOverride = false;')
replace('src/VectorSpace.Editing/EditorSession.cs', '            n.VariableModes = n.VariableModes.ToDictionary(p => ids[p.Key], p => ids[p.Value]);', '            n.VariableModes = n.VariableModes.ToDictionary(p => ids[p.Key], p => ids[p.Value]);\n            PrototypeValidation.Remap(n, new Dictionary<string, string>(), ids);')

skia = 'src/VectorSpace.Skia/SceneRenderer.cs'
replace(skia, 'public sealed class SceneRenderer', 'public sealed partial class SceneRenderer')
replace(skia, 'Matrix2D parent, RectD? viewport)', 'Matrix2D parent, RectD? viewport, Vec2? rootScroll = null)')
replace(skia, 'if (viewport is { } view &&', 'if (rootScroll is null && viewport is { } view &&')
replace(skia, '        if (node.ClipContent)\n', '        if (node.ClipContent || rootScroll.HasValue)\n')
replace(skia, '        foreach (var child in node.Children) DrawNode(canvas, child, world, viewport);', '        if (rootScroll is { } scroll) canvas.Translate((float)-scroll.X, (float)-scroll.Y);\n        foreach (var child in node.Children) DrawNode(canvas, child, world, viewport);')
replace('src/VectorSpace.Skia/VectorSpace.Skia.csproj', '  <ItemGroup>\n', '  <ItemGroup>\n    <ProjectReference Include="../VectorSpace.Prototyping/VectorSpace.Prototyping.csproj" />\n')
replace('src/VectorSpace.Prototyping/PrototypeSession.cs', 'userInitiated && trigger is PrototypeTrigger.Click or PrototypeTrigger.MouseDown or PrototypeTrigger.MouseUp or PrototypeTrigger.KeyDown', 'userInitiated && (trigger is PrototypeTrigger.Click or PrototypeTrigger.MouseDown or PrototypeTrigger.MouseUp or PrototypeTrigger.KeyDown)')
replace('src/VectorSpace.Prototyping/PrototypeSession.cs', 'Document.RebuildParents(); new VariableResolver(Document).Apply();', 'Document.RebuildParents(); ComponentService.Synchronize(Document); new VariableResolver(Document).Apply();')
replace('src/VectorSpace.Prototyping/PrototypeSession.cs', 'left.Text + "FF"', '"FF" + left.Text[1:]' if False else '"#FF" + left.Text[1..]')
replace('src/VectorSpace.Prototyping/PrototypeSession.cs', 'right.Text + "FF"', '"#FF" + right.Text[1..]')

surface = 'src/VectorSpace.Editor/DesignSurface.cs'
replace(surface, '    private string? _presentedFrame;\n', '')
replace(surface, 'public bool IsPresenting => _presentedFrame is not null;', 'public bool IsPresenting => _prototypePlayer is not null;')
replace(surface, '            _session = value;', '            ExitPresentation(); _session = value;')
replace(surface, '''            if (IsPresenting && Session?.Document.Find(_presentedFrame) is { } frame)
                Session.Viewport.Fit(frame.WorldBounds, ActualWidth, ActualHeight, 32);
''', '')
replace(surface, 'if (IsPresenting) { NavigatePrototype(world); e.Handled = true; return; }', 'if (IsPresenting) { e.Handled = true; return; }')
p = Path(surface); s = p.read_text(); start = s.index('    public void Present()'); end = s.index('    public new void Dispose()', start); p.write_text(s[:start] + s[end:])
replace(surface, 'if (_disposed) return; _disposed = true;', 'if (_disposed) return; ExitPresentation(); _disposed = true;')
render = 'src/VectorSpace.Editor/DesignSurface.Rendering.cs'
replace(render, '        if (Session is not { } editor', '        if (IsPresenting) return;\n        if (Session is not { } editor')
replace(render, '        if (IsPresenting && editor.Document.Find(_presentedFrame) is { } frame) Renderer.DrawWorldNode(canvas, frame);\n        else Renderer.Draw(canvas, editor.Page.Nodes, worldRect);', '        Renderer.Draw(canvas, editor.Page.Nodes, worldRect);')
assert '_presentedFrame' not in Path(surface).read_text() + Path(render).read_text()
replace('src/VectorSpace.Editor/PrototypePlayer.cs', 'e.Key is >= VirtualKey.Number0 and <= VirtualKey.Number9', '(int)e.Key >= (int)VirtualKey.Number0 && (int)e.Key <= (int)VirtualKey.Number9')

p = Path('src/VectorSpace.Workbench/StudioWorkbench.Inspector.cs'); s = p.read_text(); start = s.index('    private void BuildPrototypeInspector()'); p.write_text(s[:start] + '}\n')
replace('src/VectorSpace.Workbench/StudioWorkbench.cs', '        Surface.PresentationChanged += presenting =>', '        Surface.PrototypeLinkRequested += url => RunAsync(() => OpenPrototypeLinkAsync(url));\n        Surface.PresentationChanged += presenting =>')
replace('src/VectorSpace.Workbench/StudioWorkbench.Prototyping.cs', 'edit(node); node.PrototypeReactionsOverride = true;', 'edit(node); node.PrototypeReactionsOverride = node.SourceId is not null;')

p = Path('src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs'); s = p.read_text()
anchor = '                json.WriteBoolean("presenting", workbench.Surface.IsPresenting); json.WriteEndObject();'
assert anchor in s
s = s.replace(anchor, '''                json.WriteBoolean("presenting", workbench.Surface.IsPresenting);
                if (workbench.Surface.PrototypePlayer is { } player)
                {
                    var playback = player.Playback;
                    json.WriteStartObject("prototype"); json.WriteString("frame", playback.View.FrameId);
                    json.WriteNumber("overlays", playback.View.Overlays.Count); json.WriteString("input", playback.View.InputRoot.Id);
                    json.WriteNumber("history", playback.HistoryCount); json.WriteBoolean("animating", playback.IsAnimating);
                    json.WriteNumber("scrollX", playback.View.InputScroll.X); json.WriteNumber("scrollY", playback.View.InputScroll.Y);
                    json.WriteNumber("zoom", player.Viewport.Zoom); json.WriteNumber("panX", player.Viewport.Pan.X); json.WriteNumber("panY", player.Viewport.Pan.Y);
                    json.WriteString("error", playback.LastError);
                    json.WriteStartObject("values");
                    var resolver = new VectorSpace.Core.VariableResolver(playback.Document);
                    foreach (var v in playback.Document.Variables) json.WriteString(v.Id, resolver.Resolve(v.Id).ToString());
                    json.WriteEndObject(); json.WriteEndObject();
                }
                json.WriteEndObject();''')
s = s.replace('controls.Tick += (_, _) => BrowserFiles.PublishControls(workbench.CaptureAutomationState());', 'controls.Tick += (_, _) => { BrowserFiles.PublishControls(workbench.CaptureAutomationState()); Publish(); };')
p.write_text(s)

replace('tests/VectorSpace.Tests/Program.cs', 'DesignSystemTests.Register(Test);', 'DesignSystemTests.Register(Test);\nPrototypeTests.Register(Test);')
replace('tests/VectorSpace.Tests/DesignSystemTests.cs', 'read.FormatVersion == 2', 'read.FormatVersion == 3')
replace('tests/VectorSpace.Tests/DesignSystemTests.cs', 'v1 documents migrate to v2', 'v1 documents migrate to v3')
for name in ['tests/browser/editor.spec.mjs', 'tests/browser/design-system.spec.mjs']:
    replace(name, 'saved.formatVersion).toBe(2)', 'saved.formatVersion).toBe(3)')
replace('Directory.Build.props', '0.2.0-alpha.2', '0.3.0-alpha.1')
replace('README.md', '0.2.0-alpha.2', '0.3.0-alpha.1')
p = Path('VectorSpace.slnx'); s = p.read_text(); marker = '<Project Path="src/VectorSpace.Skia/VectorSpace.Skia.csproj" />'; assert marker in s
p.write_text(s.replace(marker, '<Project Path="src/VectorSpace.Prototyping/VectorSpace.Prototyping.csproj" />\n    ' + marker))
print('Prototype source integration complete.')
