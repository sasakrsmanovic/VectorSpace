using System.Diagnostics;
using Microsoft.UI.Xaml.Automation;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using VectorSpace.Prototyping;
using VectorSpace.Skia;

namespace VectorSpace.Editor;

/// <summary>Embeddable Uno/Skia prototype player. Owns its playback clock and event subscriptions;
/// a borrowed renderer retains the editor's installed fonts but is never disposed by this control.</summary>
public sealed class PrototypePlayer : UserControl, IDisposable
{
    private sealed class PlayerCanvas : SKCanvasElement
    {
        public Action<SKCanvas, Size>? Draw { get; set; }
        protected override void RenderOverride(SKCanvas canvas, Size area) => Draw?.Invoke(canvas, area);
    }
    private readonly PlayerCanvas _canvas = new();
    private readonly SceneRenderer _renderer;
    private readonly PrototypeSceneRenderer _compositor;
    private readonly bool _ownsRenderer;
    private readonly DispatcherTimer _timer = new();
    private readonly Stopwatch _clock = new();
    private readonly TextBlock _title = Studio.Text("Prototype", 12);
    private readonly StudioButton _back;
    private List<string> _hoverPath = [], _nextHoverPath = [];
    private Vec2? _hoverPoint;
    private bool _hoverPending;
    private long _displayRevision = -1;
    private double _displayClock = -1, _fitWidth = -1, _fitHeight = -1, _frameWidth = -1, _frameHeight = -1;
    private string? _fitFrameId;
    private uint? _pointer;
    private string? _downOwner;
    private long _downEpoch;
    private Vec2 _downPoint, _lastPoint;
    private bool _outsideDown, _dragged, _touch, _loaded, _disposed;
    public PrototypeSession Playback { get; }
    public Viewport Viewport { get; } = new();
    public double CanvasWidth => _canvas.ActualWidth;
    public double CanvasHeight => _canvas.ActualHeight;
    public event Action? ExitRequested;
    public event Action<string>? StatusChanged;
    public event Action<string>? LinkRequested;

