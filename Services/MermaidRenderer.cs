using AvaloniaWebView;
using System.Diagnostics;

namespace MermaidPad.Services;

/// <summary>
/// Thin wrapper around executing Mermaid render script inside the embedded WebView.
/// Now with simple file logging for debugging WebView rendering issues.
/// </summary>
public sealed class MermaidRenderer
{
    private WebView? _webView;
    private bool _isWebViewReady = false;
    private DateTime _lastRenderTime = DateTime.MinValue;
    private int _renderAttemptCount = 0;

    public MermaidRenderer()
    {
        SimpleLogger.Log("MermaidRenderer initialized");
    }

    /// <summary>Attach the WebView after it is constructed (late binding for DI).</summary>
    public void Attach(WebView webView)
    {
        SimpleLogger.Log("Attaching WebView to MermaidRenderer");

        if (_webView != null)
        {
            SimpleLogger.Log("WebView was already attached, detaching previous instance");
        }

        _webView = webView;
        _isWebViewReady = false;

        // Subscribe to WebView events for comprehensive debugging
        SetupWebViewEventHandlers();

        SimpleLogger.LogWebView("attached", $"Type: {webView.GetType().Name}");
    }

    private void SetupWebViewEventHandlers()
    {
        if (_webView == null) return;

        try
        {
            // Navigation events
            _webView.NavigationCompleted += (sender, args) =>
            {
                _isWebViewReady = args.IsSuccess;
                if (args.IsSuccess)
                {
                    SimpleLogger.LogWebView("navigation completed", $"RawArgs: {args.RawArgs}");
                }
                else
                {
                    SimpleLogger.LogWebView("navigation failed", $"RawArgs: {args.RawArgs}");
                }
            };

            _webView.NavigationStarting += (sender, args) =>
            {
                SimpleLogger.LogWebView("navigation starting", $"RawArgs: {args.RawArgs}");
            };

            SimpleLogger.Log("WebView event handlers configured successfully");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to setup WebView event handlers", ex);
        }
    }

    public async Task RenderAsync(string mermaidSource)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _renderAttemptCount++;

        SimpleLogger.Log($"Render attempt #{_renderAttemptCount} started");

        if (_webView is null)
        {
            string error = "MermaidRenderer.RenderAsync called before WebView attached";
            SimpleLogger.LogError(error);
            Debug.WriteLine(error);
            return;
        }

        if (string.IsNullOrWhiteSpace(mermaidSource))
        {
            SimpleLogger.Log("Empty mermaid source provided, clearing output");
            try
            {
                await ExecuteAsync("clearOutput();");
                stopwatch.Stop();
                SimpleLogger.LogTiming("Clear output", stopwatch.Elapsed, success: true);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                SimpleLogger.LogTiming("Clear output", stopwatch.Elapsed, success: false);
                SimpleLogger.LogError("Failed to clear output", ex);
            }
            return;
        }

        // Log render details
        SimpleLogger.Log($"Rendering Mermaid diagram: {mermaidSource.Length} characters");
        string preview = mermaidSource.Length > 200 ? mermaidSource[..200] + "..." : mermaidSource;
        SimpleLogger.Log($"Mermaid source preview: {preview}");

        string escaped = EscapeForJs(mermaidSource);
        string script = $"renderMermaid(`{escaped}`);";

        try
        {
            string? result = await ExecuteAsync(script);
            stopwatch.Stop();

            _lastRenderTime = DateTime.Now;
            SimpleLogger.LogJavaScript("renderMermaid(...)", true, result);
            SimpleLogger.LogTiming("Mermaid render", stopwatch.Elapsed, success: true);
            SimpleLogger.Log($"Render attempt #{_renderAttemptCount} completed successfully in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            SimpleLogger.LogJavaScript("renderMermaid(...)", false, ex.Message);
            SimpleLogger.LogTiming("Mermaid render", stopwatch.Elapsed, success: false);
            SimpleLogger.LogError("Failed to render mermaid diagram", ex);

            Debug.WriteLine($"Render failed: {ex}");

            // Additional diagnostic information
            await LogWebViewDiagnostics();
        }
    }

    private async Task LogWebViewDiagnostics()
    {
        try
        {
            SimpleLogger.Log("=== WebView Diagnostics ===");
            SimpleLogger.Log($"WebView ready state: {_isWebViewReady}");
            SimpleLogger.Log($"Last successful render: {(_lastRenderTime == DateTime.MinValue ? "Never" : _lastRenderTime.ToString())}");
            SimpleLogger.Log($"Total render attempts: {_renderAttemptCount}");

            // Try to get basic JavaScript info
            try
            {
                string? docReady = await ExecuteAsync("document.readyState");
                SimpleLogger.Log($"Document ready state: {docReady ?? "null"}");
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Could not get document ready state: {ex.Message}");
            }

            try
            {
                string? mermaidExists = await ExecuteAsync("typeof window.mermaid");
                SimpleLogger.Log($"Mermaid library type: {mermaidExists ?? "null"}");

                if (mermaidExists == "object")
                {
                    // Mermaid is loaded, check its version
                    try
                    {
                        string? version = await ExecuteAsync("window.mermaid.version || 'unknown'");
                        SimpleLogger.Log($"Mermaid library version: {version}");
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Log($"Could not get Mermaid version: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Could not check Mermaid library: {ex.Message}");
            }

            try
            {
                string? renderFunctionExists = await ExecuteAsync("typeof window.renderMermaid");
                SimpleLogger.Log($"renderMermaid function type: {renderFunctionExists ?? "null"}");
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Could not check renderMermaid function: {ex.Message}");
            }

            // Check what's actually in the output div
            try
            {
                string? outputContent = await ExecuteAsync("document.getElementById('output')?.innerHTML || 'no output div'");
                SimpleLogger.Log($"Output div content: {(outputContent?.Length > 200 ? outputContent[..200] + "..." : outputContent)}");

                string? outputExists = await ExecuteAsync("document.getElementById('output') ? 'exists' : 'missing'");
                SimpleLogger.Log($"Output div exists: {outputExists}");
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Could not check output div: {ex.Message}");
            }

            SimpleLogger.Log("=== End WebView Diagnostics ===");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to run WebView diagnostics", ex);
        }
    }

    private static string EscapeForJs(string text)
        => text.Replace("\\", "\\\\").Replace("`", "\\`");

    private async Task<string?> ExecuteAsync(string script)
    {
        if (_webView == null)
        {
            throw new InvalidOperationException("WebView is not attached");
        }

        string scriptPreview = script.Length > 100 ? script[..100] + "..." : script;
        SimpleLogger.Log($"Executing JavaScript: {scriptPreview}");

        try
        {
            string? result = await _webView.ExecuteScriptAsync(script);
            SimpleLogger.Log($"JavaScript execution result: {result ?? "(null)"}");
            return result;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"JavaScript execution failed for script: {script}", ex);
            throw;
        }
    }
}