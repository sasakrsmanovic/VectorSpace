using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Workbench;

namespace VectorSpace.App;

internal sealed class BrowserWorkspaceStorage : IWorkspaceStorage
{
    public async Task<string?> ReadAutosaveAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return await BrowserFiles.Load(); }
    public async Task WriteAutosaveAsync(string document, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); await BrowserFiles.Save(document); }
    public async Task<(string Name, string Text)?> OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var result = await BrowserFiles.Open();
        if (string.IsNullOrEmpty(result)) return null;
        using var data = JsonDocument.Parse(result); return (data.RootElement.GetProperty("name").GetString()!, data.RootElement.GetProperty("text").GetString()!);
    }
    public async Task SaveAsync(string name, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); await BrowserFiles.Download(name, Convert.ToBase64String(bytes), contentType);
    }
}
internal static partial class BrowserFiles
{
    [JSImport("globalThis.vectorSpaceStorage.load")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Load();
    [JSImport("globalThis.vectorSpaceStorage.save")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Save(string document);
    [JSImport("globalThis.vectorSpaceStorage.open")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Open();
    [JSImport("globalThis.vectorSpaceStorage.download")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Download(string name, string base64, string contentType);
    [JSImport("globalThis.vectorSpaceStorage.isTestMode")]
    internal static partial bool IsTestMode();
    [JSImport("globalThis.vectorSpaceStorage.publishControls")]
    internal static partial void PublishControls(string json);
    [JSImport("globalThis.vectorSpaceStorage.publishDiagnostics")]
    internal static partial void PublishDiagnostics(string json);
}
internal static class BrowserDiagnostics
{
    // Read-only diagnostics are opt-in and never expose a document mutation API.
    public static void Attach(EditorSession session, StudioWorkbench workbench)
    {
        if (!BrowserFiles.IsTestMode()) return;
        void Publish()
        {
            var primary = session.Primary;
            using var stream = new MemoryStream();
            using (var json = new Utf8JsonWriter(stream))
            {
                json.WriteStartObject(); json.WriteBoolean("ready", true); json.WriteString("tool", session.Tool.ToString());
                json.WriteNumber("nodes", session.Page.AllNodes().Count()); json.WriteNumber("roots", session.Page.Nodes.Count);
                json.WriteNumber("pages", session.Document.Pages.Count); json.WriteNumber("selection", session.Selection.Count);
                json.WriteNumber("history", session.History.Count); json.WriteNumber("zoom", session.Viewport.Zoom);
                json.WriteNumber("panX", session.Viewport.Pan.X); json.WriteNumber("panY", session.Viewport.Pan.Y);
                json.WriteNumber("canvasWidth", workbench.Surface.ActualWidth); json.WriteNumber("canvasHeight", workbench.Surface.ActualHeight);
                json.WriteString("page", session.Page.Name); json.WriteString("name", primary?.Name);
                json.WriteString("kind", primary?.Kind.ToString()); json.WriteBoolean("visible", primary?.Visible ?? false); json.WriteBoolean("locked", primary?.Locked ?? false); json.WriteNumber("x", primary?.X ?? 0); json.WriteNumber("y", primary?.Y ?? 0);
                json.WriteNumber("width", primary?.Width ?? 0); json.WriteNumber("height", primary?.Height ?? 0);
                json.WriteBoolean("canUndo", session.CanUndo); json.WriteBoolean("canRedo", session.CanRedo);
                json.WriteString("id", primary?.Id); json.WriteString("fill", primary?.Fill);
                json.WriteString("componentId", primary?.ComponentId);
                json.WriteNumber("variables", session.Document.Variables.Count);
                json.WriteNumber("collections", session.Document.VariableCollections.Count);
                json.WriteNumber("componentSets", session.Document.AllNodes().Count(n => n.Kind == VectorSpace.Core.NodeKind.ComponentSet));
                json.WriteNumber("bindings", primary?.VariableBindings.Count(p => !p.Value.Disabled) ?? 0);
                json.WriteString("layout", primary?.Layout.Direction.ToString());
                json.WriteStartObject("variants");
                if (primary is not null) foreach (var pair in primary.VariantProperties) json.WriteString(pair.Key, pair.Value);
                json.WriteEndObject();
                json.WriteStartArray("selectedIds"); foreach (var id in session.SelectedIds) json.WriteStringValue(id); json.WriteEndArray();
                json.WriteBoolean("presenting", workbench.Surface.IsPresenting); json.WriteEndObject();
            }
            BrowserFiles.PublishDiagnostics(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
        var controls = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        controls.Tick += (_, _) => BrowserFiles.PublishControls(workbench.CaptureAutomationState());
        workbench.Unloaded += (_, _) => controls.Stop(); controls.Start();
        session.Changed += (_, _) => Publish(); workbench.Surface.SizeChanged += (_, _) => Publish(); workbench.Surface.PresentationChanged += _ => Publish(); Publish();
    }
}
