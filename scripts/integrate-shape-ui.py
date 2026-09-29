"""Guarded source wiring for the authored shape controls. Removed after validation."""
from pathlib import Path

def edit(path, old, new, count=1):
    p=Path(path); s=p.read_text(); assert s.count(old)==count, (path, old[:100], s.count(old), count); p.write_text(s.replace(old,new))

surface='src/VectorSpace.Editor/DesignSurface.cs'
edit(surface,'Vertex, VertexMarquee, ImageCrop }','Vertex, VertexMarquee, ImageCrop, Shape }')
edit(surface,'if (IsImageCropping) CropTarget(out _, out _);','if (IsImageCropping) CropTarget(out _, out _);\n        if (IsShapeEditing) ShapeTarget(out _);')
edit(surface,'if (e.Kind == EditorChangeKind.Tool) { _cropNodeId = null; _cropDocument = null; }','if (e.Kind == EditorChangeKind.Tool) { _cropNodeId = null; _cropDocument = null; _shapeNodeId = null; _shapeDocument = null; _shapeGesture = null; }')
edit(surface,'if (Session is null || IsPresenting || IsImageCropping ||','if (Session is null || IsPresenting || IsImageCropping || IsShapeEditing ||')
edit(surface,'        if (PressVectorEdit(screen, world, shift)) return;','        if (PressShapeEdit(screen, world)) return;\n        if (PressVectorEdit(screen, world, shift)) return;')
edit(surface,'case Gesture.ImageCrop: MoveImageCrop(world); break;','case Gesture.Shape: MoveShapeEdit(world, shift, Keyboard.Alt || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu)); break;\n            case Gesture.ImageCrop: MoveImageCrop(world); break;')
edit(surface,'_cropNodeId = null; _cropDocument = null; _pendingDuplicate = null;', '_shapeNodeId = null; _shapeDocument = null; _shapeGesture = null; _cropNodeId = null; _cropDocument = null; _pendingDuplicate = null;')
edit(surface,'Session.ResolveSelection(Renderer.HitTest(Session.Page.Nodes, world, true, 4 / Session.Viewport.Zoom), deep)', 'Session.ResolveSelection(Renderer.HitTestForSelection(Session.Page.Nodes, world, Session.Primary?.Parent, deep, 4 / Session.Viewport.Zoom), deep)')
edit('src/VectorSpace.Editor/DesignSurface.Rendering.cs','        if (IsImageCropping)\n        {','        if (IsShapeEditing) { DrawShapeHandles(canvas); return; }\n        if (IsImageCropping)\n        {')
edit('src/VectorSpace.Editor/DesignSurface.ToolEditing.cs','if (_gesture == Gesture.ImageCrop) EndImageCrop();','if (_gesture == Gesture.ImageCrop) EndImageCrop();\n        if (_gesture == Gesture.Shape) EndShapeEdit();')

renderer='src/VectorSpace.Skia/SceneRenderer.cs'
edit(renderer,'    public DesignNode? HitTest(IEnumerable<DesignNode> roots, Vec2 point, bool deep = false, double tolerance = 4)\n    {', '''    public DesignNode? HitTest(IEnumerable<DesignNode> roots, Vec2 point, bool deep = false, double tolerance = 4) => HitTestCore(roots, point, deep, tolerance, false, null);
    public DesignNode? HitTestForSelection(IEnumerable<DesignNode> roots, Vec2 point, DesignNode? scope, bool deep = false, double tolerance = 4) => HitTestCore(roots, point, true, tolerance, !deep, scope);
    private DesignNode? HitTestCore(IEnumerable<DesignNode> roots, Vec2 point, bool deep, double tolerance, bool restrictBooleans, DesignNode? scope)
    {''')
edit(renderer,'if ((!node.IsBoolean || deep) && (!node.ClipContent || insideClip))','if ((!node.IsBoolean || deep && (!restrictBooleans || scope == node || scope?.IsDescendantOf(node) == true)) && (!node.ClipContent || insideClip))')
edit(renderer,'var child = HitTest(node.Children, point, deep, tolerance);','var child = HitTestCore(node.Children, point, deep, tolerance, restrictBooleans, scope);')
edit('src/VectorSpace.Core/ShapeStyle.cs','        var r = n.EffectiveCorners;','        if (n.Corners is null && n.CornerRadius <= 0) return true;\n        var r = n.EffectiveCorners;')
p=Path('src/VectorSpace.Editing/DrawingTargetQuery.cs'); s=p.read_text(); a=s.index('    private static bool Contains('); p.write_text(s[:a]+'    private static bool Contains(DesignNode node, Vec2 point) => ShapeGeometry.ContainsCornerBox(node, point);\n}\n')
edit('src/VectorSpace.Editing/EditorSession.Selection.cs','roots[0].IsContainer && roots[0].Kind != NodeKind.Instance','roots[0].IsContainer && !roots[0].IsBoolean && roots[0].Kind != NodeKind.Instance')

