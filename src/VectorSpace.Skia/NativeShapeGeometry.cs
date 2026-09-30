using SkiaSharp;
using VectorSpace.Core;

namespace VectorSpace.Skia;

/// <summary>Owned native geometry creation without SVG formatting/parsing. Callers dispose paths.</summary>
public static class NativeShapeGeometry
{
    public static SKPath Build(DesignNode node)
    {
        if (node.Commands is { } commands) return FromCommands(commands, node.FillRule);
        if (node.PathData is not null) return SKPath.ParseSvgPathData(node.PathData) ?? new();
        var path = new SKPath { FillType = node.FillRule == PathFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
        var w = (float)node.Width; var h = (float)node.Height;
        if (node.Kind == NodeKind.Ellipse)
        {
            if (node.Arc is null) path.AddOval(new(0, 0, w, h));
            else AddArc(path, node);
        }
        else if (node.Kind is NodeKind.Line or NodeKind.Arrow)
        {
            path.MoveTo(0, 0); path.LineTo(w, h);
            if (node.Kind == NodeKind.Arrow)
            {
                var angle = Math.Atan2(h, w); var length = Math.Min(16, Math.Sqrt((double)w * w + (double)h * h) * .25);
                path.MoveTo((float)(w - length * Math.Cos(angle - .5)), (float)(h - length * Math.Sin(angle - .5)));
                path.LineTo(w, h); path.LineTo((float)(w - length * Math.Cos(angle + .5)), (float)(h - length * Math.Sin(angle + .5)));
            }
        }
        else if (node.Kind is NodeKind.Polygon or NodeKind.Star)
        {
            var count = Math.Clamp(node.Sides, 3, 128) * (node.Kind == NodeKind.Star ? 2 : 1);
            for (var i = 0; i < count; i++)
            {
                var angle = -Math.PI / 2 + i * Math.PI * 2 / count;
                var r = node.Kind == NodeKind.Star && i % 2 == 1 ? node.StarRatio : 1;
                var p = new SKPoint((float)(node.Width / 2 + Math.Cos(angle) * node.Width / 2 * r), (float)(node.Height / 2 + Math.Sin(angle) * node.Height / 2 * r));
                if (i == 0) path.MoveTo(p); else path.LineTo(p);
            }
            path.Close();
        }
        else if (node.Kind == NodeKind.Path && node.Contours is { } contours)
        {
            foreach (var contour in contours) AddEditableContour(path, contour.Points, contour.Closed);
        }
        else if (node.Kind == NodeKind.Path && node.Points.Count > 0) AddEditableContour(path, node.Points, node.Closed);
        else if (node.Kind != NodeKind.Path) AddCornerBox(path, node);
        return path;
    }
    private static void AddEditableContour(SKPath path, IReadOnlyList<PathPoint> points, bool closed)
    {
        if (points.Count == 0) return;
        path.MoveTo(P(points[0].Position));
        for (var i = 1; i < points.Count; i++) Segment(points[i - 1], points[i]);
        if (closed) { Segment(points[^1], points[0]); path.Close(); }
        void Segment(PathPoint a, PathPoint b)
        {
            if (a.ControlOut.HasValue || b.ControlIn.HasValue) path.CubicTo(P(a.ControlOut ?? a.Position), P(b.ControlIn ?? b.Position), P(b.Position));
            else path.LineTo(P(b.Position));
        }
    }
    public static void AddCornerBox(SKPath path, DesignNode node)
    {
        var r = node.EffectiveCorners;
        var w = (float)node.Width; var h = (float)node.Height;
        var tl = (float)r.TopLeft; var tr = (float)r.TopRight;
        var br = (float)r.BottomRight; var bl = (float)r.BottomLeft;
        // Contour order is part of editable conversion: begin on the top-left edge,
        // then proceed clockwise, matching the existing portable SVG builder.
        // SKRoundRect's default starts at a different corner even for a plain rect.
        // Explicit native commands retain the allocation benefit without reindexing
        // anchors merely because the renderer stopped parsing SVG.
        const float weight = 0.7071067811865476f;
        path.MoveTo(tl, 0);
        path.LineTo(w - tr, 0);
        if (tr > 0) path.ConicTo(w, 0, w, tr, weight);
        path.LineTo(w, h - br);
        if (br > 0) path.ConicTo(w, h, w - br, h, weight);
        path.LineTo(bl, h);
        if (bl > 0) path.ConicTo(0, h, 0, h - bl, weight);
        if (tl > 0)
        {
            path.LineTo(0, tl);
            path.ConicTo(0, 0, tl, 0, weight);
        }
        path.Close();
    }
    private static void AddArc(SKPath path, DesignNode n)
    {
        var a = n.Arc!; var start = a.StartDegrees % 360; var sweep = Math.Clamp(a.SweepDegrees, -360, 360);
        if (n.Width <= 0 || n.Height <= 0 || Math.Abs(sweep) < 1e-12 || a.InnerRadius >= 1 && !a.Open) return;
        var outer = SceneRenderer.Rect(n.LocalBounds); var full = Math.Abs(sweep) >= 360 - 1e-9;
        if (full && !a.Open)
        {
            path.AddOval(outer, sweep >= 0 ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise);
            if (a.InnerRadius > 0) path.AddOval(Inner(), sweep >= 0 ? SKPathDirection.CounterClockwise : SKPathDirection.Clockwise);
            return;
        }
        Arc(outer, start, sweep, true);
        if (a.Open) return;
        if (a.InnerRadius > 0) { path.LineTo(P(ShapeGeometry.ArcPoint(n, start + sweep, a.InnerRadius))); Arc(Inner(), start + sweep, -sweep, false); }
        else path.LineTo((float)n.Width / 2, (float)n.Height / 2);
        path.Close();
        SKRect Inner() => new((float)(n.Width * (1 - a.InnerRadius) / 2), (float)(n.Height * (1 - a.InnerRadius) / 2), (float)(n.Width * (1 + a.InnerRadius) / 2), (float)(n.Height * (1 + a.InnerRadius) / 2));
        void Arc(SKRect bounds, double from, double amount, bool move)
        {
            var count = Math.Max(1, (int)Math.Ceiling(Math.Abs(amount) / 180));
            for (var i = 0; i < count; i++) path.ArcTo(bounds, (float)(from + i * amount / count), (float)(amount / count), move && i == 0);
        }
    }
    public static SKPath FromCommands(IReadOnlyList<PathCommand> commands, PathFillRule rule)
    {
        var result = new SKPath { FillType = rule == PathFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
        try
        {
            foreach (var c in commands)
                switch (c.Verb)
                {
                    case PathVerb.Move: result.MoveTo(P(c.Point)); break;
                    case PathVerb.Line: result.LineTo(P(c.Point)); break;
                    case PathVerb.Quadratic: result.QuadTo(P(c.Control1), P(c.Point)); break;
                    case PathVerb.Conic: result.ConicTo(P(c.Control1), P(c.Point), (float)c.Weight); break;
                    case PathVerb.Cubic: result.CubicTo(P(c.Control1), P(c.Control2), P(c.Point)); break;
                    case PathVerb.Close: result.Close(); break;
                    default: throw new InvalidDataException("Unknown path verb.");
                }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    public static List<PathCommand> Capture(SKPath path)
    {
        var result = new List<PathCommand>(); using var iterator = path.CreateRawIterator(); Span<SKPoint> p = stackalloc SKPoint[4];
        for (var verb = iterator.Next(p); verb != SKPathVerb.Done; verb = iterator.Next(p))
        {
            if (result.Count >= 100_000) throw new InvalidOperationException("A flattened path exceeds 100,000 native commands.");
            result.Add(verb switch
            {
                SKPathVerb.Move => new(PathVerb.Move, V(p[0])),
                SKPathVerb.Line => new(PathVerb.Line, V(p[1])),
                SKPathVerb.Quad => new(PathVerb.Quadratic, V(p[2]), V(p[1])),
                SKPathVerb.Conic => new(PathVerb.Conic, V(p[2]), V(p[1]), default, iterator.ConicWeight()),
                SKPathVerb.Cubic => new(PathVerb.Cubic, V(p[3]), V(p[1]), V(p[2])),
                SKPathVerb.Close => new(PathVerb.Close),
                _ => throw new InvalidDataException("Unsupported path verb.")
            });
        }
        return result;
    }
    public static bool IsClosed(SKPath path)
    {
        var open = false; var any = false;
        using var iterator = path.CreateRawIterator(); Span<SKPoint> p = stackalloc SKPoint[4];
        for (var verb = iterator.Next(p); verb != SKPathVerb.Done; verb = iterator.Next(p))
        {
            if (verb == SKPathVerb.Move) { if (open) return false; open = true; any = true; }
            else if (verb == SKPathVerb.Close) open = false;
        }
        return any && !open;
    }
    private static SKPoint P(Vec2 p) => new((float)p.X, (float)p.Y);
    private static Vec2 V(SKPoint p) => new(p.X, p.Y);
}
