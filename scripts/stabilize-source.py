"""One-time integration of locally validated alpha stabilization changes."""
from pathlib import Path
root = Path(__file__).resolve().parents[1]
def replace(path: str, old: str, new: str):
    p = root / path
    source = p.read_text()
    if old not in source:
        raise RuntimeError(f"Integration baseline changed: {path}: {old[:70]}")
    p.write_text(source.replace(old, new))

replace('src/VectorSpace.Workbench/StudioWorkbench.cs',
    '{n.Node.Visible}:{n.Node.Locked}:{n.Node.Expanded}',
    '{n.Node.Visible}:{n.Node.Locked}:{n.Node.Expanded}:{n.Node.Kind}:{n.Node.Children.Count}')
replace('src/VectorSpace.Workbench/StudioWorkbench.cs',
    'ToggleExpanded = () => { node.Expanded = !node.Expanded; RefreshLayers(true); },',
    'ToggleExpanded = () => { if (Session.Document.Find(node.Id) is { } current) { current.Expanded = !current.Expanded; RefreshLayers(true); } },')
replace('src/VectorSpace.Workbench/StudioWorkbench.cs',
    'ToggleVisibility = () => Run(() => Session.Edit("Toggle layer visibility", () => node.Visible = !node.Visible)),',
    'ToggleVisibility = () => Run(() => { if (Session.Document.Find(node.Id) is { } current) Session.Edit("Toggle layer visibility", () => current.Visible = !current.Visible); }),')
replace('src/VectorSpace.Workbench/StudioWorkbench.cs',
    'ToggleLocked = () => Run(() => Session.Edit("Toggle layer lock", () => node.Locked = !node.Locked)),',
    'ToggleLocked = () => Run(() => { if (Session.Document.Find(node.Id) is { } current) Session.Edit("Toggle layer lock", () => current.Locked = !current.Locked); }),')
replace('src/VectorSpace.Workbench/StudioWorkbench.cs',
    'Rename = () => RunAsync(() => RenameLayerAsync(node))',
    'Rename = () => { if (Session.Document.Find(node.Id) is { } current) RunAsync(() => RenameLayerAsync(current)); }')
replace('src/VectorSpace.Editor/DesignSurface.cs',
    '            Renderer.ClearCache(); _hover = null;',
    '''            // Undo/load can replace every node while a pen or pointer gesture is active.
            // Never let transient gesture references survive the transaction they belong to.
            if (Session?.IsInteracting != true)
            {
                _gesture = Gesture.None; _created = null; _penNode = null;
                _marquee = null; _guide = null; _snapLines = []; _originals.Clear();
                _canvas.ReleasePointerCaptures();
            }
            Renderer.ClearCache(); _hover = null;''')
replace('src/VectorSpace.Editor/DesignSurface.cs',
    'editor.Viewport.WorldToScreen(_vectorNode.WorldMatrix.Map(_vectorNode.Points[i].Position)).DistanceTo(screen)',
    'editor.Viewport.WorldToScreen(_vectorNode.WorldMatrix.Map(VectorPointPosition(_vectorNode, _vectorNode.Points[i].Position))).DistanceTo(screen)')
replace('src/VectorSpace.Editor/DesignSurface.cs',
    '    private void CaptureOriginals()',
    '''    private static Vec2 VectorPointPosition(DesignNode node, Vec2 point) =>
        node.PathWidth > 0 && node.PathHeight > 0
            ? new(point.X * node.Width / node.PathWidth, point.Y * node.Height / node.PathHeight)
            : point;
    private void CaptureOriginals()''')
replace('src/VectorSpace.Documents/SampleDocument.cs',
    'badge.Add(Text("✦  Your ideas deserve a little space", 13, 7, 232, 18, 11, 500, "#7156B8"));',
    'badge.Add(Spark("Announcement icon", 13, 9, 10, "#7156B8")); badge.Add(Text("Your ideas deserve a little space", 31, 7, 220, 18, 11, 500, "#7156B8"));')
replace('src/VectorSpace.Documents/SampleDocument.cs',
    '"A little progress,\\nevery day. ✦"', '"A little progress,\\nevery day."')
replace('src/VectorSpace.Documents/SampleDocument.cs',
    'phone.Add(Text("☰", 306, 33, 28, 28, 19));',
    'for (var line = 0; line < 3; line++) phone.Add(Rectangle("Menu line", 306, 37 + line * 6, 18, 2, "#22212D", 1));')
replace('src/VectorSpace.Documents/SampleDocument.cs',
    'phone.Add(Text("✦  A little room for possibility", 38, 115, 229, 20, 11, 500, "#7156B8"));',
    'phone.Add(Spark("Announcement icon", 37, 117, 10, "#7156B8")); phone.Add(Text("A little room for possibility", 55, 115, 205, 20, 11, 500, "#7156B8"));')
replace('src/VectorSpace.Documents/SampleDocument.cs',
    '    private static DesignNode Frame(',
    '    private static DesignNode Spark(string name, double x, double y, double size, string color) => new() { Kind = NodeKind.Star, Name = name, X = x, Y = y, Width = size, Height = size, Sides = 4, StarRatio = .3, Fill = color };\n    private static DesignNode Frame(')
replace('tests/browser/editor.spec.mjs',
    'page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, { timeout: 150_000 })',
    'page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150_000 })')
replace('src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs',
    'json.WriteString("kind", primary?.Kind.ToString());',
    'json.WriteString("kind", primary?.Kind.ToString()); json.WriteBoolean("visible", primary?.Visible ?? false); json.WriteBoolean("locked", primary?.Locked ?? false);')
replace('README.md',
    '**Status: 0.1.0-alpha.1.**',
    '![VectorSpace running in the browser](docs/images/workbench.png)\n\n**Status: 0.1.0-alpha.1.**')
replace('README.md',
    'dotnet run --project src/VectorSpace.App -f net10.0-desktop',
    'dotnet run --project src/VectorSpace.App -f net10.0-desktop -p:VectorSpaceDesktopOnly=true')
Path(__file__).unlink()
