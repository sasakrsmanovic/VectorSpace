"""Apply explicit, guarded source edits; removed after compiled integration is committed."""
from pathlib import Path

def edit(path, old, new, count=1):
    p=Path(path); s=p.read_text(); assert s.count(old)==count, (path, old[:100], s.count(old), count); p.write_text(s.replace(old,new))

core='src/VectorSpace.Core/Document.cs'
edit(core,'public sealed class StrokeStyle','public sealed partial class StrokeStyle')
edit(core,'CurrentFormatVersion = 5','CurrentFormatVersion = 6')
edit('Directory.Build.props','<Version>0.7.0-alpha.1</Version>','<Version>0.8.0-alpha.1</Version>')
app=Path('src/VectorSpace.App/VectorSpace.App.csproj'); s=app.read_text(); s=s.replace('<ApplicationDisplayVersion>0.7.0</ApplicationDisplayVersion>','<ApplicationDisplayVersion>0.8.0</ApplicationDisplayVersion>').replace('<ApplicationVersion>7</ApplicationVersion>','<ApplicationVersion>8</ApplicationVersion>'); app.write_text(s)
edit('src/VectorSpace.Core/VectorPath.cs','        var w = node.Width; var h = node.Height;', '''        if (node.IsBoolean) throw new InvalidOperationException("Use a renderer-aware export for live Boolean groups.");
        if (node.Commands is { } commands) return ShapePathSvg.Commands(commands);
        if (node.Kind == NodeKind.Ellipse && node.Arc is not null) return ShapePathSvg.Arc(node);
        if (ShapeGeometry.HasCorners(node) && node.Corners is not null) return ShapePathSvg.Corners(node);
        var w = node.Width; var h = node.Height;''')
edit('src/VectorSpace.Core/StyleCloner.cs','Dashes = [.. s.Dashes]','Dashes = [.. s.Dashes], Alignment = s.Alignment, Cap = s.Cap, Join = s.Join, MiterLimit = s.MiterLimit, DashOffset = s.DashOffset')
edit('src/VectorSpace.Core/StyleCloner.cs','a[i].Color != b[i].Color || a[i].Width', 'a[i].Alignment != b[i].Alignment || a[i].Cap != b[i].Cap || a[i].Join != b[i].Join || a[i].MiterLimit != b[i].MiterLimit || a[i].DashOffset != b[i].DashOffset || a[i].Color != b[i].Color || a[i].Width')
edit('src/VectorSpace.Documents/DocumentJson.cs','            AppearanceValidation.Validate(n, ref imageCharacters);','            ShapeValidation.Validate(n);\n            AppearanceValidation.Validate(n, ref imageCharacters);')
edit('src/VectorSpace.Documents/PropertyClipboard.cs','        if (strokes.Count > 64)', '        foreach (var s in strokes) if (s is not null) ShapeValidation.Stroke(s);\n        if (strokes.Count > 64)')
edit('src/VectorSpace.Editing/ComponentService.cs','if (node.IsContainer && node.Kind != NodeKind.Instance)','if (node.IsContainer && !node.IsBoolean && node.Kind != NodeKind.Instance)')
edit('src/VectorSpace.Editing/ComponentService.cs','instance.CornerRadius = copy.CornerRadius;', 'instance.Corners = copy.Corners; instance.CornerRadius = copy.CornerRadius;')
edit('src/VectorSpace.Skia/EditablePathConversion.cs','node.Kind = NodeKind.Path; node.Points = points;', 'node.Kind = NodeKind.Path; node.Commands = null; node.Arc = null; node.Corners = null; node.Points = points;')

