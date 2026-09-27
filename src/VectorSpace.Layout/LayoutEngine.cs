using VectorSpace.Core;

namespace VectorSpace.Layout;

/// <summary>Deterministic two-phase layout. Intrinsic measurement is bottom-up; allocation and
/// reflow are top-down. Absolutely positioned and hidden children do not consume flow space.</summary>
public static class LayoutEngine
{
    private const double Epsilon = 1e-7;
    private sealed class Context
    {
        public readonly Dictionary<DesignNode, Vec2> Desired = [];
    }
    private sealed class Line
    {
        public readonly List<DesignNode> Nodes = [];
        public double Main;
        public double Cross;
    }
    private readonly record struct Cell(DesignNode Node, int Column, int Row, int Columns, int Rows);

    public static void Arrange(IEnumerable<DesignNode> roots)
    {
        var context = new Context();
        var array = roots as IReadOnlyList<DesignNode> ?? roots.ToArray();
        foreach (var node in array) Measure(node, context);
        foreach (var node in array) ArrangeNode(node, context, false, false);
    }

    public static void Arrange(DesignNode node) => Arrange([node]);

    private static void Measure(DesignNode node, Context context)
    {
        foreach (var child in node.Children) Measure(child, context);
        var size = new Vec2(node.Width, node.Height);
        if (node.Layout.Direction != LayoutDirection.None)
        {
            var children = FlowChildren(node);
            var l = node.Layout;
            var px = Positive(l.PaddingLeft) + Positive(l.PaddingRight);
            var py = Positive(l.PaddingTop) + Positive(l.PaddingBottom);
            if (children.Length == 0) size = new(l.HugWidth ? px : size.X, l.HugHeight ? py : size.Y);
            else if (l.Direction == LayoutDirection.Grid)
            {
                var cells = PlaceCells(children, ColumnCount(l));
                var columns = MeasureTracks(cells, true, ColumnCount(l), l.Columns, context, l.Gap);
                var rows = MeasureTracks(cells, false, cells.Max(c => c.Row + c.Rows), l.Rows, context, l.CrossGap);
                size = new(l.HugWidth ? columns.Sum() + GapTotal(l.Gap, columns.Length) + px : size.X,
                           l.HugHeight ? rows.Sum() + GapTotal(l.CrossGap, rows.Length) + py : size.Y);
            }
            else
            {
                var horizontal = l.Direction == LayoutDirection.Horizontal;
                var naturalMain = children.Sum(c => Main(context.Desired[c], horizontal)) + GapTotal(l.Gap, children.Length);
                var naturalCross = children.Max(c => Cross(context.Desired[c], horizontal));
                size = new(l.HugWidth ? (horizontal ? naturalMain : naturalCross) + px : size.X,
                           l.HugHeight ? (horizontal ? naturalCross : naturalMain) + py : size.Y);
            }
        }
        context.Desired[node] = new(Clamp(node, size.X, true), Clamp(node, size.Y, false));
    }

    private static void ArrangeNode(DesignNode node, Context context, bool allocatedWidth, bool allocatedHeight)
    {
        var l = node.Layout;
        if (l.HugWidth && !allocatedWidth) node.Width = context.Desired[node].X;
        if (l.HugHeight && !allocatedHeight) node.Height = context.Desired[node].Y;
        node.Width = Clamp(node, node.Width, true); node.Height = Clamp(node, node.Height, false);
        if (l.Direction == LayoutDirection.None)
        {
            foreach (var child in node.Children) ArrangeNode(child, context, false, false);
            return;
        }
        node.Width = Math.Max(node.Width, Positive(l.PaddingLeft) + Positive(l.PaddingRight));
        node.Height = Math.Max(node.Height, Positive(l.PaddingTop) + Positive(l.PaddingBottom));
        if (l.Direction == LayoutDirection.Grid) ArrangeGrid(node, context, allocatedWidth, allocatedHeight);
        else ArrangeFlow(node, context, allocatedWidth, allocatedHeight);
        foreach (var child in node.Children.Where(c => !c.Visible || c.AbsolutePosition))
            ArrangeNode(child, context, false, false);
    }

