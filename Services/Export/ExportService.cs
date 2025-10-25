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
public sealed class ExportService
{
    private static readonly XNamespace _svgNamespace = "http://www.w3.org/2000/svg";
    private static readonly TimeSpan _defaultExportToPngTimeout = TimeSpan.FromSeconds(60);
    private static readonly SearchValues<char> _whitespaceSearchValues = SearchValues.Create(GetAllWhiteSpaceChars());

    private readonly MermaidRenderer _mermaidRenderer;

    public ExportService(MermaidRenderer mermaidRenderer)
    {
        _mermaidRenderer = mermaidRenderer;
    }

    #region SVG Export

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
            // Get SVG content from WebView (must stay on UI thread)
            string? svgContent = await GetSvgContentAsync();

            if (string.IsNullOrWhiteSpace(svgContent))
            {
                throw new InvalidOperationException("Failed to extract SVG content from diagram");
            }

            // Process SVG content
            svgContent = ProcessSvgContent(svgContent, options);

            // Write to file (can run on background thread)
            await WriteSvgToFileAsync(targetPath, svgContent, cancellationToken)
                .ConfigureAwait(false);

            SimpleLogger.Log($"SVG exported successfully: {new FileInfo(targetPath).Length:N0} bytes");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"SVG export failed: {ex.Message}", ex);
            throw;
        }
    }

    private static string ProcessSvgContent(string svgContent, SvgExportOptions options)
    {
        // Apply optimization if requested
        if (options.Optimize)
        {
            SimpleLogger.Log("Optimizing SVG content...");
            svgContent = OptimizeSvg(svgContent, options);
        }

        // Handle XML declaration
        ReadOnlySpan<char> svgSpan = svgContent.AsSpan().TrimStart();
        bool hasXmlDeclaration = svgSpan.StartsWith("<?xml");

        if (!options.IncludeXmlDeclaration && hasXmlDeclaration)
        {
            // Remove XML declaration
            int endOfDeclaration = svgSpan.IndexOf("?>");
            if (endOfDeclaration >= 0)
            {
                svgContent = svgSpan[(endOfDeclaration + 2)..].ToString().TrimStart();
            }
        }
        else if (options.IncludeXmlDeclaration && !hasXmlDeclaration)
        {
            // Add XML declaration
            svgContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + svgContent;
        }

        return svgContent;
    }

    /// <summary>
    /// Writes SVG content to file.
    /// </summary>
    private static async Task WriteSvgToFileAsync(string targetPath, string svgContent, CancellationToken cancellationToken)
    {
        // Ensure directory exists
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(targetPath, svgContent, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
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
            // Serialize with appropriate formatting
            string result = options.MinifySvg
                ? doc.ToString(SaveOptions.DisableFormatting)
                : doc.ToString(SaveOptions.None);

            // Remove whitespace between tags efficiently for minification
            if (options.MinifySvg)
            {
                result = RemoveWhitespaceBetweenTags(result);
            }

            return result;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("SVG optimization failed, returning original content", ex);
            return svgContent;
        }
    }

    /// <summary>
    /// Removes whitespace between XML tags using zero-allocation algorithm.
    /// Uses ArrayPool for memory reuse - only allocates the final string.
    /// Pattern: ">...whitespace...&lt;" becomes <![CDATA["><"]]>
    /// </summary>
    /// <remarks>
    /// For a 10 MB SVG, this approach uses ~10 MB peak memory vs ~30 MB with regex.
    /// Approximately 2-3x faster than regex for large SVGs.
    /// </remarks>
    private static string RemoveWhitespaceBetweenTags(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        ReadOnlySpan<char> source = input.AsSpan();
        char[]? buffer = null;

        try
        {
            // Rent from pool - reused memory, zero allocation
            buffer = ArrayPool<char>.Shared.Rent(input.Length);
            Span<char> output = buffer.AsSpan();

            int writePos = 0;
            int readPos = 0;

            while (readPos < source.Length)
            {
                char current = source[readPos];

                if (current == '>')
                {
                    output[writePos++] = current;
                    readPos++;

                    // Scan ahead for whitespace
                    int whitespaceStart = readPos;
                    while (readPos < source.Length && char.IsWhiteSpace(source[readPos]))
                    {
                        readPos++;
                    }

                    // Check if whitespace is followed by opening tag
                    if (readPos < source.Length && source[readPos] == '<')
                    {
                        // Pattern found: ">whitespace<" - skip whitespace entirely
                        continue;
                    }

                    // Whitespace not between tags - preserve it
                    if (readPos > whitespaceStart)
                    {
                        int whitespaceLength = readPos - whitespaceStart;
                        source.Slice(whitespaceStart, whitespaceLength).CopyTo(output[writePos..]);
                        writePos += whitespaceLength;
                    }
                }
                else
                {
                    output[writePos++] = current;
                    readPos++;
                }
            }

            // Only allocation: the final string
            return new string(buffer, 0, writePos);
        }
        finally
        {
            if (buffer is not null)
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
    }

    #endregion SVG Export

    #region PNG Export

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
    public async Task ExportPngAsync(string targetPath, PngExportOptions? options = null,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
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
            // Step 1: Start browser export
            await StartBrowserExportAsync(options);

            // Step 2: Wait for completion with progress tracking
            await WaitForExportCompletionAsync(progress, _defaultExportToPngTimeout, cancellationToken);

            // Step 3: Retrieve and decode PNG data
            string base64Data = await RetrievePngDataAsync();
            int bytesWritten = await DecodeAndWritePngAsync(base64Data, targetPath, cancellationToken)
                .ConfigureAwait(false);

            // Step 4: Cleanup and report success
            await CleanupExportGlobalVariablesAsync();
            ReportProgress(progress, new ExportProgress
            {
                Step = ExportStep.Complete,
                PercentComplete = 100,
                Message = "PNG export completed successfully!"
            });

            SimpleLogger.Log($"PNG exported successfully: {bytesWritten:N0} bytes");
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

    private async Task StartBrowserExportAsync(PngExportOptions options)
    {
        string exportOptionsJson = JsonSerializer.Serialize(new
        {
            scale = options.ScaleFactor,
            dpi = options.Dpi,
            backgroundColor = options.BackgroundColor ?? "transparent"
        });

        SimpleLogger.Log($"Export options: {exportOptionsJson}");

        string script = $"(async () => {{ return await globalThis.exportToPNG({exportOptionsJson}); }})();";
        await _mermaidRenderer.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// Waits for export completion using callback mechanism with timeout.
    /// </summary>
    private async Task WaitForExportCompletionAsync(IProgress<ExportProgress>? progress, TimeSpan timeout, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _mermaidRenderer.RegisterExportProgressCallback(ProgressCallback);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        try
        {
            await completionSource.Task.WaitAsync(linkedCts.Token);
        }
        finally
        {
            _mermaidRenderer.UnregisterExportProgressCallback(ProgressCallback);
        }

        void ProgressCallback(string statusJson) => HandleExportProgress(statusJson, completionSource, progress);
    }

    /// <summary>
    /// Retrieves PNG data from JavaScript global variable.
    /// </summary>
    private async Task<string> RetrievePngDataAsync()
    {
        const string script = "globalThis.__pngExportResult__ ? String(globalThis.__pngExportResult__) : ''";
        string? base64Data = await _mermaidRenderer.ExecuteScriptAsync(script);

        if (string.IsNullOrWhiteSpace(base64Data))
        {
            throw new InvalidOperationException("PNG data from browser export is empty or invalid");
        }

        return UnwrapJsonString(base64Data);
    }

    private async Task CleanupExportGlobalVariablesAsync()
    {
        const string script = "globalThis.__pngExportResult__ = null; globalThis.__pngExportStatus__ = '';";
        await _mermaidRenderer.ExecuteScriptAsync(script);
    }

    #endregion PNG Export

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
        return UnwrapJsonString(result);
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
    /// <param name="completionSource">A <see cref="TaskCompletionSource{TResult}"/> used to signal the completion or failure of the export operation.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> instance used to report progress updates to the caller.</param>
    private static void HandleExportProgress(string statusJson, TaskCompletionSource<bool> completionSource, IProgress<ExportProgress>? progress)
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
                _ => ExportStep.Rendering
            };

            // Report progress
            if (progress is not null)
            {
                ReportProgress(progress, new ExportProgress
                {
                    Step = exportStep,
                    PercentComplete = percent,
                    Message = message
                });
            }

            // Handle completion or error
            if (step == "complete")
            {
                SimpleLogger.Log("PNG export completed successfully");
                completionSource.TrySetResult(true);
            }
            else if (step == "error")
            {
                string errorMsg = string.IsNullOrWhiteSpace(message)
                    ? "PNG export failed with unknown error"
                    : $"PNG export failed: {message}";

                completionSource.TrySetException(new InvalidOperationException(errorMsg));
            }
        }
        catch (JsonException ex)
        {
            SimpleLogger.LogError("Failed to parse export status JSON", ex);
        }
    }

    #region Base64 Decoding

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
        // Step 1: Validate and prepare input
        string base64Clean = UnwrapJsonString(base64Data);
        ValidateBase64Size(base64Clean);

        // Step 2: Ensure directory exists
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Step 3: Process in chunks to minimize memory allocation
        return await DecodeBase64ToFileInChunksAsync(base64Clean, targetPath, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async Task<int> DecodeBase64ToFileInChunksAsync(string base64String, string targetPath, CancellationToken cancellationToken)
    {
        // Use small buffers to stay well below LOH threshold (85 KB)
        const int utf8ChunkSize = 16 * 1_024;      // 16 KB for UTF-8 conversion
        const int decodeBufferSize = 16 * 1_024;   // 16 KB for decoded output

        byte[]? utf8Buffer = null;
        byte[]? decodeBuffer = null;

        try
        {
            // Rent small buffers from ArrayPool (reused across exports, no LOH allocation)
            utf8Buffer = ArrayPool<byte>.Shared.Rent(utf8ChunkSize);
            decodeBuffer = ArrayPool<byte>.Shared.Rent(decodeBufferSize);

            // Open file stream with optimal buffer size
            int estimatedSize = EstimateDecodedLength(base64String);
            int fileBufferSize = CalculateOptimalFileBufferSize(estimatedSize);

            await using FileStream fileStream = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                fileBufferSize,
                useAsync: true);

            int totalWritten = 0;
            int position = 0;
            int base64Length = base64String.Length;

            // Track unconsumed UTF-8 bytes from previous chunk (for Base64 group boundaries)
            int pendingUtf8Bytes = 0;

            while (position < base64Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Determine chunk size - calculate without creating spans
                int remainingChars = base64Length - position;
                int chunkSize = Math.Min(utf8ChunkSize - pendingUtf8Bytes, remainingChars);

                // Convert chunk to UTF-8 bytes (filtering whitespace, validating characters)
                // Create span ONLY within this synchronous section, use immediately
                ReadOnlySpan<char> base64Span = base64String.AsSpan(position, chunkSize);
                int utf8Length = ConvertBase64ChunkToUtf8Bytes(base64Span, utf8Buffer, pendingUtf8Bytes);

                // Decode UTF-8 bytes to binary - again, create span locally, use immediately
                ReadOnlySpan<byte> utf8Span = utf8Buffer.AsSpan(0, utf8Length);
                OperationStatus status = Base64.DecodeFromUtf8(utf8Span, decodeBuffer, out int bytesConsumed, out int bytesWritten);

                // Write decoded data to file
                if (bytesWritten > 0)
                {
                    // Create ReadOnlyMemory from array - this is safe across await
                    ReadOnlyMemory<byte> fileWriteMemory = new ReadOnlyMemory<byte>(decodeBuffer, 0, bytesWritten);
                    await fileStream.WriteAsync(fileWriteMemory, cancellationToken)
                        .ConfigureAwait(false);

                    totalWritten += bytesWritten;
                }

                // Handle Base64 decoder status
                switch (status)
                {
                    case OperationStatus.NeedMoreData when position + chunkSize < base64Length:
                        // Incomplete Base64 group at chunk boundary - carry forward unconsumed bytes
                        pendingUtf8Bytes = utf8Length - bytesConsumed;
                        if (pendingUtf8Bytes > 0)
                        {
                            // Copy unconsumed bytes to start of buffer for next iteration
                            // Create span locally, use immediately, no persistence across await
                            utf8Buffer.AsSpan(bytesConsumed, pendingUtf8Bytes).CopyTo(utf8Buffer.AsSpan(0, pendingUtf8Bytes));
                        }
                        break;

                    case OperationStatus.NeedMoreData when position + chunkSize >= base64Length:
                        // End of input with incomplete group - this is an error
                        throw new InvalidOperationException($"Incomplete Base64 data: the input ended with an incomplete 4-character group. Position: {position}." +
                            $"Remaining chars: {remainingChars}. Pending UTF-8 bytes: {pendingUtf8Bytes}.");

                    case OperationStatus.InvalidData:
                        throw new InvalidOperationException("Invalid Base64 data encountered during PNG decode");

                    case OperationStatus.DestinationTooSmall:
                        // This shouldn't happen with our buffer sizes, but handle it gracefully
                        throw new InvalidOperationException(
                            $"Decode buffer too small. This indicates a PNG larger than expected. Buffer size: {decodeBufferSize:N0} bytes, consumed: {bytesConsumed}, written: {bytesWritten}");

                    case OperationStatus.Done:
                        // Chunk decoded successfully - continue to next chunk if more data exists
                        break;

                    default:
                        // Unknown status
                        throw new InvalidOperationException($"Unexpected Base64 decode status: {status}");
                }

                position += chunkSize;
            }

            return totalWritten;
        }
        finally
        {
            // Always return buffers to pool for reuse
            if (utf8Buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(utf8Buffer);
            }

            if (decodeBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(decodeBuffer);
            }
        }
    }

    /// <summary>
    /// Converts a Base64 character chunk to UTF-8 bytes, filtering whitespace and validating characters.
    /// Appends to existing buffer starting at offset (for handling chunk boundaries).
    /// </summary>
    /// <param name="chunk">The Base64 character chunk to convert</param>
    /// <param name="outputBuffer">The output buffer to write UTF-8 bytes to</param>
    /// <param name="startOffset">The offset in the output buffer to start writing at (for pending bytes from previous chunk)</param>
    /// <returns>The total number of UTF-8 bytes in the buffer (including any pending bytes from previous chunk)</returns>
    private static int ConvertBase64ChunkToUtf8Bytes(ReadOnlySpan<char> chunk, byte[] outputBuffer, int startOffset)
    {
        int outputIndex = startOffset;

        // Check for whitespace in chunk (most chunks won't have any)
        bool hasWhitespace = chunk.ContainsAny(_whitespaceSearchValues);

        if (!hasWhitespace)
        {
            // Fast path: No whitespace, validate and bulk copy
            foreach (char c in chunk)
            {
                if (!IsValidBase64Character(c))
                {
                    throw new InvalidOperationException($"Invalid Base64 character: '{c}' (ASCII {(int)c})");
                }

                outputBuffer[outputIndex++] = (byte)c;
            }
        }
        else
        {
            // Slow path: Filter whitespace and validate
            foreach (char c in chunk)
            {
                // Skip whitespace
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                // Validate and convert
                if (!IsValidBase64Character(c))
                {
                    throw new InvalidOperationException($"Invalid Base64 character: '{c}' (ASCII {(int)c})");
                }

                outputBuffer[outputIndex++] = (byte)c;
            }
        }

        return outputIndex;
    }

    #endregion Base64 Decoding

    #region Base64 Preparation and Validation

    /// <summary>
    /// Validates that a character is valid Base64: A-Z, a-z, 0-9, +, /, =
    /// </summary>
    private static bool IsValidBase64Character(char c)
    {
        return c is (>= 'A' and <= 'Z') or
                    (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or
                    '+' or '/' or '=';
    }

    /// <summary>
    /// Validates that Base64 string is not unreasonably large.
    /// </summary>
    private static void ValidateBase64Size(string base64Clean)
    {
        const int maxBase64Length = 150 * 1_024 * 1_024; // 150 MB Base64 ~ 112 MB decoded (allows ~100 MB PNG)

        if (base64Clean.Length > maxBase64Length)
        {
            throw new InvalidOperationException(
                $"Base64 data exceeds maximum allowed size. Length: {base64Clean.Length:N0} chars, Maximum: {maxBase64Length:N0} chars");
        }
    }

    /// <summary>
    /// Estimates decoded length of Base64 data, accounting for padding.
    /// </summary>
    private static int EstimateDecodedLength(string base64)
    {
        // Count non-whitespace characters
        int contentLength = 0;
        foreach (char c in base64)
        {
            if (!char.IsWhiteSpace(c))
            {
                contentLength++;
            }
        }

        if (contentLength == 0)
        {
            return 0;
        }

        // Calculate padding: Base64 padding is determined by the number of '=' characters at the end
        int padding = base64.EndsWith("==") ? 2 : (base64.EndsWith('=') ? 1 : 0);

        // Each 4 Base64 chars = 3 bytes, minus padding
        int groups = (contentLength + 3) / 4;
        return (groups * 3) - padding;
    }

    /// <summary>
    /// Calculates optimal FileStream buffer size based on estimated output size.
    /// </summary>
    private static int CalculateOptimalFileBufferSize(int estimatedSize)
    {
        return estimatedSize switch
        {
            < 100_000 => 16 * 1_024,        // 16 KB for small files
            < 1_000_000 => 80 * 1_024,      // 80 KB for medium files
            < 10_000_000 => 256 * 1_024,    // 256 KB for large files
            _ => 512 * 1_024                // 512 KB for very large files
        };
    }

    #endregion

    #region Progress Reporting

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

    #endregion Progress Reporting

    #region Utility Methods

    /// <summary>
    /// Unwraps JSON string encoding if present, with fast path optimization.
    /// </summary>
    private static string UnwrapJsonString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = input.AsSpan().Trim();
        if (span.Length >= 2 && span[0] == '"' && span[^1] == '"')
        {
            // Fast path: No escapes, just remove quotes
            if (span.IndexOf('\\') < 0)
            {
                return span[1..^1].ToString();
            }

            // Slow path: Proper JSON deserialization for escaped strings
            return JsonSerializer.Deserialize<string>(input) ?? input;
        }

        return input;
    }

    /// <summary>
    /// Retrieves an array of all Unicode characters classified as white space.
    /// </summary>
    /// <remarks>The method iterates through all Unicode characters and identifies those that are categorized
    /// as white space using the <see cref="char.IsWhiteSpace(char)"/> method. The resulting array includes all such
    /// characters defined in the Unicode standard.</remarks>
    /// <returns>An array of <see cref="char"/> containing all Unicode white space characters. The array will be empty if no
    /// white space characters are found.</returns>
    private static char[] GetAllWhiteSpaceChars()
    {
        List<char> list = new List<char>();
        for (int i = char.MinValue; i <= char.MaxValue; i++)
        {
            char c = (char)i;
            if (char.IsWhiteSpace(c))
            {
                list.Add(c);
            }
        }

        return list.ToArray();
    }

    #endregion Utility Methods
}
