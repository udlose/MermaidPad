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

using Avalonia.Threading;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MermaidPad.Services.Export;

/// <summary>
/// Provides services for exporting diagrams to SVG and PNG formats, including content extraction, optimization, and
/// file output operations.
/// </summary>
/// <remarks>The ExportService class enables asynchronous export of the current diagram to SVG and PNG files,
/// supporting customizable export options and progress reporting. All export operations that interact with the
/// diagram's rendered content require access to the UI thread due to WebView control constraints. The class is designed
/// for use in UI applications where diagrams are rendered and need to be saved or processed in standard image formats.
/// Thread safety is not guaranteed; callers should ensure that methods are invoked on the appropriate thread as
/// documented.</remarks>
public sealed partial class ExportService
{
    [GeneratedRegex(@">\s+<", RegexOptions.Compiled)]
    private static partial Regex GetWhitespaceBetweenTagsRegex();
    private static readonly XNamespace _svgNamespace = "http://www.w3.org/2000/svg";
    private static readonly TimeSpan _exportToPngTimeout = TimeSpan.FromSeconds(60);
    private const int ExportPngBufferSize = 81_920; // 80 KB buffer size for file I/O

    private readonly MermaidRenderer _mermaidRenderer;
    private readonly IImageConversionService _imageConversionService;

    public ExportService(MermaidRenderer mermaidRenderer, IImageConversionService imageConversionService)
    {
        _mermaidRenderer = mermaidRenderer;
        _imageConversionService = imageConversionService;
    }