renderer='src/VectorSpace.Skia/SceneRenderer.cs'
edit(renderer,'double PathHeight, bool Closed);','double PathHeight, bool Closed, CornerRadii? Corners, EllipseArc? Arc, PathFillRule FillRule);')
edit(renderer,'PointKey[] Points, SKPath Path, LinkedListNode<string> Recency);','PointKey[] Points, SKPath Path, LinkedListNode<string> Recency, PathCommand[]? Commands);')
edit(renderer,'ClearAppearance(); foreach', 'ClearAppearance(); ClearStrokeCache(); ClearBooleanCache(); foreach')
edit(renderer,'var active = activeIds.ToHashSet(StringComparer.Ordinal);','var active = activeIds.ToHashSet(StringComparer.Ordinal);\n        TrimStrokeCache(active); TrimBooleanCache(active);')
edit(renderer,'        var key = new GeometryKey(node.Kind,', '        if (node.IsBoolean) return BooleanGeometry(node);\n        var key = new GeometryKey(node.Kind,')
edit(renderer,'node.PathWidth, node.PathHeight, node.Closed);','node.PathWidth, node.PathHeight, node.Closed, node.Corners, node.Arc, node.FillRule);')
edit(renderer,'cache.Key == key && PointsEqual(cache.Points, node)', 'cache.Key == key && PointsEqual(cache.Points, node) && CommandsEqual(cache.Commands, node.Commands)')
edit(renderer,'        var path = node.Kind == NodeKind.Path && node.PathData is null && node.Points.Count > 0 ? BuildEditableGeometry(node) : SKPath.ParseSvgPathData(VectorPath.Build(node)) ?? new SKPath();', '        var path = NativeShapeGeometry.Build(node);\n        path.FillType = node.FillRule == PathFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;')
edit(renderer,'new(key, points, path, recency);','new(key, points, path, recency, node.Commands?.ToArray());')
s=Path(renderer).read_text(); a=s.index('    private static SKPath BuildEditableGeometry('); b=s.index('    private static bool PointsEqual(',a)
s=s[:a]+'''    private static bool CommandsEqual(PathCommand[]? cached, List<PathCommand>? commands)
    {
        if (cached is null || commands is null) return cached is null && commands is null;
        return cached.AsSpan().SequenceEqual(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(commands));
    }
'''+s[b:]; Path(renderer).write_text(s)
edit(renderer,'(node.ClipContent || node.Children.Count == 0 && node.Kind is not NodeKind.Text and not NodeKind.Path and not NodeKind.Arrow)', '(!node.IsBoolean && (node.ClipContent || node.Children.Count == 0 && node.Kind is not NodeKind.Text and not NodeKind.Path and not NodeKind.Arrow))')
edit(renderer,'node.Strokes.Where(s => s.Visible).Select(s => s.Width / 2).DefaultIfEmpty(0).Max()', 'ShapeGeometry.StrokeOutset(node)',2)
s=Path(renderer).read_text(); a=s.index('            foreach (var stroke in node.Strokes.Where(s => s.Visible && s.Width > 0))'); b=s.index('\n        }\n        if (node.ClipContent',a)
s=s[:a]+'''            for (var i = 0; i < node.Strokes.Count; i++)
            {
                var stroke = node.Strokes[i]; if (!stroke.Visible || stroke.Width <= 0 || stroke.Opacity <= 0) continue;
                if (node.Kind == NodeKind.Text)
                {
                    using var paint = StrokePaint(stroke); DrawText(canvas, node, paint);
                }
                else
                {
                    using var paint = new SKPaint { IsAntialias = true, Color = Color(stroke.Color, stroke.Opacity) };
                    canvas.DrawPath(StrokeGeometry(node, i), paint);
                }
            }'''+s[b:]
