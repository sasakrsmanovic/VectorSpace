using SkiaSharp;
using VectorSpace.Skia;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    private void Paint(SKCanvas canvas, Size size)
    {
        if (IsPresenting) return;
        if (Session is not { } editor || size.Width < 1 || size.Height < 1) return;
        canvas.Save(); canvas.ClipRect(new(0, 0, (float)size.Width, (float)size.Height));
        using var background = new SKPaint { Color = SceneRenderer.Color(IsPresenting ? "#252525" : editor.Page.Background) }; canvas.DrawRect(new SKRect(0, 0, (float)size.Width, (float)size.Height), background);
        var viewport = editor.Viewport; var zoom = (float)viewport.Zoom;
        canvas.Save(); canvas.Translate((float)viewport.Pan.X, (float)viewport.Pan.Y); canvas.Scale(zoom);
        if (editor.GridVisible && !IsPresenting) DrawGrid(canvas, size);
        Renderer.Outlines = editor.OutlinesVisible;
        var worldRect = new RectD(-viewport.Pan.X / zoom, -viewport.Pan.Y / zoom, size.Width / zoom, size.Height / zoom);
        Renderer.Draw(canvas, editor.Page.Nodes, worldRect);
        if (!IsPresenting)
        {
            using var guidePaint = new SKPaint { Color = new(242, 72, 34, 170), StrokeWidth = 1 / zoom, IsAntialias = true };
            foreach (var guide in editor.Page.Guides)
            {
                if (guide.Horizontal) canvas.DrawLine((float)worldRect.X, (float)guide.Position, (float)worldRect.Right, (float)guide.Position, guidePaint);
                else canvas.DrawLine((float)guide.Position, (float)worldRect.Y, (float)guide.Position, (float)worldRect.Bottom, guidePaint);
            }
            using var selectionPaint = new SKPaint { Color = new(13, 153, 255), StrokeWidth = 1 / zoom, Style = SKPaintStyle.Stroke, IsAntialias = true };
            if (_hover is not null && !editor.SelectedIds.Contains(_hover.Id) && _gesture == Gesture.None)
            {
                canvas.Save(); canvas.Concat(SceneRenderer.Matrix(_hover.WorldMatrix));
                if (_hover.Kind == NodeKind.Text) canvas.DrawRect(SceneRenderer.Rect(_hover.LocalBounds), selectionPaint); else canvas.DrawPath(Renderer.Geometry(_hover), selectionPaint); canvas.Restore();
            }
            using var snapPaint = new SKPaint { Color = new(242, 72, 34), StrokeWidth = 1 / zoom, IsAntialias = true };
            foreach (var line in _snapLines)
            {
                if (line.Horizontal) canvas.DrawLine((float)line.Start, (float)line.Position, (float)line.End, (float)line.Position, snapPaint);
                else canvas.DrawLine((float)line.Position, (float)line.Start, (float)line.Position, (float)line.End, snapPaint);
            }
            if (_marquee is { } box)
            {
                using var fill = new SKPaint { Color = new(13, 153, 255, 22) }; canvas.DrawRect(SceneRenderer.Rect(box), fill); canvas.DrawRect(SceneRenderer.Rect(box), selectionPaint);
            }
        }
        canvas.Restore();
        if (!IsPresenting)
        {
            using var labelPaint = new SKPaint { IsAntialias = true, Color = new(110, 110, 110) }; using var font = new SKFont(SKTypeface.Default, 11);
            foreach (var node in editor.Page.Nodes.Where(n => n.Visible && n.IsContainer))
            {
                var p = viewport.WorldToScreen(new(node.WorldBounds.X, node.WorldBounds.Y)); canvas.DrawText(node.Name, (float)p.X, (float)p.Y - 9, font, labelPaint);
            }
            DrawSelection(canvas);
            if (_vectorNode is not null) DrawVertices(canvas, _vectorNode);
            if (_penNode is not null)
            {
                DrawVertices(canvas, _penNode);
                if (_pathPreviewWorld is { } preview && _penNode.Points.Count > 0 && _gesture == Gesture.None)
                {
                    var matrix = PathEditing.PointToWorld(_penNode); var last = _penNode.Points[^1];
                    var a = viewport.WorldToScreen(matrix.Map(last.Position));
                    var b = viewport.WorldToScreen(preview);
                    if (Keyboard.Shift) b = a + DrawingGeometry.ConstrainAngle(b - a);
                    using var path = new SKPath(); path.MoveTo(P(a));
                    if (last.ControlOut is { } control) path.CubicTo(P(viewport.WorldToScreen(matrix.Map(control))), P(b), P(b)); else path.LineTo(P(b));
                    using var pen = new SKPaint { Color = new SKColor(13, 153, 255), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                    canvas.DrawPath(path, pen);
                }
            }
            var comments = editor.Document.Comments.Where(c => c.PageId == editor.Page.Id && !c.Resolved).ToArray();
            for (var i = 0; i < comments.Length; i++)
            {
                var p = viewport.WorldToScreen(comments[i].Anchor); using var pin = new SKPaint { Color = new(13, 153, 255), IsAntialias = true }; canvas.DrawCircle((float)p.X, (float)p.Y, 13, pin); pin.Color = SKColors.White; canvas.DrawText((i + 1).ToString(), (float)p.X - 3, (float)p.Y + 4, font, pin);
            }
            if (editor.RulersVisible) DrawRulers(canvas, size);
            if (ShowDiagnostics)
            {
                using var panel = new SKPaint { Color = new(255, 255, 255, 235), IsAntialias = true };
                var label = $"{Renderer.LastDrawMilliseconds:0.00} ms · {Renderer.RenderedNodes} drawn · {Renderer.CulledNodes} culled · {Renderer.CachedGeometryCount} paths";
                var width = font.MeasureText(label) + 20;
                canvas.DrawRoundRect(new SKRect(24, (float)size.Height - 60, 24 + width, (float)size.Height - 30), 6, 6, panel);
                canvas.DrawText(label, 34, (float)size.Height - 40, font, labelPaint);
            }
        }
        canvas.Restore();
    }
    private void DrawGrid(SKCanvas canvas, Size size)
    {
        if (Session is null) return;
        var v = Session.Viewport; var step = v.Zoom < .2 ? 100 : v.Zoom < .8 ? 32 : 8;
        var left = Math.Floor(-v.Pan.X / v.Zoom / step) * step; var top = Math.Floor(-v.Pan.Y / v.Zoom / step) * step;
        var right = (size.Width - v.Pan.X) / v.Zoom; var bottom = (size.Height - v.Pan.Y) / v.Zoom;
        using var paint = new SKPaint { Color = new(0, 0, 0, 34), IsAntialias = true };
        for (var x = left; x < right; x += step) for (var y = top; y < bottom; y += step) canvas.DrawCircle((float)x, (float)y, (float)(.65 / v.Zoom), paint);
    }
    private Vec2[] GetHandles()
    {
        if (Session is not { } editor || editor.SelectionRoots.Count == 0 || IsPresenting) return [];
        RectD box; Matrix2D matrix;
        if (editor.SelectionRoots.Count == 1) { var node = editor.SelectionRoots[0]; box = node.LocalBounds; matrix = node.WorldMatrix; }
        else { box = editor.SelectionBounds(); matrix = Matrix2D.Identity; }
        var points = new[] { new Vec2(box.X, box.Y), new Vec2(box.Center.X, box.Y), new Vec2(box.Right, box.Y), new Vec2(box.Right, box.Center.Y), new Vec2(box.Right, box.Bottom), new Vec2(box.Center.X, box.Bottom), new Vec2(box.X, box.Bottom), new Vec2(box.X, box.Center.Y), new Vec2(box.Center.X, box.Y - 24 / editor.Viewport.Zoom) };
        return points.Select(p => editor.Viewport.WorldToScreen(matrix.Map(p))).ToArray();
    }
    private void DrawSelection(SKCanvas canvas)
    {
        if (Session is not { } editor || editor.SelectionRoots.Count == 0 || _textEditor is not null) return;
        if (IsVectorEditing) return;
        var points = GetHandles(); if (points.Length < 8) return;
        using var blue = new SKPaint { IsAntialias = true, Color = new(13, 153, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var fill = new SKPaint { IsAntialias = true, Color = SKColors.White };
        if (editor.SelectionRoots.Count > 1)
        {
            foreach (var node in editor.SelectionRoots)
            {
                canvas.Save(); canvas.Translate((float)editor.Viewport.Pan.X, (float)editor.Viewport.Pan.Y); canvas.Scale((float)editor.Viewport.Zoom); canvas.Concat(SceneRenderer.Matrix(node.WorldMatrix));
                blue.StrokeWidth = (float)(1 / editor.Viewport.Zoom); canvas.DrawRect(SceneRenderer.Rect(node.LocalBounds), blue); canvas.Restore();
            }
            blue.StrokeWidth = 1;
        }
        for (var i = 0; i < 8; i += 2) canvas.DrawLine(P(points[i]), P(points[(i + 2) % 8]), blue);
        if (IsImageCropping)
        {
            // Crop mode manipulates image placement, not layer geometry. Replace resize/rotation
            // handles with thirds so the visible affordance matches the active gesture.
            for (var i = 1; i < 3; i++)
            {
                var t = i / 3d; blue.Color = SKColors.White.WithAlpha(150);
                canvas.DrawLine(P(points[0] + (points[2] - points[0]) * t), P(points[6] + (points[4] - points[6]) * t), blue);
                canvas.DrawLine(P(points[0] + (points[6] - points[0]) * t), P(points[2] + (points[4] - points[2]) * t), blue);
            }
            return;
        }
        if (editor.SelectionRoots.Any(n => n.IsEffectivelyLocked)) return;
        canvas.DrawLine(P(points[1]), P(points[8]), blue); canvas.DrawCircle(P(points[8]), 3.5f, fill); canvas.DrawCircle(P(points[8]), 3.5f, blue);
        for (var i = 0; i < 8; i++) { var r = new SKRect((float)points[i].X - 3, (float)points[i].Y - 3, (float)points[i].X + 3, (float)points[i].Y + 3); canvas.DrawRect(r, fill); canvas.DrawRect(r, blue); }
        var bounds = editor.SelectionRoots.Count == 1 ? editor.SelectionRoots[0].LocalBounds : editor.SelectionBounds();
        var text = Numbers.Format(bounds.Width) + " × " + Numbers.Format(bounds.Height);
        using var font = new SKFont(SKTypeface.Default, 10); var width = font.MeasureText(text) + 14; var position = points[5];
        blue.Style = SKPaintStyle.Fill; canvas.DrawRoundRect(new SKRect((float)position.X - width / 2, (float)position.Y + 10, (float)position.X + width / 2, (float)position.Y + 29), 4, 4, blue);
        canvas.DrawText(text, (float)position.X - width / 2 + 7, (float)position.Y + 23, font, fill);
    }
    private void DrawVertices(SKCanvas canvas, DesignNode node)
    {
        if (Session is null) return;
        using var stroke = new SKPaint { IsAntialias = true, Color = new(13, 153, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var fill = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var transform = PathEditing.PointToWorld(node);
        Vec2 Screen(Vec2 p) => Session.Viewport.WorldToScreen(transform.Map(p));
        for (var i = 0; i < node.Points.Count; i++)
        {
            var point = node.Points[i]; var p = Screen(point.Position);
            if (_pointSelection.Contains(i) || node == _penNode && i == node.Points.Count - 1)
            {
                if (point.ControlIn is { } incoming) Handle(incoming);
                if (point.ControlOut is { } outgoing) Handle(outgoing);
            }
            if (p.X < -8 || p.Y < -8 || p.X > ActualWidth + 8 || p.Y > ActualHeight + 8) continue;
            fill.Color = _pointSelection.Contains(i) ? stroke.Color : SKColors.White;
            canvas.DrawCircle(P(p), 4, fill); canvas.DrawCircle(P(p), 4, stroke);
            void Handle(Vec2 control)
            {
                var cp = Screen(control); fill.Color = SKColors.White;
                canvas.DrawLine(P(p), P(cp), stroke); canvas.DrawCircle(P(cp), 3, fill); canvas.DrawCircle(P(cp), 3, stroke);
            }
        }
        if (_pointMarquee is { } marquee)
        {
            fill.Color = new SKColor(13, 153, 255, 22); canvas.DrawRect(SceneRenderer.Rect(marquee), fill); canvas.DrawRect(SceneRenderer.Rect(marquee), stroke);
        }
    }
    private void DrawRulers(SKCanvas canvas, Size size)
    {
        if (Session is null) return;
        using var fill = new SKPaint { Color = new(250, 250, 250) }; canvas.DrawRect(new SKRect(0, 0, (float)size.Width, 20), fill); canvas.DrawRect(new SKRect(0, 0, 20, (float)size.Height), fill);
        using var paint = new SKPaint { Color = new(130, 130, 130), IsAntialias = true, StrokeWidth = 1 }; using var font = new SKFont(SKTypeface.Default, 8);
        var v = Session.Viewport; var step = Math.Pow(10, Math.Ceiling(Math.Log10(70 / v.Zoom))); if (step * v.Zoom > 180) step /= 2;
        for (var x = Math.Floor(-v.Pan.X / v.Zoom / step) * step; v.WorldToScreen(new(x, 0)).X < size.Width; x += step)
        {
            var sx = (float)v.WorldToScreen(new(x, 0)).X; if (sx < 20) continue; canvas.DrawLine(sx, 14, sx, 20, paint); canvas.DrawText(Numbers.Format(x), sx + 3, 10, font, paint);
        }
        for (var y = Math.Floor(-v.Pan.Y / v.Zoom / step) * step; v.WorldToScreen(new(0, y)).Y < size.Height; y += step)
        {
            var sy = (float)v.WorldToScreen(new(0, y)).Y; if (sy < 20) continue; canvas.DrawLine(14, sy, 20, sy, paint); canvas.Save(); canvas.Translate(10, sy - 3); canvas.RotateDegrees(-90); canvas.DrawText(Numbers.Format(y), 0, 0, font, paint); canvas.Restore();
        }
    }
    private static SKPoint P(Vec2 p) => new((float)p.X, (float)p.Y);
}
