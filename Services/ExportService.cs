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

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MermaidPad.Dialogs;
using MermaidPad.Models;
using System.Text;

using System.Text.Json;

namespace MermaidPad.Services;
/// <summary>
/// Provides export functionality for Mermaid diagrams to SVG and PNG formats.
/// </summary>
public sealed class ExportService
{
    private static readonly string[] _pngWildcard = ["*.png"];
    private static readonly string[] _svgWildcard = ["*.svg"];
    private static readonly string[] _wildcardAsterisk = ["*"];
    private readonly MermaidRenderer _renderer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    /// <param name="renderer">The Mermaid renderer instance.</param>
    public ExportService(MermaidRenderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>
    /// Exports the current diagram to a file using the specified export options.
    /// </summary>
    /// <remarks>This method displays a progress dialog while preparing the export and uses a save file dialog
    /// to allow the user to select the destination file. If the export is successful, a success notification is
    /// displayed. If an error occurs, an error dialog is shown.</remarks>
    /// <param name="parentWindow">The parent window used to display dialogs and notifications during the export process. Cannot be <see
    /// langword="null"/>.</param>
    /// <param name="options">The export options specifying the format and other settings for the export. Cannot be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the diagram was successfully exported; otherwise, <see langword="false"/>. Returns
    /// <see langword="false"/> if the user cancels the operation, no diagram is available to export, or an error
    /// occurs.</returns>
    public async Task<bool> ExportDiagramAsync(Window parentWindow, ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(parentWindow);
        ArgumentNullException.ThrowIfNull(options);

        ProgressDialog? progressDialog = null;

        try
        {
            // Show progress while extracting from WebView
            progressDialog = new ProgressDialog
            {
                Message = $"Preparing {options.Format} export..."
            };
            progressDialog.Show(parentWindow);

            // Extract diagram data from WebView
            string? exportData = await ExtractDiagramDataAsync(options);

            progressDialog.Close();
            progressDialog = null;

            if (string.IsNullOrEmpty(exportData))
            {
                await ShowErrorDialogAsync(parentWindow, "No diagram available to export. Please create a diagram first.");
                return false;
            }

            string formatName = options.Format.ToString().ToLower();

            // Show Avalonia SaveFileDialog
            IStorageProvider storageProvider = parentWindow.StorageProvider;
            List<FilePickerFileType> fileTypeChoices = GetFileTypeChoices(options.Format);

            string suggestedFileName = $"diagram_{DateTime.Now:yyyyMMdd_HHmmss}.{formatName}";

            IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Export Diagram as {options.Format}",
                SuggestedFileName = suggestedFileName,
                FileTypeChoices = fileTypeChoices,
                DefaultExtension = formatName
            });

            if (file is null)
            {
                return false; // User cancelled
            }

            // Save to disk using .NET IO
            await SaveToFileAsync(file, exportData, options);

            // Show success notification
            await ShowSuccessNotificationAsync(parentWindow, file.Name);
            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Export failed", ex);
            progressDialog?.Close();
            await ShowErrorDialogAsync(parentWindow, $"Export failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Executes JavaScript in the WebView and returns the result.
    /// </summary>
    /// <param name="script">JavaScript code to execute.</param>
    /// <returns>The result as a string, or null if execution fails.</returns>
    public async Task<string?> ExecuteScriptAsync(string script)
    {
        return await _renderer.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// Extracts diagram data in the specified export format asynchronously.
    /// </summary>
    /// <remarks>The method supports exporting diagrams in SVG and PNG formats. For PNG exports, the
    /// background color can be configured based on the <see cref="ExportOptions.TransparentBackground"/> property.
    /// The background color assignment and PNG extraction are combined into a single WebView script execution
    /// to reduce round-trips.</remarks>
    /// <param name="options">The export options specifying the format, background color, and scale for the diagram export.</param>
    /// <returns>A string containing the exported diagram data. For PNG format, the result is a Base64-encoded string. For SVG
    /// format, the result is the raw SVG markup. Returns <see langword="null"/> if the extraction fails.</returns>
    private async Task<string?> ExtractDiagramDataAsync(ExportOptions options)
    {
        try
        {
            // Build the script based on requested format.
            string? result = options.Format switch
            {
                ExportFormat.SVG => await ExecuteScriptAsync("window.exportDiagram ? window.exportDiagram.getSVG() : null"),

                // PNG: combine backgroundColor assignment and PNG extraction into a single execution.
                // Use JsonSerializer to safely quote the color as a JS string literal.
                ExportFormat.PNG => await ExecuteScriptAsync(
                    $"window.exportDiagram ? " +
                    $"(window.exportDiagram.backgroundColor = {JsonSerializer.Serialize(options.TransparentBackground ? "transparent" : options.BackgroundColor)}, " +
                    $"await window.exportDiagram.getPNG({options.Scale})) : null"),

                _ => null
            };

            if (string.IsNullOrEmpty(result) || result == "null")
            {
                SimpleLogger.LogError("Export extraction returned null");
                return null;
            }

            // For PNG, result is already base64 without prefix (handled in JS).
            // For SVG, de-quote if WebView returned a JSON string.
            ReadOnlySpan<char> resultSpan = result.AsSpan();
            if (options.Format == ExportFormat.SVG && resultSpan.StartsWith('"') && resultSpan.EndsWith('"'))
            {
                // Remove quotes from JSON string
                result = resultSpan.Trim('"').ToString();

                // Unescape any escaped characters
                result = JsonSerializer.Deserialize<string>('"' + result + '"') ?? result;
            }

            return result;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to extract diagram data", ex);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a list of file type choices based on the specified export format.
    /// </summary>
    /// <remarks>The returned list will contain file type patterns relevant to the specified export format.
    /// For unsupported formats, only the "All Files" option is included.</remarks>
    /// <param name="format">The export format for which file type choices are to be retrieved.</param>
    /// <returns>A list of <see cref="FilePickerFileType"/> objects representing the file type choices available for the
    /// specified export format. The list includes format-specific file types and a generic "All Files" option.</returns>
    private static List<FilePickerFileType> GetFileTypeChoices(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.SVG =>
            [
                new FilePickerFileType("SVG Files") { Patterns = _svgWildcard },
                new FilePickerFileType("All Files") { Patterns = _wildcardAsterisk }
            ],
            ExportFormat.PNG =>
            [
                new FilePickerFileType("PNG Files") { Patterns = _pngWildcard },
                new FilePickerFileType("All Files") { Patterns = _wildcardAsterisk }
            ],
            _ => [new FilePickerFileType("All Files") { Patterns = _wildcardAsterisk }]
        };
    }

    /// <summary>
    /// Asynchronously saves the specified data to the provided file using the given export options.
    /// </summary>
    /// <remarks>The method writes the data to the file in the format specified by <paramref name="options"/>.
    /// If the format is <see cref="ExportFormat.PNG"/>, the data is expected to be Base64-encoded and will be decoded
    /// before writing. For other formats, the data is written as UTF-8 encoded text.</remarks>
    /// <param name="file">The file to which the data will be written. Must support write operations.</param>
    /// <param name="data">The data to save. For PNG format, this should be a Base64-encoded string; for other formats, it should be plain
    /// text.</param>
    /// <param name="options">The export options that determine the format of the saved data.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    private static async Task SaveToFileAsync(IStorageFile file, string data, ExportOptions options)
    {
        await using Stream stream = await file.OpenWriteAsync();

        if (options.Format == ExportFormat.PNG)
        {
            byte[] bytes = Convert.FromBase64String(data);
            await stream.WriteAsync(bytes);
        }
        else
        {
            // Write SVG content as UTF8
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            await stream.WriteAsync(bytes);
        }

        await stream.FlushAsync();
    }

    /// <summary>
    /// Displays an error dialog with the specified message and title.
    /// </summary>
    /// <param name="parent">The parent window that owns the dialog. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="message">The error message to display in the dialog. This parameter cannot be <see langword="null"/> or empty.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ShowErrorDialogAsync(Window parent, string message)
    {
        await MessageDialog.ShowErrorAsync(parent, "Export Error", message);
    }

    /// <summary>
    /// Displays a success notification dialog indicating that a diagram was successfully exported.
    /// </summary>
    /// <param name="parent">The parent <see cref="Window"/> that owns the dialog.</param>
    /// <param name="fileName">The name of the file to which the diagram was exported. This value is displayed in the notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ShowSuccessNotificationAsync(Window parent, string fileName)
    {
        await MessageDialog.ShowSuccessAsync(parent, "Export Successful",
            $"Diagram exported successfully to:\n{fileName}");
    }

    //private async Task ShowErrorDialogAsync(Window parent, string message)
    //{
    //    var messageBox = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
    //        "Export Error",
    //        message,
    //        MessageBox.Avalonia.Enums.ButtonEnum.Ok,
    //        MessageBox.Avalonia.Enums.Icon.Error);
    //
    //    await messageBox.ShowDialog(parent);
    //}

    //private async Task ShowSuccessNotificationAsync(Window parent, string fileName)
    //{
    //    var messageBox = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
    //        "Export Successful",
    //        $"Diagram exported successfully to:\n{fileName}",
    //        MessageBox.Avalonia.Enums.ButtonEnum.Ok,
    //        MessageBox.Avalonia.Enums.Icon.Success);
    //
    //    await messageBox.ShowDialog(parent);
    //}

    //TODO consider deleting these....... Legacy methods - kept for potential future use
    public static void ExportSvg(string svg, string targetPath)
    {
        File.WriteAllText(targetPath, svg, Encoding.UTF8);
    }

    public void ExportPng(string svg, string targetPath)
    {
        // Future: Could implement server-side PNG conversion here
        throw new NotImplementedException("Direct SVG to PNG conversion not yet implemented");
    }
}
