using VectorSpace.Core;

namespace VectorSpace.Documents;

/// <summary>Original editable prototype playground; all artwork, interactions and tokens are scene data.</summary>
public static class PrototypeSample
{
    public static DesignDocument Create()
    {
        var home = Frame("prototype-home", "01 / Explore", 0, 0, 640, 600, "#F9FAFB"); home.PrototypeFlowName = "Explore interactions";
        var detail = Frame("prototype-detail", "02 / Details", 740, 0, 640, 600, "#EEF1FF"); detail.PrototypeOverflow = PrototypeOverflow.Vertical;
        var modal = Frame("prototype-modal", "03 / Overlay", 1480, 0, 360, 240, "#FFFFFF"); modal.CornerRadius = 18;
        home.Add(Text("Eyebrow", "AETHER  /  PROTOTYPE LAB", 36, 32, 520, 28, 12, "#687086", 650));
        home.Add(Text("Headline", "Make a little\ninteraction magic.", 36, 94, 510, 136, 46, "#15203B", 700));
        home.Add(Text("Description", "Real actions. Private playback. Your design stays unchanged.", 36, 234, 560, 30, 14, "#687086"));
        var orb = home.Add(new() { Name = "Orb", Kind = NodeKind.Ellipse, X = 430, Y = 326, Width = 142, Height = 142, Fill = "#0D99FF" });
        orb.VariableBindings[VariableTarget.Fill] = new() { VariableId = "accent", Fallback = VariableValue.Color("#0D99FF") };
        orb.Reactions = [new() { Trigger = PrototypeTrigger.MouseEnter, Actions = [new() { Kind = PrototypeActionKind.SetVariable, VariableId = "accent", Value = VariableValue.Color("#9747FF"), Transition = Transition(PrototypeTransitionKind.SmartAnimate, 220) }] }, new() { Trigger = PrototypeTrigger.MouseLeave, Actions = [new() { Kind = PrototypeActionKind.SetVariable, VariableId = "accent", Value = VariableValue.Color("#0D99FF"), Transition = Transition(PrototypeTransitionKind.SmartAnimate, 220) }] }];
        Button(home, "Explore the details  →", 36, 296, new() { Kind = PrototypeActionKind.Navigate, TargetId = detail.Id, Transition = Transition(PrototypeTransitionKind.SmartAnimate, 450) });
        Button(home, "Open an overlay", 36, 364, new() { Kind = PrototypeActionKind.OpenOverlay, TargetId = modal.Id, Transition = Transition(PrototypeTransitionKind.Dissolve, 200), Overlay = new() { BackdropOpacity = .35 } }, "#FFFFFF", "#15203B");
        home.Add(Text("Hint", "Hover the orb. Click the small state control.\nPress K to navigate, R to restart, Escape to exit.", 36, 500, 568, 58, 13, "#687086"));
        home.Reactions = [new() { Trigger = PrototypeTrigger.KeyDown, Key = "K", Actions = [new() { Kind = PrototypeActionKind.Navigate, TargetId = detail.Id, Transition = Transition(PrototypeTransitionKind.Push, 300) }] }];
        detail.Add(Text("Eyebrow", "AETHER  /  PROTOTYPE LAB", 36, 32, 520, 28, 12, "#687086", 650));
        detail.Add(Text("Headline", "Same layers.\nA different state.", 36, 94, 550, 136, 46, "#15203B", 700));
        detail.Add(Text("Description", "Matching layer names drive prepared geometry interpolation.", 36, 234, 550, 50, 14, "#687086"));
        detail.Add(new() { Name = "Orb", Kind = NodeKind.Ellipse, X = 392, Y = 332, Width = 194, Height = 194, Fill = "#9747FF" });
        Button(detail, "←  Go back", 36, 300, new() { Kind = PrototypeActionKind.Back, Transition = Transition(PrototypeTransitionKind.Dissolve, 220) });
        var notes = detail.Add(Frame("prototype-notes", "Notes", 36, 820, 568, 230, "#FFFFFF")); notes.CornerRadius = 16;
        notes.Add(Text("Notes heading", "A real scrolling viewport", 24, 26, 520, 42, 25, "#15203B", 650));
        notes.Add(Text("Notes body", "The background remains anchored. Content scrolls inside\nthe frame clip. Navigation history remembers scroll offsets.\n\nRestart restores all private prototype state.", 24, 84, 520, 125, 16, "#687086"));
        Button(detail, "Jump to notes  ↓", 36, 368, new() { Kind = PrototypeActionKind.ScrollTo, TargetId = notes.Id, Transition = Transition(PrototypeTransitionKind.SmartAnimate, 350) }, "#FFFFFF", "#15203B");
        modal.Add(Text("Modal heading", "An overlay,\nnot another page.", 24, 22, 312, 86, 29, "#15203B", 650));
        modal.Add(Text("Modal description", "Click outside to dismiss, or use Escape.", 24, 116, 312, 40, 13, "#687086"));
        Button(modal, "Got it", 24, 170, new() { Kind = PrototypeActionKind.CloseOverlay, Transition = Transition(PrototypeTransitionKind.Dissolve, 180) });
        var set = Frame("prototype-state-set", "State control", 0, 0, 352, 120, "#F4EAFF"); set.Kind = NodeKind.ComponentSet; set.Fills = [];
        var off = Frame("prototype-state-off", "State=Idle", 16, 16, 144, 42, "#E5E7EB"); off.Kind = NodeKind.Component; off.CornerRadius = 21; off.VariantProperties["State"] = "Idle";
        off.Add(Text("Label", "Click to activate", 12, 11, 124, 24, 12, "#334155", 650));
        var on = Frame("prototype-state-on", "State=Active", 180, 16, 144, 42, "#14AE5C"); on.Kind = NodeKind.Component; on.CornerRadius = 21; on.VariantProperties["State"] = "Active";
        on.Add(Text("Label", "Active — click again", 10, 11, 126, 24, 12, "#FFFFFF", 650));
        off.Reactions = [new() { Actions = [new() { Kind = PrototypeActionKind.ChangeVariant, TargetId = on.Id, Transition = Transition(PrototypeTransitionKind.SmartAnimate, 200) }] }];
        on.Reactions = [new() { Actions = [new() { Kind = PrototypeActionKind.ChangeVariant, TargetId = off.Id, Transition = Transition(PrototypeTransitionKind.SmartAnimate, 200) }] }];
        set.Add(off); set.Add(on);
        var instance = DocumentJson.CloneNode(off); foreach (var n in instance.DescendantsAndSelf()) n.SourceId = n.Id;
        DocumentJson.RegenerateIds([instance]); instance.Kind = NodeKind.Instance; instance.ComponentId = off.Id; instance.Name = "Interactive state control"; instance.X = 36; instance.Y = 438; home.Add(instance);
        var document = new DesignDocument { Name = "Aether / Prototype playground", Pages = [new() { Name = "Prototype flows", Nodes = [home, detail, modal] }, new() { Name = "Interactive components", Nodes = [set] }], VariableCollections = [new() { Id = "prototype-colors", Name = "Prototype colors", DefaultModeId = "prototype-default", Modes = [new() { Id = "prototype-default", Name = "Default" }] }], Variables = [new() { Id = "accent", Name = "Orb accent", CollectionId = "prototype-colors", Type = VariableType.Color, Values = new() { ["prototype-default"] = VariableValue.Color("#0D99FF") } }] };
        document.RebuildParents(); DocumentJson.Validate(document); return document;
    }
    private static PrototypeTransition Transition(PrototypeTransitionKind kind, double duration) => new() { Kind = kind, DurationMilliseconds = duration };
    private static DesignNode Frame(string id, string name, double x, double y, double w, double h, string fill) => new() { Id = id, Name = name, Kind = NodeKind.Frame, X = x, Y = y, Width = w, Height = h, Fill = fill, ClipContent = true };
    private static DesignNode Text(string name, string text, double x, double y, double w, double h, double size, string color, int weight = 400) => new() { Name = name, Kind = NodeKind.Text, Text = text, X = x, Y = y, Width = w, Height = h, FontSize = size, FontWeight = weight, LineHeight = 1.1, Fill = color };
    private static DesignNode Button(DesignNode parent, string label, double x, double y, PrototypeAction action, string fill = "#0D99FF", string ink = "#FFFFFF")
    {
        var b = new DesignNode { Name = label, Kind = NodeKind.Frame, X = x, Y = y, Width = 260, Height = 52, Fill = fill, CornerRadius = 10, Reactions = [new() { Actions = [action] }] };
        b.Add(Text("Button label", label, 16, 18, 236, 30, 14, ink, 600)); parent.Add(b); return b;
    }
}
