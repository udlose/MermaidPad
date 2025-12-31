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
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Xml;
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
internal sealed class ExportService
{
    private static readonly TimeSpan _defaultExportToPngTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Represents a collection of Unicode characters that are considered whitespace for text processing operations.
    /// </summary>
    /// <remarks>This array includes common whitespace characters such as space, tab, and line feed, as well
    /// as less frequently used Unicode whitespace characters. It can be used to identify or filter out whitespace in
    /// strings according to Unicode standards.</remarks>
    private static readonly char[] _whiteSpaceChars =
    [
        '\u0009', // CHARACTER TABULATION
        '\u000A', // LINE FEED
        '\u000B', // LINE TABULATION
        '\u000C', // FORM FEED
        '\u000D', // CARRIAGE RETURN
        '\u0020', // SPACE
        '\u0085', // NEXT LINE
        '\u00A0', // NO-BREAK SPACE
        '\u1680', // OGHAM SPACE MARK
        '\u180E', // MONGOLIAN VOWEL SEPARATOR
        '\u2000', // EN QUAD
        '\u2001', // EM QUAD
        '\u2002', // EN SPACE
        '\u2003', // EM SPACE
        '\u2004', // THREE-PER-EM SPACE
        '\u2005', // FOUR-PER-EM SPACE
        '\u2006', // SIX-PER-EM SPACE
        '\u2007', // FIGURE SPACE
        '\u2008', // PUNCTUATION SPACE
        '\u2009', // THIN SPACE
        '\u200A', // HAIR SPACE
        '\u2028', // LINE SEPARATOR
        '\u2029', // PARAGRAPH SEPARATOR
        '\u202F', // NARROW NO-BREAK SPACE
        '\u205F', // MEDIUM MATHEMATICAL SPACE
        '\u3000' // IDEOGRAPHIC SPACE
    ];
    private static readonly SearchValues<char> _whitespaceSearchValues = SearchValues.Create(_whiteSpaceChars);
    private readonly MermaidRenderer _mermaidRenderer;
    private readonly ILogger<ExportService> _logger;

