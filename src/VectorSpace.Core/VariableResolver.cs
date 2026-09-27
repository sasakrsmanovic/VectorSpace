namespace VectorSpace.Core;

/// <summary>Typed, bounded variable resolution. Modes inherit from the closest ancestor, then document, then collection.</summary>
public sealed class VariableResolver
{
    private readonly DesignDocument _document;
    private readonly Dictionary<string, VariableCollection> _collections;
    private readonly Dictionary<string, DesignVariable> _variables;
    public VariableResolver(DesignDocument document)
    {
        _document = document;
        _collections = document.VariableCollections.ToDictionary(c => c.Id, StringComparer.Ordinal);
        _variables = document.Variables.ToDictionary(v => v.Id, StringComparer.Ordinal);
    }
    public string ModeFor(string collectionId, DesignNode? node = null)
    {
        if (!_collections.TryGetValue(collectionId, out var collection)) throw new InvalidDataException("Unknown variable collection.");
        for (var current = node; current is not null; current = current.Parent)
            if (current.VariableModes.TryGetValue(collectionId, out var selected)) return RequireMode(collection, selected);
        return _document.VariableModes.TryGetValue(collectionId, out var mode) ? RequireMode(collection, mode) : collection.DefaultModeId;
    }
    private static string RequireMode(VariableCollection c, string mode) => c.Modes.Any(m => m.Id == mode) ? mode : throw new InvalidDataException("Unknown variable mode.");
    public VariableValue Resolve(string variableId, DesignNode? node = null) => Resolve(variableId, node, new HashSet<string>(StringComparer.Ordinal));
    private VariableValue Resolve(string id, DesignNode? node, HashSet<string> path)
    {
        if (path.Count >= 64 || !path.Add(id)) throw new InvalidDataException("A variable alias cycle or depth limit was reached.");
        if (!_variables.TryGetValue(id, out var variable)) throw new InvalidDataException("Unknown variable: " + id);
        var mode = ModeFor(variable.CollectionId, node);
        if (!variable.Values.TryGetValue(mode, out var value)) throw new InvalidDataException("Variable value missing for the selected mode.");
        if (value.AliasId is not null) value = Resolve(value.AliasId, node, path);
        if (value.Type != variable.Type) throw new InvalidDataException("Variable alias type mismatch.");
        path.Remove(id); return value;
    }
    public void Apply()
    {
        foreach (var node in _document.AllNodes())
            foreach (var (target, binding) in node.VariableBindings) Write(node, target, binding.Disabled ? binding.Fallback : Resolve(binding.VariableId, node));
    }
    public static VariableType TypeOf(VariableTarget target) => target switch
    {
        VariableTarget.Fill or VariableTarget.Stroke => VariableType.Color,
        VariableTarget.Text or VariableTarget.FontFamily => VariableType.String,
        VariableTarget.Visible => VariableType.Boolean,
        _ => VariableType.Number
    };
    public static VariableValue Read(DesignNode n, VariableTarget target) => target switch
    {
        VariableTarget.Fill => VariableValue.Color(n.Fill),
        VariableTarget.Stroke => VariableValue.Color(n.Strokes.FirstOrDefault()?.Color ?? "#000000"),
        VariableTarget.Text => VariableValue.String(n.Text),
        VariableTarget.FontFamily => VariableValue.String(n.FontFamily),
        VariableTarget.Visible => VariableValue.Bool(n.Visible),
        _ => VariableValue.Float(target switch
        {
            VariableTarget.Width => n.Width, VariableTarget.Height => n.Height, VariableTarget.Opacity => n.Opacity,
            VariableTarget.CornerRadius => n.CornerRadius, VariableTarget.FontSize => n.FontSize, VariableTarget.LetterSpacing => n.LetterSpacing,
            VariableTarget.Gap => n.Layout.Gap, VariableTarget.CrossGap => n.Layout.CrossGap,
            VariableTarget.PaddingLeft => n.Layout.PaddingLeft, VariableTarget.PaddingTop => n.Layout.PaddingTop,
            VariableTarget.PaddingRight => n.Layout.PaddingRight, VariableTarget.PaddingBottom => n.Layout.PaddingBottom,
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        })
    };
    public static void Write(DesignNode n, VariableTarget target, VariableValue value)
    {
        if (value.AliasId is not null || TypeOf(target) != value.Type) throw new InvalidDataException("The value does not match the property type.");
        var number = value.Number;
        if (!double.IsFinite(number)) throw new InvalidDataException("Variable numbers must be finite.");
        switch (target)
        {
            case VariableTarget.Fill: n.Fill = value.Text; break;
            case VariableTarget.Stroke: if (n.Strokes.Count == 0) n.Strokes.Add(new()); n.Strokes[0].Color = value.Text; break;
            case VariableTarget.Text: n.Text = value.Text; break;
            case VariableTarget.FontFamily: n.FontFamily = value.Text; break;
            case VariableTarget.Visible: n.Visible = value.Boolean; break;
            case VariableTarget.Width: n.Width = Math.Clamp(number, Math.Max(1, n.MinWidth), Math.Max(1, n.MaxWidth)); break;
            case VariableTarget.Height: n.Height = Math.Clamp(number, Math.Max(1, n.MinHeight), Math.Max(1, n.MaxHeight)); break;
            case VariableTarget.Opacity: n.Opacity = Math.Clamp(number, 0, 1); break;
            case VariableTarget.CornerRadius: n.CornerRadius = Math.Clamp(number, 0, 1e6); break;
            case VariableTarget.FontSize: n.FontSize = Math.Clamp(number, 1, 4096); break;
            case VariableTarget.LetterSpacing: n.LetterSpacing = Math.Clamp(number, -4096, 4096); break;
            case VariableTarget.Gap: n.Layout.Gap = Math.Clamp(number, -1e7, 1e7); break;
            case VariableTarget.CrossGap: n.Layout.CrossGap = Math.Clamp(number, 0, 1e7); break;
            case VariableTarget.PaddingLeft: n.Layout.PaddingLeft = Math.Clamp(number, 0, 1e7); break;
            case VariableTarget.PaddingTop: n.Layout.PaddingTop = Math.Clamp(number, 0, 1e7); break;
            case VariableTarget.PaddingRight: n.Layout.PaddingRight = Math.Clamp(number, 0, 1e7); break;
            case VariableTarget.PaddingBottom: n.Layout.PaddingBottom = Math.Clamp(number, 0, 1e7); break;
            default: throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
    public static void Validate(DesignDocument document)
    {
        if (document.Variables is null || document.VariableCollections is null || document.VariableModes is null || document.Variables.Count > 10000 || document.VariableCollections.Count > 256) throw new InvalidDataException("Invalid variable collections.");
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in document.VariableCollections)
        {
            if (c is null || string.IsNullOrWhiteSpace(c.Id) || !identifiers.Add(c.Id) || c.Modes is null || c.Modes.Count is < 1 or > 64) throw new InvalidDataException("Invalid collection or mode count.");
            foreach (var mode in c.Modes)
                if (mode is null || string.IsNullOrWhiteSpace(mode.Id) || !identifiers.Add(mode.Id)) throw new InvalidDataException("Invalid or duplicate variable mode.");
            RequireMode(c, c.DefaultModeId);
        }
        foreach (var v in document.Variables)
        {
            if (v is null || string.IsNullOrWhiteSpace(v.Id) || !identifiers.Add(v.Id) || !Enum.IsDefined(v.Type) || v.Values is null) throw new InvalidDataException("Invalid variable.");
            var c = document.VariableCollections.FirstOrDefault(c => c.Id == v.CollectionId) ?? throw new InvalidDataException("Variable references an unknown collection.");
            if (v.Values.Count != c.Modes.Count || c.Modes.Any(m => !v.Values.ContainsKey(m.Id))) throw new InvalidDataException("A variable needs one value per mode.");
            foreach (var value in v.Values.Values) CheckValue(value, v.Type);
        }
        var resolver = new VariableResolver(document);
        // Reject cycles in the union of every mode's alias edges: any inherited mode combination is then safe.
        var colors = new Dictionary<string, byte>(StringComparer.Ordinal);
        foreach (var variable in document.Variables) Visit(variable, 0);
        void Visit(DesignVariable variable, int depth)
        {
            if (colors.GetValueOrDefault(variable.Id) == 2) return;
            if (depth >= 64 || colors.GetValueOrDefault(variable.Id) == 1) throw new InvalidDataException("Cyclic variable aliases.");
            colors[variable.Id] = 1;
            foreach (var value in variable.Values.Values)
                if (value.AliasId is { } alias)
                {
                    if (!resolver._variables.TryGetValue(alias, out var next) || next.Type != variable.Type) throw new InvalidDataException("Invalid or mismatched variable alias.");
                    Visit(next, depth + 1);
                }
            colors[variable.Id] = 2;
        }
        CheckModes(document.VariableModes);
        // Node graph shape/depth is checked by DocumentJson before invoking this validator.
        foreach (var node in document.AllNodes())
        {
            if (node.VariableBindings is null || node.VariableModes is null) throw new InvalidDataException("Missing variable bindings.");
            CheckModes(node.VariableModes);
            foreach (var (target, binding) in node.VariableBindings)
            {
                if (!Enum.IsDefined(target) || binding is null) throw new InvalidDataException("Invalid variable binding.");
                if (binding.Disabled) { CheckValue(binding.Fallback, TypeOf(target)); if (binding.Fallback.AliasId is not null) throw new InvalidDataException("Binding fallback must be a literal."); continue; }
                if (!resolver._variables.TryGetValue(binding.VariableId, out var variable) || TypeOf(target) != variable.Type) throw new InvalidDataException("Invalid variable binding.");
                CheckValue(binding.Fallback, variable.Type);
                if (binding.Fallback.AliasId is not null) throw new InvalidDataException("Binding fallback must be a literal.");
            }
        }
        void CheckModes(Dictionary<string, string> modes)
        {
            foreach (var (id, mode) in modes)
                if (!resolver._collections.TryGetValue(id, out var c) || c.Modes.All(m => m.Id != mode)) throw new InvalidDataException("Invalid mode override.");
        }
    }
    private static void CheckValue(VariableValue? value, VariableType type)
    {
        if (value is null || value.Type != type || !double.IsFinite(value.Number) || value.Text is null || value.Text.Length > 1_000_000) throw new InvalidDataException("Invalid typed variable value.");
        if (type == VariableType.Color && value.AliasId is null && !(value.Text.Length is 7 or 9 && value.Text[0] == '#' && value.Text.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") == false)) throw new InvalidDataException("Variable colors use #RRGGBB or #AARRGGBB.");
    }
}
