using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Prototyping;

namespace VectorSpace.Skia;

/// <summary>Skia presentation compositor. Coordinates are local to the active flow frame; the host
/// applies fit/zoom once. Does not own the supplied renderer or mutate source/playback documents.</summary>
public sealed class PrototypeSceneRenderer(SceneRenderer renderer)
{
    public void Draw(SKCanvas canvas, PrototypeSession session)
    {
        var view = session.View;
        canvas.Save();
        try
        {
            canvas.ClipRect(new(0, 0, (float)view.Frame.Width, (float)view.Frame.Height));
            if (session.Animation is not { } a) { DrawView(canvas, view); return; }
            var t = a.Progress(session.ClockMilliseconds);
            if (a.Kind == PrototypeTransitionKind.SmartAnimate && a.Tween is { } tween)
            {
                var scroll = a.From.Scroll + (a.To.Scroll - a.From.Scroll) * t;
                renderer.DrawPrototypeFrame(canvas, tween.Sample(t), scroll); return;
            }
            if (a.Kind == PrototypeTransitionKind.Dissolve)
            {
                DrawView(canvas, a.From);
                using var alpha = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(t * 255)) };
                canvas.SaveLayer(alpha); DrawView(canvas, a.To); canvas.Restore(); return;
            }
            var direction = a.Direction switch
            {
                PrototypeDirection.Left => new Vec2(-view.Frame.Width, 0), PrototypeDirection.Up => new Vec2(0, -view.Frame.Height),
                PrototypeDirection.Down => new Vec2(0, view.Frame.Height), _ => new Vec2(view.Frame.Width, 0)
            };
            canvas.Save();
            if (a.Kind == PrototypeTransitionKind.Push) canvas.Translate((float)(-direction.X * t), (float)(-direction.Y * t));
            DrawView(canvas, a.From); canvas.Restore();
            canvas.Save(); canvas.Translate((float)(direction.X * (1 - t)), (float)(direction.Y * (1 - t))); DrawView(canvas, a.To); canvas.Restore();
        }
        finally { canvas.Restore(); }
    }
    public void DrawView(SKCanvas canvas, PrototypeView view)
    {
        renderer.DrawPrototypeFrame(canvas, view.Frame, view.Scroll);
        for (var i = 0; i < view.Overlays.Count; i++)
        {
            var overlay = view.Overlays[i]; var frame = view.OverlayFrames[i];
            using var paint = new SKPaint { Color = SceneRenderer.Color(overlay.Backdrop, overlay.BackdropOpacity) };
            canvas.DrawRect(new(0, 0, (float)view.Frame.Width, (float)view.Frame.Height), paint);
            var offset = PrototypeGeometry.OverlayPosition(view.Frame, frame, overlay);
            canvas.Save(); canvas.Translate((float)offset.X, (float)offset.Y); renderer.DrawPrototypeFrame(canvas, frame, overlay.Scroll); canvas.Restore();
        }
    }
    public DesignNode? Hit(PrototypeSession session, Vec2 point, out bool outsideOverlay)
    {
        var view = session.View; outsideOverlay = false;
        if (!view.Frame.LocalBounds.Contains(point)) return null;
        if (view.Overlays.Count == 0) return renderer.HitPrototypeFrame(view.Frame, point, view.Scroll);
        var overlay = view.Overlays[^1]; var frame = view.OverlayFrames[^1];
        var local = point - PrototypeGeometry.OverlayPosition(view.Frame, frame, overlay);
        var result = renderer.HitPrototypeFrame(frame, local, overlay.Scroll);
        outsideOverlay = result is null; return result;
    }
}
