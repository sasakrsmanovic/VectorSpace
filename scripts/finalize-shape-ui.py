from pathlib import Path

def edit(path, old, new):
    p=Path(path);s=p.read_text();assert s.count(old)==1,(path,old,s.count(old));p.write_text(s.replace(old,new))
edit('tests/VectorSpace.Tests/EditingWorkflowTests.cs','PropertyClipboard.Prefix + "{\\"version\\":2,\\"properties\\":{}}"','PropertyClipboard.Prefix + "{\\"version\\":99,\\"properties\\":{}}"')
edit('tests/VectorSpace.Tests/ShapeTests.Gestures.cs','using var before = new SKPathMeasure(original, false); using var after = new SKPathMeasure(parsed, false);','using var before = new SKPathMeasure(original, false, 1000); using var after = new SKPathMeasure(parsed, false, 1000);')
edit('tests/VectorSpace.Tests/ShapeTests.Gestures.cs',"Near(before.Length, after.Length, .03); Check(commands.SequenceEqual(saved) && svg.Contains('C'));",'''Near(before.Length, after.Length, .03); Check(commands.SequenceEqual(saved) && svg.Contains('C'));
            // Compare equal high-resolution measurements; the default coarse estimator
            // samples a long native conic differently from many exported cubic segments.
            for (var i = 0; i <= 1000; i++)
            {
                var a = before.GetPosition(before.Length * i / 1000); var b = after.GetPosition(after.Length * i / 1000);
                Check(new Vec2(a.X - b.X, a.Y - b.Y).Length < .01, "Exported conic deviated at arc-length sample " + i);
            }''')
edit('tests/VectorSpace.Tests/Program.cs','if (args.Contains("--benchmark-appearance"))','if (args.Contains("--benchmark-shapes")) return ShapeBenchmarks.Run();\nif (args.Contains("--benchmark-appearance"))')
edit('tests/VectorSpace.Tests/ShapeBenchmarks.cs','using var rebuilt = SceneRenderer.BuildStrokeRegion(renderer.Geometry(node), node.Strokes[0]);','using var referenceRegion = SceneRenderer.BuildStrokeRegion(renderer.Geometry(node), node.Strokes[0]);')
edit('tests/VectorSpace.Tests/ShapeBenchmarks.cs','NativeShapeGeometry.Capture(rebuilt)','NativeShapeGeometry.Capture(referenceRegion)')
for control in ['StrokeOptionsControl','CornerOptionsControl','ArcOptionsControl']:
    edit('src/VectorSpace.Controls/ShapeOptionsControls.cs', 'class '+control+' : StackPanel', 'class '+control+' : ShapeOptionsPanel')
