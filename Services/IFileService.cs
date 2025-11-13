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

namespace MermaidPad.Services;

/// <summary>
/// Provides file operations for opening, saving, and managing .mmd Mermaid diagram files.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Opens a file picker dialog and reads the selected .mmd file.
    /// </summary>
    /// <param name="storageProvider">The storage provider from the window/view.</param>
    /// <returns>
    /// A tuple containing the file path and content if successful; otherwise, null values.
    /// The file path will be null if the user cancels or an error occurs.
    /// </returns>
    Task<(string? FilePath, string? Content)> OpenFileAsync(IStorageProvider storageProvider);

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
    Task<string?> SaveFileAsync(IStorageProvider storageProvider, string? filePath, string content);

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
    Task<string?> SaveFileAsAsync(IStorageProvider storageProvider, string content, string? suggestedFileName = null);

    /// <summary>
    /// Validates that a file path is safe and within expected constraints.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if the file path is valid and safe; otherwise, false.</returns>
    bool ValidateFilePath(string filePath);

    /// <summary>
    /// Validates that a file size is within acceptable limits.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file size is acceptable; otherwise, false.</returns>
    bool ValidateFileSize(string filePath);

    /// <summary>
    /// Adds a file path to the recent files list, maintaining a maximum of 10 entries.
    /// </summary>
    /// <param name="filePath">The file path to add to recent files.</param>
    void AddToRecentFiles(string filePath);

    /// <summary>
    /// Gets the list of recent file paths.
    /// </summary>
    /// <returns>A read-only list of recent file paths.</returns>
    IReadOnlyList<string> GetRecentFiles();

    /// <summary>
    /// Clears the recent files list.
    /// </summary>
    void ClearRecentFiles();
}