    private static void ArrangeFlow(DesignNode node, Context context, bool allocatedWidth, bool allocatedHeight)
    {
        var l = node.Layout; var horizontal = l.Direction == LayoutDirection.Horizontal;
        var children = FlowChildren(node);
        var mainPadding = horizontal ? Positive(l.PaddingLeft) + Positive(l.PaddingRight) : Positive(l.PaddingTop) + Positive(l.PaddingBottom);
        var crossPadding = horizontal ? Positive(l.PaddingTop) + Positive(l.PaddingBottom) : Positive(l.PaddingLeft) + Positive(l.PaddingRight);
        var hugMain = horizontal ? l.HugWidth && !allocatedWidth : l.HugHeight && !allocatedHeight;
        var hugCross = horizontal ? l.HugHeight && !allocatedHeight : l.HugWidth && !allocatedWidth;
        var available = Math.Max(0, Main(node, horizontal) - mainPadding);
        var availableCross = Math.Max(0, Cross(node, horizontal) - crossPadding);
        var lines = new List<Line>(); var line = new Line();
        foreach (var child in children)
        {
            // A fill item's minimum is its wrapping basis, not the previous frame's allocation.
            var basis = Fill(child, horizontal) && !hugMain ? Minimum(child, horizontal) : Main(context.Desired[child], horizontal);
            basis = Clamp(child, basis, horizontal);
            if (l.Wrap && !hugMain && line.Nodes.Count > 0 && line.Main + l.Gap + basis > available + Epsilon)
            { lines.Add(line); line = new(); }
            if (line.Nodes.Count > 0) line.Main += l.Gap;
            line.Main += basis; line.Nodes.Add(child); SetMain(child, horizontal, basis);
        }
        if (line.Nodes.Count > 0) lines.Add(line);
        foreach (var row in lines)
        {
            var fill = row.Nodes.Where(c => Fill(c, horizontal) && !hugMain).ToArray();
            if (fill.Length > 0)
            {
                var budget = available - GapTotal(l.Gap, row.Nodes.Count) - row.Nodes.Where(c => !Fill(c, horizontal)).Sum(c => Main(c, horizontal));
                var sizes = Distribute(budget, fill.Select(c => Minimum(c, horizontal)).ToArray(), fill.Select(c => Maximum(c, horizontal)).ToArray());
                for (var i = 0; i < fill.Length; i++) SetMain(fill[i], horizontal, sizes[i]);
            }
            foreach (var child in row.Nodes)
            {
                var crossFill = (Fill(child, !horizontal) || l.Alignment == LayoutAlignment.Stretch) && !hugCross && !l.Wrap;
                if (crossFill) SetMain(child, !horizontal, Clamp(child, availableCross, !horizontal));
                ArrangeNode(child, context,
                    horizontal ? Fill(child, true) && !hugMain : crossFill,
                    horizontal ? crossFill : Fill(child, false) && !hugMain);
            }
            row.Main = row.Nodes.Sum(c => Main(c, horizontal)) + GapTotal(l.Gap, row.Nodes.Count);
            row.Cross = row.Nodes.Max(c => Cross(c, horizontal));
        }
        var contentMain = lines.Count == 0 ? 0 : lines.Max(r => r.Main);
        var contentCross = lines.Sum(r => r.Cross) + GapTotal(l.CrossGap, lines.Count);
        if (hugMain) SetMain(node, horizontal, Clamp(node, contentMain + mainPadding, horizontal));
        if (hugCross) SetMain(node, !horizontal, Clamp(node, contentCross + crossPadding, !horizontal));
        available = Math.Max(0, Main(node, horizontal) - mainPadding);
        availableCross = Math.Max(0, Cross(node, horizontal) - crossPadding);
        if (lines.Count == 1 && !l.Wrap) lines[0].Cross = availableCross;
        var crossCursor = horizontal ? Positive(l.PaddingTop) : Positive(l.PaddingLeft);
        foreach (var row in lines)
        {
            var remaining = Math.Max(0, available - row.Main);
            var gap = l.Gap; var offset = AlignOffset(l.PrimaryAlignment, remaining);
            switch (l.Distribution)
            {
                case LayoutDistribution.SpaceBetween when row.Nodes.Count > 1: gap += remaining / (row.Nodes.Count - 1); offset = 0; break;
                case LayoutDistribution.SpaceAround: gap += remaining / row.Nodes.Count; offset = remaining / row.Nodes.Count / 2; break;
                case LayoutDistribution.SpaceEvenly: gap += remaining / (row.Nodes.Count + 1); offset = remaining / (row.Nodes.Count + 1); break;
            }
            var cursor = (horizontal ? Positive(l.PaddingLeft) : Positive(l.PaddingTop)) + offset;
            foreach (var child in row.Nodes)
            {
                if (l.Wrap && (Fill(child, !horizontal) || l.Alignment == LayoutAlignment.Stretch))
                {
                    var old = Cross(child, horizontal);
                    SetMain(child, !horizontal, Clamp(child, row.Cross, !horizontal));
                    if (Math.Abs(old - Cross(child, horizontal)) > Epsilon)
                        ArrangeNode(child, context, horizontal ? Fill(child, true) && !hugMain : true, horizontal ? true : Fill(child, false) && !hugMain);
                }
                var cross = crossCursor + AlignOffset(l.Alignment, row.Cross - Cross(child, horizontal));
                if (horizontal) { child.X = cursor; child.Y = cross; }
                else { child.X = cross; child.Y = cursor; }
                cursor += Main(child, horizontal) + gap;
            }
            crossCursor += row.Cross + l.CrossGap;
        }
    }