    public ExportService(MermaidRenderer mermaidRenderer, ILogger<ExportService> logger)
    {
        _mermaidRenderer = mermaidRenderer;
        _logger = logger;
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
    /// <param name="options">The options to use for SVG export, such as optimization settings. If null, default options
    /// are used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the export operation.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the SVG content cannot be extracted from the diagram.</exception>
    internal Task ExportSvgAsync(string targetPath, SvgExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        options ??= new SvgExportOptions();
        _logger.LogInformation("Starting SVG export to: {TargetPath}", targetPath);

        return ExportSvgCoreAsync(targetPath, options, cancellationToken);
    }

    /// <summary>
    /// Exports the SVG content to the specified file path, applying optional processing options.
    /// </summary>
    /// <remarks>This method retrieves the SVG content, processes it based on the provided options, and writes
    /// it to the specified file path. If the SVG content cannot be extracted, an <see
    /// cref="InvalidOperationException"/> is thrown.</remarks>
    /// <param name="targetPath">The full file path where the SVG content will be saved. This cannot be null or empty.</param>
    /// <param name="options">Optional parameters for customizing the SVG export process. If null, default options are used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will terminate early if the token is canceled.</param>
    /// <returns>A task representing the asynchronous export operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the SVG content cannot be extracted from the diagram.</exception>
    private async Task ExportSvgCoreAsync(string targetPath, SvgExportOptions options, CancellationToken cancellationToken)
    {
        try
        {
            // Get SVG content from WebView (must stay on UI thread)
            ReadOnlyMemory<char> svgContent = await GetSvgContentAsync();

            if (svgContent.IsEmpty)
            {
                throw new InvalidOperationException("Failed to extract SVG content from diagram");
            }

            // Process SVG content (async now due to optimization)
            svgContent = await ProcessSvgContentAsync(svgContent, options, cancellationToken)
                .ConfigureAwait(false);

            // Write to file (can run on background thread)
            await WriteSvgToFileAsync(targetPath, svgContent, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("SVG exported successfully: {ByteCount:N0} bytes", new FileInfo(targetPath).Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SVG export failed: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Processes the provided SVG content based on the specified export options.
    /// </summary>
    /// <remarks>If the <paramref name="options"/> specify optimization, the method applies the optimization
    /// process to the SVG content.</remarks>
    /// <param name="svgContent">The SVG content to process, represented as a read-only memory block of characters.</param>
    /// <param name="options">The options that determine how the SVG content should be processed, such as whether to optimize it.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> containing the processed SVG content.</returns>
    private async Task<ReadOnlyMemory<char>> ProcessSvgContentAsync(
        ReadOnlyMemory<char> svgContent,
        SvgExportOptions options,
        CancellationToken cancellationToken = default)
    {
        // Apply optimization if requested
        if (options.Optimize)
        {
            _logger.LogInformation("Optimizing SVG content...");
            svgContent = await OptimizeSvgAsync(svgContent, options, cancellationToken)
                .ConfigureAwait(false);
        }

        return svgContent;
    }

    /// <summary>
    /// Writes the specified SVG content to a file at the given path asynchronously.
    /// </summary>
    /// <remarks>This method ensures that the target directory exists before writing the file. The content is
    /// written using UTF-8 encoding.</remarks>
    /// <param name="targetPath">The full path of the file where the SVG content will be written. If the directory does not exist, it will be
    /// created.</param>
    /// <param name="svgContent">The SVG content to write, provided as a <see cref="ReadOnlyMemory{Char}"/> for efficient, zero-copy operations.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will be canceled if the token is triggered.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    private static async Task WriteSvgToFileAsync(string targetPath, ReadOnlyMemory<char> svgContent, CancellationToken cancellationToken)
    {
        // Ensure directory exists
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use ReadOnlyMemory<char> overload for zero-copy write
        await File.WriteAllTextAsync(targetPath, svgContent, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Optimizes the provided SVG content by removing unnecessary elements, attributes, and whitespace based on the
    /// specified options.
    /// </summary>
    /// <remarks>This method processes the SVG content asynchronously, applying optimizations such as removing
    /// comments, skipping metadata elements, and optionally minifying the content. It ensures that the resulting SVG
    /// remains valid.  The method is designed to handle large SVG files efficiently, but it may log a warning if the
    /// input size exceeds 5 MB.  If the input content is invalid XML, the method logs an error and returns the original
    /// content.</remarks>
    /// <param name="svgContent">The SVG content to optimize, represented as a read-only memory of characters.</param>
    /// <param name="options">The options that control the optimization process, such as whether to remove comments or minify the SVG.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will terminate early if cancellation is requested.</param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> containing the optimized SVG content. If the optimization fails, the original
    /// content is returned.</returns>
    private async Task<ReadOnlyMemory<char>> OptimizeSvgAsync(ReadOnlyMemory<char> svgContent,
        SvgExportOptions options, CancellationToken cancellationToken = default)
    {
        if (svgContent.Length > 5_000_000) // 5 MB
        {
            _logger.LogInformation("Optimizing large SVG ({CharacterCount:N0} characters). This may take a few seconds...", svgContent.Length);
        }

        try
        {
            // Configure reader for streaming
            XmlReaderSettings readerSettings = new XmlReaderSettings
            {
                Async = true,
                IgnoreComments = options.RemoveComments,    // Let reader skip comments for us
                IgnoreWhitespace = options.MinifySvg,       // Let reader skip whitespace if minifying
                DtdProcessing = DtdProcessing.Prohibit,     // Security: prevent XXE attacks
                XmlResolver = null                          // Security: no external entity resolution
            };

            // Configure writer for output
            XmlWriterSettings writerSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = !options.MinifySvg,                // Indent unless minifying
                IndentChars = "  ",                         // 2 spaces
                OmitXmlDeclaration = true,                  // SVG doesn't need XML declaration
                NamespaceHandling = NamespaceHandling.OmitDuplicates
            };

            StringBuilder output = new StringBuilder(svgContent.Length);

            // Use custom MemoryTextReader for zero-copy input reading
            using MemoryTextReader memoryReader = new MemoryTextReader(svgContent);
            using XmlReader xmlReader = XmlReader.Create(memoryReader, readerSettings);
            await using StringWriter stringWriter = new StringWriter(output);
            await using XmlWriter xmlWriter = XmlWriter.Create(stringWriter, writerSettings);
            int depth = 0;
            bool skipElement = false;
            int skipDepth = 0;
            const string svgXmlNamespace = "http://www.w3.org/2000/svg";// DevSkim: ignore DS137138
            while (await xmlReader.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Track depth consistently for ALL elements (before any processing logic)
                // This ensures depth tracking is consistent regardless of skip state
                if (xmlReader is { NodeType: XmlNodeType.Element, IsEmptyElement: false })
                {
                    depth++;
                }
                else if (xmlReader.NodeType == XmlNodeType.EndElement)
                {
                    depth--;

                    // Check if we're exiting a skipped element
                    if (skipElement && depth < skipDepth)
                    {
                        skipElement = false;
                    }
                }

                // If we're inside a skipped element, skip all content
                if (skipElement)
                {
                    continue;
                }

                switch (xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        // Check if this is an element we want to skip
                        if (xmlReader is { LocalName: "metadata", NamespaceURI: svgXmlNamespace })
                        {
                            skipElement = true;
                            skipDepth = depth;
                            continue;
                        }

                        // Write element start
                        await xmlWriter.WriteStartElementAsync(xmlReader.Prefix,
                            xmlReader.LocalName, xmlReader.NamespaceURI)
                            .ConfigureAwait(false);

                        // Copy attributes with filtering
                        if (xmlReader.HasAttributes)
                        {
                            while (xmlReader.MoveToNextAttribute())
                            {
                                // Skip xml:space attribute
                                if (xmlReader.LocalName == "space" && xmlReader.NamespaceURI == XNamespace.Xml.NamespaceName)
                                {
                                    continue;
                                }

                                // Skip empty attributes
                                if (string.IsNullOrWhiteSpace(xmlReader.Value))
                                {
                                    continue;
                                }

                                await xmlWriter.WriteAttributeStringAsync(xmlReader.Prefix,
                                    xmlReader.LocalName, xmlReader.NamespaceURI, xmlReader.Value)
                                    .ConfigureAwait(false);
                            }
                            xmlReader.MoveToElement();
                        }

                        // Handle self-closing elements
                        if (xmlReader.IsEmptyElement)
                        {
                            await xmlWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }
                        break;

                    case XmlNodeType.EndElement:
                        await xmlWriter.WriteEndElementAsync().ConfigureAwait(false);
                        break;

                    case XmlNodeType.Text:
                        await xmlWriter.WriteStringAsync(xmlReader.Value).ConfigureAwait(false);
                        break;

                    case XmlNodeType.CDATA:
                        await xmlWriter.WriteCDataAsync(xmlReader.Value).ConfigureAwait(false);
                        break;

                    case XmlNodeType.Comment:
                        // Only reached if IgnoreComments = false
                        if (!options.RemoveComments)
                        {
                            await xmlWriter.WriteCommentAsync(xmlReader.Value).ConfigureAwait(false);
                        }
                        break;

                    case XmlNodeType.ProcessingInstruction:
                        await xmlWriter.WriteProcessingInstructionAsync(xmlReader.Name, xmlReader.Value)
                            .ConfigureAwait(false);
                        break;

                        // Whitespace, SignificantWhitespace handled by reader settings
                }
            }

            await xmlWriter.FlushAsync().ConfigureAwait(false);
            string result = output.ToString();

            _logger.LogInformation("SVG optimization complete. Original: {OriginalLength:N0} characters, Optimized: {OptimizedLength:N0} characters ({Reduction:F1}% reduction)",
                svgContent.Length, result.Length, (1.0 - ((double)result.Length / svgContent.Length)) * 100);

            return result.AsMemory();
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "SVG optimization failed due to invalid XML, returning original content");
            return svgContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SVG optimization failed, returning original content");
            return svgContent;
        }
    }

    #endregion SVG Export

    #region PNG Export

    /// <summary>
    /// Exports the current content to a PNG file at the specified path.
    /// </summary>
    /// <param name="targetPath">The file path where the PNG will be saved. Cannot be null or empty.</param>
    /// <param name="options">Optional settings for the PNG export, such as DPI and scale factor.
    /// If not provided, default options are used.</param>
    /// <param name="progress">An optional progress reporter to track the export operation's progress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.
    /// The operation will terminate early if cancellation is requested.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    internal Task ExportPngAsync(string targetPath, PngExportOptions? options = null,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        options ??= new PngExportOptions();
        _logger.LogInformation("Starting browser-based PNG export to: {TargetPath} (DPI: {Dpi}, Scale: {ScaleFactor}x)", targetPath, options.Dpi, options.ScaleFactor);

        return ExportPngCoreAsync(targetPath, options, progress, cancellationToken);
    }

    /// <summary>
    /// Exports content to a PNG file asynchronously, with support for progress reporting and cancellation.
    /// </summary>
    /// <remarks>This method ensures that any UI-bound operations required for the export are executed on the
    /// UI thread. If the operation is canceled, an <see cref="OperationCanceledException"/> is thrown. If an error
    /// occurs during the export, the exception is logged and rethrown.</remarks>
    /// <param name="targetPath">The file path where the PNG will be saved. This cannot be null or empty.</param>
    /// <param name="options">The options specifying the configuration for the PNG export, such as resolution and quality.</param>
    /// <param name="progress">An optional progress reporter that receives updates about the export
    /// process, including the current step and percentage completed.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the export operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous export operation. The task completes when the PNG export is finished,
    /// or throws an exception if the operation is cancelled or an error occurs.
    /// </returns>

    private async Task ExportPngCoreAsync(string targetPath, PngExportOptions options,
        IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
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
                await Dispatcher.UIThread.InvokeAsync(() => ExportPngOnUiThreadAsync(targetPath, options, progress, cancellationToken));
                return;
            }

            // Already on UI thread - run the UI-bound implementation directly.
            await ExportPngOnUiThreadAsync(targetPath, options, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PNG export cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PNG export failed: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Exports a PNG image asynchronously on the UI thread using the specified options and writes it to the target
    /// path.
    /// </summary>
    /// <remarks>This method performs the export operation in multiple steps, including initiating the export,
    /// tracking progress, retrieving and decoding the PNG data, and writing it to the specified file path. If the
    /// operation is canceled or an error occurs, the method ensures proper cleanup and logs the failure.</remarks>
    /// <param name="targetPath">The file path where the exported PNG image will be saved. This path must be writable.</param>
    /// <param name="options">The <see cref="PngExportOptions"/> that specify the configuration for the PNG export process.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> instance to report the progress of the export operation.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the operation to complete. The operation can be
    /// canceled by the caller.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the PNG export process finishes
    /// successfully or fails.</returns>
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
            ReadOnlyMemory<char> base64Data = await RetrievePngDataAsync();
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

            _logger.LogInformation("PNG exported successfully: {ByteCount:N0} bytes", bytesWritten);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PNG export cancelled or timed out");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PNG export failed: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Initiates an asynchronous export operation to generate a PNG image using the specified export options.
    /// </summary>
    /// <remarks>This method serializes the provided export options into a JSON object and executes a
    /// JavaScript function to perform the export operation. The export process is handled asynchronously.</remarks>
    /// <param name="options">The options that define the scale, DPI, and background color for the PNG export. The <see
    /// cref="PngExportOptions.BackgroundColor"/> property can be set to <c>null</c> to use a transparent background.</param>
    /// <returns>A task that represents the asynchronous operation of starting the PNG export in the browser context.</returns>
    private async Task StartBrowserExportAsync(PngExportOptions options)
    {
        string exportOptionsJson = JsonSerializer.Serialize(new
        {
            scale = options.ScaleFactor,
            dpi = options.Dpi,
            backgroundColor = options.BackgroundColor ?? "transparent"
        });

        _logger.LogDebug("Export options: {ExportOptions}", exportOptionsJson);

        string script = $"(async () => {{ return await globalThis.exportToPNG({exportOptionsJson}); }})();";
        await _mermaidRenderer.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// Waits for the completion of an export operation, monitoring its progress and respecting a specified timeout and
    /// cancellation token.
    /// </summary>
    /// <remarks>This method registers a callback to monitor the export progress and waits asynchronously for
    /// the operation to complete. If the operation does not complete within the specified timeout, or if the
    /// cancellation token is triggered, the wait is terminated.</remarks>
    /// <param name="progress">An optional progress reporter that receives updates about the export operation's progress.</param>
    /// <param name="timeout">The maximum amount of time to wait for the export operation to complete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests, which can be used to terminate the wait operation prematurely.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
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
            // Unregister asynchronously to ensure polling CTS is cancelled/awaited safely
            await _mermaidRenderer.UnregisterExportProgressCallbackAsync(ProgressCallback).ConfigureAwait(false);
        }

        void ProgressCallback(string statusJson) => HandleExportProgress(statusJson, completionSource, progress);
    }

    /// <summary>
    /// Retrieves PNG data as a Base64-encoded string from the browser's global JavaScript context.
    /// </summary>
    /// <remarks>This method executes a JavaScript script in the browser to extract the PNG data stored in a
    /// global variable. If the data is empty or invalid, an exception is thrown. The returned data is unwrapped from
    /// its JSON string representation.</remarks>
    /// <returns>A read-only memory region containing the Base64-encoded PNG data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the PNG data retrieved from the browser is empty or invalid.</exception>
    private async Task<ReadOnlyMemory<char>> RetrievePngDataAsync()
    {
        const string script = "globalThis.__pngExportResult__ ? String(globalThis.__pngExportResult__) : ''";
        string? base64Data = await _mermaidRenderer.ExecuteScriptAsync(script);

        if (string.IsNullOrWhiteSpace(base64Data))
        {
            throw new InvalidOperationException("PNG data from browser export is empty or invalid");
        }

        return UnwrapJsonString(base64Data.AsMemory());
    }

    /// <summary>
    /// Resets global variables used for PNG export in the rendering context.
    /// </summary>
    /// <remarks>This method clears the values of the global variables <c>__pngExportResult__</c> and
    /// <c>__pngExportStatus__</c> in the JavaScript execution environment. It ensures that any previous export state is
    /// removed, preparing the context for a new export operation.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CleanupExportGlobalVariablesAsync()
    {
        const string script = "globalThis.__pngExportResult__ = null; globalThis.__pngExportStatus__ = '';";
        await _mermaidRenderer.ExecuteScriptAsync(script);
    }

    #endregion PNG Export

    /// <summary>
    /// Retrieves the SVG content rendered within the WebView as a string.
    /// </summary>
    /// <remarks>This method executes a JavaScript script to locate and serialize the SVG element within the
    /// WebView. The SVG content is returned as a read-only memory of characters. If no SVG element is found or the
    /// result is empty, an empty memory is returned.</remarks>
    /// <returns>A <see cref="ReadOnlyMemory{Char}"/> containing the SVG content as a string. Returns <see
    /// cref="ReadOnlyMemory{Char}.Empty"/> if no SVG content is found.</returns>
    private async Task<ReadOnlyMemory<char>> GetSvgContentAsync()
    {
        const string script = """
        (function() {
          const svgElement = document.querySelector('#output svg');
          if (!svgElement) return null;

          // Clone to avoid modifying the original
          const clone = svgElement.cloneNode(true);

          // Serialize to string
          const serializer = new XMLSerializer();
          const svgString = serializer.serializeToString(clone);

          // Return SVG document as a primitive string (bridge-friendly)
          return svgString;
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
            result = await Dispatcher.UIThread.InvokeAsync(() => _mermaidRenderer.ExecuteScriptAsync(script));
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            return ReadOnlyMemory<char>.Empty;
        }

        // Remove any JSON escaping if present and return as memory
        return UnwrapJsonString(result.AsMemory());
    }

    #region Base64 Decoding

    /// <summary>
    /// Decodes a Base64-encoded string and writes the resulting PNG image to the specified file path.
    /// </summary>
    /// <remarks>This method processes the Base64 data in chunks to minimize memory usage and avoid large
    /// allocations. Ensure that the <paramref name="base64Data"/> contains valid Base64-encoded data and that the
    /// <paramref name="targetPath"/> specifies a valid file path where the application has write permissions.</remarks>
    /// <param name="base64Data">The Base64-encoded string containing the image data. The input is trimmed and unwrapped before decoding.</param>
    /// <param name="targetPath">The file path where the decoded PNG image will be written. If the directory does not exist, it will be created.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests during the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of bytes written to the
    /// file.</returns>
    [SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Improves readability")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static async Task<int> DecodeAndWritePngAsync(ReadOnlyMemory<char> base64Data, string targetPath, CancellationToken cancellationToken)
    {
        // Step 1: Trim and unwrap using zero-copy slicing
        ReadOnlyMemory<char> base64Clean = TrimMemory(base64Data);
        base64Clean = UnwrapJsonString(base64Clean);

        // Step 2: Validate size
        ValidateBase64Size(base64Clean);

        // Step 3: Ensure directory exists
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Step 4: Process in chunks to avoid large allocations
        return await DecodeBase64ToFileInChunksAsync(base64Clean, targetPath, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Decodes a Base64-encoded string into binary data and writes the decoded data to a file in chunks.
    /// </summary>
    /// <remarks>This method processes the input data in chunks to minimize memory usage and avoid large
    /// object heap (LOH) allocations.  It uses buffers rented from the <see cref="System.Buffers.ArrayPool{T}"/> to
    /// optimize performance and reduce memory pressure.  The method ensures that the Base64 input is decoded correctly,
    /// handling incomplete or invalid data by throwing appropriate exceptions.</remarks>
    /// <param name="base64Memory">The Base64-encoded input data represented as a <see cref="ReadOnlyMemory{T}"/> of characters.</param>
    /// <param name="targetPath">The file path where the decoded binary data will be written. The file will be created or overwritten.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>The total number of bytes written to the file.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Base64 input contains invalid data, ends with an incomplete 4-character group, or if the decode
    /// buffer is too small.</exception>
    private static async Task<int> DecodeBase64ToFileInChunksAsync(ReadOnlyMemory<char> base64Memory, string targetPath, CancellationToken cancellationToken)
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
            int estimatedSize = EstimateDecodedLength(base64Memory);
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
            int base64Length = base64Memory.Length;

            // Track unconsumed UTF-8 bytes from previous chunk (for Base64 group boundaries)
            int pendingUtf8Bytes = 0;

            while (position < base64Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Determine chunk size - calculate without creating spans
                int remainingChars = base64Length - position;
                int chunkSize = Math.Min(utf8ChunkSize - pendingUtf8Bytes, remainingChars);

                // Create span from memory slice - synchronous, consumed before await
                ReadOnlySpan<char> chunk = base64Memory.Slice(position, chunkSize).Span;

                int utf8Length = ConvertBase64ChunkToUtf8Bytes(chunk, utf8Buffer, pendingUtf8Bytes);

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
                            utf8Buffer.AsSpan(bytesConsumed, pendingUtf8Bytes)
                                .CopyTo(utf8Buffer.AsSpan(0, pendingUtf8Bytes));
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
    /// Determines whether the specified character is a valid Base64 character.
    /// </summary>
    /// <param name="c">The character to validate.</param>
    /// <returns><see langword="true"/> if the character is a valid Base64 character  (letters, digits, '+', '/', or '=');
    /// otherwise, <see langword="false"/>.</returns>
    private static bool IsValidBase64Character(char c)
    {
        return c is (>= 'A' and <= 'Z') or
                    (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or
                    '+' or '/' or '=';
    }

    /// <summary>
    /// Validates that the provided Base64-encoded data does not exceed the maximum allowed size.
    /// </summary>
    /// <remarks>This method ensures that the Base64 data remains within a size limit to prevent excessive
    /// memory usage. The maximum size is defined as 150 MB of Base64-encoded data, which corresponds to approximately
    /// 112 MB of decoded data.</remarks>
    /// <param name="base64Memory">A read-only memory region containing the Base64-encoded data to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the length of <paramref name="base64Memory"/> exceeds the maximum allowed size of 150 MB
    /// (approximately 112 MB decoded).</exception>
    private static void ValidateBase64Size(ReadOnlyMemory<char> base64Memory)
    {
        const int maxBase64Length = 150 * 1_024 * 1_024; // 150 MB Base64 ~ 112 MB decoded (allows ~100 MB PNG)

        if (base64Memory.Length > maxBase64Length)
        {
            throw new InvalidOperationException(
                $"Base64 data exceeds maximum allowed size. Length: {base64Memory.Length:N0} chars, Maximum: {maxBase64Length:N0} chars");
        }
    }

    /// <summary>
    /// Estimates the length of the decoded byte array from a Base64-encoded string.
    /// </summary>
    /// <remarks>This method accounts for whitespace characters in the input and adjusts the calculation based
    /// on the presence of Base64 padding characters ('=') at the end of the input.</remarks>
    /// <param name="base64Memory">A read-only memory region containing the Base64-encoded string.</param>
    /// <returns>The estimated length of the decoded byte array. Returns 0 if the input contains no non-whitespace characters.</returns>
    private static int EstimateDecodedLength(ReadOnlyMemory<char> base64Memory)
    {
        ReadOnlySpan<char> base64Span = base64Memory.Span;

        // Count non-whitespace characters
        int contentLength = 0;
        foreach (char c in base64Span)
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
        int padding = 0;
        if (base64Span.Length >= 2 && base64Span[^1] == '=' && base64Span[^2] == '=')
        {
            padding = 2;
        }
        else if (base64Span.Length >= 1 && base64Span[^1] == '=')
        {
            padding = 1;
        }

        // Each 4 Base64 chars = 3 bytes, minus padding
        int groups = (contentLength + 3) / 4;
        return (groups * 3) - padding;
    }

    /// <summary>
    /// Calculates the optimal buffer size for file operations based on the estimated file size.
    /// </summary>
    /// <param name="estimatedSize">The estimated size of the file, in bytes. Must be a non-negative value.</param>
    /// <returns>The optimal buffer size, in bytes, for efficient file operations.</returns>
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
    private void HandleExportProgress(string statusJson, TaskCompletionSource<bool> completionSource, IProgress<ExportProgress>? progress)
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

            _logger.LogDebug("PNG export progress: {Step} - {Percent}% - {Message}", step, percent, message);

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
                _logger.LogInformation("PNG export completed successfully");
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
            _logger.LogError(ex, "Failed to parse export status JSON");
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
    private void ReportProgress(IProgress<ExportProgress>? progress, ExportProgress exportProgress)
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
                    _logger.LogError(ex, "Progress report failed: {ErrorMessage}", ex.Message);
                }
            });
        }
    }

    #endregion Progress Reporting

    #region Utility Methods

    /// <summary>
    /// Trims leading and trailing white-space characters from the specified read-only memory of characters.
    /// </summary>
    /// <param name="input">The <see cref="ReadOnlyMemory{T}"/> of characters to trim.</param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> of characters with leading and trailing white-space characters removed.
    /// Returns <see cref="ReadOnlyMemory{T}.Empty"/> if the input is empty or consists only of white-space characters.</returns>
    private static ReadOnlyMemory<char> TrimMemory(ReadOnlyMemory<char> input)
    {
        if (input.IsEmpty)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        ReadOnlySpan<char> span = input.Span;

        // Trim start
        int start = 0;
        while (start < span.Length && char.IsWhiteSpace(span[start]))
        {
            start++;
        }

        if (start >= span.Length)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        // Trim end
        int end = span.Length - 1;
        while (end >= start && char.IsWhiteSpace(span[end]))
        {
            end--;
        }

        int length = end - start + 1;
        return length > 0 ? input.Slice(start, length) : ReadOnlyMemory<char>.Empty;
    }

    /// <summary>
    /// Removes enclosing double quotes from a JSON string and processes escape sequences if present.
    /// </summary>
    /// <remarks>If the input string is enclosed in double quotes and contains escape sequences, the method
    /// deserializes the string to handle the escape sequences, which may involve memory allocation. If no escape
    /// sequences are present, the method slices the input to remove the quotes without additional
    /// allocations.</remarks>
    /// <param name="input">The input JSON string as a <see cref="ReadOnlyMemory{T}"/> of characters.</param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> of characters representing the unwrapped string without enclosing quotes. If
    /// the input is empty, returns <see cref="ReadOnlyMemory{T}.Empty"/>.</returns>
    private static ReadOnlyMemory<char> UnwrapJsonString(ReadOnlyMemory<char> input)
    {
        if (input.IsEmpty)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        ReadOnlySpan<char> span = input.Span;

        // Check for JSON string quotes
        if (span.Length >= 2 && span[0] == '"' && span[^1] == '"')
        {
            // Check for escape sequences
            if (span.IndexOf('\\') < 0)
            {
                // No escapes - just slice off the quotes (zero-copy!)
                return input[1..^1];
            }

            // Has escapes - need to deserialize (unavoidable allocation)
            // Use ReadOnlySpan<char> overload to avoid ToString()
            string unescaped = JsonSerializer.Deserialize<string>(span) ?? input.ToString();
            return unescaped.AsMemory();
        }

        return input;
    }

    #endregion Utility Methods

    #region MemoryTextReader

    /// <summary>
    /// Provides a <see cref="TextReader"/> implementation that reads from a <see cref="ReadOnlyMemory{T}"/> of
    /// characters.
    /// </summary>
    /// <remarks>This class enables reading character data from a memory buffer without requiring additional
    /// allocations. It supports synchronous and asynchronous read operations, as well as peeking at the next
    /// character.</remarks>
    private sealed class MemoryTextReader : TextReader
    {
        private readonly ReadOnlyMemory<char> _memory;
        private int _position;

        public MemoryTextReader(ReadOnlyMemory<char> memory)
        {
            _memory = memory;
            _position = 0;
        }

        /// <summary>
        /// Returns the next character in the memory buffer without advancing the position.
        /// </summary>
        /// <remarks>If the current position is at the end of the memory buffer, the method returns -1.
        /// This method does not modify the current position in the buffer.</remarks>
        /// <returns>The next character in the memory buffer as an integer,
        /// or -1 if the end of the buffer is reached.</returns>
        public override int Peek()
        {
            return _position < _memory.Length ? _memory.Span[_position] : -1;
        }

        /// <summary>
        /// Reads a specified number of characters from the current position in the memory
        /// buffer into the provided array.
        /// </summary>
        /// <param name="buffer">The array to which characters will be copied. The array
        /// must have sufficient space to accommodate the characters being read.</param>
        /// <param name="index">The zero-based index in the <paramref name="buffer"/> at which
        /// to begin storing the characters.</param>
        /// <param name="count">The maximum number of characters to read from the memory buffer.</param>
        /// <returns>The number of characters successfully read into the <paramref name="buffer"/>.
        /// Returns 0 if the end of the memory buffer is reached.</returns>
        public override int Read(char[] buffer, int index, int count)
        {
            int remaining = _memory.Length - _position;
            int toRead = Math.Min(count, remaining);

            if (toRead > 0)
            {
                _memory.Span.Slice(_position, toRead)
                    .CopyTo(buffer.AsSpan(index, toRead));
                _position += toRead;
            }

            return toRead;
        }

        /// <summary>
        /// Reads the next character from the memory buffer and advances the position by one.
        /// </summary>
        /// <returns>The next character in the memory buffer as an integer,
        /// or -1 if the end of the buffer has been reached.</returns>
        public override int Read()
        {
            return _position < _memory.Length ? _memory.Span[_position++] : -1;
        }

        /// <summary>
        /// Asynchronously reads a specified number of characters from the current stream and writes them into a buffer,
        /// starting at the specified index.
        /// </summary>
        /// <remarks>This method provides an asynchronous wrapper for the Read method.
        /// However, the operation is executed synchronously and completed immediately.</remarks>
        /// <param name="buffer">The character array to which the data will be written.
        /// The array must have sufficient space to accommodate the characters read.</param>
        /// <param name="index">The zero-based index in the <paramref name="buffer"/> at
        /// which to begin writing the characters read from the stream.</param>
        /// <param name="count">The maximum number of characters to read from the stream.</param>
        /// <returns>A task that represents the asynchronous read operation. The task result contains the total number of
        /// characters read into the buffer. This value can be less than <paramref name="count"/> if the end of the
        /// stream is reached.</returns>
        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            int charsRead = Read(buffer, index, count);
            return Task.FromResult(charsRead);
        }

        /// <summary>
        /// Asynchronously reads a sequence of characters from the current memory
        /// buffer into the specified destination buffer.
        /// </summary>
        /// <remarks>This method reads up to the smaller of the remaining characters in the memory buffer
        /// or the length of the destination buffer. If the operation is canceled via the <paramref
        /// name="cancellationToken"/>, the task will complete in a canceled state.</remarks>
        /// <param name="buffer">The destination buffer to which the characters will be written.
        /// The length of the buffer determines the maximum number of characters to read.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.
        /// The operation will be canceled if the token is triggered.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// The result contains the number of characters successfully read into the buffer.
        /// Returns 0 if the end of the memory buffer is reached.</returns>
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            int remaining = _memory.Length - _position;
            int toRead = Math.Min(buffer.Length, remaining);

            if (toRead > 0)
            {
                _memory.Span.Slice(_position, toRead).CopyTo(buffer.Span);
                _position += toRead;
            }

            return ValueTask.FromResult(toRead);
        }
    }

    #endregion MemoryTextReader
}
