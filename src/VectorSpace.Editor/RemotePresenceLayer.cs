using Microsoft.UI.Xaml;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using VectorSpace.Core;
using VectorSpace.Editing;

namespace VectorSpace.Editor;

public sealed record RemotePeer(string Id, string Name, string Color, string PageId, Vec2? Cursor, IReadOnlyList<string> Selection);

/// <summary>Network-independent, noninteractive presence overlay. Cursor-only updates reuse
/// prepared selection geometry and do not invalidate the editor's document canvas.</summary>
public sealed class RemotePresenceLayer : SKCanvasElement, IDisposable
{
    private sealed record Outline(string Peer, Vec2[] Corners);
    private IReadOnlyList<RemotePeer> _peers = [];
    private readonly List<Outline> _outlines = [];
    private DesignDocument? _document;
    private string _selectionSignature = "";
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKFont _font = new(SKTypeface.Default, 11);
    private readonly SKPath _cursor = new();
    private bool _disposed;
    public EditorSession? Session { get; set; }
    public RemotePresenceLayer()
    {
        IsHitTestVisible = false; HorizontalAlignment = HorizontalAlignment.Stretch; VerticalAlignment = VerticalAlignment.Stretch;
        _cursor.MoveTo(0, 0); _cursor.LineTo(0, 17); _cursor.LineTo(5, 12); _cursor.LineTo(10, 19);
        _cursor.LineTo(13, 17); _cursor.LineTo(8, 10); _cursor.LineTo(16, 10); _cursor.Close();
    }
    public void Update(IReadOnlyList<RemotePeer> peers)
    {
        if (_disposed) return;
        _peers = peers;
        if (Session is { } session)
        {
            var signature = session.Page.Id + "|" + string.Join('|', peers.Where(p => p.PageId == session.Page.Id).Select(p => p.Id + ":" + string.Join(',', p.Selection)));
            if (!ReferenceEquals(_document, session.Document) || signature != _selectionSignature)
            {
                _document = session.Document; _selectionSignature = signature; _outlines.Clear();
                var requested = peers.Where(p => p.PageId == session.Page.Id).SelectMany(p => p.Selection).ToHashSet(StringComparer.Ordinal);
                var nodes = session.Page.AllNodes().Where(n => requested.Contains(n.Id)).ToDictionary(n => n.Id, StringComparer.Ordinal);
                foreach (var peer in peers.Where(p => p.PageId == session.Page.Id))
                    foreach (var id in peer.Selection)
                        if (nodes.TryGetValue(id, out var n) && n.Visible)
                        {
                            var m = n.WorldMatrix;
                            _outlines.Add(new(peer.Id, [m.Map(Vec2.Zero), m.Map(new Vec2(n.Width, 0)), m.Map(new Vec2(n.Width, n.Height)), m.Map(new Vec2(0, n.Height))]));
                        }
            }
        }
        Invalidate();
    }
    public void DocumentChanged() { _document = null; Update(_peers); }
    protected override void RenderOverride(SKCanvas canvas, Windows.Foundation.Size area)
    {
        if (_disposed || Session is not { } session) return;
        canvas.Clear(SKColors.Transparent);
        canvas.Save(); canvas.ClipRect(new(0, 0, (float)area.Width, (float)area.Height));
        foreach (var peer in _peers)
        {
            if (peer.PageId != session.Page.Id) continue;
            _paint.Color = SKColor.TryParse(peer.Color, out var color) ? color : SKColors.CornflowerBlue;
            _paint.Style = SKPaintStyle.Stroke; _paint.StrokeWidth = 1.5f;
            foreach (var outline in _outlines)
            {
                if (outline.Peer != peer.Id) continue;
                for (var i = 0; i < 4; i++)
                {
                    var a = session.Viewport.WorldToScreen(outline.Corners[i]); var b = session.Viewport.WorldToScreen(outline.Corners[(i + 1) % 4]);
                    canvas.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, _paint);
                }
            }
            if (peer.Cursor is not { } cursor) continue;
            var point = session.Viewport.WorldToScreen(cursor);
            if (point.X < -30 || point.Y < -30 || point.X > area.Width || point.Y > area.Height) continue;
            canvas.Save(); canvas.Translate((float)point.X, (float)point.Y);
            _paint.Style = SKPaintStyle.Stroke; _paint.StrokeWidth = 2; _paint.Color = SKColors.White; canvas.DrawPath(_cursor, _paint);
            _paint.Style = SKPaintStyle.Fill; _paint.Color = color; canvas.DrawPath(_cursor, _paint);
            var width = Math.Min(220, _font.MeasureText(peer.Name)) + 12;
            canvas.DrawRoundRect(new(12, 19, 12 + width, 38), 3, 3, _paint);
            _paint.Color = SKColors.White; canvas.DrawText(peer.Name, 18, 32, _font, _paint);
            canvas.Restore();
        }
        canvas.Restore();
    }
    public new void Dispose()
    {
        if (_disposed) return; _disposed = true; _paint.Dispose(); _font.Dispose(); _cursor.Dispose(); _peers = []; _outlines.Clear(); Session = null;
    }
}