s=s.replace('using var clip = new SKPath(); clip.AddRoundRect(new SKRect(0, 0, (float)node.Width, (float)node.Height), (float)node.CornerRadius, (float)node.CornerRadius); canvas.ClipPath(clip, SKClipOperation.Intersect, true);', 'using var clip = new SKPath(); NativeShapeGeometry.AddCornerBox(clip, node); canvas.ClipPath(clip, SKClipOperation.Intersect, true);')
s=s.replace('foreach (var child in node.Children) DrawNode(canvas, child, world, childViewport);', 'if (!node.IsBoolean) foreach (var child in node.Children) DrawNode(canvas, child, world, childViewport);')
a=s.index('    public DesignNode? HitTest('); b=s.index('    public byte[] ExportPng(',a)
s=s[:a]+'''    public DesignNode? HitTest(IEnumerable<DesignNode> roots, Vec2 point, bool deep = false, double tolerance = 4)
    {
        foreach (var node in roots.Reverse())
        {
            if (!node.IsEffectivelyVisible || node.Opacity <= 0 || node.IsEffectivelyLocked || node.Kind == NodeKind.Slice) continue;
            if (!node.WorldMatrix.TryInvert(out var inverse)) continue;
            var local = inverse.Map(point); var inside = node.LocalBounds.Contains(local);
            var scale = Math.Max(Math.Sqrt(inverse.M11 * inverse.M11 + inverse.M12 * inverse.M12), Math.Sqrt(inverse.M21 * inverse.M21 + inverse.M22 * inverse.M22));
            var localTolerance = Math.Max(0, tolerance) * scale;
            if (node.Children.Count == 0 && node.Kind is not NodeKind.Path and not NodeKind.Arrow && !node.LocalBounds.Inflate(localTolerance + ShapeGeometry.StrokeOutset(node)).Contains(local)) continue;
            var insideClip = ShapeGeometry.ContainsCornerBox(node, local);
            if ((!node.IsBoolean || deep) && (!node.ClipContent || insideClip))
            {
                var child = HitTest(node.Children, point, deep, tolerance);
                if (child is not null) return deep || node.Kind is NodeKind.Frame or NodeKind.Section ? child : node;
            }
            if (node.Kind == NodeKind.Text && inside) return node;
            if (node.Kind == NodeKind.Group && !node.IsBoolean && node.Children.Count > 0) continue;
            var path = Geometry(node);
            if (node.Arc?.Open != true && node.Fills.Any(f => f.Visible && f.Opacity > 0) && path.Contains((float)local.X, (float)local.Y)) return node;
            for (var i = 0; i < node.Strokes.Count; i++)
                if (node.Strokes[i] is { Visible: true, Width: > 0, Opacity: > 0 } && StrokeContains(node, i, local, localTolerance)) return node;
        }
        return null;
    }
'''+s[b:]; Path(renderer).write_text(s)
edit('src/VectorSpace.Skia/SceneRenderer.Appearance.cs','    private void DrawFill(SKCanvas canvas, DesignNode node, FillStyle fill, int fillIndex)\n    {','    private void DrawFill(SKCanvas canvas, DesignNode node, FillStyle fill, int fillIndex)\n    {\n        if (node.Arc?.Open == true && node.Kind == NodeKind.Ellipse) return;')
edit('src/VectorSpace.Skia/SceneRenderer.Appearance.cs','node.Strokes.Where(s => s.Visible).Select(s => s.Width / 2).DefaultIfEmpty(0).Max()', 'ShapeGeometry.StrokeOutset(node)')

