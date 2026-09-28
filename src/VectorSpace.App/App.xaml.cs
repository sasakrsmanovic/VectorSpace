using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using VectorSpace.Controls;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Workbench;
using Windows.Storage;

namespace VectorSpace.App;

public partial class App : Application
{
    private Window? _window;
    private StudioWorkbench? _workbench;
    public App() { InitializeComponent(); RequestedTheme = ApplicationTheme.Light; }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "VectorSpace" };
        _window.Content = new Grid { Background = Studio.Brush("#F5F5F5"), Children = { new TextBlock { Text = "VectorSpace", FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
        _window.Activate();
        try
        {
#if __WASM__
            IWorkspaceStorage storage = new BrowserWorkspaceStorage();
#else
            IWorkspaceStorage storage = new DesktopWorkspaceStorage();
#endif
            var document = SampleDocument.Create(); string? warning = null;
            try { var saved = await storage.ReadAutosaveAsync(); if (!string.IsNullOrWhiteSpace(saved)) document = DocumentJson.Load(saved); }
            catch (Exception ex) { warning = "The recovery copy could not be opened: " + ex.Message; }
            Studio.Font = new FontFamily("ms-appx:///Assets/Fonts/Inter.ttf#Inter");
            var session = new EditorSession(document);
            _workbench = new StudioWorkbench(session, storage);
            _window.Content = _workbench;
            _window.Closed += (_, _) => _workbench.Dispose();
            _window.Activated += (_, e) => { if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated) _workbench.Surface.IsSpaceDown = false; };
            if (warning is not null) _workbench.ShowStatus(warning, true);
            try
            {
                var fontFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Fonts/Inter.ttf"));
                using var stream = await fontFile.OpenStreamForReadAsync();
                using var fontBytes = new MemoryStream(); await stream.CopyToAsync(fontBytes);
                using var fontData = SKData.CreateCopy(fontBytes.ToArray());
                var typeface = SKTypeface.FromData(fontData);
                if (typeface is not null) { _workbench.Surface.Renderer.SetTypeface(typeface); _workbench.Surface.Invalidate(); }
            }
            catch (Exception ex) { Console.WriteLine("Optional Inter font unavailable: " + ex.Message); }
#if __WASM__
            BrowserDiagnostics.Attach(session, _workbench);
            _workbench.CollaborationApplicationUrl = BrowserSharedFiles.ApplicationUrl();
            var invitation = BrowserSharedFiles.TakeInvitation();
            if (!string.IsNullOrWhiteSpace(invitation)) _workbench.OpenCollaborationInvitation(invitation);
#endif
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            _window.Content = new ScrollViewer { Content = new TextBlock { Text = "VectorSpace could not start.\n\n" + ex.Message + "\n\nYour saved data has not been deleted. Reload to retry.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(40), FontSize = 16 } };
        }
    }
}
