using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MermaidPad.Services;

public class FileService
{
    private const string _defaultMermaidExtension = "mmd";
    private static readonly string[] _mermaidFileExtensions = [$"*.{_defaultMermaidExtension}", "*.mermaid"];
    private static readonly string[] _textFileExtensions = ["*.txt"];
    private static readonly string[] _wildcardFileExtension = ["*.*"];

    public static async Task<string?> OpenFileAsync(Window parent)
    {
        IStorageProvider storageProvider = parent.StorageProvider;

        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Mermaid File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Mermaid Files") { Patterns = _mermaidFileExtensions },
                new FilePickerFileType("Text Files") { Patterns = _textFileExtensions },
                new FilePickerFileType("All Files") { Patterns = _wildcardFileExtension }
            ]
        });

        if (files.Count > 0)
        {
            await using Stream stream = await files[0].OpenReadAsync();
            using StreamReader reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        return null;
    }

    public static async Task<bool> SaveFileAsync(Window parent, string content, string? suggestedFileName = null)
    {
        IStorageProvider storageProvider = parent.StorageProvider;

        IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Mermaid File",
            SuggestedFileName = suggestedFileName ?? $"diagram.{_defaultMermaidExtension}",
            DefaultExtension = _defaultMermaidExtension,
            FileTypeChoices =
            [
                new FilePickerFileType("Mermaid Files") { Patterns = _mermaidFileExtensions },
                new FilePickerFileType("Text Files") { Patterns = _textFileExtensions }
            ]
        });

        if (file is not null)
        {
            await using Stream stream = await file.OpenWriteAsync();
            await using StreamWriter writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
            return true;
        }

        return false;
    }
}