    private static void ArrangeGrid(DesignNode node, Context context, bool allocatedWidth, bool allocatedHeight)
    {
        var l = node.Layout; var children = FlowChildren(node);
        var px = Positive(l.PaddingLeft) + Positive(l.PaddingRight); var py = Positive(l.PaddingTop) + Positive(l.PaddingBottom);
        if (children.Length == 0) { if (l.HugWidth && !allocatedWidth) node.Width = Clamp(node, px, true); if (l.HugHeight && !allocatedHeight) node.Height = Clamp(node, py, false); return; }
        var columnCount = ColumnCount(l); var cells = PlaceCells(children, columnCount); var rowCount = cells.Max(c => c.Row + c.Rows);
        var columns = MeasureTracks(cells, true, columnCount, l.Columns, context, l.Gap);
        ResolveTracks(columns, l.Columns, node.Width - px - GapTotal(l.Gap, columnCount), l.HugWidth && !allocatedWidth);
        // Allocate widths before measuring row heights, so nested wrapping layouts reflow correctly.
        foreach (var cell in cells)
        {
            var width = Span(columns, cell.Column, cell.Columns, l.Gap);
            if (cell.Node.FillWidth) cell.Node.Width = Clamp(cell.Node, width, true);
            ArrangeNode(cell.Node, context, cell.Node.FillWidth, false);
            context.Desired[cell.Node] = new(cell.Node.Width, cell.Node.Height);
        }
        var rows = MeasureTracks(cells, false, rowCount, l.Rows, context, l.CrossGap);
        ResolveTracks(rows, l.Rows, node.Height - py - GapTotal(l.CrossGap, rowCount), l.HugHeight && !allocatedHeight);
        if (l.HugWidth && !allocatedWidth) node.Width = Clamp(node, columns.Sum() + GapTotal(l.Gap, columnCount) + px, true);
        if (l.HugHeight && !allocatedHeight) node.Height = Clamp(node, rows.Sum() + GapTotal(l.CrossGap, rowCount) + py, false);
        var x = Offsets(columns, l.Gap, Positive(l.PaddingLeft)); var y = Offsets(rows, l.CrossGap, Positive(l.PaddingTop));
        foreach (var cell in cells)
        {
            var n = cell.Node; var width = Span(columns, cell.Column, cell.Columns, l.Gap); var height = Span(rows, cell.Row, cell.Rows, l.CrossGap);
            if (n.FillHeight) n.Height = Clamp(n, height, false);
            ArrangeNode(n, context, n.FillWidth, n.FillHeight);
            n.X = x[cell.Column] + AlignOffset(l.PrimaryAlignment, width - n.Width);
            n.Y = y[cell.Row] + AlignOffset(l.Alignment, height - n.Height);
        }
    }

