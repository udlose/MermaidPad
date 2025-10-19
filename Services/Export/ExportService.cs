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
            string? svgContent = await GetSvgContentAsync(forPngExport: false);

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
    /// Exports the current diagram as a PNG image to the specified file path asynchronously.
    /// </summary>
    /// <remarks>The export operation runs asynchronously and may perform file I/O and image conversion on
    /// background threads. If the target directory does not exist, it will be created automatically. Progress updates,
    /// if requested, are reported at key stages of the export process.</remarks>
    /// <param name="targetPath">The file path where the exported PNG image will be saved. Cannot be null or empty.</param>
    /// <param name="options">The options to use for PNG export, such as DPI and scale factor. If null, default options are used.</param>
    /// <param name="progress">An optional progress reporter that receives updates about the export operation. Progress updates are marshaled
    /// to the UI thread if provided.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the export operation.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the diagram's SVG content cannot be extracted.</exception>
    public async Task ExportPngAsync(
        string targetPath,
        PngExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        options ??= new PngExportOptions();
        SimpleLogger.Log($"Starting PNG export to: {targetPath} (DPI: {options.Dpi}, Scale: {options.ScaleFactor}x)");

        try
        {
            // Report initial progress - marshal to UI thread
            ReportProgress(progress, new ExportProgress
            {
                Step = ExportStep.Initializing,
                PercentComplete = 0,
                Message = "Initializing export..."
            });

            // Get SVG content from the WebView - MUST stay on UI thread (WebView access)
            string? svgContent = await GetSvgContentAsync(forPngExport: true);

            if (string.IsNullOrWhiteSpace(svgContent))
            {
                throw new InvalidOperationException("Failed to extract SVG content from diagram");
            }

            // Wrap progress so all updates are marshaled to the UI thread
            IProgress<ExportProgress>? uiProgress = progress is null
                ? null
                : new Progress<ExportProgress>(p => ReportProgress(progress, p));

            // Convert to PNG on background thread
            ReadOnlyMemory<byte> pngData = await _imageConversionService.ConvertSvgToPngAsync(
                svgContent,
                options,
                uiProgress,
                cancellationToken)
            .ConfigureAwait(false);

            // File I/O on background thread - ConfigureAwait(false) keeps us on background thread
            string? directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(targetPath, pngData, cancellationToken)
                .ConfigureAwait(false);

            // Report completion - marshal to UI thread
            ReportProgress(progress, new ExportProgress
            {
                Step = ExportStep.Complete,
                PercentComplete = 100,
                Message = "Export completed successfully!"
            });

            SimpleLogger.Log($"PNG exported successfully: {pngData.Length:N0} bytes");
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
    /// Retrieves the SVG content of the currently displayed diagram, optionally re-rendering it for PNG export compatibility.
    /// </summary>
    /// <remarks>When <paramref name="forPngExport"/> is <see langword="true"/>, the method ensures
    /// compatibility with rendering engines that do not support certain SVG elements, such as <c>foreignObject</c>.
    /// This is useful for scenarios where the SVG will be converted to a PNG image.  The method interacts with a
    /// WebView to execute JavaScript for retrieving or re-rendering the SVG content. It ensures that all WebView
    /// interactions occur on the UI thread.</remarks>
    /// <param name="forPngExport">A boolean value indicating whether the SVG should be re-rendered for PNG export. If <see langword="true"/>, the
    /// method re-renders the SVG to exclude unsupported elements like <c>foreignObject</c>. If <see langword="false"/>,
    /// the currently displayed SVG is returned as-is.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result is a string containing the SVG
    /// content, or <see langword="null"/> if the SVG content could not be retrieved.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the JavaScript rendering process fails or if the exported SVG content cannot be retrieved.</exception>
    public async Task<string?> GetSvgContentAsync(bool forPngExport)
    {
        if (!forPngExport)
        {
            // Get currently displayed SVG
            const string script = """
            (function() {
              const svgElement = document.querySelector('#output svg');
              if (!svgElement) return null;

              // Clone to avoid modifying the original
              const clone = svgElement.cloneNode(true);

              // Add XML declaration and doctype
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
                result = await Dispatcher.UIThread.InvokeAsync(
                    () => _mermaidRenderer.ExecuteScriptAsync(script));
            }

            // Remove any JSON escaping if present
            if (!string.IsNullOrWhiteSpace(result) && result.StartsWith('\"') && result.EndsWith('\"'))
            {
                result = JsonSerializer.Deserialize<string>(result) ?? result;
            }

            return result;
        }

        // Re-render for export without foreignObjects since SkiaSharp does not support them
        // Having foreignObjects in the SVG can lead to missing labels in the PNG output
        try
        {
            // Get the current diagram source
            string? originalDiagramSource = await _mermaidRenderer.ExecuteScriptAsync("globalThis.lastMermaidSource || ''");
            SimpleLogger.Log($"originalDiagramSource: {Environment.NewLine}{originalDiagramSource}{Environment.NewLine}");
            if (string.IsNullOrWhiteSpace(originalDiagramSource) || originalDiagramSource == "''")
            {
                SimpleLogger.Log("No diagram source available for export render");
                return null;
            }

            // Clean up JavaScript string quotes
            string trimmedDiagramSource = originalDiagramSource.Trim('\'', '\"');
            SimpleLogger.Log($"trimmedDiagramSource: {Environment.NewLine}{trimmedDiagramSource}{Environment.NewLine}");
            if (string.IsNullOrWhiteSpace(trimmedDiagramSource))
            {
                return null;
            }

            // STEP 1: Store the diagram source in a global variable
            // This is already a JavaScript string, so we can use it directly
            const string setSourceScript = "globalThis.exportDiagramSource = globalThis.lastMermaidSource;";
            await _mermaidRenderer.ExecuteScriptAsync(setSourceScript);
            SimpleLogger.Log("Diagram source stored in global variable");

            // STEP 2: Kick off the async export in-page without returning a promise to .NET
            // We will poll a synchronous getter for a primitive string result (__exportStatus__) to avoid interop coercion.
            const string startExportScript = """
            (function(){
              try{
                if (!globalThis.exportDiagramSource) {
                  globalThis.__exportStatus__ = JSON.stringify({ status:{ success:false, error:'No diagram source available' }, svg:'' });
                  globalThis.lastExportedSvg = null;
                  return;
                }

                // Remove quotes if present (it's already a string)
                let src = globalThis.exportDiagramSource;
                if (typeof src === 'string') src = src.replace(/^["']|["']$/g, '');

                // Reset status before starting
                globalThis.__exportStatus__ = '';

                // Start async export but DO NOT return the promise (bridge expects sync return)
                (async () => {
                  try {
                    const statusObj = await globalThis.exportMermaidWithoutForeignObject(src);
                    globalThis.exportDiagramSource = null;

                    let svg = '';
                    if (statusObj && statusObj.success && globalThis.lastExportedSvg) {
                      const xmlDecl = '<?xml version="1.0" encoding="UTF-8"?>';
                      const raw = String(globalThis.lastExportedSvg);
                      svg = raw.startsWith('<?xml') ? raw : (xmlDecl + raw);
                    }

                    globalThis.__exportStatus__ = JSON.stringify({ status: statusObj || { success:false, error:'No result' }, svg });

                  } catch (e) {
                    globalThis.exportDiagramSource = null;
                    globalThis.lastExportedSvg = null;
                    globalThis.__exportStatus__ = JSON.stringify({ status:{ success:false, error: (e && e.message) || 'Unknown error' }, svg:'' });
                  }
                })();
              } catch (e){
                globalThis.exportDiagramSource = null;
                globalThis.lastExportedSvg = null;
                globalThis.__exportStatus__ = JSON.stringify({ status:{ success:false, error: (e && e.message) || 'Unknown error' }, svg:'' });
              }
            })();
            """;

            SimpleLogger.Log("Calling export function (async start, no return)...");
            await _mermaidRenderer.ExecuteScriptAsync(startExportScript);

            // STEP 3: Poll a synchronous getter for a primitive string result
            const string readStatusScript = "globalThis.__exportStatus__ ? String(globalThis.__exportStatus__) : ''";

            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            string? payload = "";
            while (sw.Elapsed < TimeSpan.FromSeconds(10))
            {
                payload = await _mermaidRenderer.ExecuteScriptAsync(readStatusScript);

                // If the bridge wrapped it in quotes, it will still be non-empty here ("..."), so we can break.
                if (!string.IsNullOrWhiteSpace(payload) && payload != "\"\"" && payload != "null" && payload != "{}")
                {
                    break;
                }

                await Task.Delay(50);
            }

            if (string.IsNullOrWhiteSpace(payload) || payload == "\"\"" || payload == "null" || payload == "{}")
            {
                SimpleLogger.LogError("Export function returned no response");
                return null;
            }

            // Remove any JSON escaping if present
            if (payload.StartsWith('\"') && payload.EndsWith('\"'))
            {
                payload = JsonSerializer.Deserialize<string>(payload) ?? payload;
            }

            // Parse combined status + svg
            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("status", out JsonElement statusEl) ||
                !statusEl.TryGetProperty("success", out JsonElement okEl) ||
                !okEl.GetBoolean())
            {
                string err = (statusEl.TryGetProperty("error", out JsonElement errEl) ? errEl.GetString() : "Unknown error") ?? "Unknown error";
                SimpleLogger.LogError($"JavaScript render failed: {err}");
                throw new InvalidOperationException($"JavaScript render failed: {err}");
            }

            string svg = root.TryGetProperty("svg", out JsonElement svgEl) ? (svgEl.GetString() ?? "") : "";
            if (string.IsNullOrWhiteSpace(svg))
            {
                SimpleLogger.LogError("Failed to retrieve exported SVG string");
                return null;
            }

            SimpleLogger.Log($"Export SVG retrieved successfully ({svg.Length:N0} chars)");

            // Optional: cleanup globals
            await _mermaidRenderer.ExecuteScriptAsync("globalThis.lastExportedSvg = null; globalThis.__exportStatus__ = '';");

            return svg;
        }
        catch (JsonException je)
        {
            SimpleLogger.LogError($"Failed to parse result: {je.Message}", je);
            return null;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Failed to get export SVG content: {ex.Message}", ex);
            throw new InvalidOperationException("Failed to retrieve export SVG content", ex);
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
