using VectorSpace.Core;

namespace VectorSpace.Documents;

public static class ShapeValidation
{
    public static void Validate(DesignNode n)
    {
        if (!Enum.IsDefined(n.FillRule)) throw new InvalidDataException("Invalid path fill rule.");
        if (n.Corners is { } r && new[] { r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft }.Any(v => !Finite(v, 0, 1e6)))
            throw new InvalidDataException("Invalid independent corner radii.");
        if (n.Arc is { } arc && (!Finite(arc.StartDegrees, -360000, 360000) || !Finite(arc.SweepDegrees, -360, 360) || !Finite(arc.InnerRadius, 0, 1)))
            throw new InvalidDataException("Invalid ellipse arc.");
        if (n.Boolean is { } op && (!Enum.IsDefined(op) || n.Kind != NodeKind.Group || n.Layout.Direction != LayoutDirection.None || n.Children.Count > 128 || n.Children.Any(c => c is null || !ShapeGeometry.IsOperand(c) || !c.IsBoolean && c.Children.Count != 0)))
            throw new InvalidDataException("A live Boolean group requires at most 128 vector operands and no auto-layout.");
        if (n.Commands is not { } commands) return;
        if (n.Kind != NodeKind.Path || n.PathData is not null || n.Points.Count != 0 || commands.Count > 100000)
            throw new InvalidDataException("Native contour commands cannot coexist with SVG data or editable anchors.");
        var open = false;
        foreach (var c in commands)
        {
            if (!Enum.IsDefined(c.Verb) || !Point(c.Point) || !Point(c.Control1) || !Point(c.Control2) || !Finite(c.Weight, .000001, 1e6))
                throw new InvalidDataException("Invalid native path command.");
            if (c.Verb == PathVerb.Move) open = true;
            else if (!open) throw new InvalidDataException("A native contour must begin with a move command.");
            else if (c.Verb == PathVerb.Close) open = false;
        }
    }
    public static void Stroke(StrokeStyle s)
    {
        if (!Enum.IsDefined(s.Alignment) || !Enum.IsDefined(s.Cap) || !Enum.IsDefined(s.Join) || !Finite(s.MiterLimit, 1, 128) || !Finite(s.DashOffset, -1e9, 1e9))
            throw new InvalidDataException("Invalid stroke alignment, cap, join or dash offset.");
    }
    private static bool Point(Vec2 p) => p.IsFinite && Math.Abs(p.X) <= 1e9 && Math.Abs(p.Y) <= 1e9;
    private static bool Finite(double n, double min, double max) => double.IsFinite(n) && n >= min && n <= max;
}