svg='src/VectorSpace.Documents/SvgFormat.cs'
edit(svg,'Export(IEnumerable<DesignNode> roots, RectD bounds)', 'Export(IEnumerable<DesignNode> roots, RectD bounds, Func<DesignNode, int, string>? strokeOutline = null)')
edit(svg,'ExportNode(node, defs, true)', 'ExportNode(node, defs, true, strokeOutline)')
edit(svg,'ExportNode(DesignNode node, XElement defs, bool world = false)', 'ExportNode(DesignNode node, XElement defs, bool world = false, Func<DesignNode, int, string>? strokeOutline = null)')
edit(svg,'        if (!node.Visible || node.Kind == NodeKind.Slice) return null;', '        if (!node.Visible || node.Kind == NodeKind.Slice) return null;\n        if (node.IsBoolean) throw new InvalidOperationException("Live Boolean SVG export requires SceneSvg.Export from VectorSpace.Skia.");')
edit(svg,'ExportNode(child, defs)', 'ExportNode(child, defs, false, strokeOutline)')
s=Path(svg).read_text(); a=s.index('        foreach (var stroke in node.Strokes.Where(s => s.Visible))'); b=s.index('        var children =',a)
s=s[:a]+'''        for (var i = 0; i < node.Strokes.Count; i++)
        {
            var stroke = node.Strokes[i]; if (!stroke.Visible || stroke.Width <= 0) continue;
            var color = CssColor(stroke.Color);
            if (stroke.Alignment != StrokeAlignment.Center && node.Kind != NodeKind.Text)
            {
                if (strokeOutline is null) throw new InvalidOperationException("Aligned stroke SVG export requires SceneSvg.Export from VectorSpace.Skia.");
                group.Add(new XElement(Ns + "path", new XAttribute("d", strokeOutline(node, i)), new XAttribute("fill", color.Color), new XAttribute("fill-opacity", F(stroke.Opacity * color.Alpha))));
                continue;
            }
            var shape = Shape(node); shape.SetAttributeValue("fill", "none"); shape.SetAttributeValue("stroke", color.Color);
            shape.SetAttributeValue("stroke-width", F(stroke.Width)); shape.SetAttributeValue("stroke-opacity", F(stroke.Opacity * color.Alpha));
            shape.SetAttributeValue("stroke-linejoin", stroke.Join.ToString().ToLowerInvariant()); shape.SetAttributeValue("stroke-linecap", stroke.Cap.ToString().ToLowerInvariant());
            shape.SetAttributeValue("stroke-miterlimit", F(stroke.MiterLimit)); shape.SetAttributeValue("stroke-dashoffset", F(stroke.DashOffset));
            if (stroke.Dashes.Count > 0) shape.SetAttributeValue("stroke-dasharray", string.Join(" ", stroke.Dashes.Select(F))); group.Add(shape);
        }
'''+s[b:]
s=s.replace('new XElement(Ns + "rect", new XAttribute("width", F(node.Width)), new XAttribute("height", F(node.Height)), new XAttribute("rx", F(node.CornerRadius)))', 'new XElement(Ns + "path", new XAttribute("d", ShapePathSvg.Corners(node)))')
s=s.replace('var shape = new XElement(Ns + "path", new XAttribute("d", VectorPath.Build(node)));','var shape = new XElement(Ns + "path", new XAttribute("d", VectorPath.Build(node)), new XAttribute("fill-rule", node.FillRule == PathFillRule.EvenOdd ? "evenodd" : "nonzero"));')
s=s.replace('var fill = node.Fills[i]; if (!fill.Visible) continue;', 'var fill = node.Fills[i]; if (!fill.Visible || node.Kind == NodeKind.Ellipse && node.Arc?.Open == true) continue;')
s=s.replace('node.Opacity = Numbers.Parse(Style(element, "opacity")', '''if (node.Strokes.LastOrDefault() is { } importedStroke)
            {
                importedStroke.Cap = Attribute("stroke-linecap") switch { "round" => StrokeCap.Round, "square" => StrokeCap.Square, _ => StrokeCap.Butt };
                importedStroke.Join = Attribute("stroke-linejoin") switch { "round" => StrokeJoin.Round, "bevel" => StrokeJoin.Bevel, _ => StrokeJoin.Miter };
                importedStroke.MiterLimit = Math.Clamp(Numbers.Parse(Attribute("stroke-miterlimit") ?? "4", 4), 1, 128);
                importedStroke.DashOffset = Numbers.Parse(Attribute("stroke-dashoffset") ?? "0", 0);
                importedStroke.Dashes = Values(Attribute("stroke-dasharray")).ToList();
            }
            node.FillRule = Attribute("fill-rule") == "evenodd" ? PathFillRule.EvenOdd : PathFillRule.NonZero;
            node.Opacity = Numbers.Parse(Style(element, "opacity")''')
Path(svg).write_text(s)
for p in Path('src/VectorSpace.Workbench').glob('*.cs'):
    s=p.read_text().replace('SvgFormat.Export(', 'SceneSvg.Export(Surface.Renderer, ')
    s=s.replace('BooleanOperations.Apply(Session, Surface.Renderer, op)', 'LiveBooleanOperations.Create(Session, Surface.Renderer, (BooleanKind)op)')
    p.write_text(s)
# Existing output assertions target the current schema; older input fixtures remain migration tests.
for p in Path('tests').rglob('*'):
    if p.suffix not in ('.cs','.mjs','.py'): continue
    s=p.read_text(); n=s.replace('.formatVersion).toBe(5)', '.formatVersion).toBe(6)').replace('.FormatVersion == 5', '.FormatVersion == 6')
    if n!=s: p.write_text(n)
print('Geometry and stroke integration applied.')
