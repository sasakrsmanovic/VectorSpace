using VectorSpace.Core;

namespace VectorSpace.Prototyping;

public sealed record PrototypeOverlay(string FrameId, PrototypePlacement Placement, Vec2 Offset, string Backdrop, double BackdropOpacity, bool CloseOnOutsideClick, Vec2 Scroll);
public sealed class PrototypeView
{
    public DesignDocument Document { get; }
    public string FrameId { get; }
    public Vec2 Scroll { get; }
    public IReadOnlyList<PrototypeOverlay> Overlays { get; }
    public DesignNode Frame { get; }
    public IReadOnlyList<DesignNode> OverlayFrames { get; }
    public DesignNode InputRoot => Overlays.Count == 0 ? Frame : OverlayFrames[^1];
    public Vec2 InputScroll => Overlays.Count == 0 ? Scroll : Overlays[^1].Scroll;
    public PrototypeView(DesignDocument document, string frameId, Vec2 scroll, IReadOnlyList<PrototypeOverlay> overlays)
    {
        Document = document; FrameId = frameId; Scroll = scroll; Overlays = overlays;
        // Resolve once at a state boundary, not through O(N) document searches every animation sample.
        Frame = document.Find(frameId) ?? throw new InvalidOperationException("The prototype frame no longer exists.");
        OverlayFrames = Array.AsReadOnly(overlays.Select(o => document.Find(o.FrameId) ?? throw new InvalidOperationException("The prototype overlay no longer exists.")).ToArray());
    }
}

public sealed class PrototypeAnimation
{
    public PrototypeView From { get; }
    public PrototypeView To { get; }
    public PrototypeTransitionKind Kind { get; }
    public PrototypeDirection Direction { get; }
    public PrototypeEasing Easing { get; }
    public double StartedAt { get; }
    public double Duration { get; }
    public PrototypeTween? Tween { get; }
    public PrototypeAnimation(PrototypeView from, PrototypeView to, PrototypeTransition transition, double now)
    {
        From = from; To = to; Kind = transition.Kind; Direction = transition.Direction; Easing = transition.Easing;
        StartedAt = now; Duration = transition.DurationMilliseconds;
        if (Kind == PrototypeTransitionKind.SmartAnimate)
        {
            if (from.Overlays.Count == 0 && to.Overlays.Count == 0) Tween = new(from.Frame, to.Frame);
            else Kind = PrototypeTransitionKind.Dissolve;
        }
    }
    public double Progress(double now) => Ease(Math.Clamp(Duration <= 0 ? 1 : (now - StartedAt) / Duration, 0, 1), Easing);
    public static double Ease(double t, PrototypeEasing easing) => easing switch
    {
        PrototypeEasing.EaseIn => t * t * t,
        PrototypeEasing.EaseOut => 1 - Math.Pow(1 - t, 3),
        PrototypeEasing.EaseInOut => t < .5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2,
        _ => t
    };
}

public static class PrototypeGeometry
{
    public static Vec2 OverlayPosition(DesignNode frame, DesignNode overlay, PrototypeOverlay settings)
    {
        var x = settings.Placement switch
        {
            PrototypePlacement.TopLeft or PrototypePlacement.BottomLeft or PrototypePlacement.Manual => 0,
            PrototypePlacement.TopRight or PrototypePlacement.BottomRight => frame.Width - overlay.Width,
            _ => (frame.Width - overlay.Width) / 2
        };
        var y = settings.Placement switch
        {
            PrototypePlacement.TopLeft or PrototypePlacement.TopCenter or PrototypePlacement.TopRight or PrototypePlacement.Manual => 0,
            PrototypePlacement.BottomLeft or PrototypePlacement.BottomCenter or PrototypePlacement.BottomRight => frame.Height - overlay.Height,
            _ => (frame.Height - overlay.Height) / 2
        };
        return new Vec2(x, y) + settings.Offset;
    }
    public static Vec2 ClampScroll(DesignNode frame, Vec2 scroll)
    {
        var right = frame.Width; var bottom = frame.Height;
        foreach (var child in frame.Children.Where(n => n.Visible))
        {
            var bounds = child.LocalMatrix.Map(child.LocalBounds);
            right = Math.Max(right, bounds.Right); bottom = Math.Max(bottom, bounds.Bottom);
        }
        return new(frame.PrototypeOverflow is PrototypeOverflow.Horizontal or PrototypeOverflow.Both ? Math.Clamp(scroll.X, 0, right - frame.Width) : 0,
            frame.PrototypeOverflow is PrototypeOverflow.Vertical or PrototypeOverflow.Both ? Math.Clamp(scroll.Y, 0, bottom - frame.Height) : 0);
    }
}
