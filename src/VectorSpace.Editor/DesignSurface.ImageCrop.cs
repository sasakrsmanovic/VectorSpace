using VectorSpace.Skia;

namespace VectorSpace.Editor;

public sealed partial class DesignSurface
{
    private string? _cropNodeId;
    private int _cropFillIndex;
    private DesignDocument? _cropDocument;
    private Vec2 _cropOffset, _cropStart;
    public bool IsImageCropping => _cropNodeId is not null;

    public void BeginImageCrop(string nodeId, int fillIndex)
    {
        if (Session is not { } editor || IsPresenting) return;
        CancelGesture(); FinishTextEdit(true);
        var node = editor.Document.Find(nodeId);
        if (node is null || node.IsEffectivelyLocked || fillIndex < 0 || fillIndex >= node.Fills.Count ||
            node.Fills[fillIndex] is not { Kind: FillKind.Image, ImageData: not null } fill || Renderer.Images.Get(fill.ImageData) is not { } image)
            throw new InvalidOperationException("Select an unlocked layer with a valid image fill.");
        editor.Select(node);
        if (fill.ImageMode != ImageScaleMode.Crop)
        {
            editor.Edit("Enable image crop", () =>
            {
                var old = ImagePlacement.Calculate(fill, node.Width, node.Height, image.Width, image.Height);
                // Fill/Fit ignore stored crop offsets. Do not reactivate an old offset
                // when entering crop mode: the currently visible image must stay put.
                if (fill.ImageMode is ImageScaleMode.Fill or ImageScaleMode.Fit) fill.ImageOffset = Vec2.Zero;
                fill.ImageMode = ImageScaleMode.Crop; fill.ImageScale = 1;
                var next = ImagePlacement.Calculate(fill, node.Width, node.Height, image.Width, image.Height);
                var oldScale = Math.Sqrt(old.M11 * old.M11 + old.M12 * old.M12);
                var newScale = Math.Sqrt(next.M11 * next.M11 + next.M12 * next.M12);
                fill.ImageScale = Math.Clamp(oldScale / newScale, .001, 1000);
                ComponentService.SetAppearanceOverride(node, true, false);
            });
        }
        _cropNodeId = nodeId; _cropFillIndex = fillIndex; _cropDocument = editor.Document;
        StatusChanged?.Invoke("Crop image · drag to reposition · wheel to zoom · Enter to finish · Escape to cancel active drag");
        editor.Notify(EditorChangeKind.Selection); FocusCanvas(); RequestFrame();
    }
    public void EndImageCrop(bool cancel = false)
    {
        if (!cancel && _gesture == Gesture.ImageCrop && CropTarget(out var cropped, out _))
            ComponentService.SetAppearanceOverride(cropped, true, false);
        _cropNodeId = null; _cropDocument = null;
        if (_gesture == Gesture.ImageCrop)
        {
            _gesture = Gesture.None;
            if (cancel) Session?.CancelInteraction(); else Session?.CommitInteraction();
        }
        _canvas.ReleasePointerCaptures(); Session?.Notify(EditorChangeKind.Selection); RequestFrame();
    }
    private bool CropTarget(out DesignNode node, out FillStyle fill)
    {
        node = null!; fill = null!;
        if (_cropNodeId is null || Session is not { } editor || !ReferenceEquals(editor.Document, _cropDocument) ||
            editor.Primary?.Id != _cropNodeId || editor.Document.Find(_cropNodeId) is not { } target || target.IsEffectivelyLocked ||
            _cropFillIndex >= target.Fills.Count || target.Fills[_cropFillIndex].Kind != FillKind.Image)
        { _cropNodeId = null; _cropDocument = null; return false; }
        node = target; fill = target.Fills[_cropFillIndex]; return true;
    }
    private bool PressImageCrop(Vec2 world)
    {
        if (!CropTarget(out var node, out var fill)) return false;
        var local = node.WorldMatrix.Inverse.Map(world);
        if (!node.LocalBounds.Contains(local)) { EndImageCrop(); return true; }
        _cropStart = local; _cropOffset = fill.ImageOffset;
        Session!.BeginInteraction("Reposition image crop"); _gesture = Gesture.ImageCrop; return true;
    }
    private void MoveImageCrop(Vec2 world)
    {
        if (!CropTarget(out var node, out var fill)) return;
        var delta = node.WorldMatrix.Inverse.Map(world) - _cropStart;
        fill.ImageOffset = new(Math.Clamp(_cropOffset.X + delta.X / Math.Max(node.Width, 1e-9), -1000, 1000),
            Math.Clamp(_cropOffset.Y + delta.Y / Math.Max(node.Height, 1e-9), -1000, 1000));
        Session!.Preview(false);
    }
    private bool ZoomImageCrop(Vec2 screen, double wheel)
    {
        if (!CropTarget(out var node, out var fill) || fill.ImageData is null || Renderer.Images.Get(fill.ImageData) is not { } image) return false;
        if (Session!.IsInteracting) return true;
        var local = node.WorldMatrix.Inverse.Map(Session.Viewport.ScreenToWorld(screen));
        var pixel = ImagePlacement.Calculate(fill, node.Width, node.Height, image.Width, image.Height).Inverse.Map(local);
        Session.Edit("Zoom image crop", () =>
        {
            fill.ImageScale = Math.Clamp(fill.ImageScale * Math.Exp(Math.Clamp(wheel * .0015, -3, 3)), .001, 1000);
            var moved = ImagePlacement.Calculate(fill, node.Width, node.Height, image.Width, image.Height).Map(pixel);
            fill.ImageOffset += new Vec2((local.X - moved.X) / Math.Max(node.Width, 1e-9), (local.Y - moved.Y) / Math.Max(node.Height, 1e-9));
            fill.ImageOffset = new(Math.Clamp(fill.ImageOffset.X, -1000, 1000), Math.Clamp(fill.ImageOffset.Y, -1000, 1000));
            ComponentService.SetAppearanceOverride(node, true, false);
        });
        return true;
    }
}
