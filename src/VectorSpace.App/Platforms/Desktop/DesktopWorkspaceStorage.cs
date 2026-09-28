using VectorSpace.Documents;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VectorSpace.App;

internal sealed partial class DesktopWorkspaceStorage : IWorkspaceStorage
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
    public async Task<(string Name, byte[] Bytes)?> OpenImageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileOpenPicker();
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp" }) picker.FileTypeFilter.Add(extension);
        var file = await picker.PickSingleFileAsync(); if (file is null) return null;
        var properties = await file.GetBasicPropertiesAsync();
        if (properties.Size > EmbeddedImage.MaxEncodedBytes) throw new InvalidDataException("Images are limited to 8 MiB.");
        using var input = await file.OpenStreamForReadAsync(); using var output = new MemoryStream();
        var buffer = new byte[65536]; int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > EmbeddedImage.MaxEncodedBytes) throw new InvalidDataException("Image size changed during import.");
            output.Write(buffer, 0, read);
        }
        return (file.Name, output.ToArray());
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
