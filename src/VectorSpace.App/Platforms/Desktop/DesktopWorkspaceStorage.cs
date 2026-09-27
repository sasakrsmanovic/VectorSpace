using VectorSpace.Documents;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VectorSpace.App;

internal sealed class DesktopWorkspaceStorage : IWorkspaceStorage
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VectorSpace");
    private static string AutosavePath => Path.Combine(DirectoryPath, "workspace.vectorspace");
    public async Task<string?> ReadAutosaveAsync(CancellationToken cancellationToken = default) => File.Exists(AutosavePath) ? await File.ReadAllTextAsync(AutosavePath, cancellationToken) : null;
    public async Task WriteAutosaveAsync(string document, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = AutosavePath + ".tmp";
        await File.WriteAllTextAsync(temporary, document, cancellationToken);
        File.Move(temporary, AutosavePath, true);
    }
    public async Task<(string Name, string Text)?> OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileOpenPicker();
        foreach (var extension in new[] { ".vectorspace", ".json", ".svg" }) picker.FileTypeFilter.Add(extension);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        var properties = await file.GetBasicPropertiesAsync();
        if (properties.Size > DocumentJson.MaxDocumentCharacters) throw new InvalidDataException("This document exceeds the import size limit.");
        return (file.Name, await FileIO.ReadTextAsync(file));
    }
    public async Task SaveAsync(string name, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(name) };
        picker.FileTypeChoices.Add(contentType, [Path.GetExtension(name)]);
        var file = await picker.PickSaveFileAsync();
        if (file is null) throw new OperationCanceledException("Save was cancelled.");
        await FileIO.WriteBytesAsync(file, bytes);
    }
}
