// MIT License
// Copyright (c) 2025 Dave Black
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Avalonia.Platform.Storage;
using MermaidPad.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MermaidPad.Services;

/// <summary>
/// Provides file operations for opening, saving, and managing .mmd Mermaid diagram files
/// using Avalonia's cross-platform StorageProvider API.
/// </summary>
internal sealed class FileService : IFileService
{
    private readonly ILogger<FileService> _logger;
    private readonly SettingsService _settingsService;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal const double OneMBInBytes = 1_024 * 1_024;
    internal const double MaxFileSizeBytes = 10 * OneMBInBytes; // 10MB file size limit
    private const int MaxRecentFiles = 10;
    private const string MermaidFileExtension = "mmd";
    private const string MermaidFileExtensionWithDot = ".mmd";
    private static readonly List<string> _allowedFilePatterns = [$"*{MermaidFileExtensionWithDot}", "*.txt"];

    /// <summary>
    /// Initializes a new instance of the <see cref="FileService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="settingsService">The settings service for persisting file state.</param>
    public FileService(ILogger<FileService> logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Opens a file picker dialog and reads the selected .mmd file.
    /// </summary>
    /// <param name="storageProvider">The storage provider from the window/view.</param>
    /// <returns>
    /// A tuple containing the file path and content if successful; otherwise, null values.
    /// The file path will be null if the user cancels or an error occurs.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    public Task<(string? FilePath, string? Content)> OpenFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return OpenFileCoreAsync(storageProvider);
    }