    public PrototypePlayer(DesignDocument document, string? startId = null, SceneRenderer? renderer = null)
    {
        Playback = new(document, startId); _renderer = renderer ?? new(); _ownsRenderer = renderer is null; _compositor = new(_renderer);
        IsTabStop = true; HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Prototype player"); AutomationProperties.SetName(_canvas, "Prototype canvas");
        var root = new Grid { Background = Studio.Brush("#252525") };
        root.RowDefinitions.Add(new() { Height = new(44) }); root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _back = new StudioButton("Back", () => Run(Playback.Back)); AutomationProperties.SetName(_back, "Prototype back");
        var restart = new StudioButton("Restart", Restart); AutomationProperties.SetName(restart, "Prototype restart");
        var close = new StudioButton("Close", () => ExitRequested?.Invoke()); AutomationProperties.SetName(close, "Close prototype");
        var toolbar = new Border { Background = Studio.Brush("#FFFFFF"), Padding = new(12, 5, 12, 5), Child = Studio.Columns((_back, 60), (restart, 72), (_title, -1), (close, 64)) };
        root.Children.Add(toolbar); Grid.SetRow(_canvas, 1); root.Children.Add(_canvas); Content = root;
        _canvas.Draw = Paint;
        _canvas.PointerPressed += Pressed; _canvas.PointerMoved += Moved; _canvas.PointerReleased += Released;
        _canvas.PointerCanceled += CancelPointer; _canvas.PointerCaptureLost += CancelPointer;
        _canvas.PointerExited += (_, _) =>
        {
            _hoverPoint = null;
            if (_pointer is null) Run(() => { _hoverPending = false; Hover(null); });
        };
        _canvas.PointerWheelChanged += Wheel; _canvas.SizeChanged += (_, _) => Refresh(force: true);
        KeyDown += OnKeyDown;
        _timer.Tick += Tick;
        Loaded += (_, _) => { _loaded = true; _clock.Start(); Focus(FocusState.Programmatic); Refresh(force: true); };
        Unloaded += (_, _) => { _loaded = false; _clock.Stop(); _timer.Stop(); };
    }
    private void Paint(SKCanvas canvas, Size size)
    {
        canvas.Save(); var outlines = _renderer.Outlines; _renderer.Outlines = false;
        try
        {
            canvas.ClipRect(new(0, 0, (float)size.Width, (float)size.Height));
            using var background = new SKPaint { Color = SceneRenderer.Color("#252525") }; canvas.DrawRect(new(0, 0, (float)size.Width, (float)size.Height), background);
            canvas.Translate((float)Viewport.Pan.X, (float)Viewport.Pan.Y); canvas.Scale((float)Viewport.Zoom);
            _compositor.Draw(canvas, Playback);
        }
        finally { _renderer.Outlines = outlines; canvas.Restore(); }
    }
    private void Advance() => Playback.AdvanceTo(_clock.Elapsed.TotalMilliseconds);
    private void Tick(object? sender, object e) => Run(() => { Advance(); if (Playback.LastError is { } error) StatusChanged?.Invoke(error); });
    private void Run(Action action)
    {
        if (_disposed) return;
        try
        {
            action();
            // Activation is suppressed during a transition, but pointer location is not lost.
            // A completed animation must reconcile hover even without another mouse event.
            if (_hoverPending && !Playback.IsAnimating && _pointer is null)
            {
                _hoverPending = false;
                Hover(_hoverPoint is { } point ? Hit(point, out _)?.Id : null);
            }
            foreach (var url in Playback.DrainRequestedUrls()) LinkRequested?.Invoke(url);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or ArgumentException) { StatusChanged?.Invoke(ex.Message); }
        Refresh();
    }
    private void Refresh(bool force = false)
    {
        if (_disposed) return;
        var frame = Playback.View.Frame;
        var width = _canvas.ActualWidth; var height = _canvas.ActualHeight;
        var fitChanged = _fitFrameId != frame.Id || _fitWidth != width || _fitHeight != height || _frameWidth != frame.Width || _frameHeight != frame.Height;
        if (fitChanged)
        {
            Viewport.Fit(new(0, 0, frame.Width, frame.Height), width, height, 24);
            _fitFrameId = frame.Id; _fitWidth = width; _fitHeight = height; _frameWidth = frame.Width; _frameHeight = frame.Height;
        }
        var stateChanged = _displayRevision != Playback.Revision;
        if (force || stateChanged)
        {
            _back.IsEnabled = Playback.CanGoBack;
            var title = (frame.PrototypeFlowName ?? frame.Name) + (Playback.View.Overlays.Count == 0 ? "" : "  ·  " + Playback.View.InputRoot.Name);
            if (_title.Text != title) _title.Text = title;
        }
        if (force || fitChanged || stateChanged || Playback.IsAnimating && _displayClock != Playback.ClockMilliseconds)
            _canvas.Invalidate();
        _displayRevision = Playback.Revision; _displayClock = Playback.ClockMilliseconds;
        _timer.Stop();
        if (_loaded && Playback.NextWakeMilliseconds is { } due)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(due, 1, 600_000)); _timer.Start();
        }
    }
    public void Restart()
    {
        Run(() =>
        {
            Playback.Restart(); _clock.Restart(); _hoverPath.Clear(); _nextHoverPath.Clear();
            _hoverPoint = null; _hoverPending = false; _fitFrameId = null;
            _pointer = null; _canvas.ReleasePointerCaptures();
        });
    }
    private Vec2 Position(PointerRoutedEventArgs e) { var p = e.GetCurrentPoint(_canvas).Position; return new(p.X, p.Y); }
    private DesignNode? Hit(Vec2 p, out bool outside) => _compositor.Hit(Playback, Viewport.ScreenToWorld(p), out outside);
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(_canvas).Properties.IsRightButtonPressed || _pointer is not null || Playback.IsAnimating) return;
        e.Handled = true; Focus(FocusState.Programmatic);
        Run(() =>
        {
            Advance(); var p = Position(e); _hoverPoint = p; var hit = Hit(p, out var outside);
            _pointer = e.Pointer.PointerId; _canvas.CapturePointer(e.Pointer); _downEpoch = Playback.SceneEpoch;
            _downOwner = Playback.TriggerOwner(hit?.Id, PrototypeTrigger.Click); _downPoint = _lastPoint = p;
            _outsideDown = outside; _dragged = false; _touch = e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch;
            if (!outside) Playback.Dispatch(PrototypeTrigger.MouseDown, hit?.Id, userInitiated: true);
        });
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        var p = Position(e); _hoverPoint = p;
        if (Playback.IsAnimating) { _hoverPending = true; return; }
        Run(() =>
        {
            Advance();
            if (_pointer == e.Pointer.PointerId)
            {
                if (p.DistanceTo(_downPoint) > 6) _dragged = true;
                if (_touch && _dragged) Playback.ScrollBy((_lastPoint - p) / Viewport.Zoom);
                _lastPoint = p;
            }
            else if (_pointer is null) { _hoverPending = false; Hover(Hit(p, out _)?.Id); }
        });
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer != e.Pointer.PointerId) return;
        e.Handled = true;
        Run(() =>
        {
            Advance(); var owner = _downOwner; var epoch = _downEpoch; var p = Position(e); _hoverPoint = p; var hit = Hit(p, out var outside);
            _pointer = null; _canvas.ReleasePointerCapture(e.Pointer);
            if (epoch != Playback.SceneEpoch || _dragged || p.DistanceTo(_downPoint) > 6) return;
            if (_outsideDown && outside) { Playback.OutsideClick(); return; }
            if (outside) return;
            Playback.Dispatch(PrototypeTrigger.MouseUp, hit?.Id, userInitiated: true);
            if (epoch == Playback.SceneEpoch && owner is not null && owner == Playback.TriggerOwner(hit?.Id, PrototypeTrigger.Click))
                Playback.Dispatch(PrototypeTrigger.Click, hit?.Id, userInitiated: true);
        });
    }
    private void CancelPointer(object sender, PointerRoutedEventArgs e) { if (_pointer == e.Pointer.PointerId) _pointer = null; }
    private void Hover(string? hitId)
    {
        var next = _nextHoverPath; next.Clear(); var root = Playback.View.InputRoot;
        for (var n = Playback.Find(hitId); n is not null; n = n.Parent)
        {
            if (n.Reactions.Any(r => r.Trigger is PrototypeTrigger.MouseEnter or PrototypeTrigger.MouseLeave)) next.Add(n.Id);
            if (n == root) break;
        }
        var epoch = Playback.SceneEpoch;
        for (var i = 0; i < _hoverPath.Count; i++)
        {
            var id = _hoverPath[i];
            if (!next.Contains(id) && Playback.Find(id)?.Reactions.Any(r => r.Trigger == PrototypeTrigger.MouseLeave) == true)
                Playback.Dispatch(PrototypeTrigger.MouseLeave, id);
            if (epoch != Playback.SceneEpoch) { _hoverPath.Clear(); return; }
        }
        for (var i = next.Count - 1; i >= 0; i--)
        {
            var id = next[i];
            if (!_hoverPath.Contains(id) && Playback.Find(id)?.Reactions.Any(r => r.Trigger == PrototypeTrigger.MouseEnter) == true)
                Playback.Dispatch(PrototypeTrigger.MouseEnter, id);
            if (epoch != Playback.SceneEpoch) { _hoverPath.Clear(); return; }
        }
        _nextHoverPath = _hoverPath; _hoverPath = next;
    }
    private void Wheel(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true; var point = e.GetCurrentPoint(_canvas);
        Run(() =>
        {
            Advance(); var delta = -point.Properties.MouseWheelDelta * .65 / Viewport.Zoom;
            Playback.ScrollBy(point.Properties.IsHorizontalMouseWheel || Keyboard.Shift ? new(delta, 0) : new(0, delta));
        });
    }
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            if (Playback.View.Overlays.Count > 0) Run(Playback.Back); else ExitRequested?.Invoke();
            return;
        }
        var key = (int)e.Key >= (int)VirtualKey.Number0 && (int)e.Key <= (int)VirtualKey.Number9 ? ((int)e.Key - (int)VirtualKey.Number0).ToString() : e.Key.ToString();
        key = (Keyboard.Control ? "CTRL+" : "") + (Keyboard.Shift ? "SHIFT+" : "") + (Keyboard.Alt ? "ALT+" : "") + key;
        var handled = false;
        Run(() => { Advance(); handled = Playback.DispatchKey(key); });
        if (!handled && e.Key == VirtualKey.Back) { Run(Playback.Back); handled = true; }
        if (!handled && e.Key == VirtualKey.R && !Keyboard.Control && !Keyboard.Alt) { Restart(); handled = true; }
        e.Handled = handled;
    }
    public new void Dispose()
    {
        if (_disposed) return; _disposed = true; _timer.Stop(); _timer.Tick -= Tick; _clock.Stop(); _canvas.ReleasePointerCaptures();
        _hoverPath.Clear(); _nextHoverPath.Clear(); _canvas.Draw = null; if (_ownsRenderer) _renderer.Dispose();
    }
}
