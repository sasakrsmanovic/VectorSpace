using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Layout;

namespace VectorSpace.Prototyping;

/// <summary>Single-threaded deterministic player. Navigation is allocation-bounded; a document is copied
/// only when an interaction changes variables or component definitions, never on animation frames.
/// The caller owns the monotonic clock and decides whether to open requested external URLs.</summary>
public sealed class PrototypeSession
{
    private sealed record Navigation(string FrameId, Vec2 Scroll, PrototypeOverlay[] Overlays);
    private sealed record Scheduled(string NodeId, string ReactionId, long Epoch);
    private sealed class Transaction(DesignDocument document, Navigation navigation, List<Navigation> history)
    {
        public DesignDocument Document = document;
        public Navigation Navigation = navigation;
        public List<Navigation> History = history;
        public bool Mutable, Changed, SceneChanged;
        public PrototypeTransition Transition = new();
        public readonly List<string> Links = [];
        public int Steps;
        public void MakeMutable()
        {
            if (Mutable) return;
            Document = DocumentJson.Load(DocumentJson.Save(Document)); Mutable = true;
        }
    }
    private readonly string _initialJson;
    private readonly string _startId;
    private Dictionary<string, DesignNode> _nodes = [];
    private Navigation _navigation = null!;
    private List<Navigation> _history = [];
    private readonly PriorityQueue<Scheduled, (double Deadline, long Order)> _timers = new();
    private readonly Queue<string> _links = new();
    private long _timerOrder;
    public DesignDocument Document { get; private set; } = null!;
    public PrototypeView View { get; private set; } = null!;
    public PrototypeAnimation? Animation { get; private set; }
    public double ClockMilliseconds { get; private set; }
    public long SceneEpoch { get; private set; }
    public long Revision { get; private set; }
    public string? LastError { get; private set; }
    public int HistoryCount => _history.Count;
    public bool CanGoBack => _history.Count > 0 || _navigation.Overlays.Length > 0;
    public bool IsAnimating => Animation is not null;
    public double? NextWakeMilliseconds => Animation is not null ? 16 : _timers.TryPeek(out _, out var due) ? Math.Max(1, due.Deadline - ClockMilliseconds) : null;

