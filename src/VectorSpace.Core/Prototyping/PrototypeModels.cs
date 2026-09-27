namespace VectorSpace.Core;

public enum PrototypeTrigger { Click, MouseEnter, MouseLeave, MouseDown, MouseUp, KeyDown, AfterDelay }
public enum PrototypeActionKind { Navigate, Back, OpenOverlay, SwapOverlay, CloseOverlay, ScrollTo, SetVariable, Conditional, ChangeVariant, OpenUrl }
public enum PrototypeTransitionKind { Instant, Dissolve, MoveIn, Push, SmartAnimate }
public enum PrototypeDirection { Left, Right, Up, Down }
public enum PrototypeEasing { Linear, EaseIn, EaseOut, EaseInOut }
public enum PrototypePlacement { Center, TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight, Manual }
public enum PrototypeOverflow { None, Horizontal, Vertical, Both }
public enum PrototypeComparison { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }
public enum PrototypeVariableOperation { Assign, Toggle, Add }

public sealed class PrototypeTransition
{
    public PrototypeTransitionKind Kind { get; set; }
    public PrototypeDirection Direction { get; set; } = PrototypeDirection.Right;
    public PrototypeEasing Easing { get; set; } = PrototypeEasing.EaseInOut;
    public double DurationMilliseconds { get; set; } = 300;
}

public sealed class PrototypeOverlaySettings
{
    public PrototypePlacement Placement { get; set; }
    public Vec2 Offset { get; set; }
    public string Backdrop { get; set; } = "#000000";
    public double BackdropOpacity { get; set; } = .35;
    public bool CloseOnOutsideClick { get; set; } = true;
}

public sealed class PrototypeCondition
{
    public string VariableId { get; set; } = "";
    public PrototypeComparison Comparison { get; set; }
    public VariableValue Value { get; set; } = VariableValue.Bool(true);
}

/// <summary>A declarative action. Conditional branches contain actions, never executable code.</summary>
public sealed class PrototypeAction
{
    public PrototypeActionKind Kind { get; set; }
    public string? TargetId { get; set; }
    public string? InstanceId { get; set; }
    public string? Url { get; set; }
    public string? VariableId { get; set; }
    public VariableValue Value { get; set; } = VariableValue.Bool(true);
    public PrototypeVariableOperation Operation { get; set; }
    public PrototypeCondition? Condition { get; set; }
    public List<PrototypeAction> Then { get; set; } = [];
    public List<PrototypeAction> Else { get; set; } = [];
    public PrototypeTransition Transition { get; set; } = new();
    public PrototypeOverlaySettings Overlay { get; set; } = new();
    public bool PreserveScroll { get; set; }
}

public sealed class PrototypeReaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public PrototypeTrigger Trigger { get; set; }
    public string Key { get; set; } = "ENTER";
    public double DelayMilliseconds { get; set; } = 800;
    public List<PrototypeAction> Actions { get; set; } = [];
}

public sealed partial class DesignNode
{
    public List<PrototypeReaction> Reactions { get; set; } = [];
    public string? PrototypeFlowName { get; set; }
    public PrototypeOverflow PrototypeOverflow { get; set; }
}
