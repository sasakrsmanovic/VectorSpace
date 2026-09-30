from pathlib import Path

def edit(path, old, new, count=1):
    p=Path(path);s=p.read_text();assert s.count(old)==count,(path,old[:120],s.count(old),count);p.write_text(s.replace(old,new))

edit('src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs','surface.WriteShapeDiagnostics(json);','workbench.Surface.WriteShapeDiagnostics(json);')
edit('src/VectorSpace.Editing/ComponentService.Properties.cs','if (value.CornerRadius is { } radius) node.CornerRadius = radius;', 'if (value.CornerRadius is { } radius) { node.CornerRadius = radius; node.Corners = null; }')
edit('tests/VectorSpace.Tests/Program.cs','ShapeTests.Register(Test);','ShapeTests.Register(Test);\nShapeIntegrationTests.Register(Test);\nStrokeOutlineExtentTests.Register(Test);')
outline='src/VectorSpace.Skia/LiveBooleanOperations.cs'
edit(outline,'var n = nodes[i]; n.Kind = NodeKind.Group; n.Boolean = null;', '''var n = nodes[i]; var localMatrix = n.LocalMatrix;
                var degenerate = n.Width < 1 || n.Height < 1;
                n.Kind = NodeKind.Group;
                if (degenerate)
                {
                    // Groups/paths have nonzero layout frames. Preserve the authored stroke
                    // coordinates and old transform instead of scaling a zero extent by 1e9.
                    n.Width = Math.Max(1, n.Width); n.Height = Math.Max(1, n.Height);
                    NodeGeometry.SetLocalMatrix(n, localMatrix);
                }
                n.Boolean = null;''')
edit(outline,'Name = name, Width = source.Width, Height = source.Height,', 'Name = name, Width = Math.Max(1, source.Width), Height = Math.Max(1, source.Height),')
# Keep reusable public control types in focused files.
p=Path('src/VectorSpace.Controls/ShapeOptionsControls.cs');s=p.read_text();header=s[:s.index('public sealed record StrokeOptions')]
markers=['public sealed record StrokeOptions','public sealed record CornerOptions','public sealed record ArcOptions']
starts=[s.index(m) for m in markers]+[len(s)]
for i,name in enumerate(['StrokeOptionsControl','CornerOptionsControl','ArcOptionsControl']):
    Path('src/VectorSpace.Controls/'+name+'.cs').write_text(header+s[starts[i]:starts[i+1]])
p.unlink()
p=Path(outline);s=p.read_text();marker='public static class SceneSvg';start=s.index(marker);Path('src/VectorSpace.Skia/SceneSvg.cs').write_text(s[:s.index('public static class LiveBooleanOperations')]+s[start:]);p.write_text(s[:start])

server='tests/server/test_collaboration.py'
edit(server,"self.assertEqual(current['cells']['$root\\x1fformatVersion'], '5')", "self.assertEqual(current['cells']['$root\\x1fformatVersion'], '6')")
edit(server,"if __name__ == '__main__':",'''    def test_schema_five_strokes_upgrade_and_remain_editable_after_restart(self):
        path, token = self.room()
        self.stop()
        metadata = Path(self.directory.name) / (path.rsplit('/', 1)[1] + '.room.json')
        data = json.loads(metadata.read_text())
        data['initial']['cells']['$root\\x1fformatVersion'] = '5'
        data['initial']['cells']['node:a\\x1fstrokes'] = '[{"width":4}]'
        metadata.write_text(json.dumps(data))
        self.start()
        current = self.snapshot(path, token)
        self.assertEqual(current['revision'], 1)
        self.assertEqual(current['cells']['$root\\x1fformatVersion'], '6')
        stroke = json.loads(current['cells']['node:a\\x1fstrokes'])[0]
        self.assertEqual(stroke['cap'], 'Round')
        self.assertEqual(stroke['alignment'], 'Center')
        stroke['alignment'] = 'Outside'
        batch = self.batch(current, 'strokes', json.dumps([stroke]))
        self.assertTrue(self.request(path + '/edits', batch, token)[1]['receipt']['accepted'])
        self.stop(); self.start()
        restored = self.snapshot(path, token)
        self.assertEqual(restored['revision'], 2)
        self.assertEqual(json.loads(restored['cells']['node:a\\x1fstrokes'])[0]['alignment'], 'Outside')
        historic = self.request(path + '/versions/0', token=token)[1]
        self.assertEqual(historic['pages'][0]['nodes'][0]['strokes'][0]['alignment'], 'Center')

if __name__ == '__main__':''')
workflow=Path('.github/workflows/build.yml');s=workflow.read_text();marker='          cat artifacts/benchmarks/editing-workflows.json';assert s.count(marker)==1;s=s.replace(marker,marker+'\n          dotnet run --project tests/VectorSpace.Tests -c Release --no-build -- --benchmark-shapes > artifacts/benchmarks/shapes.json\n          cat artifacts/benchmarks/shapes.json');workflow.write_text(s)

