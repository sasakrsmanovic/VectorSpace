using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

namespace VectorSpace.Skia;

public enum BooleanOperation { Union, Subtract, Intersect, Exclude }

public static class BooleanOperations
{
    public static void Apply(EditorSession editor, SceneRenderer renderer, BooleanOperation operation)
    {
        var nodes = editor.SelectionRoots.Where(n => n.Kind != NodeKind.Text && !n.IsContainer && !n.IsEffectivelyLocked).ToArray();
        if (nodes.Length < 2) throw new InvalidOperationException("Select at least two vector shapes.");
        var parent = nodes[0].Parent;
        if (nodes.Any(n => n.Parent != parent)) throw new InvalidOperationException("Boolean shapes must have the same parent.");
        var op = operation switch { BooleanOperation.Subtract => SKPathOp.Difference, BooleanOperation.Intersect => SKPathOp.Intersect, BooleanOperation.Exclude => SKPathOp.Xor, _ => SKPathOp.Union };
        using var result = new SKPath(renderer.Geometry(nodes[0])); result.Transform(SceneRenderer.Matrix(nodes[0].LocalMatrix));
        for (var i = 1; i < nodes.Length; i++)
        {
            using var path = new SKPath(renderer.Geometry(nodes[i])); path.Transform(SceneRenderer.Matrix(nodes[i].LocalMatrix));
            using var combined = result.Op(path, op) ?? throw new InvalidOperationException("Skia could not compute this Boolean operation."); result.Reset(); result.AddPath(combined);
        }
        if (result.IsEmpty) { editor.DeleteSelection(); return; }
        var bounds = result.TightBounds; result.Transform(SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top));
        var node = DocumentJson.CloneNode(nodes[0], true);
        node.Kind = NodeKind.Path; node.Name = operation + " result"; node.X = bounds.Left; node.Y = bounds.Top; node.Width = Math.Max(1, bounds.Width); node.Height = Math.Max(1, bounds.Height); node.PathWidth = node.Width; node.PathHeight = node.Height;
        node.Rotation = 0; node.FlipX = node.FlipY = false; node.Points.Clear(); node.PathData = result.ToSvgPathData(); node.Children.Clear();
        editor.Edit(operation + " shapes", () => { foreach (var old in nodes) editor.RemoveNode(old); editor.AddNode(node, parent); editor.Select(node); });
    }
}
