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

using AsyncAwaitBestPractices;
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

    // Fields for centralized export-status callbacks / polling:
    private readonly List<Action<string>> _exportProgressCallbacks = new List<Action<string>>();
    private CancellationTokenSource? _exportPollingCts;
    private Task? _exportPollingTask;
    private string? _lastExportStatus;
    private readonly Lock _exportCallbackLock = new Lock();

    // WebView ready detection
    public event EventHandler? WebViewReadyChanged;
    private DispatcherTimer? _webViewReadyTimer;
    private const int WebViewReadyPollIntervalMs = 100;

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

        StartWebViewReadyTimer();
    }

    /// <summary>
    /// Starts a timer to periodically check if the WebView has completed its first render.
    /// </summary>
    /// <remarks>The timer runs at a fixed interval and triggers the <see cref="OnWebViewReadyTimerTick"/>
    /// event handler on each tick. This method is typically used to monitor the readiness of the WebView for
    /// further operations.</remarks>
    private void StartWebViewReadyTimer()
    {
        SimpleLogger.Log("Starting WebView ready detection timer");

        _webViewReadyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(WebViewReadyPollIntervalMs)
        };

        _webViewReadyTimer.Tick += OnWebViewReadyTimerTick;
        _webViewReadyTimer.Start();

        SimpleLogger.Log("WebView ready timer started (checks for first render completion)");
    }

    /// <summary>
    /// Handles the timer tick event to check the readiness status of the WebView control.
    /// </summary>
    /// <remarks>This method ensures that the readiness status of the WebView is checked asynchronously. If
    /// the WebView instance is null, the method exits without performing any action.</remarks>
    /// <param name="sender">The source of the event, typically the timer triggering the tick.</param>
    /// <param name="e">The event data associated with the timer tick.</param>
    private void OnWebViewReadyTimerTick(object? sender, EventArgs e)
    {
        if (_webView is null)
        {
            return;
        }

        OnWebViewReadyTimerTickAsync()
            .SafeFireAndForget(onException: static ex => SimpleLogger.LogError("Error checking WebView ready status", ex));
    }

    /// <summary>
    /// Handles the periodic timer tick to check if the WebView's first render has completed.
    /// </summary>
    /// <remarks>This method executes a JavaScript script to determine if the WebView has completed its first
    /// render.  If the render is complete, the timer is stopped, and the <see cref="WebViewReadyChanged"/> event is
    /// raised.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task OnWebViewReadyTimerTickAsync()
    {
        // Query JavaScript to check if FIRST RENDER has completed
        // This flag is set in hideLoadingIndicator() which is called AFTER successful render
        const string readyCheckScript = "typeof globalThis.__renderingComplete__ !== 'undefined' && globalThis.__renderingComplete__ === true";
        string? result = await ExecuteScriptAsync(readyCheckScript);

        if (result == "true")
        {
            // First render is complete! Stop the timer and fire the event
            _webViewReadyTimer?.Stop();
            SimpleLogger.Log("WebView is ready - First render completed");

            // Fire the event (already on UI thread)
            WebViewReadyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Initializes the application by preparing content, starting an HTTP server, and navigating to the server.
    /// </summary>
    /// <remarks>This method performs the following steps: 1. Prepares the required content from disk. 2.
    /// Starts an HTTP server to serve the content. 3. Waits for the HTTP server to be ready, with a timeout of 10
    /// seconds. 4. Navigates to the HTTP server once it is ready.  If the HTTP server fails to start within the timeout
    /// period, a <see cref="TimeoutException"/> is thrown.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">Thrown if the HTTP server does not become ready within the 10-second timeout period.</exception>
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
    /// Asynchronously loads and prepares content from disk, including HTML, JavaScript, and other assets, for use in
    /// the application.
    /// </summary>
    /// <remarks>This method retrieves multiple assets in parallel to optimize performance. The loaded content
    /// is stored in memory for subsequent use. The method logs the size of each asset and the total time taken to
    /// complete the operation.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
    /// Starts an HTTP server on an available port and begins listening for incoming requests.
    /// </summary>
    /// <remarks>This method initializes an <see cref="HttpListener"/> instance, binds it to an available port
    /// on localhost, and starts a background task to handle incoming HTTP requests asynchronously. The server runs
    /// until explicitly stopped.</remarks>
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
    /// Navigates the embedded web view to the server URL asynchronously.
    /// </summary>
    /// <remarks>This method sets the web view's URL to the server address and waits for the navigation to
    /// complete. If the navigation does not complete within the specified timeout,
    /// a <see cref="InvalidOperationException"/> is thrown. The method must be called on
    /// the UI thread as it interacts with the web view and dispatcher.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the navigation does not complete within the allowed time.</exception>
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
    /// Finds and returns the first available port within the range 8083 to 8199.
    /// </summary>
    /// <remarks>This method attempts to bind to each port in the specified range, starting from 8083, to
    /// determine if it is available for use. If a port is in use, the method proceeds to the next port. If no available
    /// ports are found within the range, an <see cref="InvalidOperationException"/> is thrown.</remarks>
    /// <returns>The first available port number within the range 8083 to 8199.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no available ports are found in the range 8083-8199.</exception>
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
    /// Renders a Mermaid diagram in the associated WebView control using the provided Mermaid source code.
    /// </summary>
    /// <remarks>This method executes JavaScript in the WebView to render the Mermaid diagram. The source code
    /// is escaped to ensure compatibility with JavaScript string literals. If the WebView is not initialized, the
    /// method logs an error and exits without performing any action.  This method must be called on the UI thread, as
    /// it interacts with the WebView control. Exceptions during rendering or clearing are logged but do not propagate
    /// to the caller.</remarks>
    /// <param name="mermaidSource">The Mermaid source code to render. If the source is null, empty, or consists only of whitespace, the output in
    /// the WebView will be cleared instead.</param>
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
                // Copy locals to explicit locals to make intent clear and avoid captures
                WebView webView = _webView!;

                // Explicit call (lambda still captures webView local, but the static local function prevents
                // implicit capture of surrounding variables inside the function body).
                await Dispatcher.UIThread.InvokeAsync(async () => await ClearOutputAsync(webView));

                SimpleLogger.Log("Cleared output");

                // Static local function to avoid capturing outer variables inside the function
                static Task ClearOutputAsync(WebView webViewParam) => webViewParam.ExecuteScriptAsync("clearOutput();");
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

            // Explicit invocation on UI thread
            await Dispatcher.UIThread.InvokeAsync(async () => await RenderMermaidAsync());
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
    /// Executes the specified JavaScript code asynchronously in the context of the WebView.
    /// </summary>
    /// <remarks>This method invokes the script execution on the UI thread. If the WebView is not initialized,
    /// the method logs an error and returns <see langword="null"/>. Any exceptions encountered during script execution
    /// are logged, and <see langword="null"/> is returned.</remarks>
    /// <param name="script">The JavaScript code to execute. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the script execution
    /// as a string, or <see langword="null"/> if the WebView is not initialized or an error occurs during execution.</returns>
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
                // TODO should i re-enable this later?
                // SimpleLogger.LogJavaScript(script, true, writeToDebug: true, result);
                return result;
            });
        }
        catch (Exception ex)
        {
            // TODO should i re-enable this later?
            // SimpleLogger.LogJavaScript(script, false, writeToDebug: true, ex.Message);
            SimpleLogger.LogError("Script execution failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Registers a callback to receive updates on the export progress.
    /// </summary>
    /// <remarks>The provided callback will be added to the list of registered callbacks and invoked
    /// periodically with updates on the export progress. If no polling is currently running,
    /// this method will start a background task to monitor and report export progress.</remarks>
    /// <param name="callback">An <see cref="Action{T}"/> delegate that will be invoked with a
    /// string parameter containing the export progress
    /// details. The parameter cannot be <see langword="null"/>.</param>
    public void RegisterExportProgressCallback(Action<string> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_exportCallbackLock)
        {
            _exportProgressCallbacks.Add(callback);

            // Start polling if needed
            if (_exportPollingCts?.IsCancellationRequested != false)
            {
                _exportPollingCts = new CancellationTokenSource();

                // Store the Task so DisposeAsync can await clean shutdown
                _exportPollingTask = StartExportStatusPollingAsync(_exportPollingCts.Token);
            }
        }
    }

    /// <summary>
    /// Unregisters a previously registered callback for export progress updates.
    /// </summary>
    /// <remarks>If this is the last registered callback, the export status polling process will be
    /// stopped.</remarks>
    /// <param name="callback">The callback to unregister. If the specified callback is <see langword="null"/> or was not previously
    /// registered, the method has no effect.</param>
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
                    _exportPollingCts?.Cancel();
                    _exportPollingCts?.Dispose();
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError("Failed to stop export status polling", ex);
                }
                finally
                {
                    _exportPollingCts = null;
                    _lastExportStatus = null;
                }
            }
        }
    }

    /// <summary>
    /// Starts a polling task that periodically queries the web page for the export status and notifies registered
    /// callbacks of any changes.
    /// </summary>
    /// <remarks>This method runs on the UI thread and uses <see cref="ExecuteScriptAsync"/> to query the
    /// JavaScript variable <c>globalThis.__pngExportStatus__</c> on the page. It forwards updates to registered
    /// callbacks only when the status changes, minimizing unnecessary processing. The polling interval is 200
    /// milliseconds by default. The task continues until the provided <see cref="CancellationToken"/> is
    /// canceled.</remarks>
    /// <param name="token">A <see cref="CancellationToken"/> used to cancel the polling task. The task will stop gracefully when
    /// cancellation is requested.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation of the polling task.</returns>
    private async Task StartExportStatusPollingAsync(CancellationToken token)
    {
        SimpleLogger.Log("Starting export status polling");
        try
        {
            const int pollingIntervalMs = 200;
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

                            foreach (Action<string> callback in callbacks)
                            {
                                try
                                {
                                    callback(statusJson);
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
                    SimpleLogger.LogError("Export status polling script error", ex);
                }

                try
                {
                    await Task.Delay(pollingIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            SimpleLogger.Log("Export status polling stopped");
        }
    }

    /// <summary>
    /// Asynchronously releases the resources used by the current instance.
    /// </summary>
    /// <remarks>This method performs a clean shutdown of internal components, including canceling ongoing
    /// operations, stopping background tasks, and releasing unmanaged resources.
    /// It ensures that all asynchronous operations are awaited and disposed of
    /// properly to prevent resource leaks. Exceptions encountered during
    /// disposal are logged but do not propagate.</remarks>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Stop and dispose WebView ready timer
            if (_webViewReadyTimer is not null)
            {
                _webViewReadyTimer.Stop();
                _webViewReadyTimer.Tick -= OnWebViewReadyTimerTick;
                _webViewReadyTimer = null;
            }

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

            // Stop and cleanup export polling if running
            try
            {
                // Cancel polling
                if (_exportPollingCts is not null)
                {
                    try
                    {
                        await _exportPollingCts.CancelAsync()
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError("Failed to cancel export polling CTS", ex);
                    }
                }

                // Await polling task for a short timeout to ensure clean shutdown
                if (_exportPollingTask is not null)
                {
                    try
                    {
                        await _exportPollingTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Log but do not rethrow during dispose
                        SimpleLogger.LogError("Export polling did not stop cleanly", ex);
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
                    _exportPollingCts?.Dispose();
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError("Failed to dispose export polling CTS", ex);
                }
                _exportPollingCts = null;
                _exportPollingTask = null;
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
