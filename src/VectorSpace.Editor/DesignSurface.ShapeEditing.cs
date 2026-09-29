using System.Text.Json;
using SkiaSharp;
using VectorSpace.Skia;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    private string? _shapeNodeId;
    private DesignDocument? _shapeDocument;
    private ShapeGesture? _shapeGesture;
    public bool IsShapeEditing => _shapeNodeId is not null;
    public void BeginShapeEdit()
    {
        if (Session is not { } editor || IsPresenting) return;
        FinishTextEdit(true); FinishPath(false); CancelGesture();
        if (editor.SelectionRoots.Count != 1 || editor.Primary is not { } node || !CanEditShape(node))
            throw new InvalidOperationException("Select one unlocked ellipse or corner-bearing shape outside an instance.");
        if (editor.SharedHistory?.CanEdit("Edit shape") == false) throw new InvalidOperationException("This shared file is read-only for shape editing.");
        editor.Tool = EditorTool.Move;
        _shapeNodeId = node.Id; _shapeDocument = editor.Document;
        StatusChanged?.Invoke(node.Kind == NodeKind.Ellipse
            ? "Arc editing · drag Start, End or inner radius · Shift snaps angles · Enter finishes · Escape cancels"
            : "Corner editing · drag a circular grip · Alt edits one corner · Enter finishes · Escape cancels");
        editor.Notify(EditorChangeKind.Selection); FocusCanvas(); RequestFrame();
    }
    private static bool CanEditShape(DesignNode node)
    {
        if (node.IsEffectivelyLocked || node.Kind != NodeKind.Ellipse && !ShapeGeometry.HasCorners(node)) return false;
        for (var p = node; p is not null; p = p.Parent) if (p.Kind == NodeKind.Instance) return false;
        return true;
    }
    private bool ShapeTarget(out DesignNode node)
    {
        node = null!;
        if (_shapeNodeId is null || Session is not { } editor || !ReferenceEquals(editor.Document, _shapeDocument) ||
            editor.SelectionRoots.Count != 1 || editor.Primary is not { } selected || selected.Id != _shapeNodeId || !CanEditShape(selected))
        {
            _shapeNodeId = null; _shapeDocument = null; _shapeGesture = null; return false;
        }
        node = selected; return true;
    }
    public void EndShapeEdit() => FinishShapeEdit(false);
    private void FinishShapeEdit(bool cancel)
    {
        var gesture = _gesture;
        _shapeNodeId = null; _shapeDocument = null; _shapeGesture = null;
        if (gesture == Gesture.Shape)
        {
            _gesture = Gesture.None;
            if (cancel) Session?.CancelInteraction(); else Session?.CommitInteraction();
        }
        _canvas.ReleasePointerCaptures(); Session?.Notify(EditorChangeKind.Selection); RequestFrame();
    }
    public bool HandleShapeKey(VirtualKey key, bool control, bool shift, bool alt)
    {
        if (!IsShapeEditing || control) return false;
        if (key == VirtualKey.Escape) { FinishShapeEdit(true); return true; }
        if (key == VirtualKey.Enter) { EndShapeEdit(); return true; }
        return false;
    }
    private Vec2[] ShapeHandles(DesignNode node)
    {
        var zoom = Session!.Viewport.Zoom;
        if (node.Kind == NodeKind.Ellipse)
        {
            var a = node.Arc ?? new();
            var start = ShapeGeometry.ArcPoint(node, a.StartDegrees);
            var end = ShapeGeometry.ArcPoint(node, a.StartDegrees + a.SweepDegrees);
            // A full sweep has coincident geometric endpoints; separate its End grip radially.
            if (start.DistanceTo(end) * zoom < 18)
            {
                var radial = end - new Vec2(node.Width / 2, node.Height / 2);
                end += radial * (18 / zoom / Math.Max(radial.Length, 1e-9));
            }
            var inner = ShapeGeometry.ArcPoint(node, a.StartDegrees + a.SweepDegrees / 2, a.InnerRadius);
            return a.Open ? [start, end] : [start, end, inner];
        }
        var r = node.EffectiveCorners;
        var inset = Math.Min(Math.Min(node.Width, node.Height) / 3, 12 / zoom);
        var tl = Math.Max(r.TopLeft, inset); var tr = Math.Max(r.TopRight, inset);
        var br = Math.Max(r.BottomRight, inset); var bl = Math.Max(r.BottomLeft, inset);
        return [new(tl, tl), new(node.Width - tr, tr), new(node.Width - br, node.Height - br), new(bl, node.Height - bl)];
    }
    private bool PressShapeEdit(Vec2 screen, Vec2 world)
    {
        if (!ShapeTarget(out var node)) return false;
        var handles = ShapeHandles(node);
        for (var i = handles.Length - 1; i >= 0; i--)
        {
            if (Session!.Viewport.WorldToScreen(node.WorldMatrix.Map(handles[i])).DistanceTo(screen) > 8) continue;
            Session.BeginInteraction(node.Kind == NodeKind.Ellipse ? "Edit ellipse arc" : "Edit shape corners");
            _shapeGesture = new(node, i, world); _gesture = Gesture.Shape; return true;
        }
        EndShapeEdit(); return true;
    }
    private void MoveShapeEdit(Vec2 world, bool shift, bool alt)
    {
        if (_shapeGesture is not { } gesture || !ShapeTarget(out var node)) return;
        gesture.Apply(node, world, alt, shift); Session!.Preview(false);
    }
    private void DrawShapeHandles(SKCanvas canvas)
    {
        if (!ShapeTarget(out var node)) return;
        var handles = ShapeHandles(node); var viewport = Session!.Viewport;
        using var blue = new SKPaint { IsAntialias = true, Color = new(13, 153, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var fill = new SKPaint { IsAntialias = true, Color = SKColors.White };
        using var font = new SKFont(SKTypeface.Default, 10);
        var center = viewport.WorldToScreen(node.WorldMatrix.Map(new Vec2(node.Width / 2, node.Height / 2)));
        for (var i = 0; i < handles.Length; i++)
        {
            var point = viewport.WorldToScreen(node.WorldMatrix.Map(handles[i]));
            if (node.Kind == NodeKind.Ellipse) { blue.Color = new(13, 153, 255, 100); canvas.DrawLine(P(center), P(point), blue); blue.Color = new(13, 153, 255); }
            canvas.DrawCircle(P(point), 5, fill); canvas.DrawCircle(P(point), 5, blue);
            if (node.Kind == NodeKind.Ellipse)
            {
                var label = i == 0 ? "Start" : i == 1 ? "End" : "Inner";
                blue.Style = SKPaintStyle.Fill; canvas.DrawText(label, (float)point.X + 9, (float)point.Y - 7, font, blue); blue.Style = SKPaintStyle.Stroke;
            }
        }
    }
    /// <summary>Read-only gesture metadata for host diagnostics and actual pointer-based acceptance.</summary>
    public void WriteShapeDiagnostics(Utf8JsonWriter json)
    {
        json.WriteBoolean("shapeEditing", IsShapeEditing);
        var node = Session?.Primary;
        if (node is not null)
        {
            json.WriteString("booleanOperation", node.Boolean?.ToString());
            if (node.Corners is { } r) { json.WriteStartArray("corners"); for (var i = 0; i < 4; i++) json.WriteNumberValue(r.At(i)); json.WriteEndArray(); }
            json.WriteNumber("cornerRadius", node.CornerRadius);
            if (node.Arc is { } a) { json.WriteNumber("arcStart", a.StartDegrees); json.WriteNumber("arcSweep", a.SweepDegrees); json.WriteNumber("arcInner", a.InnerRadius); json.WriteBoolean("arcOpen", a.Open); }
            json.WriteNumber("nativeCommands", node.Commands?.Count ?? 0);
        }
        json.WriteStartArray("shapeHandles");
        if (ShapeTarget(out var target))
            foreach (var local in ShapeHandles(target))
            {
                var p = Session!.Viewport.WorldToScreen(target.WorldMatrix.Map(local));
                json.WriteStartObject(); json.WriteNumber("x", p.X); json.WriteNumber("y", p.Y); json.WriteEndObject();
            }
        json.WriteEndArray();
    }
}
