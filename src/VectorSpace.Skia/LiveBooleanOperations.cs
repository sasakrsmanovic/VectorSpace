using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

namespace VectorSpace.Skia;

public static class LiveBooleanOperations
{
    public static DesignNode Create(EditorSession editor, SceneRenderer renderer, BooleanKind operation)
    {
        var selected = editor.SelectionRoots.ToHashSet();
        if (selected.Count is < 2 or > 128) throw new InvalidOperationException("Select 2–128 sibling vector shapes.");
        foreach (var n in selected) Require(n);
        var parent = selected.First().Parent;
        if (selected.Any(n => n.Parent != parent)) throw new InvalidOperationException("Boolean operands must share one parent.");
        var siblings = parent?.Children ?? editor.Page.Nodes;
        var ordered = siblings.Where(selected.Contains).ToArray();
        var bounds = ordered.Select(n => n.LocalMatrix.Map(n.LocalBounds)).Aggregate(RectD.Union);
        var source = ordered[0];
        var group = new DesignNode
        {
            Kind = NodeKind.Group, Boolean = operation, Name = operation + " group", X = bounds.X, Y = bounds.Y,
            Width = Math.Max(1, bounds.Width), Height = Math.Max(1, bounds.Height), Fills = StyleCloner.Fills(source.Fills),
            Strokes = StyleCloner.Strokes(source.Strokes), Shadows = StyleCloner.Effects(source.Shadows), Opacity = source.Opacity, Blend = source.Blend
        };
        // Prove the operation succeeds without changing the editor before entering a transaction.
        var preview = DocumentJson.CloneNode(group);
        foreach (var n in ordered) { var clone = DocumentJson.CloneNode(n); clone.X -= bounds.X; clone.Y -= bounds.Y; preview.Add(clone); }
        using (var check = new SKPath(renderer.Geometry(preview))) { }
        editor.Edit(operation + " shapes (live)", () =>
        {
            var index = ordered.Min(siblings.IndexOf);
            foreach (var n in ordered) { siblings.Remove(n); n.X -= bounds.X; n.Y -= bounds.Y; group.Add(n); }
            group.Parent = parent; siblings.Insert(index, group); editor.Select(group);
        });
        return group;
    }
    public static void SetOperation(EditorSession editor, BooleanKind operation)
    {
        var nodes = editor.SelectionRoots.Where(n => n.IsBoolean).ToArray();
        if (nodes.Length == 0) throw new InvalidOperationException("Select a live Boolean group.");
        foreach (var n in nodes) Require(n);
        editor.Edit("Change Boolean operation", () => { foreach (var n in nodes) n.Boolean = operation; });
    }
    public static void Flatten(EditorSession editor, SceneRenderer renderer)
    {
        var nodes = editor.SelectionRoots.Where(n => n.IsBoolean).ToArray();
        if (nodes.Length == 0) throw new InvalidOperationException("Select a live Boolean group to flatten.");
        foreach (var n in nodes) Require(n);
        var paths = nodes.Select(n => Capture(renderer.Geometry(n))).ToArray();
        editor.Edit("Flatten Boolean result", () =>
        {
            for (var i = 0; i < nodes.Length; i++) SetPath(nodes[i], paths[i].Commands, paths[i].Rule);
        });
    }
    public static void Release(EditorSession editor)
    {
        var roots = editor.SelectionRoots.ToArray();
        if (roots.Length == 0 || roots.Any(n => !n.IsBoolean)) throw new InvalidOperationException("Select only live Boolean groups.");
        foreach (var n in roots) Require(n);
        // This restores the retained operand paints, transforms and identities, not an SVG reconstruction.
        editor.UngroupSelection();
    }
    public static void Outline(EditorSession editor, SceneRenderer renderer)
    {
        var nodes = editor.SelectionRoots.ToArray();
        if (nodes.Length == 0) throw new InvalidOperationException("Select stroked vector shapes.");
        foreach (var n in nodes) Require(n, allowLine: true);
        if (nodes.Any(n => !n.Strokes.Any(s => s.Visible && s.Width > 0))) throw new InvalidOperationException("Each selected shape must have a visible nonzero stroke.");
        var children = new List<DesignNode>[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i]; var parts = children[i] = [];
            if (n.Fills.Any(f => f.Visible) && n.Arc?.Open != true && n.Kind is not NodeKind.Line and not NodeKind.Arrow)
            {
                var data = Capture(renderer.Geometry(n)); var fill = Part(n, "Fill", data);
                fill.Fills = StyleCloner.Fills(n.Fills); CopyBinding(n, VariableTarget.Fill, fill, VariableTarget.Fill); parts.Add(fill);
            }
            for (var j = 0; j < n.Strokes.Count; j++)
            {
                var s = n.Strokes[j]; if (!s.Visible || s.Width <= 0) continue;
                var data = Capture(renderer.StrokeGeometry(n, j)); var outlined = Part(n, "Stroke " + (j + 1), data);
                outlined.Fills = [new() { Color = s.Color, Opacity = s.Opacity }];
                if (j == 0) CopyBinding(n, VariableTarget.Stroke, outlined, VariableTarget.Fill);
                parts.Add(outlined);
            }
        }
        editor.Edit("Outline stroke", () =>
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i]; var localMatrix = n.LocalMatrix;
                var degenerate = n.Width < 1 || n.Height < 1;
                n.Kind = NodeKind.Group;
                if (degenerate)
                {
                    // Groups/paths have nonzero layout frames. Preserve the authored stroke
                    // coordinates and old transform instead of scaling a zero extent by 1e9.
                    n.Width = Math.Max(1, n.Width); n.Height = Math.Max(1, n.Height);
                    NodeGeometry.SetLocalMatrix(n, localMatrix);
                }
                n.Boolean = null; n.Arc = null; n.Corners = null;
                n.Commands = null; n.Points.Clear(); n.PathData = null; n.Fills.Clear(); n.Strokes.Clear(); n.Children.Clear(); n.ClipContent = false;
                n.VariableBindings.Remove(VariableTarget.Fill); n.VariableBindings.Remove(VariableTarget.Stroke);
                foreach (var part in children[i]) n.Add(part);
            }
        });
    }
    private static DesignNode Part(DesignNode source, string name, (List<PathCommand> Commands, PathFillRule Rule) data)
    {
        var result = new DesignNode { Name = name, Width = Math.Max(1, source.Width), Height = Math.Max(1, source.Height), Fills = [], HorizontalConstraint = AxisConstraint.Scale, VerticalConstraint = AxisConstraint.Scale };
        SetPath(result, data.Commands, data.Rule); return result;
    }
    internal static (List<PathCommand> Commands, PathFillRule Rule) Capture(SKPath path) =>
        (NativeShapeGeometry.Capture(path), path.FillType == SKPathFillType.EvenOdd ? PathFillRule.EvenOdd : PathFillRule.NonZero);
    internal static void SetPath(DesignNode node, List<PathCommand> commands, PathFillRule rule)
    {
        node.Kind = NodeKind.Path; node.Boolean = null; node.Arc = null; node.Corners = null; node.PathData = null; node.Points.Clear(); node.Children.Clear();
        node.Commands = commands; node.FillRule = rule; node.PathWidth = Math.Max(1e-9, node.Width); node.PathHeight = Math.Max(1e-9, node.Height);
        node.Closed = commands.Count > 0 && commands[^1].Verb == PathVerb.Close; node.ClipContent = false;
    }
    private static void CopyBinding(DesignNode from, VariableTarget source, DesignNode to, VariableTarget target)
    {
        if (from.VariableBindings.TryGetValue(source, out var b)) to.VariableBindings[target] = new() { VariableId = b.VariableId, Disabled = b.Disabled, IsOverride = b.IsOverride, Fallback = b.Fallback };
    }
    private static void Require(DesignNode node, bool allowLine = false)
    {
        if (node.IsEffectivelyLocked || !(ShapeGeometry.IsOperand(node) || allowLine && node.Kind is NodeKind.Line or NodeKind.Arrow) || node.Children.Count > 0 && !node.IsBoolean)
            throw new InvalidOperationException("Choose unlocked vector shapes or live Boolean groups, without ordinary child layers.");
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (parent.Kind == NodeKind.Instance) throw new InvalidOperationException("Detach the instance before changing its shape structure.");
    }
}