    /// <summary>
    /// Exports the current diagram as an SVG file to the specified path asynchronously.
    /// </summary>
    /// <remarks>The export operation reads the SVG content from the current diagram and writes it to the
    /// specified file. If the directory for the target path does not exist, it will be created. The method must be
    /// called from the UI thread due to WebView access requirements. The export can be customized using the provided
    /// options.</remarks>
    /// <param name="targetPath">The file system path where the SVG file will be saved. Cannot be null or empty.</param>
    /// <param name="options">The options to use for SVG export, such as optimization and XML declaration settings. If null, default options
    /// are used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the export operation.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the SVG content cannot be extracted from the diagram.</exception>
    public async Task ExportSvgAsync(string targetPath, SvgExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        options ??= new SvgExportOptions();
        SimpleLogger.Log($"Starting SVG export to: {targetPath}");

        try
        {
            // Get SVG content from the WebView - MUST stay on UI thread (WebView access)
            string? svgContent = await GetSvgContentAsync();

            if (string.IsNullOrWhiteSpace(svgContent))
            {
                throw new InvalidOperationException("Failed to extract SVG content from diagram");
            }

            // Apply SVG optimization if requested
            if (options.Optimize)
            {
                SimpleLogger.Log("Optimizing SVG content...");
                svgContent = OptimizeSvg(svgContent, options);
                SimpleLogger.Log($"SVG optimization complete. Original length: {svgContent.Length}");
            }

            // Apply XML declaration option
            ReadOnlySpan<char> svgSpan = svgContent.AsSpan().TrimStart();
            if (!options.IncludeXmlDeclaration && svgSpan.StartsWith("<?xml"))
            {
                // Remove XML declaration
                int endOfDeclaration = svgSpan.IndexOf("?>", StringComparison.Ordinal);
                if (endOfDeclaration >= 0)
                {
                    svgContent = svgSpan[(endOfDeclaration + 2)..].ToString().TrimStart();
                }
            }
            else if (options.IncludeXmlDeclaration && !svgSpan.StartsWith("<?xml"))
            {
                // Add XML declaration if not present
                svgContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + svgContent;
            }

            // File I/O can be done on background thread
            await Task.Run(async () =>
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write SVG content - ConfigureAwait(false) is OK here
                await File.WriteAllTextAsync(targetPath, svgContent, Encoding.UTF8, cancellationToken)
                    .ConfigureAwait(false);
            }, cancellationToken)
            .ConfigureAwait(false);

            SimpleLogger.Log($"SVG exported successfully: {new FileInfo(targetPath).Length:N0} bytes");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"SVG export failed: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Optimizes the content of an SVG file based on the specified options.
    /// </summary>
    /// <remarks>This method performs various optimizations on the SVG content, including: <list
    /// type="bullet"> <item>Removing comments if specified in the options.</item> <item>Removing common metadata
    /// elements, such as <c>&lt;metadata&gt;</c>.</item> <item>Removing unnecessary attributes, such as empty
    /// attributes or <c>xml:space</c> attributes.</item> <item>Minifying the SVG content by removing unnecessary
    /// whitespace, if specified in the options.</item> </list> If an exception occurs during the optimization process,
    /// the method logs the error and returns the original SVG content.</remarks>
    /// <param name="svgContent">The SVG content to be optimized, represented as a string.</param>
    /// <param name="options">An <see cref="SvgExportOptions"/> object specifying the optimization options, such as whether to remove comments
    /// or minify the SVG.</param>
    /// <returns>A string containing the optimized SVG content. If an error occurs during optimization, the original SVG content
    /// is returned.</returns>
    [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "Explicit is clearer here")]
    private static string OptimizeSvg(string svgContent, SvgExportOptions options)
    {
        try
        {
            // Parse the SVG as XML
            XDocument doc = XDocument.Parse(svgContent, LoadOptions.PreserveWhitespace);

            // TODO review this for performance optimization - could be slow on large SVGs

            // Remove comments if requested
            if (options.RemoveComments)
            {
                doc.DescendantNodes()
                    .OfType<XComment>()
                    .ToList()
                    .ForEach(static comment => comment.Remove());
            }

            // Remove common metadata elements

            // Remove <metadata> elements
            doc.Descendants(_svgNamespace + "metadata").Remove();

            // TODO Remove <title> elements (optional - some prefer to keep these)
            // doc.Descendants(svg + "title").Remove();

            // TODO Remove <desc> elements (optional)
            // doc.Descendants(svg + "desc").Remove();

            // Remove unnecessary attributes
            foreach (XElement element in doc.Descendants())
            {
                // Remove xml:space attributes
                element.Attributes().Where(static a => a.Name.LocalName == "space" && a.Name.Namespace == XNamespace.Xml).Remove();

                // Remove empty attributes
                element.Attributes().Where(static a => string.IsNullOrWhiteSpace(a.Value)).Remove();
            }

            // Minify if requested
            if (options.MinifySvg)
            {
                // Save without indentation for minification
                string result = doc.ToString(SaveOptions.DisableFormatting);

                // Remove unnecessary whitespace between tags
                return GetWhitespaceBetweenTagsRegex().Replace(result, "><");
            }

            // Save with normal formatting
            return doc.ToString(SaveOptions.None);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("SVG optimization failed, returning original content", ex);
            return svgContent;
        }
    }

    /// <summary>
    /// Exports the current diagram as a PNG image to the specified file path asynchronously using browser-based rendering.
    /// </summary>
    /// <remarks>
    /// This method uses the browser's canvas API to render the diagram to PNG, which provides pixel-perfect
    /// accuracy matching the live preview. This approach correctly handles foreignObject elements, HTML/CSS styling,
    /// and all Mermaid features including ELK layouts. The export operation runs asynchronously with progress reporting.
    /// </remarks>
    /// <param name="targetPath">The file path where the exported PNG image will be saved. Cannot be null or empty.</param>
    /// <param name="options">The options to use for PNG export, such as DPI and scale factor. If null, default options are used.</param>
    /// <param name="progress">An optional progress reporter that receives updates about the export operation. Progress updates are marshaled
    /// to the UI thread if provided.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the export operation.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the PNG export fails.</exception>
    public async Task ExportPngAsync(
        string targetPath,
        PngExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        options ??= new PngExportOptions();
        SimpleLogger.Log($"Starting browser-based PNG export to: {targetPath} (DPI: {options.Dpi}, Scale: {options.ScaleFactor}x)");

        try
        {
            // Report initial progress - marshal to UI thread
            ReportProgress(progress, new ExportProgress
            {
                Step = ExportStep.Initializing,
                PercentComplete = 0,
                Message = "Initializing PNG export..."
            });

            // Ensure we're on UI thread for WebView access.
            // Instead of recursively calling ExportPngAsync on the UI thread (which caused recursion),
            // marshal only the UI-bound implementation to the UI thread.
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await ExportPngOnUiThreadAsync(targetPath, options, progress, cancellationToken));
                return;
            }

