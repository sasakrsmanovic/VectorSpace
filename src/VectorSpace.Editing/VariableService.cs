using VectorSpace.Core;

namespace VectorSpace.Editing;

/// <summary>Undoable design-token authoring. Public methods resolve identifiers at execution time, so undo never leaves stale references.</summary>
public static class VariableService
{
    public static VariableCollection CreateCollection(EditorSession editor, string name)
    {
        var mode = new VariableMode();
        var collection = new VariableCollection { Name = RequireName(name), DefaultModeId = mode.Id, Modes = [mode] };
        editor.Edit("Create variable collection", () => editor.Document.VariableCollections.Add(collection)); return collection;
    }
    public static VariableMode AddMode(EditorSession editor, string collectionId, string name)
    {
        var mode = new VariableMode { Name = RequireName(name) };
        editor.Edit("Add variable mode", () =>
        {
            var collection = Collection(editor, collectionId);
            if (collection.Modes.Count >= 64) throw new InvalidOperationException("A collection supports up to 64 modes.");
            collection.Modes.Add(mode);
            foreach (var variable in editor.Document.Variables.Where(v => v.CollectionId == collectionId)) variable.Values[mode.Id] = variable.Values[collection.DefaultModeId];
        }); return mode;
    }
    public static void RemoveMode(EditorSession editor, string collectionId, string modeId) => editor.Edit("Delete variable mode", () =>
    {
        var c = Collection(editor, collectionId);
        if (c.Modes.Count == 1) throw new InvalidOperationException("Keep at least one mode.");
        if (c.Modes.RemoveAll(m => m.Id == modeId) != 1) throw new InvalidOperationException("Unknown mode.");
        if (c.DefaultModeId == modeId) c.DefaultModeId = c.Modes[0].Id;
        foreach (var variable in editor.Document.Variables.Where(v => v.CollectionId == collectionId)) variable.Values.Remove(modeId);
        if (editor.Document.VariableModes.GetValueOrDefault(collectionId) == modeId) editor.Document.VariableModes.Remove(collectionId);
        foreach (var node in editor.Document.AllNodes()) if (node.VariableModes.GetValueOrDefault(collectionId) == modeId) node.VariableModes.Remove(collectionId);
    });
    public static DesignVariable Create(EditorSession editor, string collectionId, string name, VariableValue value)
    {
        var variable = new DesignVariable { CollectionId = collectionId, Name = RequireName(name), Type = value.Type };
        editor.Edit("Create variable", () =>
        {
            foreach (var mode in Collection(editor, collectionId).Modes) variable.Values.Add(mode.Id, value);
            editor.Document.Variables.Add(variable);
        }); return variable;
    }
    public static void SetValue(EditorSession editor, string id, string modeId, VariableValue value) => editor.Edit("Edit variable value", () =>
    {
        var variable = Variable(editor, id);
        if (variable.Type != value.Type || !variable.Values.ContainsKey(modeId)) throw new InvalidOperationException("Incompatible value or unknown mode.");
        variable.Values[modeId] = value;
        VariableResolver.Validate(editor.Document);
    });
    public static void Bind(EditorSession editor, string nodeId, VariableTarget target, string variableId) => editor.Edit("Bind " + target + " variable", () =>
    {
        var node = editor.Document.Find(nodeId) ?? throw new InvalidOperationException("Layer no longer exists.");
        if (node.IsEffectivelyLocked) throw new InvalidOperationException("The layer is locked.");
        var variable = Variable(editor, variableId);
        if (VariableResolver.TypeOf(target) != variable.Type) throw new InvalidOperationException("The variable type is incompatible with this property.");
        var fallback = node.VariableBindings.TryGetValue(target, out var prior) ? prior.Fallback : VariableResolver.Read(node, target);
        node.VariableBindings[target] = new() { VariableId = variableId, Fallback = fallback, IsOverride = InInstance(node) };
        if (target == VariableTarget.Width) { node.FillWidth = false; node.Layout.HugWidth = false; }
        if (target == VariableTarget.Height) { node.FillHeight = false; node.Layout.HugHeight = false; }
    });
    public static void Unbind(EditorSession editor, string nodeId, VariableTarget target, bool restoreFallback = false) => editor.Edit("Remove variable binding", () =>
    {
        var node = editor.Document.Find(nodeId) ?? throw new InvalidOperationException("Layer no longer exists.");
        if (node.IsEffectivelyLocked) throw new InvalidOperationException("The layer is locked.");
        if (node.VariableBindings.Remove(target, out var binding))
        {
            var value = restoreFallback ? binding.Fallback : VariableResolver.Read(node, target);
            if (InInstance(node)) node.VariableBindings[target] = new() { IsOverride = true, Disabled = true, Fallback = value };
            VariableResolver.Write(node, target, value);
        }
    });
    public static void SetMode(EditorSession editor, string collectionId, string? modeId, string? nodeId = null) => editor.Edit("Change variable mode", () =>
    {
        var collection = Collection(editor, collectionId);
        if (modeId is not null && !collection.Modes.Any(m => m.Id == modeId)) throw new InvalidOperationException("Unknown mode.");
        var node = nodeId is null ? null : editor.Document.Find(nodeId) ?? throw new InvalidOperationException("Layer no longer exists.");
        if (node?.IsEffectivelyLocked == true) throw new InvalidOperationException("The layer is locked.");
        var modes = node?.VariableModes ?? editor.Document.VariableModes;
        if (modeId is null) modes.Remove(collectionId); else modes[collectionId] = modeId;
    });
    public static void Delete(EditorSession editor, string id) => editor.Edit("Delete variable", () =>
    {
        // An alias is a dependency, not a copied value. Refuse destructive deletion until aliases are reassigned.
        if (editor.Document.Variables.Any(v => v.Id != id && v.Values.Values.Any(value => value.AliasId == id))) throw new InvalidOperationException("Other variables alias this variable. Reassign those aliases first.");
        foreach (var n in editor.Document.AllNodes())
            foreach (var target in n.VariableBindings.Where(x => x.Value.VariableId == id).Select(x => x.Key).ToArray()) n.VariableBindings.Remove(target);
        editor.Document.Variables.Remove(Variable(editor, id));
    });
    private static bool InInstance(DesignNode node) { for (var n = node; n is not null; n = n.Parent) if (n.Kind == NodeKind.Instance) return true; return false; }
    private static string RequireName(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A name is required.") : value.Trim();
    private static VariableCollection Collection(EditorSession e, string id) => e.Document.VariableCollections.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException("Collection no longer exists.");
    private static DesignVariable Variable(EditorSession e, string id) => e.Document.Variables.FirstOrDefault(v => v.Id == id) ?? throw new InvalidOperationException("Variable no longer exists.");
}
