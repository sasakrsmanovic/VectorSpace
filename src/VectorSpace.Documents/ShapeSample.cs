using VectorSpace.Core;

namespace VectorSpace.Documents;

/// <summary>Original editable shape study. Contains no external assets or pre-rendered screenshots.</summary>
public static class ShapeSample
{
    public static DesignDocument Create()
    {
        var board = new DesignNode { Kind = NodeKind.Frame, Name = "Shape lab", Width = 980, Height = 700, Fill = "#F8F9FB", ClipContent = true };
        Text("Shape lab", "Corners, arcs & live geometry", 42, 32, 36, 700, 560, 48);
        Text("Subtitle", "Every shape is editable. Select one, then explore its contextual controls.", 44, 96, 14, 400, 820, 24);
        Label("01  INDEPENDENT CORNERS", 52, 154);
        board.Add(new() { Id = "shape-corners", Name = "Independent corner study", X = 62, Y = 205, Width = 205, Height = 160, Corners = new(12, 70, 12, 45), Fill = "#2264F5" });
        Text("Corner caption", "Drag all radii · Alt for one corner", 52, 390, 12, 400, 260, 22);
        Label("02  ARC & INNER RADIUS", 360, 154);
        board.Add(new() { Id = "shape-ring", Name = "Editable ring sector", Kind = NodeKind.Ellipse, X = 385, Y = 185, Width = 190, Height = 190,
            Arc = new(-80, 285, .57), Fills = [new() { Kind = FillKind.LinearGradient, Stops = [new() { Color = "#9469FF" }, new() { Offset = 1, Color = "#E74FAD" }] }] });
        Text("Arc caption", "Start, end and inner-radius grips", 360, 390, 12, 400, 270, 22);
        Label("03  STROKE GEOMETRY", 682, 154);
        board.Add(new() { Id = "shape-stroke", Name = "Dashed open arc", Kind = NodeKind.Ellipse, X = 710, Y = 195, Width = 172, Height = 172,
            Arc = new(35, 290, 0, true), Fills = [], Strokes = [new() { Color = "#E66A32", Width = 18, Cap = StrokeCap.Round, Dashes = [10, 20], DashOffset = 4 }] });
        Text("Stroke caption", "Caps, joins, phase and alignment", 682, 390, 12, 400, 270, 22);
        var line = board.Add(new() { Name = "Divider", X = 44, Y = 442, Width = 892, Height = 1, Fill = "#DEE2EA" });
        Label("04  NON-DESTRUCTIVE BOOLEAN", 52, 474);
        var boolean = board.Add(new() { Id = "shape-boolean", Kind = NodeKind.Group, Boolean = BooleanKind.Subtract, Name = "Live subtraction", X = 70, Y = 520, Width = 210, Height = 128, Fill = "#159975" });
        boolean.Add(new() { Id = "shape-base", Name = "Retained base", Width = 210, Height = 128, CornerRadius = 24, Fill = "#159975" });
        boolean.Add(new() { Id = "shape-cutout", Name = "Retained cutout", Kind = NodeKind.Ellipse, X = 70, Y = -22, Width = 110, Height = 110, Fill = "#FFBD64" });
        Text("Boolean title", "Keep the ingredients", 365, 526, 24, 600, 470, 38);
        Text("Boolean caption", "Enter the group to edit operands. Change the operation, release the original shapes, or flatten to exact native contours.", 365, 573, 14, 400, 475, 64);
        var doc = new DesignDocument { Name = "Shape lab", Pages = [new() { Name = "Shape studies", Nodes = [board] }] };
        doc.RebuildParents(); DocumentJson.Validate(doc); return doc;
        void Text(string name, string content, double x, double y, double size, int weight, double w, double h) =>
            board.Add(new() { Name = name, Kind = NodeKind.Text, Text = content, X = x, Y = y, FontSize = size, FontWeight = weight, Width = w, Height = h, Fill = "#222B3B" });
        void Label(string text, double x, double y) => Text(text, text, x, y, 11, 600, 280, 24);
    }
}
