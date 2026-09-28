using System.Text.Json;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    /// <summary>Input callbacks cannot propagate an expected rejected edit through the Uno
    /// event loop. The session retains its rollback snapshot until submission succeeds.</summary>
    private void ExecuteInput(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException or JsonException)
        {
            CancelGesture();
            StatusChanged?.Invoke(error.Message);
        }
    }
}
