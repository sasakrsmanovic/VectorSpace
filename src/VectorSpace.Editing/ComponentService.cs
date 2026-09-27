using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Editing;

/// <summary>Reusable local components with linked instances and explicit text/fill overrides.</summary>
public static class ComponentService
{
    public static void MakeComponent(EditorSession editor)
    {
        if (editor.SelectionRoots.Count != 1 || editor.Primary is null) return;
        editor.UpdateSelection("Create component", n => { n.Kind = NodeKind.Component; n.ComponentId = null; });
    }
    public static DesignNode InsertInstance(EditorSession editor, DesignNode component, Vec2 position)
    {
        if (component.Kind != NodeKind.Component) throw new ArgumentException("The source must be a component.", nameof(component));
        var clone = DocumentJson.CloneNode(component);
        SetSources(clone); DocumentJson.RegenerateIds([clone]); clone.Kind = NodeKind.Instance; clone.ComponentId = component.Id; clone.X = position.X; clone.Y = position.Y;
        editor.Edit("Insert component instance", () => { editor.AddNode(clone); editor.Select(clone); }); return clone;
    }
    public static void Detach(EditorSession editor) => editor.UpdateSelection("Detach instance", n =>
    {
        if (n.Kind != NodeKind.Instance) return; n.Kind = NodeKind.Frame; n.ComponentId = null; n.Overrides.Clear(); foreach (var c in n.DescendantsAndSelf()) c.SourceId = null;
    });
    public static void ResetOverrides(EditorSession editor)
    {
        editor.Edit("Reset instance overrides", () => { foreach (var n in editor.Selection.Where(n => n.Kind == NodeKind.Instance)) n.Overrides.Clear(); Synchronize(editor.Document); });
    }
    public static void SetOverride(DesignNode node, string? text = null, string? fill = null)
    {
        var instance = node; while (instance is not null && instance.Kind != NodeKind.Instance) instance = instance.Parent;
        if (instance is null || node.SourceId is null) return;
        if (!instance.Overrides.TryGetValue(node.SourceId, out var value)) instance.Overrides[node.SourceId] = value = new();
        if (text is not null) value.Text = text; if (fill is not null) value.Fill = fill;
    }
    public static void Synchronize(DesignDocument document)
    {
        var components = document.AllNodes().Where(n => n.Kind == NodeKind.Component).ToDictionary(n => n.Id);
        foreach (var instance in document.AllNodes().Where(n => n.Kind == NodeKind.Instance).ToArray())
        {
            if (instance.ComponentId is null || !components.TryGetValue(instance.ComponentId, out var definition)) continue;
            var existing = instance.DescendantsAndSelf().Where(n => n.SourceId is not null).GroupBy(n => n.SourceId!).ToDictionary(g => g.Key, g => g.First().Id);
            var copy = DocumentJson.CloneNode(definition); SetSources(copy);
            foreach (var n in copy.DescendantsAndSelf())
            {
                var source = n.SourceId!; n.Id = existing.GetValueOrDefault(source) ?? Guid.NewGuid().ToString("N");
                if (instance.Overrides.TryGetValue(source, out var o)) { if (o.Text is not null) n.Text = o.Text; if (o.Fill is not null) n.Fill = o.Fill; if (o.Visible.HasValue) n.Visible = o.Visible.Value; }
            }
            instance.Children = copy.Children; foreach (var child in instance.Children) child.Parent = instance;
            instance.Fills = copy.Fills; instance.Strokes = copy.Strokes; instance.Shadows = copy.Shadows; instance.CornerRadius = copy.CornerRadius;
            instance.Layout = copy.Layout;
        }
    }
    private static void SetSources(DesignNode node) { foreach (var n in node.DescendantsAndSelf()) n.SourceId = n.Id; }
}
