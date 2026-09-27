using System.Text.Json.Serialization;

namespace VectorSpace.Core;

public enum NodeKind { Frame, Group, Rectangle, Ellipse, Line, Arrow, Polygon, Star, Path, Text, Component, Instance, Section, Slice, ComponentSet }
public enum FillKind { Solid, LinearGradient, RadialGradient }
public enum BlendKind { Normal, Multiply, Screen, Overlay, Darken, Lighten, Difference }
public enum LayoutDirection { None, Horizontal, Vertical, Grid }
public enum LayoutDistribution { Packed, SpaceBetween, SpaceAround, SpaceEvenly }
public enum GridTrackSizing { Fraction, Fixed, Hug }
public enum AxisConstraint { Start, Center, End, Stretch, Scale }
public enum LayoutAlignment { Start, Center, End, Stretch }
public enum TextAlignment { Left, Center, Right }

public sealed class GradientStop
{
    public double Offset { get; set; }
    public string Color { get; set; } = "#FFFFFF";
}
public sealed class FillStyle
{
    public FillKind Kind { get; set; }
    public string Color { get; set; } = "#D9D9D9";
    public double Opacity { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public Vec2 Start { get; set; } = new(0, 0);
    public Vec2 End { get; set; } = new(1, 1);
    public List<GradientStop> Stops { get; set; } = [new() { Offset = 0, Color = "#A78BFA" }, new() { Offset = 1, Color = "#6D28D9" }];
}
public sealed class StrokeStyle
{
    public string Color { get; set; } = "#1E1E1E";
    public double Width { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public List<double> Dashes { get; set; } = [];
}
public sealed class ShadowStyle
{
    public bool Visible { get; set; } = true;
    public string Color { get; set; } = "#000000";
    public double Opacity { get; set; } = .15;
    public double X { get; set; }
    public double Y { get; set; } = 4;
    public double Blur { get; set; } = 12;
}
public sealed class GridTrack
{
    public GridTrackSizing Sizing { get; set; }
    public double Value { get; set; } = 1;
    public double Min { get; set; }
    public double Max { get; set; } = 1e7;
}

public sealed class AutoLayout
{
    public LayoutDirection Direction { get; set; }
    public double Gap { get; set; } = 16;
    public double PaddingLeft { get; set; } = 16;
    public double PaddingTop { get; set; } = 16;
    public double PaddingRight { get; set; } = 16;
    public double PaddingBottom { get; set; } = 16;
    public bool HugWidth { get; set; }
    public bool HugHeight { get; set; }
    public LayoutAlignment Alignment { get; set; }
    public LayoutAlignment PrimaryAlignment { get; set; }
    public LayoutDistribution Distribution { get; set; }
    public bool Wrap { get; set; }
    public double CrossGap { get; set; } = 16;
    public int GridColumns { get; set; } = 3;
    public List<GridTrack> Columns { get; set; } = [];
    public List<GridTrack> Rows { get; set; } = [];
}
public sealed class PathPoint
{
    public Vec2 Position { get; set; }
    public Vec2? ControlIn { get; set; }
    public Vec2? ControlOut { get; set; }
}
public sealed class InstanceOverride
{
    public string? Text { get; set; }
    public string? Fill { get; set; }
    public bool? Visible { get; set; }
}

/// <summary>A serializable scene node. Coordinates are relative to the parent, not the canvas.</summary>
public sealed class DesignNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Rectangle";
    public NodeKind Kind { get; set; } = NodeKind.Rectangle;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 100;
    public double Rotation { get; set; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public double CornerRadius { get; set; }
    public double Opacity { get; set; } = 1;
    public BlendKind Blend { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public bool ClipContent { get; set; }
    public bool Expanded { get; set; } = true;
    public int Sides { get; set; } = 5;
    public double StarRatio { get; set; } = .45;
    public List<FillStyle> Fills { get; set; } = [new()];
    public List<StrokeStyle> Strokes { get; set; } = [];
    public List<ShadowStyle> Shadows { get; set; } = [];
    public string Text { get; set; } = "Text";
    public string FontFamily { get; set; } = "Inter";
    public double FontSize { get; set; } = 24;
    public int FontWeight { get; set; } = 400;
    public double LineHeight { get; set; } = 1.25;
    public double LetterSpacing { get; set; }
    public TextAlignment TextAlign { get; set; }
    public string? PathData { get; set; }
    public double PathWidth { get; set; }
    public double PathHeight { get; set; }
    public List<PathPoint> Points { get; set; } = [];
    public bool Closed { get; set; }
    public AutoLayout Layout { get; set; } = new();
    public AxisConstraint HorizontalConstraint { get; set; }
    public AxisConstraint VerticalConstraint { get; set; }
    public bool FillWidth { get; set; }
    public bool FillHeight { get; set; }
    public bool AbsolutePosition { get; set; }
    public double MinWidth { get; set; } = 1;
    public double MinHeight { get; set; } = 1;
    public double MaxWidth { get; set; } = 1e7;
    public double MaxHeight { get; set; } = 1e7;
    public int GridColumn { get; set; } = -1;
    public int GridRow { get; set; } = -1;
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string? ComponentId { get; set; }
    public string? SourceId { get; set; }
    public Dictionary<string, InstanceOverride> Overrides { get; set; } = [];
    public Dictionary<string, string> VariantProperties { get; set; } = [];
    public Dictionary<VariableTarget, VariableBinding> VariableBindings { get; set; } = [];
    public Dictionary<string, string> VariableModes { get; set; } = [];
    public string? PrototypeTargetId { get; set; }
    public List<DesignNode> Children { get; set; } = [];
    [JsonIgnore] public DesignNode? Parent { get; set; }
    [JsonIgnore] public bool IsContainer => Kind is NodeKind.Frame or NodeKind.Group or NodeKind.Component or NodeKind.Instance or NodeKind.Section or NodeKind.ComponentSet;
    [JsonIgnore] public bool IsFrame => Kind is NodeKind.Frame or NodeKind.Component or NodeKind.Instance;
    [JsonIgnore] public bool IsEffectivelyLocked => Locked || (Parent?.IsEffectivelyLocked ?? false);
    [JsonIgnore] public bool IsEffectivelyVisible => Visible && (Parent?.IsEffectivelyVisible ?? true);
    [JsonIgnore] public RectD Bounds => new(X, Y, Width, Height);
    [JsonIgnore] public RectD LocalBounds => new(0, 0, Width, Height);
    [JsonIgnore] public Matrix2D LocalMatrix => Matrix2D.Translation(-Width / 2, -Height / 2) * Matrix2D.Scale(FlipX ? -1 : 1, FlipY ? -1 : 1) * Matrix2D.Rotation(Rotation) * Matrix2D.Translation(X + Width / 2, Y + Height / 2);
    [JsonIgnore] public Matrix2D WorldMatrix => Parent is null ? LocalMatrix : LocalMatrix * Parent.WorldMatrix;
    [JsonIgnore] public RectD WorldBounds => WorldMatrix.Map(LocalBounds);
    [JsonIgnore] public string Fill
    {
        get => Fills.FirstOrDefault()?.Color ?? "#D9D9D9";
        set { if (Fills.Count == 0) Fills.Add(new()); Fills[0].Color = value; }
    }
    public DesignNode Add(DesignNode child) { child.Parent = this; Children.Add(child); return child; }
    public IEnumerable<DesignNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children) foreach (var node in child.DescendantsAndSelf()) yield return node;
    }
    public bool IsDescendantOf(DesignNode other)
    {
        for (var p = Parent; p is not null; p = p.Parent) if (p == other) return true;
        return false;
    }
}

public sealed class DesignPage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Page 1";
    public string Background { get; set; } = "#E5E5E5";
    public List<DesignNode> Nodes { get; set; } = [];
    public List<Guide> Guides { get; set; } = [];
    public IEnumerable<DesignNode> AllNodes() => Nodes.SelectMany(n => n.DescendantsAndSelf());
}
public sealed class Guide
{
    public bool Horizontal { get; set; }
    public double Position { get; set; }
}
public sealed class CommentThread
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PageId { get; set; } = "";
    public Vec2 Anchor { get; set; }
    public string Author { get; set; } = "You";
    public string Text { get; set; } = "";
    public bool Resolved { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Replies { get; set; } = [];
}
public sealed class DesignDocument
{
    public int FormatVersion { get; set; } = 2;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";
    public List<DesignPage> Pages { get; set; } = [new()];
    public List<CommentThread> Comments { get; set; } = [];
    public Dictionary<string, string> ColorStyles { get; set; } = [];
    public List<VariableCollection> VariableCollections { get; set; } = [];
    public List<DesignVariable> Variables { get; set; } = [];
    public Dictionary<string, string> VariableModes { get; set; } = [];
    public IEnumerable<DesignNode> AllNodes() => Pages.SelectMany(p => p.AllNodes());
    public DesignNode? Find(string? id) => id is null ? null : AllNodes().FirstOrDefault(n => n.Id == id);
    public void RebuildParents()
    {
        foreach (var page in Pages) foreach (var node in page.Nodes) Attach(node, null);
        static void Attach(DesignNode n, DesignNode? p) { n.Parent = p; foreach (var c in n.Children) Attach(c, n); }
    }
}

public static class NodeGeometry
{
    /// <summary>Re-expresses a node in another rigid/scaled coordinate system without losing its center.</summary>
    public static void SetLocalMatrix(DesignNode node, Matrix2D matrix)
    {
        var center = matrix.Map(new Vec2(node.Width / 2, node.Height / 2));
        var sx = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var determinant = matrix.M11 * matrix.M22 - matrix.M12 * matrix.M21;
        var sy = sx > 1e-9 ? determinant / sx : 1;
        node.Width *= Math.Max(.0001, sx); node.Height *= Math.Max(.0001, Math.Abs(sy));
        node.FlipX = false; node.FlipY = sy < 0;
        node.Rotation = Math.Atan2(matrix.M12, matrix.M11) * 180 / Math.PI;
        node.X = center.X - node.Width / 2; node.Y = center.Y - node.Height / 2;
    }
}
