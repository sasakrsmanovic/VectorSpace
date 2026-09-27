using System.Globalization;

namespace VectorSpace.Core;

public enum VariableType { Color, Number, String, Boolean }
public enum VariableTarget { Fill, Stroke, Text, FontFamily, Visible, Width, Height, Opacity, CornerRadius, FontSize, LetterSpacing, Gap, CrossGap, PaddingLeft, PaddingTop, PaddingRight, PaddingBottom }

/// <summary>A typed literal or a reference to another variable. Values are immutable and safe to share.</summary>
public sealed record VariableValue
{
    public VariableType Type { get; init; }
    public string Text { get; init; } = "";
    public double Number { get; init; }
    public bool Boolean { get; init; }
    public string? AliasId { get; init; }
    public static VariableValue Color(string value) => new() { Type = VariableType.Color, Text = value };
    public static VariableValue Float(double value) => new() { Type = VariableType.Number, Number = value };
    public static VariableValue String(string value) => new() { Type = VariableType.String, Text = value };
    public static VariableValue Bool(bool value) => new() { Type = VariableType.Boolean, Boolean = value };
    public static VariableValue Alias(DesignVariable variable) => new() { Type = variable.Type, AliasId = variable.Id };
    public override string ToString() => AliasId is not null ? "Alias: " + AliasId : Type switch
    {
        VariableType.Number => Number.ToString("0.###", CultureInfo.InvariantCulture),
        VariableType.Boolean => Boolean ? "True" : "False",
        _ => Text
    };
}
public sealed class VariableMode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default";
}
public sealed class VariableCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Collection";
    public string DefaultModeId { get; set; } = "";
    public List<VariableMode> Modes { get; set; } = [];
}
public sealed class DesignVariable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Variable";
    public string CollectionId { get; set; } = "";
    public VariableType Type { get; set; }
    public Dictionary<string, VariableValue> Values { get; set; } = [];
}
public sealed class VariableBinding
{
    public string VariableId { get; set; } = "";
    public bool IsOverride { get; set; }
    public bool Disabled { get; set; }
    public VariableValue Fallback { get; set; } = new();
}