    private static Cell[] PlaceCells(DesignNode[] children, int columns)
    {
        var result = new List<Cell>(children.Length); var occupied = new HashSet<(int, int)>(); var cursor = 0;
        foreach (var child in children)
        {
            var spanX = Math.Clamp(child.ColumnSpan, 1, columns); var spanY = Math.Clamp(child.RowSpan, 1, 128);
            var col = child.GridColumn < 0 ? -1 : Math.Clamp(child.GridColumn, 0, columns - spanX);
            var row = child.GridRow < 0 ? -1 : Math.Clamp(child.GridRow, 0, 10000);
            var candidate = row >= 0 ? row * columns : col >= 0 ? col : cursor;
            while (true)
            {
                var r = candidate / columns; var c = col >= 0 ? col : candidate % columns;
                if (c + spanX <= columns && Free(c, r, spanX, spanY)) { col = c; row = r; break; }
                candidate += child.GridColumn >= 0 ? columns : 1;
                if (candidate > 20_000_000) throw new InvalidOperationException("Grid placement limit exceeded.");
            }
            for (var r = row; r < row + spanY; r++) for (var c = col; c < col + spanX; c++) occupied.Add((c, r));
            result.Add(new(child, col, row, spanX, spanY)); cursor = Math.Max(cursor, row * columns + col + spanX);
        }
        return result.ToArray();
        bool Free(int col, int row, int spanX, int spanY)
        { for (var r = row; r < row + spanY; r++) for (var c = col; c < col + spanX; c++) if (occupied.Contains((c, r))) return false; return true; }
    }

    private static double[] MeasureTracks(Cell[] cells, bool horizontal, int count, List<GridTrack> definitions, Context context, double gap)
    {
        var sizes = new double[count];
        for (var i = 0; i < count; i++) { var d = Track(definitions, i); sizes[i] = d.Sizing == GridTrackSizing.Fixed ? Math.Clamp(d.Value, d.Min, Math.Max(d.Min, d.Max)) : d.Min; }
        foreach (var cell in cells.OrderBy(c => horizontal ? c.Columns : c.Rows))
        {
            var start = horizontal ? cell.Column : cell.Row; var span = horizontal ? cell.Columns : cell.Rows;
            var desired = Main(context.Desired[cell.Node], horizontal);
            var deficit = desired - Span(sizes, start, span, gap);
            if (deficit <= 0) continue;
            var flexible = Enumerable.Range(start, span).Where(i => Track(definitions, i).Sizing != GridTrackSizing.Fixed).ToArray();
            foreach (var i in flexible) sizes[i] = Math.Min(Track(definitions, i).Max, sizes[i] + deficit / flexible.Length);
        }
        return sizes;
    }