readme=Path('README.md');s=readme.read_text();assert 'Source version: 0.7.0-alpha.1.' in s;s=s.replace('Source version: 0.7.0-alpha.1.','Source version: 0.8.0-alpha.1.')
s=s.replace('## New editing workflows', '''## Shape editing and native geometry

Independent corners, ellipse sectors/rings/open arcs, aligned strokes, caps/joins/miter/dash phase and live Boolean groups are editable through custom contextual controls. Corner/arc grips operate directly on the canvas with modifier, undo and cancellation support. Flattening and stroke outlines preserve exact native conic/compound contours rather than round-tripping the document through SVG text.

The stroke/Boolean caches share geometry between drawing and picking, retain unchanged resources and dispose evicted paths. Open **Ctrl+K → Shape playground** for the original editable study. [Shape workflows, APIs and boundaries](docs/SHAPES.md) describe the supported behavior; this is not full vector-network editing or pixel-certified Figma parity. Native schema **6** requires updating collaborative clients and the self-hosted server together.

## New editing workflows''')
readme.write_text(s)
changelog=Path('CHANGELOG.md');s=changelog.read_text();assert s.startswith('# Changelog\n');s=s.replace('# Changelog\n','# Changelog\n\n## 0.8.0-alpha.1\n\n- Add independent corner radii, signed ellipse arcs/rings/open contours, and custom contextual controls with direct canvas grips.\n- Add centered/inside/outside stroke regions, cap/join/miter/dash-phase editing, shared drawing/picking geometry and bounded native caches.\n- Retain live Boolean operands with editable operations, release, exact native flattening and per-stroke outline layers.\n- Build primitives directly in Skia and preserve rational conics in native contour commands; add bounded renderer-aware SVG conversion.\n- Preserve zero-extent line stroke coordinates during outlining, independent prototype clips and uniform instance-radius overrides.\n- Upgrade to native schema 6 and style clipboard packet 2, retain older native/clipboard inputs, and verify persisted schema-5 rooms upgrade once.\n- Add 80 native regressions, eight static browser workflows and one multi-window shape-editing workflow; retain scoped stroke-construction benchmarks.\n- Preserve the existing published NuGet package documentation and release configuration. Full Figma feature/pixel parity remains unfinished.\n',1);changelog.write_text(s)
performance=Path('docs/PERFORMANCE.md');s=performance.read_text();s+='''

## Retained stroke and Boolean geometry (0.8)

`--benchmark-shapes` compares rebuilding stroke fill regions against descriptor-keyed reuse for 160 native rectangle/ring-sector paths, mixed alignment/caps/joins and dash patterns. Five warmed interleaved batches each query ten passes. Exact captured native commands are compared before measurement, and the retained phase must build zero additional stroke regions.

Paint color/opacity and a whole-layer transform do not invalidate local stroke geometry. Source geometry, width, alignment, cap, join, miter, dashes or phase do. LRU entries own their region and optional hit-tolerance path; capacity reductions and deleted-layer trimming dispose excess resources immediately. Live Boolean keys track operation, ordered operand identities, transforms and geometry references instead of cloning or serializing descendants every frame.

The report includes per-query managed bytes and elapsed batch time. It excludes painting, native allocations, UI, layout, history, network and cold setup. Source geometry is cached in both implementations; this is a resource-construction benchmark, not an application-wide frame-rate or Figma comparison. See [shape semantics](SHAPES.md). Native conic round-trip pixel equality and high-resolution SVG approximation checks are separate correctness regressions.
''';performance.write_text(s)
print('Final browser, native, migration and documentation edits applied.')
