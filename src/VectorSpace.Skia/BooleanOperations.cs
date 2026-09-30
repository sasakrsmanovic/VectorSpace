using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

namespace VectorSpace.Skia;

public enum BooleanOperation { Union, Subtract, Intersect, Exclude }

/// <summary>Destructive Boolean compatibility API. New authoring workflows use
/// LiveBooleanOperations; this API still accepts native schema-six contour paths.</summary>
public static class BooleanOperations
{
    public static void Apply(EditorSession editor, SceneRenderer renderer, BooleanOperation operation)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(renderer);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        var nodes = editor.SelectionRoots.Where(n => n.Kind != NodeKind.Text && !n.IsContainer && !n.IsEffectivelyLocked).ToArray();
        if (nodes.Length < 2) throw new InvalidOperationException("Select at least two vector shapes.");
        var parent = nodes[0].Parent;
        if (nodes.Any(n => n.Parent != parent)) throw new InvalidOperationException("Boolean shapes must have the same parent.");
        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.Kind == NodeKind.Instance) throw new InvalidOperationException("Detach the instance before changing its shape structure.");
        var op = operation switch { BooleanOperation.Subtract => SKPathOp.Difference, BooleanOperation.Intersect => SKPathOp.Intersect, BooleanOperation.Exclude => SKPathOp.Xor, _ => SKPathOp.Union };
        using var result = new SKPath(renderer.Geometry(nodes[0])); result.Transform(SceneRenderer.Matrix(nodes[0].LocalMatrix));
        for (var i = 1; i < nodes.Length; i++)
        {
            using var path = new SKPath(renderer.Geometry(nodes[i])); path.Transform(SceneRenderer.Matrix(nodes[i].LocalMatrix));
            using var combined = result.Op(path, op) ?? throw new InvalidOperationException("Skia could not compute this Boolean operation.");
            result.Reset(); result.AddPath(combined); result.FillType = combined.FillType;
        }
        if (result.IsEmpty)
        {
            editor.Edit(operation + " shapes", () =>
            {
                foreach (var old in nodes) editor.RemoveNode(old);
                editor.Select((DesignNode?)null);
            });
            return;
        }
        var bounds = result.TightBounds; result.Transform(SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top));
        var node = DocumentJson.CloneNode(nodes[0], true);
        node.Name = operation + " result"; node.X = bounds.Left; node.Y = bounds.Top;
        node.Width = Math.Max(1, bounds.Width); node.Height = Math.Max(1, bounds.Height);
        node.Rotation = 0; node.FlipX = node.FlipY = false;
        var native = LiveBooleanOperations.Capture(result);
        // Clear all source representations/modifiers atomically. Keeping Commands on
        // a clone while assigning SVG PathData violates the native schema contract.
        LiveBooleanOperations.SetPath(node, native.Commands, native.Rule);
        editor.Edit(operation + " shapes", () =>
        {
            foreach (var old in nodes) editor.RemoveNode(old);
            editor.AddNode(node, parent); editor.Select(node);
        });
    }
}