    private static void ResolveTracks(double[] sizes, List<GridTrack> definitions, double budget, bool hug)
    {
        if (hug) return;
        var flexible = Enumerable.Range(0, sizes.Length).Where(i => Track(definitions, i).Sizing == GridTrackSizing.Fraction).ToArray();
        var fixedTotal = Enumerable.Range(0, sizes.Length).Where(i => Track(definitions, i).Sizing != GridTrackSizing.Fraction).Sum(i => sizes[i]);
        var distributed = Distribute(budget - fixedTotal, flexible.Select(i => Track(definitions, i).Min).ToArray(), flexible.Select(i => Track(definitions, i).Max).ToArray(), flexible.Select(i => Math.Max(Epsilon, Track(definitions, i).Value)).ToArray());
        for (var i = 0; i < flexible.Length; i++) sizes[flexible[i]] = distributed[i];
    }

    /// <summary>Bounded water-filling. Frozen min/max items are removed before redistributing the remainder.</summary>
    private static double[] Distribute(double budget, double[] minimum, double[] maximum, double[]? weights = null)
    {
        var sizes = new double[minimum.Length]; var active = Enumerable.Range(0, sizes.Length).ToHashSet();
        budget = Math.Max(budget, minimum.Sum());
        while (active.Count > 0)
        {
            var weight = active.Sum(i => weights?[i] ?? 1);
            var lower = active.Where(i => budget * (weights?[i] ?? 1) / weight < minimum[i]).ToArray();
            if (lower.Length > 0) { foreach (var i in lower) { sizes[i] = minimum[i]; budget -= sizes[i]; active.Remove(i); } continue; }
            var upper = active.Where(i => budget * (weights?[i] ?? 1) / weight > maximum[i]).ToArray();
            if (upper.Length > 0) { foreach (var i in upper) { sizes[i] = maximum[i]; budget -= sizes[i]; active.Remove(i); } continue; }
            foreach (var i in active) sizes[i] = budget * (weights?[i] ?? 1) / weight;
            break;
        }
        return sizes;
    }

    public static void Resize(DesignNode node, double width, double height)
    {
        width = Clamp(node, width, true); height = Clamp(node, height, false);
        var oldW = Math.Max(1, node.Width); var oldH = Math.Max(1, node.Height);
        foreach (var child in node.Children)
        {
            if (node.Layout.Direction != LayoutDirection.None && !child.AbsolutePosition) continue;
            var (x, w) = Constrain(child.X, child.Width, oldW, width, child.HorizontalConstraint);
            var (y, h) = Constrain(child.Y, child.Height, oldH, height, child.VerticalConstraint);
            child.X = x; child.Y = y; Resize(child, w, h);
        }
        node.Width = width; node.Height = height; Arrange(node);
    }
    private static (double Position, double Size) Constrain(double p, double size, double oldSize, double newSize, AxisConstraint mode) => mode switch
    {
        AxisConstraint.End => (p + newSize - oldSize, size),
        AxisConstraint.Center => (p + (newSize - oldSize) / 2, size),
        AxisConstraint.Stretch => (p, Math.Max(1, size + newSize - oldSize)),
        AxisConstraint.Scale => (p * newSize / oldSize, Math.Max(1, size * newSize / oldSize)),
        _ => (p, size)
    };
    private static DesignNode[] FlowChildren(DesignNode node) => node.Children.Where(c => c.Visible && !c.AbsolutePosition).ToArray();
    private static GridTrack Track(List<GridTrack> tracks, int i) => i < tracks.Count ? tracks[i] : DefaultTrack;
    private static readonly GridTrack DefaultTrack = new();
    private static int ColumnCount(AutoLayout l) => Math.Clamp(l.Columns.Count > 0 ? l.Columns.Count : l.GridColumns, 1, 128);
    private static double[] Offsets(double[] tracks, double gap, double start) { var result = new double[tracks.Length]; for (var i = 0; i < tracks.Length; i++) { result[i] = start; start += tracks[i] + gap; } return result; }
    private static double Span(double[] tracks, int start, int count, double gap) { var result = GapTotal(gap, count); for (var i = start; i < start + count; i++) result += tracks[i]; return result; }
    private static double GapTotal(double gap, int count) => double.IsFinite(gap) ? gap * Math.Max(0, count - 1) : 0;
    private static double Positive(double n) => double.IsFinite(n) ? Math.Max(0, n) : 0;
    private static double AlignOffset(LayoutAlignment alignment, double space) => alignment == LayoutAlignment.Center ? space / 2 : alignment == LayoutAlignment.End ? space : 0;
    private static double Main(Vec2 p, bool horizontal) => horizontal ? p.X : p.Y;
    private static double Cross(Vec2 p, bool horizontal) => horizontal ? p.Y : p.X;
    private static double Main(DesignNode n, bool horizontal) => horizontal ? n.Width : n.Height;
    private static double Cross(DesignNode n, bool horizontal) => horizontal ? n.Height : n.Width;
    private static bool Fill(DesignNode n, bool horizontal) => horizontal ? n.FillWidth : n.FillHeight;
    private static double Minimum(DesignNode n, bool horizontal) => Math.Max(1, horizontal ? n.MinWidth : n.MinHeight);
    private static double Maximum(DesignNode n, bool horizontal) => Math.Max(Minimum(n, horizontal), horizontal ? n.MaxWidth : n.MaxHeight);
    private static double Clamp(DesignNode n, double value, bool horizontal) => Math.Clamp(double.IsFinite(value) ? value : Minimum(n, horizontal), Minimum(n, horizontal), Maximum(n, horizontal));
    private static void SetMain(DesignNode n, bool horizontal, double value) { if (horizontal) n.Width = value; else n.Height = value; }
}