    /// <summary>
    /// Asynchronously opens a Mermaid Markdown file using the specified storage provider and returns its local file
    /// path and content.
    /// </summary>
    /// <remarks>The method filters for Mermaid Markdown files and enforces file path and size validation
    /// before reading the content. The file content is read using UTF-8 encoding. If the operation fails or is
    /// cancelled, null values are returned.</remarks>
    /// <param name="storageProvider">The storage provider used to display the file picker and access the selected file. Must support opening files
    /// and reading their contents.</param>
    /// <returns>A tuple containing the local file path and the file content as strings. If the user cancels the operation or the
    /// file is invalid, both values are null.</returns>
    private async Task<(string? FilePath, string? Content)> OpenFileCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            FilePickerOpenOptions options = new FilePickerOpenOptions
            {
                Title = "Open Mermaid Diagram",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Mermaid Markdown")
                    {
                        Patterns = _allowedFilePatterns,
                        MimeTypes = ["text/plain"]
                    }
                ]
            };

            IReadOnlyList<IStorageFile> result = await storageProvider.OpenFilePickerAsync(options);
            if (result.Count == 0)
            {
                // User cancelled
                return (null, null);
            }

            IStorageFile file = result[0];
            string? filePath = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogError("Failed to get local path from selected file");
                return (null, null);
            }

            if (!ValidateFilePath(filePath))
            {
                _logger.LogError("File path validation failed: {FilePath}", filePath);
                return (null, null);
            }

            if (!ValidateFileSize(filePath))
            {
                _logger.LogError("File size exceeds maximum allowed size: {FilePath}", filePath);
                return (null, null);
            }

            // Read file content with UTF-8 encoding
            string content;
            await using (Stream stream = await file.OpenReadAsync())
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                content = await reader.ReadToEndAsync();
            }

            _logger.LogInformation("Successfully opened file: {FilePath} ({CharacterCount} characters)", filePath, content.Length);

            AddToRecentFiles(filePath);
            return (filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file");
            return (null, null);
        }
    }

    /// <summary>
    /// Saves content to the specified file path.
    /// </summary>
    /// <param name="storageProvider">The storage provider from the window/view.</param>
    /// <param name="filePath">The file path to save to. If null, shows a Save As dialog.</param>
    /// <param name="content">The content to save.</param>
    /// <returns>
    /// The file path where content was saved if successful; otherwise, null.
    /// Returns null if the user cancels or an error occurs.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="content"/> is null.</exception>
    public Task<string?> SaveFileAsync(IStorageProvider storageProvider, string? filePath, string content)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(content);     // allow for empty/whitespace content

        return SaveFileCoreAsync(storageProvider, filePath, content);
    }

    /// <summary>
    /// Saves the specified content to a file using the provided storage provider. If no file path is specified, prompts
    /// the user to select a location for saving.
    /// </summary>
    /// <remarks>The content is saved using UTF-8 encoding. If the file path is invalid or an error occurs
    /// during saving, the method returns null. The file path is added to the list of recent files upon successful
    /// save.</remarks>
    /// <param name="storageProvider">The storage provider used to access and save the file.</param>
    /// <param name="filePath">The path of the file to save the content to. If null or empty, a 'Save As' dialog is presented to the user.</param>
    /// <param name="content">The text content to be saved to the file.</param>
    /// <returns>The file path where the content was saved, or null if the operation was cancelled or failed.</returns>
    private async Task<string?> SaveFileCoreAsync(IStorageProvider storageProvider, string? filePath, string content)
    {
        // If no file path is provided, use SaveAs
        if (string.IsNullOrEmpty(filePath))
        {
            return await SaveFileAsAsync(storageProvider, content);
        }

        try
        {
            if (!ValidateFilePath(filePath))
            {
                _logger.LogError("File path validation failed: {FilePath}", filePath);
                return null;
            }

            // Save with UTF-8 encoding
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

            _logger.LogInformation("Successfully saved file: {FilePath} ({CharacterCount} characters)", filePath, content.Length);

            AddToRecentFiles(filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Shows a Save As dialog and saves content to the selected location.
    /// </summary>
    /// <param name="storageProvider">The storage provider from the window/view.</param>
    /// <param name="content">The content to save.</param>
    /// <param name="suggestedFileName">Optional suggested file name.</param>
    /// <returns>
    /// The file path where content was saved if successful; otherwise, null.
    /// Returns null if the user cancels or an error occurs.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="content"/> is null.</exception>
    public Task<string?> SaveFileAsAsync(IStorageProvider storageProvider, string content, string? suggestedFileName = null)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(content);     // allow for empty/whitespace content

        return SaveFileAsCoreAsync(storageProvider, content, suggestedFileName);
    }

    /// <summary>
    /// Saves the specified content to a file using the provided storage provider, optionally suggesting a file name.
    /// Returns the local file path if the operation succeeds; otherwise, returns null.
    /// </summary>
    /// <remarks>The file is saved with a .mmd extension and validated for allowed patterns. If the user
    /// cancels the dialog or an error occurs during saving, the method returns null. The saved file is added to the
    /// recent files list upon success.</remarks>
    /// <param name="storageProvider">The storage provider used to display the save file dialog and handle file creation.</param>
    /// <param name="content">The text content to be saved to the file. The content is written using UTF-8 encoding.</param>
    /// <param name="suggestedFileName">An optional suggested file name to pre-populate in the save dialog. If null, a default name is used.</param>
    /// <returns>The local file path of the saved file if the operation is successful; otherwise, null if the user cancels or an
    /// error occurs.</returns>
    private async Task<string?> SaveFileAsCoreAsync(IStorageProvider storageProvider, string content, string? suggestedFileName = null)
    {
        try
        {
            FilePickerSaveOptions options = new FilePickerSaveOptions
            {
                Title = "Save Mermaid Diagram",
                SuggestedFileName = suggestedFileName ?? $"diagram{MermaidFileExtensionWithDot}",
                DefaultExtension = MermaidFileExtension,
                FileTypeChoices =
                [
                    new FilePickerFileType("Mermaid Markdown")
                    {
                        Patterns = _allowedFilePatterns
                    }
                ],
                ShowOverwritePrompt = true
            };

            IStorageFile? result = await storageProvider.SaveFilePickerAsync(options);
            if (result is null)
            {
                // User cancelled
                return null;
            }

            string? filePath = result.TryGetLocalPath();
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogError("Failed to get local path from save dialog");
                return null;
            }

            if (!ValidateFilePath(filePath))
            {
                _logger.LogError("File path validation failed: {FilePath}", filePath);
                return null;
            }

            // Ensure .mmd extension
            if (!filePath.EndsWith(MermaidFileExtensionWithDot, StringComparison.OrdinalIgnoreCase))
            {
                filePath = $"{filePath}{MermaidFileExtensionWithDot}";
            }

            // Save with UTF-8 encoding
            await using (Stream stream = await result.OpenWriteAsync())
            {
                await using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(content);
                await writer.FlushAsync();
            }

            _logger.LogInformation("Successfully saved file as: {FilePath} ({CharacterCount} characters)", filePath, content.Length);

            AddToRecentFiles(filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file as");
            return null;
        }
    }

    /// <summary>
    /// Validates whether the specified file path is a well-formed, absolute path to a file with a .mmd extension.
    /// </summary>
    /// <remarks>The method checks for invalid path characters and ensures the file extension is .mmd
    /// (case-insensitive). If validation fails, an error is logged. The method does not check for file
    /// existence.</remarks>
    /// <param name="filePath">The file path to validate. Must be a non-empty string representing the location of a .mmd file.</param>
    /// <returns>true if the file path is valid and has a .mmd extension; otherwise, false.</returns>
    public bool ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            // Get full path to normalize
            string fullPath = Path.GetFullPath(filePath);
            if (fullPath.AsSpan().ContainsAny(Path.GetInvalidPathChars()))
            {
                return false;
            }

            // Validate file extension
            string extension = Path.GetExtension(fullPath);
            if (string.Equals(extension, MermaidFileExtensionWithDot, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            _logger.LogError("Invalid file extension: {Extension}. Expected {ExpectedExtension}", extension, MermaidFileExtensionWithDot);
            return false;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File path validation failed");
            return false;
        }
    }

    /// <summary>
    /// Validates that a file size is within acceptable limits - <see cref="MaxFileSizeBytes"/>.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file size is acceptable; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="filePath"/> is null.</exception>
    public bool ValidateFileSize(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);    // allow for empty filePath
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return true; // New file, no size constraints
            }

            if (fileInfo.Length > MaxFileSizeBytes)
            {
                // ReSharper disable once InconsistentNaming
                double sizeMB = fileInfo.Length / OneMBInBytes;
                _logger.LogError("File size ({SizeMB:F2} MB) exceeds maximum allowed size ({MaxSizeMB} MB)", sizeMB, MaxFileSizeBytes / OneMBInBytes);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate file size");
            return false;
        }
    }

    /// <summary>
    /// Adds a file path to the recent files list, maintaining a maximum of <see cref="MaxRecentFiles"/> entries.
    /// </summary>
    /// <param name="filePath">The file path to add to recent files.</param>
    public void AddToRecentFiles(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            AppSettings settings = _settingsService.Settings;

            // Remove if already exists (to move it to top)
            settings.RecentFiles.Remove(filePath);

            // Add to beginning
            settings.RecentFiles.Insert(0, filePath);

            // Keep only the most recent MaxRecentFiles
            if (settings.RecentFiles.Count > MaxRecentFiles)
            {
                settings.RecentFiles.RemoveRange(MaxRecentFiles, settings.RecentFiles.Count - MaxRecentFiles);
            }

            _settingsService.Save();

            _logger.LogInformation("Added to recent files: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add to recent files");
        }
    }

    /// <summary>
    /// Gets the list of recent file paths.
    /// </summary>
    /// <returns>A read-only list of recent file paths.</returns>
    public IReadOnlyList<string> GetRecentFiles() => _settingsService.Settings.RecentFiles.AsReadOnly();

    /// <summary>
    /// Clears the recent files list.
    /// </summary>
    public void ClearRecentFiles()
    {
        _settingsService.Settings.RecentFiles.Clear();
        _settingsService.Save();
        _logger.LogInformation("Recent files cleared");
    }
}
