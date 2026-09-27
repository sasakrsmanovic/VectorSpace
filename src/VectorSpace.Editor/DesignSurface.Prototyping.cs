namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    private PrototypePlayer? _prototypePlayer;
    public PrototypePlayer? PrototypePlayer => _prototypePlayer;
    public event Action<string>? PrototypeLinkRequested;
    public void Present()
    {
        if (Session is not { } editor || _prototypePlayer is not null) return;
        FinishTextEdit(true); if (editor.IsInteracting) editor.CommitInteraction(); CancelGesture();
        var frame = editor.SelectionRoots.FirstOrDefault(n => n.IsFrame)
            ?? editor.Page.AllNodes().FirstOrDefault(n => n.IsFrame && !string.IsNullOrWhiteSpace(n.PrototypeFlowName))
            ?? editor.Page.Nodes.FirstOrDefault(n => n.IsFrame);
        if (frame is null) { StatusChanged?.Invoke("Create a frame to present a prototype."); return; }
        var player = new PrototypePlayer(editor.Document, frame.Id, Renderer);
        player.ExitRequested += ExitPresentation;
        player.StatusChanged += message => StatusChanged?.Invoke(message);
        player.LinkRequested += url => PrototypeLinkRequested?.Invoke(url);
        _prototypePlayer = player; ((Grid)Content).Children.Add(player);
        PresentationChanged?.Invoke(true); RequestFrame();
    }
    public void ExitPresentation()
    {
        if (_prototypePlayer is not { } player) return;
        _prototypePlayer = null; ((Grid)Content).Children.Remove(player); player.Dispose();
        PresentationChanged?.Invoke(false); FocusCanvas(); RequestFrame();
    }
}