commands='src/VectorSpace.Workbench/StudioWorkbench.Commands.cs'
edit(commands,'foreach (var action in PropertyActions()) yield return action;', 'foreach (var action in PropertyActions()) yield return action;\n        foreach (var action in ShapeActions()) yield return action;')
edit(commands,'Surface.HandleToolKey(e.Key, control, shift, alt) || Surface.HandlePointKey', 'Surface.HandleToolKey(e.Key, control, shift, alt) || Surface.HandleShapeKey(e.Key, control, shift, alt) || Surface.HandlePointKey')
edit(commands,'        AddMenu(menu, "Edit vector points",', '''        AddMenu(menu, "Edit shape on canvas", () => Run(Surface.BeginShapeEdit), Session.Primary is { } shape && ShapeEditable(shape) && (ShapeGeometry.HasCorners(shape) || shape.Kind == NodeKind.Ellipse));
        AddMenu(menu, "Outline stroke", () => Run(() => LiveBooleanOperations.Outline(Session, Surface.Renderer)), selected);
        if (Session.Primary?.IsBoolean == true)
        {
            AddMenu(menu, "Flatten Boolean result", () => Run(() => LiveBooleanOperations.Flatten(Session, Surface.Renderer)));
            AddMenu(menu, "Release Boolean operands", () => Run(() => LiveBooleanOperations.Release(Session)));
        }
        AddMenu(menu, "Edit vector points",''')
inspector='src/VectorSpace.Workbench/StudioWorkbench.Inspector.cs'
edit(inspector,'if (node.IsContainer)','if (node.IsContainer && !node.IsBoolean)')
edit(inspector,'if (node.Kind == NodeKind.Text) BuildTypography(node);','BuildShapeInspector(node);\n        if (node.Kind == NodeKind.Text) BuildTypography(node);')
edit(inspector,'n => n.CornerRadius = v','n => { n.CornerRadius = v; n.Corners = null; }')
# Add geometry controls immediately after the existing dash preset row.
p=Path(inspector); s=p.read_text(); marker='"Stroke dash"), -1)));'; assert s.count(marker)==1; s=s.replace(marker,marker+'\n            BuildStrokeGeometryControls(section, node, i);'); p.write_text(s)
workbench='src/VectorSpace.Workbench/StudioWorkbench.cs'
edit(workbench,'Glyph = Glyph(node.Kind), HasChildren','Glyph = node.IsBoolean ? node.Boolean!.Value.ToString().ToLowerInvariant() : Glyph(node.Kind), HasChildren')
edit(workbench,'{n.Node.Kind}:{n.Node.Children.Count}', '{n.Node.Kind}:{n.Node.Boolean}:{n.Node.Children.Count}')

# Preserve the existing two-argument binary API and expose the renderer-aware overload separately.
svg='src/VectorSpace.Documents/SvgFormat.cs'
edit(svg,'public static string Export(IEnumerable<DesignNode> roots, RectD bounds, Func<DesignNode, int, string>? strokeOutline = null)', 'public static string Export(IEnumerable<DesignNode> roots, RectD bounds) => Export(roots, bounds, null);\n    public static string Export(IEnumerable<DesignNode> roots, RectD bounds, Func<DesignNode, int, string>? strokeOutline)')
edit(svg,'if (stroke is not null && stroke != "none") node.Strokes.Add', 'if (kind is not "g" and not "svg" && stroke is not null && stroke != "none") node.Strokes.Add')

# Style packet 2 prevents old clients silently discarding stroke alignment/cap/join semantics.
edit('src/VectorSpace.Documents/PropertyClipboard.cs','Version { get; set; } = 1;', 'Version { get; set; } = 2;')
edit('src/VectorSpace.Documents/PropertyClipboard.cs','packet.Version != 1', 'packet.Version is not (1 or 2)')

browser='src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs'
edit(browser,'json.WriteStartObject(); workbench.WriteCollaborationDiagnostics(json);', 'json.WriteStartObject(); surface.WriteShapeDiagnostics(json); workbench.WriteCollaborationDiagnostics(json);')
edit('tests/VectorSpace.Tests/Program.cs','EditingWorkflowTests.Register(Test);','EditingWorkflowTests.Register(Test);\nShapeTests.Register(Test);')
# Strengthen the non-mutation assertion without comparing fresh randomly identified documents.
tests='tests/VectorSpace.Tests/ShapeTests.cs'
edit(tests,'var before = DocumentJson.Save(Editor(n).Document);','var before = DocumentJson.SaveNodes([n]);')
edit(tests,'Check(DocumentJson.Save(Editor(n).Document).Contains("\\\"outside\\\"", StringComparison.OrdinalIgnoreCase));','Check(DocumentJson.SaveNodes([n]) == before);')
print('Shape controls, input boundaries and native-pixel tests are wired.')
