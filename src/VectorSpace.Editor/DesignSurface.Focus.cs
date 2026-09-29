using Microsoft.UI.Xaml.Input;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    /// <summary>Read-only host/test signal; it never changes focus or executes a document mutation.</summary>
    public bool HasCanvasKeyboardFocus => XamlRoot is not null && ReferenceEquals(FocusManager.GetFocusedElement(XamlRoot), this);
}