public readonly record struct SnapLine(bool Horizontal, double Position, double Start, double End);
public readonly record struct SnapResult(Vec2 Correction, IReadOnlyList<SnapLine> Lines);

public static class SnapEngine
{
    public static SnapResult Snap(RectD moving, IEnumerable<RectD> targets, double tolerance, IEnumerable<Guide>? guides = null)
    {
        var x = new[] { moving.X, moving.Center.X, moving.Right };
        var y = new[] { moving.Y, moving.Center.Y, moving.Bottom };
        double dx = 0, dy = 0, bx = tolerance + 1, by = tolerance + 1;
        SnapLine? lx = null, ly = null;
        foreach (var target in targets)
        {
            foreach (var a in x) foreach (var v in new[] { target.X, target.Center.X, target.Right })
                if (Math.Abs(v - a) < bx && Math.Abs(v - a) <= tolerance) { bx = Math.Abs(v - a); dx = v - a; lx = new(false, v, Math.Min(moving.Y, target.Y), Math.Max(moving.Bottom, target.Bottom)); }
            foreach (var a in y) foreach (var v in new[] { target.Y, target.Center.Y, target.Bottom })
                if (Math.Abs(v - a) < by && Math.Abs(v - a) <= tolerance) { by = Math.Abs(v - a); dy = v - a; ly = new(true, v, Math.Min(moving.X, target.X), Math.Max(moving.Right, target.Right)); }
        }
        foreach (var guide in guides ?? [])
        {
            foreach (var a in guide.Horizontal ? y : x)
            {
                var d = guide.Position - a;
                if (guide.Horizontal && Math.Abs(d) < by && Math.Abs(d) <= tolerance) { dy = d; by = Math.Abs(d); ly = new(true, guide.Position, moving.X - 100, moving.Right + 100); }
                if (!guide.Horizontal && Math.Abs(d) < bx && Math.Abs(d) <= tolerance) { dx = d; bx = Math.Abs(d); lx = new(false, guide.Position, moving.Y - 100, moving.Bottom + 100); }
            }
        }
        var lines = new List<SnapLine>(); if (lx.HasValue) lines.Add(lx.Value); if (ly.HasValue) lines.Add(ly.Value);
        return new(new(dx, dy), lines);
    }
}
