using System.Globalization;
using VectorSpace.Prototyping;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private void EditPrototype(string nodeId, string label, Action<DesignNode> edit) => Run(() => Session.Edit(label, () =>
    {
        var node = Session.Document.Find(nodeId) ?? throw new InvalidOperationException("The layer no longer exists.");
        if (node.IsEffectivelyLocked) throw new InvalidOperationException("Unlock the layer before editing its prototype settings.");
        edit(node); node.PrototypeReactionsOverride = node.SourceId is not null;
    }));
    private static ComboBox PrototypeChoice<T>(T value, Action<T> changed, string name) where T : struct, Enum =>
        Studio.Choice(Enum.GetNames<T>(), value.ToString(), s => changed(Enum.Parse<T>(s)), name);
    private static TextBox PrototypeText(string text, string name, Action<string> commit)
    {
        var box = Studio.Input(text, name); var previous = text;
        void Apply() { if (box.Text == previous) return; previous = box.Text; commit(previous); }
        box.LostFocus += (_, _) => Apply();
        box.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { Apply(); e.Handled = true; } };
        return box;
    }
    private static ComboBox PrototypeIds(IEnumerable<(string Id, string Name)> items, string? selected, Action<string> changed, string name, bool optional = false)
    {
        var box = new ComboBox { Style = (Style)StudioResources.Current["VS.ComboBox"], Height = 32, HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = Studio.Font };
        AutomationProperties.SetName(box, name);
        if (optional) box.Items.Add(new ComboBoxItem { Content = "Nearest instance", Tag = "" });
        foreach (var item in items) box.Items.Add(new ComboBoxItem { Content = item.Name, Tag = item.Id });
        var match = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == (selected ?? ""));
        if (match is null && !string.IsNullOrEmpty(selected))
        {
            match = new ComboBoxItem { Content = "Missing: " + selected, Tag = selected }; box.Items.Add(match);
        }
        box.SelectedItem = match;
        box.SelectionChanged += (_, _) => { if (box.SelectedItem is ComboBoxItem item) changed((string)item.Tag); };
        return box;
    }
    private void BuildPrototypeInspector()
    {
        var top = AddSection("Prototype");
        top.Body.Children.Add(new StudioButton("Present prototype", () => Run(Surface.Present)) { IsPrimary = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        top.Body.Children.Add(Wrapped("Preview runs in isolation. Back, Restart and Escape never change the design or its undo history.", 10));
        if (Session.Primary is not { } node)
        {
            var flows = AddSection("Flow starting points");
            foreach (var frame in Session.Page.AllNodes().Where(n => n.IsFrame && !string.IsNullOrWhiteSpace(n.PrototypeFlowName)))
            {
                var id = frame.Id;
                flows.Body.Children.Add(new StudioButton(frame.PrototypeFlowName!, () => { Session.Select(Session.Document.Find(id)); Run(Surface.Present); }));
            }
            flows.Body.Children.Add(Wrapped("Select a frame to name a flow, or select any layer to configure interactions."));
            return;
        }
        var nodeId = node.Id;
        if (node.IsFrame)
        {
            var frame = AddSection("Flow and scrolling");
            frame.Body.Children.Add(PrototypeText(node.PrototypeFlowName ?? "", "Flow name", value => EditPrototype(nodeId, "Name prototype flow", n => n.PrototypeFlowName = string.IsNullOrWhiteSpace(value) ? null : value.Trim())));
            frame.Body.Children.Add(PrototypeChoice(node.PrototypeOverflow, value => EditPrototype(nodeId, "Prototype overflow", n => n.PrototypeOverflow = value), "Prototype overflow"));
        }
        var interactions = AddSection("Interactions", "plus", () => EditPrototype(nodeId, "Add prototype interaction", n =>
        {
            if (n.Reactions.Count >= PrototypeValidation.MaxReactionsPerNode) throw new InvalidOperationException("This layer has reached the reaction limit.");
            n.Reactions.Add(new() { Actions = [new() { Kind = PrototypeActionKind.Back }] });
        }));
        if (node.PrototypeTargetId is { } legacy)
        {
            interactions.Body.Children.Add(Wrapped("Legacy click → " + (Session.Document.Find(legacy)?.Name ?? "Missing destination"), 10));
            interactions.Body.Children.Add(new StudioButton("Upgrade legacy interaction", () => EditPrototype(nodeId, "Upgrade prototype link", n =>
            {
                n.Reactions.Insert(0, new() { Actions = [new() { Kind = PrototypeActionKind.Navigate, TargetId = n.PrototypeTargetId }] }); n.PrototypeTargetId = null;
            })));
        }
        if (node.Reactions.Count == 0) interactions.Body.Children.Add(Wrapped("Add an interaction with +. Click fires on release; hover, press, keyboard and delay triggers can run ordered action sequences.", 10));
        foreach (var reaction in node.Reactions)
        {
            var reactionId = reaction.Id;
            PrototypeReaction Resolve(DesignNode n) => n.Reactions.First(r => r.Id == reactionId);
            void Edit(string label, Action<PrototypeReaction> change) => EditPrototype(nodeId, label, n => change(Resolve(n)));
            var section = AddSection(reaction.Trigger + " interaction", "minus", () => EditPrototype(nodeId, "Remove interaction", n => n.Reactions.RemoveAll(r => r.Id == reactionId)));
            section.Body.Children.Add(PrototypeChoice(reaction.Trigger, value => Edit("Change trigger", r => r.Trigger = value), "Interaction trigger"));
            if (reaction.Trigger == PrototypeTrigger.KeyDown)
                section.Body.Children.Add(PrototypeText(reaction.Key, "Interaction key", value => Edit("Keyboard trigger", r => r.Key = PrototypeSession.NormalizeKey(value))));
            if (reaction.Trigger == PrototypeTrigger.AfterDelay)
                section.Body.Children.Add(Number("Delay", reaction.DelayMilliseconds, value => Edit("Trigger delay", r => r.DelayMilliseconds = value), 16, 600000));
            BuildPrototypeActions(section.Body, nodeId, n => Resolve(n).Actions, reaction.Actions, 0);
        }
        if (node.SourceId is not null && node.PrototypeReactionsOverride)
            interactions.Body.Children.Add(new StudioButton("Reset inherited interactions", () => Run(() => Session.Edit("Reset inherited interactions", () =>
            {
                var n = Session.Document.Find(nodeId)!; n.PrototypeReactionsOverride = false;
            }))));
        var note = AddSection("Playback details");
        note.Body.Children.Add(Wrapped("External links require confirmation. Timed and hover events cannot open URLs. Missing targets remain editable and produce a playback error instead of changing the design.", 10));
    }
    private PrototypeAction NewPrototypeAction(PrototypeActionKind kind)
    {
        var action = new PrototypeAction { Kind = kind };
        if (kind is PrototypeActionKind.Navigate or PrototypeActionKind.OpenOverlay or PrototypeActionKind.SwapOverlay or PrototypeActionKind.ScrollTo)
            action.TargetId = Session.Document.AllNodes().FirstOrDefault(n => n.IsFrame)?.Id ?? throw new InvalidOperationException("Create a frame before choosing this action.");
        if (kind == PrototypeActionKind.ChangeVariant)
            action.TargetId = Session.Document.AllNodes().FirstOrDefault(n => n.Kind == NodeKind.Component && n.Parent?.Kind == NodeKind.ComponentSet)?.Id ?? throw new InvalidOperationException("Create a component variant set first.");
        if (kind == PrototypeActionKind.OpenUrl) action.Url = "https://example.com";
        if (kind is PrototypeActionKind.SetVariable or PrototypeActionKind.Conditional)
        {
            var variable = Session.Document.Variables.FirstOrDefault() ?? throw new InvalidOperationException("Create a local variable first.");
            var value = new VariableResolver(Session.Document).Resolve(variable.Id, Session.Primary);
            if (kind == PrototypeActionKind.SetVariable) { action.VariableId = variable.Id; action.Value = value; }
            else action.Condition = new() { VariableId = variable.Id, Value = value };
        }
        return action;
    }
    private void BuildPrototypeActions(StackPanel panel, string nodeId, Func<DesignNode, List<PrototypeAction>> resolve, List<PrototypeAction> actions, int depth)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            var i = index; var action = actions[i];
            PrototypeAction Resolve(DesignNode n) => resolve(n)[i];
            void Edit(string label, Action<PrototypeAction> change) => EditPrototype(nodeId, label, n => change(Resolve(n)));
            var body = new StackPanel { Spacing = 7 };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            header.Children.Add(Studio.Text("Action " + (i + 1), 11, Studio.Muted, true));
            if (i > 0) header.Children.Add(new StudioButton("↑", () => EditPrototype(nodeId, "Reorder action", n => { var list = resolve(n); (list[i - 1], list[i]) = (list[i], list[i - 1]); })) { Width = 28 });
            header.Children.Add(new IconButton("minus", "Remove action " + (i + 1), () => EditPrototype(nodeId, "Remove action", n =>
            {
                var list = resolve(n); if (depth == 0 && list.Count == 1) throw new InvalidOperationException("Remove the interaction to delete its last action."); list.RemoveAt(i);
            })) { Width = 24 });
            body.Children.Add(header);
            body.Children.Add(PrototypeChoice(action.Kind, value => EditPrototype(nodeId, "Change prototype action", n => resolve(n)[i] = NewPrototypeAction(value)), "Action type"));
            if (action.Kind is PrototypeActionKind.Navigate or PrototypeActionKind.OpenOverlay or PrototypeActionKind.SwapOverlay or PrototypeActionKind.ScrollTo or PrototypeActionKind.ChangeVariant)
            {
                var candidates = Session.Document.AllNodes().Where(n => action.Kind == PrototypeActionKind.ScrollTo || (action.Kind == PrototypeActionKind.ChangeVariant ? n.Kind == NodeKind.Component && n.Parent?.Kind == NodeKind.ComponentSet : n.IsFrame));
                body.Children.Add(PrototypeIds(candidates.Select(n => (n.Id, n.Name)), action.TargetId, value => Edit("Set destination", a => a.TargetId = value), "Prototype destination"));
            }
            if (action.Kind == PrototypeActionKind.ChangeVariant)
                body.Children.Add(PrototypeIds(Session.Document.AllNodes().Where(n => n.Kind == NodeKind.Instance).Select(n => (n.Id, n.Name)), action.InstanceId, value => Edit("Interactive instance", a => a.InstanceId = string.IsNullOrEmpty(value) ? null : value), "Interactive instance", true));
            if (action.Kind == PrototypeActionKind.OpenUrl)
                body.Children.Add(PrototypeText(action.Url ?? "", "Prototype URL", value => Edit("Edit prototype URL", a => a.Url = value.Trim())));
            if (action.Kind == PrototypeActionKind.Navigate)
                body.Children.Add(Check("Preserve scroll", action.PreserveScroll, value => Edit("Preserve prototype scroll", a => a.PreserveScroll = value)));
            if (action.Kind is PrototypeActionKind.OpenOverlay or PrototypeActionKind.SwapOverlay)
            {
                body.Children.Add(PrototypeChoice(action.Overlay.Placement, value => Edit("Overlay placement", a => a.Overlay.Placement = value), "Overlay placement"));
                body.Children.Add(Studio.Columns((Number("X", action.Overlay.Offset.X, value => Edit("Overlay X", a => a.Overlay.Offset = a.Overlay.Offset with { X = value })), -1), (Number("Y", action.Overlay.Offset.Y, value => Edit("Overlay Y", a => a.Overlay.Offset = a.Overlay.Offset with { Y = value })), -1)));
                body.Children.Add(Studio.Columns((new ColorField(action.Overlay.Backdrop, value => Edit("Overlay background", a => a.Overlay.Backdrop = value)), -1), (Number("%", action.Overlay.BackdropOpacity * 100, value => Edit("Overlay opacity", a => a.Overlay.BackdropOpacity = value / 100), 0, 100), 74)));
                body.Children.Add(Check("Close when clicking outside", action.Overlay.CloseOnOutsideClick, value => Edit("Outside dismissal", a => a.Overlay.CloseOnOutsideClick = value)));
            }
            if (action.Kind is PrototypeActionKind.SetVariable or PrototypeActionKind.Conditional)
            {
                var variableId = action.Kind == PrototypeActionKind.SetVariable ? action.VariableId : action.Condition?.VariableId;
                body.Children.Add(PrototypeIds(Session.Document.Variables.Select(v => (v.Id, v.Name)), variableId, value => Edit("Prototype variable", a =>
                {
                    var literal = new VariableResolver(Session.Document).Resolve(value, Session.Document.Find(nodeId));
                    if (a.Kind == PrototypeActionKind.SetVariable) { a.VariableId = value; a.Value = literal; a.Operation = PrototypeVariableOperation.Assign; }
                    else a.Condition = new() { VariableId = value, Value = literal };
                }), "Prototype variable"));
                if (action.Kind == PrototypeActionKind.SetVariable)
                    body.Children.Add(PrototypeChoice(action.Operation, value => Edit("Variable operation", a => a.Operation = value), "Variable operation"));
                else body.Children.Add(PrototypeChoice(action.Condition!.Comparison, value => Edit("Prototype comparison", a => a.Condition!.Comparison = value), "Prototype comparison"));
                if (action.Kind == PrototypeActionKind.Conditional || action.Operation != PrototypeVariableOperation.Toggle)
                    body.Children.Add(PrototypeValueEditor(action.Kind == PrototypeActionKind.Conditional ? action.Condition!.Value : action.Value, value => Edit("Prototype value", a => { if (a.Kind == PrototypeActionKind.Conditional) a.Condition!.Value = value; else a.Value = value; })));
                if (action.Kind == PrototypeActionKind.Conditional && depth < PrototypeValidation.MaxBranchDepth)
                {
                    body.Children.Add(Studio.Text("Then", 11, Studio.Ink, true));
                    BuildPrototypeActions(body, nodeId, n => Resolve(n).Then, action.Then, depth + 1);
                    body.Children.Add(Studio.Text("Else", 11, Studio.Ink, true));
                    BuildPrototypeActions(body, nodeId, n => Resolve(n).Else, action.Else, depth + 1);
                }
            }
            if (action.Kind is not (PrototypeActionKind.OpenUrl or PrototypeActionKind.Conditional))
            {
                body.Children.Add(PrototypeChoice(action.Transition.Kind, value => Edit("Prototype transition", a => a.Transition.Kind = value), "Prototype transition"));
                if (action.Transition.Kind != PrototypeTransitionKind.Instant)
                {
                    body.Children.Add(Number("ms", action.Transition.DurationMilliseconds, value => Edit("Transition duration", a => a.Transition.DurationMilliseconds = value), 0, 10000));
                    body.Children.Add(PrototypeChoice(action.Transition.Easing, value => Edit("Transition easing", a => a.Transition.Easing = value), "Transition easing"));
                    if (action.Transition.Kind is PrototypeTransitionKind.MoveIn or PrototypeTransitionKind.Push)
                        body.Children.Add(PrototypeChoice(action.Transition.Direction, value => Edit("Transition direction", a => a.Transition.Direction = value), "Transition direction"));
                }
            }
            panel.Children.Add(new Border { Padding = new(9), CornerRadius = new(6), BorderBrush = Studio.Brush(Studio.Line), BorderThickness = new(1), Child = body });
        }
        if (actions.Count < 32)
            panel.Children.Add(new StudioButton(depth == 0 ? "Add action" : "Add branch action", () => EditPrototype(nodeId, "Add prototype action", n => resolve(n).Add(new() { Kind = PrototypeActionKind.Back }))) { HorizontalAlignment = HorizontalAlignment.Stretch });
    }
    private UIElement PrototypeValueEditor(VariableValue value, Action<VariableValue> change) => value.Type switch
    {
        VariableType.Color => new ColorField(value.Text, color => change(VariableValue.Color(color))),
        VariableType.Number => Number("Value", value.Number, number => change(VariableValue.Float(number)), -1e9, 1e9),
        VariableType.Boolean => Check("True", value.Boolean, flag => change(VariableValue.Bool(flag))),
        _ => PrototypeText(value.Text, "Prototype value", text => change(VariableValue.String(text)))
    };
    private async Task OpenPrototypePlaygroundAsync()
    {
        if (!await ConfirmAsync("Open prototype playground?", "This replaces the current document with an editable interaction sample. Save a copy first to retain your current document.")) return;
        Session.Load(PrototypeSample.Create()); Surface.Fit(firstFrame: true); _prototype = true; RefreshInspector();
        ShowStatus("Choose Present to explore navigation, overlays, hover effects and interactive variants.");
    }
    private async Task OpenPrototypeLinkAsync(string url)
    {
        if (!PrototypeValidation.SafeUrl(url, out var uri)) { ShowStatus("The prototype link is unsafe.", true); return; }
        if (await ConfirmAsync("Open external website?", "This link leaves VectorSpace:\n\n" + uri!.AbsoluteUri))
            await Windows.System.Launcher.LaunchUriAsync(uri);
    }
}
