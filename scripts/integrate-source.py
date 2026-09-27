"""Apply locally validated integration corrections; removed after the integration commit."""
from pathlib import Path
r = Path(__file__).resolve().parents[1]
for p in (r / 'src').rglob('*.cs'):
    s = p.read_text(); s = s.replace('.Map(new(', '.Map(new Vec2(').replace('Map(new(r.', 'Map(new Vec2(r.')
    p.write_text(s)
p = r / 'tests/VectorSpace.Tests/Program.cs'; p.write_text(p.read_text().replace('.Map(new(', '.Map(new Vec2('))
p = r / 'src/VectorSpace.Workbench/StudioWorkbench.Inspector.cs'; p.write_text(p.read_text().replace('(TextAlignment)index', '(VectorSpace.Core.TextAlignment)index'))
p = r / 'src/VectorSpace.Editor/DesignSurface.cs'; p.write_text(p.read_text().replace('ContextRequested', 'CanvasContextRequested').replace('public void Dispose()', 'public new void Dispose()'))
p = r / 'src/VectorSpace.Workbench/StudioWorkbench.cs'; p.write_text(p.read_text().replace('Surface.ContextRequested', 'Surface.CanvasContextRequested').replace('public void Dispose()', 'public new void Dispose()').replace('_autosaveTimer.Tick += (_, _) => { _autosaveTimer.Stop(); RunAsync(AutosaveAsync); };', '_autosaveTimer.Tick += async (_, _) => { _autosaveTimer.Stop(); await AutosaveAsync(); };'))
p = r / 'src/VectorSpace.Controls/LayerRow.cs'; p.write_text(p.read_text().replace('PointerEntered += ShowActions; PointerExited += HideActions;', 'grid.PointerEntered += ShowActions; grid.PointerExited += HideActions;'))
p = r / 'src/VectorSpace.Editing/EditorSession.cs'; s = p.read_text()
s = s.replace('        LayoutEngine.Arrange(Page.Nodes);\n        var before = _before;', '        ComponentService.Synchronize(Document);\n        foreach (var page in Document.Pages) LayoutEngine.Arrange(page.Nodes);\n        var before = _before;')
s = s.replace('    public void MoveSelection(double x, double y) => UpdateSelection("Move layers", n => { n.X += x; n.Y += y; });', '''    public void MoveSelection(double x, double y)
    {
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray();
        if (nodes.Length == 0) return;
        Edit("Move layers", () =>
        {
            foreach (var node in nodes)
            {
                var inverse = node.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity;
                var delta = inverse.Map(new Vec2(x, y)) - inverse.Map(Vec2.Zero);
                node.X += delta.X; node.Y += delta.Y;
            }
        });
    }''')
s = s.replace('    public string CopySelection() => DocumentJson.SaveNodes(SelectionRoots);', '''    public string CopySelection() => DocumentJson.SaveNodes(SelectionRoots.Select(node =>
    {
        var clone = DocumentJson.CloneNode(node);
        NodeGeometry.SetLocalMatrix(clone, node.WorldMatrix);
        return clone;
    }));'''); p.write_text(s)
p = r / 'src/VectorSpace.Editing/ComponentService.cs'; s = p.read_text(); a = s.index('        if (editor.SelectionRoots.Count != 1'); b = s.index('\n    public static DesignNode InsertInstance', a)
s = s[:a] + '''        if (editor.SelectionRoots.Count != 1 || editor.Primary is not { } node || node.IsEffectivelyLocked) return;
        editor.Edit("Create component", () =>
        {
            if (node.IsContainer && node.Kind != NodeKind.Instance)
            {
                node.Kind = NodeKind.Component; node.ComponentId = null; return;
            }
            var siblings = node.Parent?.Children ?? editor.Page.Nodes;
            var index = siblings.IndexOf(node);
            var bounds = node.LocalMatrix.Map(node.LocalBounds);
            var component = new DesignNode { Kind = NodeKind.Component, Name = node.Name + " / Component", X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, Fills = [], Parent = node.Parent };
            siblings.RemoveAt(index); node.X -= bounds.X; node.Y -= bounds.Y; component.Add(node); siblings.Insert(index, component); editor.Select(component);
        });
    }
''' + s[b:]; p.write_text(s)
p = r / 'src/VectorSpace.Editor/DesignSurface.cs'; s = p.read_text().replace('        _canvas.SizeChanged += (_, _) => _canvas.Invalidate();', '''        _canvas.SizeChanged += (_, _) =>
        {
            if (IsPresenting && Session?.Document.Find(_presentedFrame) is { } frame)
                Session.Viewport.Fit(frame.WorldBounds, ActualWidth, ActualHeight, 32);
            _canvas.Invalidate();
        };'''); s = s.replace('        _gesture = Gesture.None; _created = null; _marquee = null; _snapLines = []; _guide = null; _penNode = null;', '        _gesture = Gesture.None; _created = null; _marquee = null; _snapLines = []; _guide = null; _penNode = null; _vectorNode = null;'); p.write_text(s)
p = r / 'tests/VectorSpace.Tests/Program.cs'; s = p.read_text(); extra = '''Test("nested selection nudges once", () => { var parent = Node(); var child = parent.Add(Node(10, 10)); var e = Editor(parent); e.Select([parent.Id, child.Id]); e.MoveSelection(1, 0); Equal(parent.X, 1); Equal(child.X, 10); });
Test("clipboard captures world placement", () => { var parent = Node(100, 200); var child = parent.Add(Node(10, 20)); var e = Editor(parent); e.Select(child); e.Paste(e.CopySelection()); Equal(e.Primary!.X, 134); Equal(e.Primary.Y, 244); });
Test("component creation preserves leaf content", () => { var n = Node(30, 40); n.Fill = "#FF0000"; var e = Editor(n); e.Select(n); ComponentService.MakeComponent(e); Check(e.Primary!.Kind == NodeKind.Component); Equal(e.Primary.Children.Count, 1); Check(e.Primary.Children[0].Fill == "#FF0000"); Equal(e.Primary.Children[0].WorldBounds.X, 30); e.Undo(); Check(e.Primary!.Kind == NodeKind.Rectangle); });
Test("source edits synchronize instances atomically", () => { var c = Node(); c.Kind = NodeKind.Component; c.Add(new() { Kind = NodeKind.Text, Text = "Before" }); var e = Editor(c); var i = ComponentService.InsertInstance(e, c, new(300, 0)); var instanceId = i.Id; e.Select(c.Children[0]); e.UpdateSelection("Edit source", n => n.Text = "After"); Check(e.Document.Find(instanceId)!.Children[0].Text == "After"); e.Undo(); Check(e.Document.Find(instanceId)!.Children[0].Text == "Before"); });
Test("sample is valid and renderable", () => { var document = SampleDocument.Create(); DocumentJson.Validate(document); using var renderer = new SceneRenderer(); var frame = document.Pages[0].Nodes[0]; var png = renderer.ExportPng([frame], frame.WorldBounds, .25); using var bitmap = SKBitmap.Decode(png); Check(bitmap.Width == 260 && bitmap.Height == 205); });

'''; p.write_text(s.replace('var failed = 0;', extra + 'var failed = 0;'))
Path(__file__).unlink()
