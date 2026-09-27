using System.Globalization;
using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Prototyping;

/// <summary>Prepared name/kind/occurrence tree interpolation. Setup allocates once;
/// samples reuse the scene tree. Unsupported paint/text changes cross-fade rather than inventing geometry.</summary>
public sealed class PrototypeTween
{
    private sealed record Pair(DesignNode Working, DesignNode? From, DesignNode To, bool FadeOut);
    private readonly List<Pair> _pairs = [];
    public DesignNode Frame { get; }
    public int MatchedNodes { get; private set; }
    public PrototypeTween(DesignNode from, DesignNode to)
    {
        Frame = DocumentJson.CloneNode(to); Prepare(Frame, from, to);
    }
    private void Prepare(DesignNode working, DesignNode? from, DesignNode to)
    {
        _pairs.Add(new(working, from, to, false));
        if (from is null) return; // Fade an unmatched branch as a whole, not every descendant twice.
        MatchedNodes++;
        var candidates = new Dictionary<(string, NodeKind), Queue<DesignNode>>();
        foreach (var child in from.Children)
        {
            var key = (child.Name, child.Kind);
            if (!candidates.TryGetValue(key, out var queue)) candidates[key] = queue = new();
            queue.Enqueue(child);
        }
        for (var i = 0; i < to.Children.Count; i++)
        {
            var target = to.Children[i]; DesignNode? source = null;
            if (candidates.TryGetValue((target.Name, target.Kind), out var queue) && queue.TryDequeue(out var next)) source = next;
            if (source is not null && !Compatible(source, target)) { Ghost(working, source); source = null; }
            Prepare(working.Children[i], source, target);
        }
        foreach (var queue in candidates.Values) foreach (var source in queue) Ghost(working, source);
    }
    private static bool Compatible(DesignNode from, DesignNode to) =>
        (to.Kind != NodeKind.Text || from.Text == to.Text && from.FontFamily == to.FontFamily && from.FontWeight == to.FontWeight) &&
        (to.Kind != NodeKind.Path || from.PathData == to.PathData && from.Points.Count == 0 && to.Points.Count == 0) &&
        from.Fills.Count == to.Fills.Count && from.Fills.All(f => f.Kind == FillKind.Solid) && to.Fills.All(f => f.Kind == FillKind.Solid);
    private void Ghost(DesignNode parent, DesignNode source)
    {
        var ghost = DocumentJson.CloneNode(source, true); parent.Add(ghost);
        _pairs.Add(new(ghost, source, source, true));
    }
    public DesignNode Sample(double progress)
    {
        if (!double.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
        var t = Math.Clamp(progress, 0, 1);
        foreach (var pair in _pairs)
        {
            var w = pair.Working; var b = pair.To; var a = pair.From;
            if (pair.FadeOut) { w.Opacity = b.Opacity * (1 - t); continue; }
            if (a is null) { w.Opacity = b.Opacity * t; continue; }
            w.X = Lerp(a.X, b.X, t); w.Y = Lerp(a.Y, b.Y, t);
            w.Width = Lerp(a.Width, b.Width, t); w.Height = Lerp(a.Height, b.Height, t);
            var angle = ((b.Rotation - a.Rotation) % 360 + 540) % 360 - 180;
            w.Rotation = a.Rotation + angle * t;
            w.CornerRadius = Lerp(a.CornerRadius, b.CornerRadius, t); w.Opacity = Lerp(a.Visible ? a.Opacity : 0, b.Visible ? b.Opacity : 0, t);
            w.Visible = a.Visible || b.Visible; w.FontSize = Lerp(a.FontSize, b.FontSize, t); w.LetterSpacing = Lerp(a.LetterSpacing, b.LetterSpacing, t);
            w.FlipX = t < .5 ? a.FlipX : b.FlipX; w.FlipY = t < .5 ? a.FlipY : b.FlipY;
            for (var i = 0; i < Math.Min(a.Fills.Count, b.Fills.Count); i++)
            {
                var af = a.Fills[i]; var bf = b.Fills[i]; var wf = w.Fills[i];
                wf.Opacity = Lerp(af.Visible ? af.Opacity : 0, bf.Visible ? bf.Opacity : 0, t); wf.Visible = af.Visible || bf.Visible;
                wf.Color = Rgb(af.Color, bf.Color, t);
            }
        }
        return Frame;
    }
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static string Rgb(string a, string b, double t)
    {
        if (a == b) return b;
        if (a.Length != 7 || b.Length != 7 || !uint.TryParse(a.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var av) || !uint.TryParse(b.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bv)) return t < .5 ? a : b;
        var r = (int)Math.Round(Lerp((av >> 16) & 255, (bv >> 16) & 255, t));
        var g = (int)Math.Round(Lerp((av >> 8) & 255, (bv >> 8) & 255, t));
        var bl = (int)Math.Round(Lerp(av & 255, bv & 255, t));
        return $"#{r:X2}{g:X2}{bl:X2}";
    }
}
