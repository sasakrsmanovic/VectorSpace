namespace VectorSpace.Core;

/// <summary>Bounded structural validation shared by native documents and prototype playback.
/// Missing destinations are retained as repairable authoring links; execution rejects them atomically.</summary>
public static class PrototypeValidation
{
    public const int MaxReactionsPerNode = 64;
    public const int MaxActions = 100_000;
    public const int MaxBranchDepth = 8;

    public static void Validate(DesignDocument document)
    {
        var actions = 0;
        foreach (var node in document.AllNodes())
        {
            if (node.Reactions is null || node.Reactions.Count > MaxReactionsPerNode || !Enum.IsDefined(node.PrototypeOverflow)) Fail("Invalid prototype properties.");
            if (node.PrototypeFlowName?.Length > 256 || node.PrototypeFlowName is not null && !node.IsFrame) Fail("A flow start must be a frame with a name of at most 256 characters.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reaction in node.Reactions!)
            {
                if (reaction is null || string.IsNullOrWhiteSpace(reaction.Id) || !ids.Add(reaction.Id) || !Enum.IsDefined(reaction.Trigger)) Fail("Invalid or duplicate prototype reaction.");
                if (!double.IsFinite(reaction!.DelayMilliseconds) || reaction.DelayMilliseconds is < 16 or > 600_000) Fail("Prototype delays must be between 16 and 600000 ms.");
                if (reaction.Key is null || reaction.Key.Length > 64 || reaction.Trigger == PrototypeTrigger.KeyDown && string.IsNullOrWhiteSpace(reaction.Key)) Fail("Invalid keyboard trigger.");
                if (reaction.Actions is null || reaction.Actions.Count is < 1 or > 32) Fail("A reaction needs between 1 and 32 actions.");
                CheckActions(reaction.Actions!, 0);
            }
        }
        void CheckActions(List<PrototypeAction> list, int depth)
        {
            if (list.Count == 0) return;
            if (depth > MaxBranchDepth || list.Count > 32) Fail("Prototype branch limit exceeded.");
            foreach (var a in list)
            {
                if (++actions > MaxActions || a is null || !Enum.IsDefined(a.Kind) || a.Transition is null || a.Overlay is null || a.Then is null || a.Else is null) Fail("Invalid prototype action or action limit exceeded.");
                var t = a!.Transition;
                if (!Enum.IsDefined(t.Kind) || !Enum.IsDefined(t.Easing) || !Enum.IsDefined(t.Direction) || !double.IsFinite(t.DurationMilliseconds) || t.DurationMilliseconds is < 0 or > 10_000) Fail("Invalid prototype transition.");
                var o = a.Overlay;
                if (!Enum.IsDefined(o.Placement) || !o.Offset.IsFinite || Math.Abs(o.Offset.X) > 1e7 || Math.Abs(o.Offset.Y) > 1e7 || !double.IsFinite(o.BackdropOpacity) || o.BackdropOpacity is < 0 or > 1 || !HexColor(o.Backdrop)) Fail("Invalid overlay settings.");
                if (a.Kind is PrototypeActionKind.Navigate or PrototypeActionKind.OpenOverlay or PrototypeActionKind.SwapOverlay or PrototypeActionKind.ScrollTo or PrototypeActionKind.ChangeVariant)
                    if (string.IsNullOrWhiteSpace(a.TargetId)) Fail("The action requires a destination.");
                if (a.Kind == PrototypeActionKind.OpenUrl && !SafeUrl(a.Url, out _)) Fail("Prototype links must be absolute HTTP or HTTPS URLs without embedded credentials.");
                if (a.Kind == PrototypeActionKind.SetVariable)
                {
                    if (string.IsNullOrWhiteSpace(a.VariableId) || !Enum.IsDefined(a.Operation)) Fail("Invalid variable action.");
                    CheckLiteral(a.Value);
                }
                if (a.Kind == PrototypeActionKind.Conditional)
                {
                    if (a.Condition is not { } c || string.IsNullOrWhiteSpace(c.VariableId) || !Enum.IsDefined(c.Comparison)) Fail("Invalid prototype condition.");
                    CheckLiteral(a.Condition!.Value);
                }
                CheckActions(a.Then, depth + 1); CheckActions(a.Else, depth + 1);
            }
        }
    }
    public static void CheckLiteral(VariableValue? value)
    {
        if (value is null || !Enum.IsDefined(value.Type) || value.AliasId is not null || !double.IsFinite(value.Number) || value.Text is null || value.Text.Length > 16384 || value.Type == VariableType.Color && !HexColor(value.Text)) Fail("Invalid prototype variable literal.");
    }
    public static bool SafeUrl(string? text, out Uri? uri)
    {
        uri = null;
        if (text is null || text.Length > 4096 || !Uri.TryCreate(text, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("https" or "http") || parsed.UserInfo.Length != 0 || parsed.Host.Length == 0) return false;
        uri = parsed; return true;
    }
    public static bool HexColor(string? text) => text is not null && (text.Length is 7 or 9) && text[0] == '#' && text.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    public static IEnumerable<PrototypeAction> Actions(DesignNode node) => node.Reactions.SelectMany(r => Flatten(r.Actions));
    private static IEnumerable<PrototypeAction> Flatten(IEnumerable<PrototypeAction> actions)
    {
        foreach (var a in actions) { yield return a; foreach (var b in Flatten(a.Then)) yield return b; foreach (var b in Flatten(a.Else)) yield return b; }
    }
    public static void Remap(DesignNode node, IReadOnlyDictionary<string, string> nodeIds, IReadOnlyDictionary<string, string>? variableIds = null)
    {
        foreach (var a in Actions(node))
        {
            if (a.TargetId is { } t && nodeIds.TryGetValue(t, out var id)) a.TargetId = id;
            if (a.InstanceId is { } i && nodeIds.TryGetValue(i, out id)) a.InstanceId = id;
            if (variableIds is null) continue;
            if (a.VariableId is { } v && variableIds.TryGetValue(v, out id)) a.VariableId = id;
            if (a.Condition is { } c && variableIds.TryGetValue(c.VariableId, out id)) c.VariableId = id;
        }
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string message) => throw new InvalidDataException(message);
}