    public PrototypeSession(DesignDocument source, string? startId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        // Validate the copy so even normalization during validation cannot alter the editor document.
        _initialJson = DocumentJson.Save(source);
        Document = DocumentJson.Load(_initialJson);
        _startId = startId ?? Document.AllNodes().FirstOrDefault(n => n.IsFrame && !string.IsNullOrWhiteSpace(n.PrototypeFlowName))?.Id
            ?? Document.AllNodes().FirstOrDefault(n => n.IsFrame)?.Id ?? throw new InvalidOperationException("Create a frame to present a prototype.");
        Initialize();
    }
    private void Initialize()
    {
        Document.RebuildParents(); ComponentService.Synchronize(Document); new VariableResolver(Document).Apply();
        foreach (var page in Document.Pages) LayoutEngine.Arrange(page.Nodes);
        _nodes = Document.AllNodes().ToDictionary(n => n.Id, StringComparer.Ordinal);
        RequireFrame(Document, _startId);
        _navigation = new(_startId, Vec2.Zero, []); _history.Clear(); _links.Clear(); Animation = null; LastError = null;
        ClockMilliseconds = 0; SceneEpoch++; Revision++; UpdateView(); ScheduleTimers();
    }
    public void Restart() { Document = DocumentJson.Load(_initialJson); Initialize(); }
    public DesignNode? Find(string? id) => id is not null && _nodes.TryGetValue(id, out var node) ? node : null;
    public string? TriggerOwner(string? hitId, PrototypeTrigger trigger, string? key = null)
    {
        var root = View.InputRoot;
        for (var n = Find(hitId); n is not null; n = n.Parent)
        {
            if (n != root && !n.IsDescendantOf(root) || !n.IsEffectivelyVisible) return null;
            if (n.Reactions.Any(r => Matches(r, trigger, key)) || trigger == PrototypeTrigger.Click && n.PrototypeTargetId is not null) return n.Id;
            if (n == root) break;
        }
        return null;
    }
    private static bool Matches(PrototypeReaction r, PrototypeTrigger trigger, string? key) => r.Trigger == trigger && (trigger != PrototypeTrigger.KeyDown || NormalizeKey(r.Key) == NormalizeKey(key ?? ""));
    public static string NormalizeKey(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    public bool Dispatch(PrototypeTrigger trigger, string? hitId, string? key = null, bool userInitiated = false)
    {
        var owner = TriggerOwner(hitId, trigger, key);
        if (owner is null) return false;
        var node = _nodes[owner];
        var reaction = node.Reactions.FirstOrDefault(r => Matches(r, trigger, key));
        if (reaction is not null) Execute(reaction.Actions, owner, userInitiated && (trigger is PrototypeTrigger.Click or PrototypeTrigger.MouseDown or PrototypeTrigger.MouseUp or PrototypeTrigger.KeyDown));
        else Execute([new() { Kind = PrototypeActionKind.Navigate, TargetId = node.PrototypeTargetId }], owner, userInitiated);
        return true;
    }
    public bool DispatchKey(string key)
    {
        foreach (var node in View.InputRoot.DescendantsAndSelf().Reverse())
        {
            if (!node.IsEffectivelyVisible || !node.Reactions.Any(r => Matches(r, PrototypeTrigger.KeyDown, key))) continue;
            return Dispatch(PrototypeTrigger.KeyDown, node.Id, key, true);
        }
        return false;
    }
    public void Back() => Execute([new() { Kind = PrototypeActionKind.Back }], View.InputRoot.Id, false);
    public bool OutsideClick()
    {
        if (_navigation.Overlays.Length == 0) return false;
        if (_navigation.Overlays[^1].CloseOnOutsideClick) Execute([new() { Kind = PrototypeActionKind.CloseOverlay }], View.InputRoot.Id, false);
        return true; // Modal overlays always consume the outside click; never click through.
    }
    public bool ScrollBy(Vec2 delta)
    {
        if (!delta.IsFinite) throw new ArgumentOutOfRangeException(nameof(delta));
        var scroll = PrototypeGeometry.ClampScroll(View.InputRoot, View.InputScroll + delta);
        if (scroll == View.InputScroll) return false;
        _navigation = WithScroll(_navigation, scroll); Animation = null; Revision++; UpdateView(); return true;
    }
    public string[] DrainRequestedUrls()
    {
        var result = _links.ToArray(); _links.Clear(); return result;
    }
    public void AdvanceTo(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < ClockMilliseconds) throw new ArgumentOutOfRangeException(nameof(milliseconds), "The prototype clock must be finite and monotonic.");
        ClockMilliseconds = milliseconds;
        if (Animation is { } animation && milliseconds >= animation.StartedAt + animation.Duration) { Animation = null; Revision++; }
        var epoch = SceneEpoch;
        for (var count = 0; count < 64 && _timers.TryPeek(out var timer, out var due) && due.Deadline <= milliseconds; count++)
        {
            _timers.Dequeue();
            if (timer.Epoch != SceneEpoch || Find(timer.NodeId) is not { IsEffectivelyVisible: true } node) continue;
            var reaction = node.Reactions.FirstOrDefault(r => r.Id == timer.ReactionId && r.Trigger == PrototypeTrigger.AfterDelay);
            if (reaction is null) continue;
            try { Execute(reaction.Actions, node.Id, false); }
            catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or ArgumentException)
            { LastError = ex.Message; Revision++; }
            // A newly entered scene starts its delays at the current clock, not the old deadline.
            if (SceneEpoch != epoch) break;
        }
    }
    private void Execute(IReadOnlyList<PrototypeAction> actions, string sourceId, bool userInitiated)
    {
        var tx = new Transaction(Document, _navigation, [.. _history]);
        Apply(actions, sourceId, tx, userInitiated, 0);
        if (tx.Mutable)
        {
            VariableResolver.Validate(tx.Document); ComponentService.Synchronize(tx.Document);
            new VariableResolver(tx.Document).Apply();
            foreach (var page in tx.Document.Pages) LayoutEngine.Arrange(page.Nodes);
            DocumentJson.Validate(tx.Document);
            tx.Navigation = WithScroll(tx.Navigation, PrototypeGeometry.ClampScroll(InputRoot(tx), InputScroll(tx.Navigation)));
        }
        if (!tx.Changed && tx.Links.Count == 0) return;
        var from = View;
        Document = tx.Document; _navigation = tx.Navigation; _history = tx.History;
        if (tx.Mutable) _nodes = Document.AllNodes().ToDictionary(n => n.Id, StringComparer.Ordinal);
        LastError = null; Revision++; UpdateView();
        Animation = tx.Changed && tx.Transition.Kind != PrototypeTransitionKind.Instant && tx.Transition.DurationMilliseconds > 0
            ? new(from, View, tx.Transition, ClockMilliseconds) : null;
        if (tx.SceneChanged) { SceneEpoch++; ScheduleTimers(); }
        foreach (var link in tx.Links) if (_links.Count < 32) _links.Enqueue(link);
    }
    private static void Apply(IReadOnlyList<PrototypeAction> actions, string sourceId, Transaction tx, bool userInitiated, int depth)
    {
        if (depth > PrototypeValidation.MaxBranchDepth) throw new InvalidDataException("Prototype conditional depth exceeded.");
        foreach (var action in actions)
        {
            if (++tx.Steps > 256) throw new InvalidDataException("Prototype action budget exceeded.");
            var state = tx.Navigation;
            switch (action.Kind)
            {
                case PrototypeActionKind.Navigate:
                {
                    var destination = RequireFrame(tx.Document, action.TargetId);
                    tx.History.Add(state with { Overlays = [] });
                    if (tx.History.Count > 128) tx.History.RemoveAt(0);
                    tx.Navigation = new(destination.Id, action.PreserveScroll ? PrototypeGeometry.ClampScroll(destination, state.Scroll) : Vec2.Zero, []);
                    tx.SceneChanged = tx.Changed = true; break;
                }
                case PrototypeActionKind.Back:
                    if (state.Overlays.Length != 0) tx.Navigation = state with { Overlays = state.Overlays[..^1] };
                    else if (tx.History.Count > 0) { tx.Navigation = tx.History[^1]; tx.History.RemoveAt(tx.History.Count - 1); }
                    else continue;
                    tx.SceneChanged = tx.Changed = true; break;
                case PrototypeActionKind.OpenOverlay:
                case PrototypeActionKind.SwapOverlay:
                {
                    var frame = RequireFrame(tx.Document, action.TargetId);
                    if (action.Kind == PrototypeActionKind.SwapOverlay && state.Overlays.Length == 0) throw new InvalidOperationException("Swap overlay requires an open overlay.");
                    if (action.Kind == PrototypeActionKind.OpenOverlay && state.Overlays.Length >= 16) throw new InvalidOperationException("Overlay stack limit reached.");
                    var o = action.Overlay;
                    var overlay = new PrototypeOverlay(frame.Id, o.Placement, o.Offset, o.Backdrop, o.BackdropOpacity, o.CloseOnOutsideClick, Vec2.Zero);
                    tx.Navigation = state with { Overlays = action.Kind == PrototypeActionKind.OpenOverlay ? [.. state.Overlays, overlay] : [.. state.Overlays[..^1], overlay] };
                    tx.SceneChanged = tx.Changed = true; break;
                }
                case PrototypeActionKind.CloseOverlay:
                    if (state.Overlays.Length == 0) continue;
                    tx.Navigation = state with { Overlays = state.Overlays[..^1] }; tx.SceneChanged = tx.Changed = true; break;
                case PrototypeActionKind.ScrollTo:
                {
                    var target = tx.Document.Find(action.TargetId) ?? throw new InvalidOperationException("Scroll destination no longer exists.");
                    var root = InputRoot(tx);
                    if (target != root && !target.IsDescendantOf(root)) throw new InvalidOperationException("Scroll destination must be inside the active frame or overlay.");
                    var local = root.WorldMatrix.Inverse.Map(target.WorldMatrix.Map(Vec2.Zero));
                    tx.Navigation = WithScroll(state, PrototypeGeometry.ClampScroll(root, local)); tx.Changed = true; break;
                }
                case PrototypeActionKind.SetVariable:
                {
                    tx.MakeMutable();
                    var variable = tx.Document.Variables.FirstOrDefault(v => v.Id == action.VariableId) ?? throw new InvalidOperationException("Prototype variable no longer exists.");
                    var resolver = new VariableResolver(tx.Document); var source = tx.Document.Find(sourceId);
                    var old = resolver.Resolve(variable.Id, source); VariableValue value;
                    if (action.Operation == PrototypeVariableOperation.Toggle)
                    {
                        if (variable.Type != VariableType.Boolean) throw new InvalidOperationException("Toggle requires a Boolean variable.");
                        value = VariableValue.Bool(!old.Boolean);
                    }
                    else if (action.Operation == PrototypeVariableOperation.Add)
                    {
                        if (variable.Type != VariableType.Number || action.Value.Type != VariableType.Number) throw new InvalidOperationException("Add requires numeric values.");
                        value = VariableValue.Float(old.Number + action.Value.Number);
                    }
                    else value = action.Value;
                    PrototypeValidation.CheckLiteral(value);
                    if (value.Type != variable.Type) throw new InvalidOperationException("Prototype variable assignment has the wrong type.");
                    variable.Values[resolver.ModeFor(variable.CollectionId, source)] = value; tx.Changed = true; break;
                }
                case PrototypeActionKind.Conditional:
                {
                    var condition = action.Condition ?? throw new InvalidOperationException("Prototype condition is missing.");
                    var value = new VariableResolver(tx.Document).Resolve(condition.VariableId, tx.Document.Find(sourceId));
                    Apply(Compare(value, condition.Value, condition.Comparison) ? action.Then : action.Else, sourceId, tx, userInitiated, depth + 1); continue;
                }
                case PrototypeActionKind.ChangeVariant:
                {
                    tx.MakeMutable();
                    var instance = tx.Document.Find(action.InstanceId ?? sourceId);
                    while (instance is not null && instance.Kind != NodeKind.Instance) instance = instance.Parent;
                    if (instance is null) throw new InvalidOperationException("Change variant requires an instance hotspot or an explicit instance.");
                    var target = tx.Document.Find(action.TargetId);
                    if (target?.Parent?.Kind != NodeKind.ComponentSet || ComponentVariants.SetFor(tx.Document, instance)?.Id != target.Parent.Id) throw new InvalidOperationException("Interactive variants must belong to the same component set.");
                    ComponentVariants.SwitchInDocument(tx.Document, instance.Id, target.Id, allowLocked: true);
                    tx.SceneChanged = tx.Changed = true; break;
                }
                case PrototypeActionKind.OpenUrl:
                    if (!PrototypeValidation.SafeUrl(action.Url, out var uri)) throw new InvalidOperationException("Unsafe prototype URL.");
                    if (userInitiated) tx.Links.Add(uri!.AbsoluteUri);
                    break;
                default: throw new InvalidDataException("Unknown prototype action.");
            }
            if (action.Kind != PrototypeActionKind.OpenUrl) tx.Transition = action.Transition;
        }
    }
    public static bool Compare(VariableValue left, VariableValue right, PrototypeComparison comparison)
    {
        if (left.Type != right.Type || left.AliasId is not null || right.AliasId is not null) throw new InvalidOperationException("Conditional values must have the same resolved type.");
        var equal = left.Type switch
        {
            VariableType.Number => left.Number == right.Number,
            VariableType.Boolean => left.Boolean == right.Boolean,
            VariableType.Color => string.Equals(left.Text.Length == 7 ? "#FF" + left.Text[1..] : left.Text, right.Text.Length == 7 ? "#FF" + right.Text[1..] : right.Text, StringComparison.OrdinalIgnoreCase),
            _ => left.Text == right.Text
        };
        if (comparison == PrototypeComparison.Equal) return equal;
        if (comparison == PrototypeComparison.NotEqual) return !equal;
        if (left.Type != VariableType.Number) throw new InvalidOperationException("Ordered comparisons require numbers.");
        return comparison switch { PrototypeComparison.Less => left.Number < right.Number, PrototypeComparison.LessOrEqual => left.Number <= right.Number, PrototypeComparison.Greater => left.Number > right.Number, PrototypeComparison.GreaterOrEqual => left.Number >= right.Number, _ => throw new InvalidOperationException("Unknown comparison.") };
    }
    private static DesignNode RequireFrame(DesignDocument document, string? id) => document.Find(id) is { IsFrame: true } frame ? frame : throw new InvalidOperationException("Prototype destination must be an existing frame, component or instance.");
    private static DesignNode InputRoot(Transaction tx) => RequireFrame(tx.Document, tx.Navigation.Overlays.Length == 0 ? tx.Navigation.FrameId : tx.Navigation.Overlays[^1].FrameId);
    private static Vec2 InputScroll(Navigation n) => n.Overlays.Length == 0 ? n.Scroll : n.Overlays[^1].Scroll;
    private static Navigation WithScroll(Navigation n, Vec2 scroll) => n.Overlays.Length == 0 ? n with { Scroll = scroll } : n with { Overlays = [.. n.Overlays[..^1], n.Overlays[^1] with { Scroll = scroll }] };
    private void UpdateView() => View = new(Document, _navigation.FrameId, _navigation.Scroll, Array.AsReadOnly(_navigation.Overlays));
    private void ScheduleTimers()
    {
        _timers.Clear();
        foreach (var node in View.InputRoot.DescendantsAndSelf())
        {
            if (!node.IsEffectivelyVisible) continue;
            foreach (var reaction in node.Reactions.Where(r => r.Trigger == PrototypeTrigger.AfterDelay))
                _timers.Enqueue(new(node.Id, reaction.Id, SceneEpoch), (ClockMilliseconds + reaction.DelayMilliseconds, _timerOrder++));
        }
    }
}
