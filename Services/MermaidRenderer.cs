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
using AvaloniaWebView;
using MermaidPad.Services.Platforms;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;

namespace MermaidPad.Services;

/// <summary>
/// Provides rendering of Mermaid diagrams using a local HTTP server and Avalonia WebView.
/// Serves separate HTML and JS files to avoid JavaScript injection issues.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public sealed class MermaidRenderer : IAsyncDisposable
{
    private const string MermaidRequestPath = $"/{AssetHelper.MermaidMinJsFilePath}";
    private const string IndexRequestPath = $"/{AssetHelper.IndexHtmlFilePath}";
    private const string JsYamlRequestPath = $"/{AssetHelper.JsYamlFilePath}";
    private readonly string MermaidLayoutElkRequestPath = $"/{AssetHelper.MermaidLayoutElkPath}".Replace(Path.DirectorySeparatorChar, '/');
    private readonly string MermaidLayoutElkChunkSP2CHFBERequestPath = $"/{AssetHelper.MermaidLayoutElkChunkSP2CHFBEPath}".Replace(Path.DirectorySeparatorChar, '/');
    private readonly string MermaidLayoutElkRenderAVRWSH4DRequestPath = $"/{AssetHelper.MermaidLayoutElkRenderAVRWSH4DPath}".Replace(Path.DirectorySeparatorChar, '/');

    private WebView? _webView;
    private int _renderAttemptCount;
    private HttpListener? _httpListener;
    private byte[]? _htmlContent;
    private byte[]? _mermaidJs;
    private byte[]? _jsYamlJs;
    private byte[]? _mermaidLayoutElkJs;
    private byte[]? _mermaidLayoutElkChunkSP2CHFBEJs;
    private byte[]? _mermaidLayoutElkRenderAVRWSH4DJs;
    private int _serverPort;
    private readonly SemaphoreSlim _serverReadySemaphore = new SemaphoreSlim(0, 1);
    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;

    // Fields for centralized export-status callbacks / poller:
    private readonly List<Action<string>> _exportProgressCallbacks = new List<Action<string>>();
    private CancellationTokenSource? _exportPollerCts;
    private Task? _exportPollerTask;
    private string? _lastExportStatus;
    private readonly Lock _exportCallbackLock = new Lock();

    /// <summary>
    /// Initializes the MermaidRenderer with the specified WebView and assets directory.
    /// </summary>
    /// <param name="webView">The WebView to render Mermaid diagrams in.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync(WebView webView)
    {
        SimpleLogger.Log("=== MermaidRenderer Initialization ===");
        _webView = webView;

        await InitializeWithHttpServerAsync();  // NO ConfigureAwait - caller expects to continue on UI thread
    }

    /// <summary>
    /// Prepares content and starts the local HTTP server for serving Mermaid assets.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task InitializeWithHttpServerAsync()
    {
        // Step 1: Prepare content (HTML and JS separately)
        await PrepareContentFromDiskAsync()
            .ConfigureAwait(false);

        // Step 2: Start HTTP server
        StartHttpServer();

        // Step 3: Wait for server ready
        SimpleLogger.Log("Waiting for HTTP server to be ready...");
        bool serverReady = await _serverReadySemaphore.WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        if (!serverReady)
        {
            throw new TimeoutException("HTTP server failed to start within timeout");
        }

        // Step 4: Navigate to server
        await NavigateToServerAsync();  // Navigation needs UI context, so no ConfigureAwait(false) here
    }

    /// <summary>
    /// Reads the required HTML and JS asset files from disk.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PrepareContentFromDiskAsync()
    {
        Stopwatch sw = Stopwatch.StartNew();

        // Get assets in parallel
        Task<byte[]> indexHtmlTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.IndexHtmlFilePath);
        Task<byte[]> jsTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.MermaidMinJsFilePath);
        Task<byte[]> jsYamlTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.JsYamlFilePath);
        Task<byte[]> mermaidLayoutElkTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.MermaidLayoutElkPath);
        Task<byte[]> mermaidLayoutElkChunkSP2CHFBETask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.MermaidLayoutElkChunkSP2CHFBEPath);
        Task<byte[]> mermaidLayoutElkRenderAVRWSH4DTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.MermaidLayoutElkRenderAVRWSH4DPath);
        await Task.WhenAll(indexHtmlTask, jsTask, jsYamlTask, mermaidLayoutElkTask, mermaidLayoutElkChunkSP2CHFBETask, mermaidLayoutElkRenderAVRWSH4DTask)
            .ConfigureAwait(false);
        _htmlContent = await indexHtmlTask.ConfigureAwait(false);
        _mermaidJs = await jsTask.ConfigureAwait(false);
        _jsYamlJs = await jsYamlTask.ConfigureAwait(false);
        _mermaidLayoutElkJs = await mermaidLayoutElkTask.ConfigureAwait(false);
        _mermaidLayoutElkChunkSP2CHFBEJs = await mermaidLayoutElkChunkSP2CHFBETask.ConfigureAwait(false);
        _mermaidLayoutElkRenderAVRWSH4DJs = await mermaidLayoutElkRenderAVRWSH4DTask.ConfigureAwait(false);

        sw.Stop();
        SimpleLogger.Log($"Prepared HTML: {_htmlContent.Length} bytes");
        SimpleLogger.Log($"Prepared JS: {_mermaidJs.Length} bytes");
        SimpleLogger.Log($"Prepared YAML: {_jsYamlJs.Length} bytes");
        SimpleLogger.Log($"Prepared ELK: {_mermaidLayoutElkJs.Length} bytes");
        SimpleLogger.Log($"Prepared ELK Chunk: {_mermaidLayoutElkChunkSP2CHFBEJs.Length} bytes");
        SimpleLogger.Log($"Prepared ELK Render: {_mermaidLayoutElkRenderAVRWSH4DJs.Length} bytes");

        SimpleLogger.Log($"{nameof(PrepareContentFromDiskAsync)} took {sw.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Starts the local HTTP server to serve Mermaid assets.
    /// </summary>
    private void StartHttpServer()
    {
        _serverPort = GetAvailablePort();
        _serverCancellation = new CancellationTokenSource();

        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{_serverPort}/");
        _httpListener.Start();

        // Background task - _serverTask is needed for proper cleanup
        _serverTask = Task.Run(async () =>
        {
            try
            {
                await HandleHttpRequestsAsync(_serverCancellation.Token);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("HTTP server task error", ex);
            }
        });

        SimpleLogger.Log($"HTTP server started on port {_serverPort}");
    }

    /// <summary>
    /// Handles incoming HTTP requests and serves the appropriate content.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request handling loop.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleHttpRequestsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Signal server ready
            _serverReadySemaphore.Release();
            SimpleLogger.Log("HTTP server ready");

            while (_httpListener?.IsListening == true && !cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    // Use WaitAsync for responsive cancellation while waiting for requests
                    context = await _httpListener.GetContextAsync()
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                    await ProcessRequestAsync(context)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested, exit loop
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError("Error processing HTTP request", ex);
                    try
                    {
                        context?.Response.Close();
                    }
                    catch
                    {
                        Debug.WriteLine("Error closing response stream");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            SimpleLogger.LogError("HTTP server error", ex);
        }
        finally
        {
            SimpleLogger.Log("HTTP request handler stopped");
        }
    }

    /// <summary>
    /// Processes a single HTTP request and serves the requested asset.
    /// </summary>
    /// <param name="context">The HTTP request context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            string requestPath = context.Request.Url?.LocalPath ?? "/";
            SimpleLogger.Log($"Processing request: {requestPath}");

            byte[]? responseBytes = null;
            string? contentType = null;

            // Separate file handling is needed to avoid JavaScript injection issues
            if (string.Equals(requestPath, MermaidRequestPath, StringComparison.OrdinalIgnoreCase) && _mermaidJs is not null)
            {
                responseBytes = _mermaidJs;
                contentType = "application/javascript; charset=utf-8";
            }
            else if (string.Equals(requestPath, MermaidRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 404;
            }
            else if (string.Equals(requestPath, "/", StringComparison.OrdinalIgnoreCase) || string.Equals(requestPath, IndexRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                responseBytes = _htmlContent ?? "<html><body>Content not ready</body></html>"u8.ToArray();
                contentType = "text/html; charset=utf-8";
            }
            else if (string.Equals(requestPath, JsYamlRequestPath, StringComparison.OrdinalIgnoreCase) && _jsYamlJs is not null)
            {
                responseBytes = _jsYamlJs;
                contentType = "application/javascript; charset=utf-8";
            }
            else if (string.Equals(requestPath, MermaidLayoutElkRequestPath, StringComparison.OrdinalIgnoreCase) && _mermaidLayoutElkJs is not null)
            {
                responseBytes = _mermaidLayoutElkJs;
                contentType = "application/javascript; charset=utf-8";
            }
            else if (string.Equals(requestPath, MermaidLayoutElkChunkSP2CHFBERequestPath, StringComparison.OrdinalIgnoreCase) &&
                     _mermaidLayoutElkChunkSP2CHFBEJs is not null)
            {
                responseBytes = _mermaidLayoutElkChunkSP2CHFBEJs;
                contentType = "application/javascript; charset=utf-8";
            }
            else if (string.Equals(requestPath, MermaidLayoutElkRenderAVRWSH4DRequestPath, StringComparison.OrdinalIgnoreCase) &&
                     _mermaidLayoutElkRenderAVRWSH4DJs is not null)
            {
                responseBytes = _mermaidLayoutElkRenderAVRWSH4DJs;
                contentType = "application/javascript; charset=utf-8";
            }
            else
            {
                context.Response.StatusCode = 404;
                SimpleLogger.Log($"404 for: {requestPath}");
            }

            if (responseBytes?.Length > 0 && !string.IsNullOrWhiteSpace(contentType))
            {
                // set the response code before writing to the output stream
                context.Response.StatusCode = 200;
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = responseBytes.Length;
                await context.Response.OutputStream.WriteAsync(responseBytes)
                    .ConfigureAwait(false);

                SimpleLogger.Log($"Served {requestPath}: {responseBytes.Length} bytes");
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error processing HTTP request", ex);

            // Setting the StatusCode property can throw exceptions in certain scenarios
            try
            {
                context.Response.StatusCode = 500;
            }
            catch
            {
                Debug.WriteLine("Error setting response status code");
            }
        }
        finally
        {
            try
            {
                context.Response.OutputStream.Close();
            }
            catch
            {
                Debug.WriteLine("Error closing response stream");
            }
        }
    }

    /// <summary>
    /// Navigates the WebView to the local HTTP server URL.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task NavigateToServerAsync()
    {
        string serverUrl = $"http://localhost:{_serverPort}/";
        SimpleLogger.Log($"Navigating to: {serverUrl}");

        bool navigationCompleted = false;

        _webView!.NavigationCompleted += OnNavigationCompleted;

        await Dispatcher.UIThread.InvokeAsync(() => _webView.Url = new Uri(serverUrl));

        // Wait for navigation
        for (int i = 0; i < 50 && !navigationCompleted; i++)
        {
            await Task.Delay(100);  // NO ConfigureAwait - caller needs UI context
        }

        if (navigationCompleted)
        {
            SimpleLogger.Log("Navigation completed successfully");
        }
        else
        {
            throw new InvalidOperationException("Navigation failed");
        }

        return;

        void OnNavigationCompleted(object? sender, EventArgs e)
        {
            navigationCompleted = true;
            SimpleLogger.LogWebView("navigation completed", "HTTP mode");
            _webView!.NavigationCompleted -= OnNavigationCompleted; // Unsubscribe to avoid memory leaks
        }
    }

    /// <summary>
    /// Finds an available port in the range 8083-8199 for the HTTP server.
    /// </summary>
    /// <returns>An available port number.</returns>
    private static int GetAvailablePort()
    {
        for (int port = 8083; port < 8200; port++)
        {
            try
            {
                using HttpListener listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                listener.Stop();
                return port;
            }
            catch
            {
                // Port in use, try next
                SimpleLogger.Log($"Port {port} is in use, trying next: {port + 1}");
            }
        }
        throw new InvalidOperationException("No available ports found in range 8083-8199");
    }

    /// <summary>
    /// Renders the specified Mermaid diagram source in the WebView.
    /// </summary>
    /// <param name="mermaidSource">The Mermaid diagram source code.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RenderAsync(string mermaidSource)
    {
        _renderAttemptCount++;
        SimpleLogger.Log($"Render attempt #{_renderAttemptCount}");

        if (_webView is null)
        {
            SimpleLogger.LogError("WebView not initialized");
            return;
        }

        if (string.IsNullOrWhiteSpace(mermaidSource))
        {
            try
            {
                async Task ClearOutputAsync() => await _webView.ExecuteScriptAsync("clearOutput();");
                await Dispatcher.UIThread.InvokeAsync(ClearOutputAsync);
                SimpleLogger.Log("Cleared output");
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("Clear failed", ex);
            }
            return;
        }

        // Simple JavaScript execution
        string escaped;
        ReadOnlySpan<char> sourceSpan = mermaidSource.AsSpan();
        if (!sourceSpan.Contains('\\') && !sourceSpan.Contains('`'))
        {
            escaped = mermaidSource;
        }
        else
        {
            escaped = EscapeSource(mermaidSource);
        }

        string script = $"renderMermaid(`{escaped}`);";
        try
        {
            async Task RenderMermaidAsync()
            {
                string? result = await _webView.ExecuteScriptAsync(script);
                SimpleLogger.Log($"Render result: {result ?? "(null)"}");
            }
            await Dispatcher.UIThread.InvokeAsync(RenderMermaidAsync);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Render failed", ex);
        }

        static string EscapeSource(string source)
        {
            ReadOnlySpan<char> sourceSpan = source.AsSpan();
            StringBuilder sb = new StringBuilder(sourceSpan.Length);
            foreach (char c in sourceSpan)
            {
                // Prefer Append(char) for single characters
                switch (c)
                {
                    case '\\':
                        sb.Append('\\').Append('\\');
                        break;
                    case '`':
                        sb.Append('\\').Append('`');
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Executes JavaScript in the WebView and returns the result.
    /// </summary>
    /// <param name="script">The JavaScript code to execute.</param>
    /// <returns>The result of the JavaScript execution as a string, or null if execution fails.</returns>
    public async Task<string?> ExecuteScriptAsync(string script)
    {
        if (_webView is null)
        {
            SimpleLogger.LogError("WebView not initialized for script execution");
            return null;
        }

        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                string? result = await _webView.ExecuteScriptAsync(script);
                SimpleLogger.LogJavaScript(script, true, writeToDebug: true, result);
                return result;
            });
        }
        catch (Exception ex)
        {
            SimpleLogger.LogJavaScript(script, false, writeToDebug: true, ex.Message);
            SimpleLogger.LogError("Script execution failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Register a callback to receive export progress status JSON from the page.
    /// MermaidRenderer will start a single centralized poller (ExecuteScriptAsync) while any callbacks are registered.
    /// This avoids multiple consumers polling independently.
    /// </summary>
    public void RegisterExportProgressCallback(Action<string> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_exportCallbackLock)
        {
            _exportProgressCallbacks.Add(callback);

            // Start poller if needed
            if (_exportPollerCts is null || _exportPollerCts.IsCancellationRequested)
            {
                _exportPollerCts = new CancellationTokenSource();
                // Store the Task so DisposeAsync can await clean shutdown
                _exportPollerTask = StartExportStatusPollerAsync(_exportPollerCts.Token);
            }
        }
    }

    /// <summary>
    /// Unregister a previously registered export progress callback.
    /// </summary>
    public void UnregisterExportProgressCallback(Action<string>? callback)
    {
        if (callback is null)
        {
            return;
        }

        lock (_exportCallbackLock)
        {
            _exportProgressCallbacks.Remove(callback);

            if (_exportProgressCallbacks.Count == 0)
            {
                try
                {
                    _exportPollerCts?.Cancel();
                    _exportPollerCts?.Dispose();
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError("Failed to stop export status poller", ex);
                }
                finally
                {
                    _exportPollerCts = null;
                    _lastExportStatus = null;
                }
            }
        }
    }

    /// <summary>
    /// Centralized poller that queries the page for globalThis.__pngExportStatus__ and forwards updates to registered callbacks.
    /// Runs on UI thread via ExecuteScriptAsync calls and is lightweight when there is no change.
    /// </summary>
    private async Task StartExportStatusPollerAsync(CancellationToken token)
    {
        SimpleLogger.Log("Starting export status poller");
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Query the page for the status (bridge-friendly string)
                    const string statusScript = "globalThis.__pngExportStatus__ ? String(globalThis.__pngExportStatus__) : ''";
                    string? statusJson = await ExecuteScriptAsync(statusScript);

                    if (!string.IsNullOrWhiteSpace(statusJson))
                    {
                        // Remove JSON quoting if present
                        if (statusJson.StartsWith('\"') && statusJson.EndsWith('\"'))
                        {
                            statusJson = JsonSerializer.Deserialize<string>(statusJson) ?? statusJson;
                        }

                        // Forward only when it changes to reduce churn
                        if (statusJson != _lastExportStatus)
                        {
                            _lastExportStatus = statusJson;

                            Action<string>[] callbacks;
                            lock (_exportCallbackLock)
                            {
                                callbacks = _exportProgressCallbacks.ToArray();
                            }

                            foreach (Action<string> cb in callbacks)
                            {
                                try
                                {
                                    cb(statusJson);
                                }
                                catch (Exception ex)
                                {
                                    SimpleLogger.LogError("Export progress callback threw", ex);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignore transient script errors but log for diagnostics
                    SimpleLogger.LogError("Export status poller script error", ex);
                }

                // Poll interval - moderate (250ms). Centralized poller keeps this to one source instead of many.
                try
                {
                    await Task.Delay(250, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            SimpleLogger.Log("Export status poller stopped");
        }
    }

    /// <summary>
    /// Disposes the MermaidRenderer, stopping the HTTP server and cleaning up resources.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Cancel server operations
            if (_serverCancellation is not null)
            {
                await _serverCancellation.CancelAsync()
                    .ConfigureAwait(false);
            }

            // Stop and close HTTP listener
            if (_httpListener?.IsListening == true)
            {
                _httpListener.Stop();
            }
            _httpListener?.Close();

            // Stop and cleanup export poller if running
            try
            {
                // Cancel poller
                if (_exportPollerCts is not null)
                {
                    try
                    {
                        await _exportPollerCts.CancelAsync()
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError("Failed to cancel export poller CTS", ex);
                    }
                }

                // Await poller task for a short timeout to ensure clean shutdown
                if (_exportPollerTask is not null)
                {
                    try
                    {
                        await _exportPollerTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Log but do not rethrow during dispose
                        SimpleLogger.LogError("Export poller did not stop cleanly", ex);
                    }
                }
            }
            finally
            {
                // Clear callbacks and dispose CTS
                lock (_exportCallbackLock)
                {
                    _exportProgressCallbacks.Clear();
                    _lastExportStatus = null;
                }

                try
                {
                    _exportPollerCts?.Dispose();
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError("Failed to dispose export poller CTS", ex);
                }
                _exportPollerCts = null;
                _exportPollerTask = null;
            }

            // Wait for server task to complete (with timeout)
            if (_serverTask is not null)
            {
                try
                {
                    const int maxWaitSeconds = 5;
                    await _serverTask.WaitAsync(TimeSpan.FromSeconds(maxWaitSeconds))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError("Error waiting for server task", ex);
                }
            }

            _serverCancellation?.Dispose();
            _serverReadySemaphore.Dispose();
            _serverTask?.Dispose();

            SimpleLogger.Log("MermaidRenderer disposed");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error during disposal", ex);
        }
    }
}
