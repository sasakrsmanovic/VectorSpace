using VectorSpace.Core;

namespace VectorSpace.Documents;

/// <summary>Original editable studies of holes, independent cubic contours and open endpoints.</summary>
public static class VectorSample
{
    public static DesignDocument Create()
    {
        var board = new DesignNode { Kind = NodeKind.Frame, Name = "Vector lab", Width = 980, Height = 620, Fill = "#F8F9FB", ClipContent = true };
        Text("Vector lab", "A layer. Many contours.", 44, 32, 36, 700, 840, 50);
        Text("Introduction", "Select a study and press Enter. Edit its anchors without losing holes or disconnected paths.", 44, 98, 14, 400, 890, 50);
        Text("Holes", "01  HOLES & ISLANDS", 52, 170, 11, 600, 260, 24);
        board.Add(new() { Id = "vector-holes", Kind = NodeKind.Path, Name = "Compound badge", X = 64, Y = 228, Width = 250, Height = 180, PathWidth = 250, PathHeight = 180,
            Fill = "#2264F5", Contours = [Box(0,0,180,160), Box(45,40,90,80,true), Box(205,0,40,40)] });
        Text("Hole hint", "Select an inner contour. Reverse it or switch the fill rule.", 52, 430, 13, 400, 270, 58);
        Text("Curves", "02  CUBIC CONTOURS", 366, 170, 11, 600, 260, 24);
        board.Add(new() { Id = "vector-curves", Kind = NodeKind.Path, Name = "Paired cubic strokes", X = 370, Y = 230, Width = 240, Height = 165, PathWidth = 240, PathHeight = 165,
            Fills = [], Strokes = [new() { Color = "#8855D9", Width = 7, Cap = StrokeCap.Round }], Contours = [Wave(0), Wave(70)] });
        Text("Curve hint", "Shift-select anchors across contours. Drag handles or subdivide.", 366, 430, 13, 400, 260, 58);
        Text("Endpoints", "03  CUT & JOIN", 684, 170, 11, 600, 245, 24);
        board.Add(new() { Id = "vector-endpoints", Kind = NodeKind.Path, Name = "Open endpoint study", X = 700, Y = 230, Width = 220, Height = 180, PathWidth = 220, PathHeight = 180,
            Fills = [], Strokes = [new() { Color = "#159975", Width = 7, Cap = StrokeCap.Round, Join = StrokeJoin.Round }], Contours = [
                new() { Points = [P(0,0),P(0,120),P(75,120)] }, new() { Points = [P(120,30),P(200,30),P(200,150)] }] });
        Text("Endpoint hint", "Select two open ends and join. Cut an interior anchor with X.", 684, 430, 13, 400, 250, 58);
        board.Add(new() { Name = "Divider", X = 44, Y = 512, Width = 892, Height = 1, Fill = "#DEE2EA" });
        Text("Footer", "Native geometry · Undoable edits · Reusable controls", 44, 545, 16, 600, 840, 34);
        var document = new DesignDocument { Name = "Vector lab", Pages = [new() { Name = "Vector studies", Nodes = [board] }] };
        document.RebuildParents(); DocumentJson.Validate(document); return document;
        void Text(string name, string text, double x, double y, double size, int weight, double width, double height) => board.Add(new()
        { Name = name, Kind = NodeKind.Text, Text = text, X = x, Y = y, FontSize = size, FontWeight = weight, Width = width, Height = height, Fill = "#222B3B" });
    }
    private static PathPoint P(double x, double y) => new() { Position = new(x,y) };
    private static PathContour Box(double x, double y, double w, double h, bool reverse = false)
    {
        var points = new List<PathPoint> { P(x,y),P(x+w,y),P(x+w,y+h),P(x,y+h) }; if (reverse) points.Reverse();
        return new() { Points = points, Closed = true };
    }
    private static PathContour Wave(double y) => new() { Points = [
        new() { Position = new(0,y+45), ControlOut = new(30,y-15) },
        new() { Position = new(115,y+45), ControlIn = new(80,y-15), ControlOut = new(145,y+105) },
        new() { Position = new(230,y+45), ControlIn = new(200,y+105) }] };
}