            // Already on UI thread - run the UI-bound implementation directly.
            await ExportPngOnUiThreadAsync(targetPath, options, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            SimpleLogger.Log("PNG export cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"PNG export failed: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// UI-thread-bound portion of the PNG export. Separated to avoid recursive marshalling to the UI thread.
    /// This method assumes it is running on the UI thread.
    /// </summary>
    [SuppressMessage("Style", "IDE0047:Remove unnecessary parentheses", Justification = "Improves readability")]
    private async Task ExportPngOnUiThreadAsync(string targetPath, PngExportOptions options, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            // Prepare export options for JavaScript
            string backgroundColor = options.BackgroundColor ?? "transparent";

            string exportOptionsJson = JsonSerializer.Serialize(new
            {
                scale = options.ScaleFactor,
                dpi = options.Dpi,
                backgroundColor
            });

            SimpleLogger.Log($"Export options: {exportOptionsJson}");

            // Call the browser-based PNG export function
            string startExportScript = $"(async () => {{ return await globalThis.exportToPNG({exportOptionsJson}); }})();";

            // Start the export (this initiates async work in the browser)
            await _mermaidRenderer.ExecuteScriptAsync(startExportScript);

            // Use event/callback mechanism instead of polling for export status.
            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            _mermaidRenderer.RegisterExportProgressCallback(HandleExportProgressDelegate);

            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_exportToPngTimeout);

            try
            {
                // Wait for JS-side export to report completion or error (with timeout/cancellation)
                await tcs.Task.WaitAsync(linkedCts.Token);
            }
            finally
            {
                // Ensure callback is removed even on timeout/cancel
                try
                {
                    _mermaidRenderer.UnregisterExportProgressCallback(HandleExportProgressDelegate);
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError($"Failed to unregister export progress callback: {ex.Message}", ex);
                }
            }

            // Retrieve the PNG data
            const string getResultScript = "globalThis.__pngExportResult__ ? String(globalThis.__pngExportResult__) : ''";
            string? base64Data = await _mermaidRenderer.ExecuteScriptAsync(getResultScript);

            if (string.IsNullOrWhiteSpace(base64Data))
            {
                throw new InvalidOperationException("Failed to retrieve PNG data from export");
            }

            // Normalize possible JSON quoting and whitespace
            if (base64Data.StartsWith('\"') && base64Data.EndsWith('\"'))
            {
                base64Data = JsonSerializer.Deserialize<string>(base64Data) ?? base64Data;
            }

            // Decode and write the PNG to disk (handles pooled buffer + fallback)
            int bytesWritten = await DecodeAndWritePngAsync(base64Data, targetPath, linkedCts.Token)
                .ConfigureAwait(false);

            // Clean up JavaScript globals
            await _mermaidRenderer.ExecuteScriptAsync("globalThis.__pngExportResult__ = null; globalThis.__pngExportStatus__ = '';");

            // Report completion - marshal to UI thread
            ReportProgress(progress, new ExportProgress
            {
                Step = ExportStep.Complete,
                PercentComplete = 100,
                Message = "PNG export completed successfully!"
            });

            SimpleLogger.Log($"PNG exported successfully: {bytesWritten:N0} bytes");
            return;

            // Register a single delegate instance so unregister works reliably
            void HandleExportProgressDelegate(string statusJson) => HandleExportProgress(statusJson, tcs, progress);
        }
        catch (OperationCanceledException)
        {
            SimpleLogger.Log("PNG export cancelled or timed out");
            throw;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"PNG export failed: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the SVG content of the currently displayed diagram.
    /// </summary>
    /// <remarks>
    /// This method extracts the SVG from the live preview in the WebView. The method interacts with the
    /// WebView to execute JavaScript for retrieving the SVG content. It ensures that all WebView
    /// interactions occur on the UI thread.
    /// </remarks>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result is a string containing the SVG
    /// content, or <see langword="null"/> if the SVG content could not be retrieved.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the JavaScript execution fails.</exception>
    public async Task<string?> GetSvgContentAsync()
    {
        const string script = """
        (function() {
          const svgElement = document.querySelector('#output svg');
          if (!svgElement) return null;

          // Clone to avoid modifying the original
          const clone = svgElement.cloneNode(true);

          // Add XML declaration
          const serializer = new XMLSerializer();
          const svgString = serializer.serializeToString(clone);

          // Return complete SVG document as a primitive string (bridge-friendly)
          return '<?xml version="1.0" encoding="UTF-8"?>' + svgString;
        })();
        """;

        // Ensure WebView access happens on the UI thread
        string? result;
        if (Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread - execute directly
            result = await _mermaidRenderer.ExecuteScriptAsync(script);
        }
        else
        {
            // Not on UI thread - marshal to UI thread
            result = await Dispatcher.UIThread.InvokeAsync(async () =>
                await _mermaidRenderer.ExecuteScriptAsync(script));
        }

        // Remove any JSON escaping if present
        if (!string.IsNullOrWhiteSpace(result) && result.StartsWith('\"') && result.EndsWith('\"'))
        {
            result = JsonSerializer.Deserialize<string>(result) ?? result;
        }

        return result;
    }

    /// <summary>
    /// Handles the progress updates for an export operation by parsing the provided status JSON, reporting progress,
    /// and signaling task completion or failure.
    /// </summary>
    /// <remarks>This method parses the provided JSON to extract progress information, including the current
    /// step, percentage complete, and any associated message. It maps the step to an <see cref="ExportStep"/> value and
    /// reports progress using the <paramref name="progress"/> parameter if provided. If the step indicates completion,
    /// the task is marked as successfully completed. If the step indicates an error, the task is marked as failed with
    /// an appropriate exception.</remarks>
    /// <param name="statusJson">A JSON string representing the current status of the export operation. The JSON is expected to contain
    /// properties such as "step", "percent", and "message".</param>
    /// <param name="tcs">A <see cref="TaskCompletionSource{TResult}"/> used to signal the completion or failure of the export operation.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> instance used to report progress updates to the caller.</param>
    private static void HandleExportProgress(string statusJson, TaskCompletionSource<bool> tcs, IProgress<ExportProgress>? progress)
    {
        if (string.IsNullOrWhiteSpace(statusJson))
        {
            return;
        }

        try
        {
            using JsonDocument statusDoc = JsonDocument.Parse(statusJson);
            JsonElement root = statusDoc.RootElement;

            string step = root.TryGetProperty("step", out JsonElement stepEl) ? stepEl.GetString() ?? "unknown" : "unknown";
            int percent = root.TryGetProperty("percent", out JsonElement percentEl) ? percentEl.GetInt32() : 0;
            string message = root.TryGetProperty("message", out JsonElement msgEl) ? msgEl.GetString() ?? string.Empty : string.Empty;

            SimpleLogger.Log($"PNG export progress: {step} - {percent}% - {message}");

            ExportStep exportStep = step switch
            {
                "initializing" => ExportStep.Initializing,
                "rendering" => ExportStep.Rendering,
                "creating-canvas" => ExportStep.CreatingCanvas,
                "converting" or "drawing" => ExportStep.Rendering,
                "encoding" => ExportStep.Encoding,
                "complete" => ExportStep.Complete,
                "error" => ExportStep.Initializing, // error handled below
                _ => ExportStep.Rendering
            };

            ReportProgress(progress, new ExportProgress
            {
                Step = exportStep,
                PercentComplete = percent,
                Message = message
            });

            if (step == "complete")
            {
                SimpleLogger.Log("PNG export completed successfully");
                tcs.TrySetResult(true);
            }
            else if (step == "error")
            {
                tcs.TrySetException(new InvalidOperationException($"PNG export failed: {message}"));
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parse errors during callback
        }
    }

    /// <summary>
    /// Decodes a Base64-encoded PNG image and writes the decoded binary data to the specified file path.
    /// </summary>
    /// <remarks>This method ensures that the target directory exists before writing the file. It uses a
    /// streaming approach: the Base64 input is converted to UTF-8 bytes (whitespace removed) and then decoded
    /// in chunks using `Base64.DecodeFromUtf8` into a pooled output buffer which is written to disk. This
    /// avoids allocating a single large decoded array while still keeping memory usage bounded.</remarks>
    /// <param name="base64Data">The Base64-encoded string representing the PNG image data.
    /// The string may include whitespace or be JSON-encoded.</param>
    /// <param name="targetPath">The file path where the decoded PNG data will be written.
    /// If the directory does not exist, it will be created.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests during the
    /// asynchronous operation.</param>
    /// <returns>The number of bytes written to the file.</returns>
    [SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Improves readability")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static async Task<int> DecodeAndWritePngAsync(string base64Data, string targetPath, CancellationToken cancellationToken)
    {
        // Clean input (trim and unwrap possible JSON quoting)
        string base64Clean = base64Data.Trim();
        if (base64Clean.Length >= 2 && base64Clean[0] == '"' && base64Clean[^1] == '"')
        {
            base64Clean = JsonSerializer.Deserialize<string>(base64Clean) ?? base64Clean;
        }

        // Ensure directory exists before writing
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Construct a whitespace-free UTF8 byte buffer of the Base64 input using a rented buffer
        byte[]? inputBytes = null;
        byte[]? outputBuffer = null;
        try
        {
            int charCount = base64Clean.Length;
            inputBytes = ArrayPool<byte>.Shared.Rent(charCount);

            // Copy non-whitespace ASCII chars into the input buffer as single bytes
            int inputLen = 0;
            for (int i = 0; i < charCount; i++)
            {
                char c = base64Clean[i];
                if (!char.IsWhiteSpace(c))
                {
                    // Base64 should only contain ASCII characters. Validate to avoid silent truncation.
                    if (c > 127)
                    {
                        throw new InvalidOperationException("Non-ASCII character found in Base64 data");
                    }

                    inputBytes[inputLen++] = (byte)c;
                }
            }

            // Open file stream for async writing
            await using FileStream fs = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: ExportPngBufferSize,
                useAsync: true);

            // Estimate decoded length for sizing guidance (kept conservative)
            int estimated = CalculateDecodedLength(base64Clean);

            // Choose an initial output buffer size:
            // - If we have a positive estimate, clamp it between 32KB and ExportPngBufferSize.
            // - Otherwise start with ExportPngBufferSize.
            const int oneKB = 1_024;
            const int minOutBufferSize = 32 * oneKB;
            int outputBufferSize = estimated > 0
                ? Math.Clamp(estimated, minOutBufferSize, ExportPngBufferSize)
                : ExportPngBufferSize;

            outputBuffer = ArrayPool<byte>.Shared.Rent(outputBufferSize);

            // Cap growth to avoid infinite/unbounded growth
            const int oneMB = oneKB * oneKB;
            const int maxOutBufferSize = 16 * oneMB; // 16 MB

            int inputOffset = 0;
            int totalWritten = 0;

            while (inputOffset < inputLen)
            {
                ReadOnlySpan<byte> inputSpan = inputBytes.AsSpan(inputOffset, inputLen - inputOffset);
                Span<byte> outputSpan = outputBuffer.AsSpan(0, outputBuffer.Length);

                OperationStatus status = Base64.DecodeFromUtf8(inputSpan, outputSpan, out int consumed, out int written);

                if (written > 0)
                {
                    await fs.WriteAsync(new ReadOnlyMemory<byte>(outputBuffer, 0, written), cancellationToken)
                        .ConfigureAwait(false);
                    totalWritten += written;
                }

                inputOffset += consumed;

                if (status == OperationStatus.Done)
                {
                    break;
                }

                if (status == OperationStatus.InvalidData)
                {
                    throw new InvalidOperationException("Invalid Base64 data during PNG decode");
                }

                if (status == OperationStatus.DestinationTooSmall)
                {
                    // If the decoder consumed some input, loop will continue; otherwise we must grow the output buffer.
                    if (consumed == 0)
                    {
                        // Grow the buffer, but cap growth to a reasonable maximum to avoid OOM / infinite growth.
                        int newSize = Math.Min(outputBuffer.Length * 2, maxOutBufferSize);

                        if (newSize <= outputBuffer.Length)
                        {
                            // Already at or above max allowed size - fail with clear message
                            throw new InvalidOperationException("Decoded data requires an output buffer larger than the allowed maximum size");
                        }

                        byte[] newBuf = ArrayPool<byte>.Shared.Rent(newSize);

                        // No need to copy existing data since we write immediately after decoding; just replace buffer used for future writes.
                        ArrayPool<byte>.Shared.Return(outputBuffer);
                        outputBuffer = newBuf;

                        // Continue attempting to decode with the larger buffer
                    }

                    // If consumed > 0 we made progress; continue the loop to decode remaining input.
                    continue;
                }

                if (status == OperationStatus.NeedMoreData)
                {
                    // Decoder needs more bytes; if we've consumed some, loop will continue with remaining bytes.
                    // If consumed == 0 and no more input is available, this is unexpected for well-formed Base64.
                    if (consumed == 0 && inputOffset >= inputLen)
                    {
                        // Nothing more to provide - break to avoid infinite loop
                        break;
                    }

                    continue;
                }
            }

            return totalWritten;

            // Local helper reused from previous implementation - estimates decoded length ignoring whitespace
            static int CalculateDecodedLength(string base64)
            {
                int contentLen = 0;
                foreach (char c in base64)
                {
                    if (!char.IsWhiteSpace(c))
                    {
                        contentLen++;
                    }
                }

                if (contentLen == 0)
                {
                    return 0;
                }

                int padding = 0;
                if (base64.EndsWith("=="))
                {
                    padding = 2;
                }
                else if (base64.EndsWith('='))
                {
                    padding = 1;
                }

                int groups = (contentLen + 3) / 4;
                return (groups * 3) - padding;
            }
        }
        finally
        {
            if (inputBytes is not null)
            {
                ArrayPool<byte>.Shared.Return(inputBytes);
            }

            if (outputBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(outputBuffer);
            }
        }
    }

    /// <summary>
    /// Reports the current export progress to the specified progress handler, ensuring updates are marshaled to the UI
    /// thread if necessary.
    /// </summary>
    /// <remarks>If called from a non-UI thread, the progress update is posted to the UI thread to ensure
    /// thread safety for UI-bound progress handlers. Exceptions thrown during progress reporting are logged but not
    /// propagated.</remarks>
    /// <param name="progress">An optional progress handler that receives export progress updates. If null, no progress is reported.</param>
    /// <param name="exportProgress">The current state of the export operation to report to the progress handler.</param>
    private static void ReportProgress(IProgress<ExportProgress>? progress, ExportProgress exportProgress)
    {
        if (progress is null)
        {
            return;
        }

        // Marshal to UI thread for ViewModel property updates
        if (Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread
            progress.Report(exportProgress);
        }
        else
        {
            // Marshal to UI thread - fire and forget since progress updates don't need to be awaited
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    progress.Report(exportProgress);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    SimpleLogger.LogError($"Progress report failed: {ex.Message}", ex);
                }
            });
        }
    }
}
