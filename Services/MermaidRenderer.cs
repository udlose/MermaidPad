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
using MermaidPad.Exceptions.Assets;
using MermaidPad.Extensions;
using MermaidPad.Services.Export;
using MermaidPad.Services.Platforms;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security;
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
    private readonly ILogger<MermaidRenderer> _logger;
    private readonly AssetService _assetService;

    private readonly string MermaidRequestPath;
    private readonly string IndexRequestPath;
    private readonly string JsYamlRequestPath;
    private readonly string MermaidLayoutElkRequestPath;
    private readonly string MermaidLayoutElkChunkSP2CHFBERequestPath;
    private readonly string MermaidLayoutElkRenderAVRWSH4DRequestPath;

    private int _isDisposeStarted; // 0 = not started, 1 = disposing/disposed
    private WebView? _webView;
    private int _serverPort;
    private Task? _serverTask;
    private bool _isWebViewReady;
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in DisposeAsync using captured reference")]
    private HttpListener? _httpListener;

    private readonly SemaphoreSlim _serverReadySemaphore = new SemaphoreSlim(0, 1);

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in DisposeAsync using captured reference")]
    private CancellationTokenSource? _serverCancellation;

    // Fields for centralized export-status callbacks / polling:
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in DisposeAsync using captured reference")]
    private CancellationTokenSource? _exportPollingCts;
    private readonly List<Action<string>> _exportProgressCallbacks = [];
    private Task? _exportPollingTask;
    private string? _lastExportStatus;
    private readonly Lock _exportCallbackLock = new Lock();
    private static readonly TimeSpan CancelAsyncTimeout = TimeSpan.FromMilliseconds(225);
    private static readonly TimeSpan PollingTaskCancellationTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidRenderer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="assetService">The asset service for managing assets.</param>
    public MermaidRenderer(ILogger<MermaidRenderer> logger, AssetService assetService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));

        // Initialize request paths using AssetService constants and properties
        MermaidRequestPath = $"/{AssetService.MermaidMinJsFilePath}";
        IndexRequestPath = $"/{AssetService.IndexHtmlFilePath}";
        JsYamlRequestPath = $"/{AssetService.JsYamlFilePath}";
        MermaidLayoutElkRequestPath = $"/{AssetService.MermaidLayoutElkPath}".Replace(Path.DirectorySeparatorChar, '/');
        MermaidLayoutElkChunkSP2CHFBERequestPath = $"/{AssetService.MermaidLayoutElkChunkSP2CHFBEPath}".Replace(Path.DirectorySeparatorChar, '/');
        MermaidLayoutElkRenderAVRWSH4DRequestPath = $"/{AssetService.MermaidLayoutElkRenderAVRWSH4DPath}".Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Initializes the MermaidRenderer with the specified WebView and assets directory.
    /// </summary>
    /// <param name="webView">The WebView to render Mermaid diagrams in.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task InitializeAsync(WebView webView)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(webView);

        _logger.LogInformation("=== MermaidRenderer Initialization ===");
        _webView = webView;
        return InitializeCoreAsync();
    }

    /// <summary>
    /// Initializes the core components of the MermaidRenderer asynchronously.
    /// </summary>
    /// <remarks>This method prepares the necessary content, starts the HTTP server, and performs navigation.
    /// It must be called on the UI thread as the caller expects to continue execution on the same thread.</remarks>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    private async Task InitializeCoreAsync()
    {
        // Prepare content, start HTTP server, navigate
        await InitializeWithHttpServerAsync(); // NO ConfigureAwait: caller expects to continue on UI thread
    }

    /// <summary>
    /// Ensures that the WebView is fully initialized and ready for its first render within the specified timeout
    /// period.
    /// </summary>
    /// <param name="timeout">The maximum amount of time to wait for the WebView to become ready. Must be greater than <see
    /// cref="TimeSpan.Zero"/>.</param>
    /// <returns>A task that completes when the WebView is ready for its first render. If the WebView is already ready, the task
    /// completes immediately.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the WebView is not initialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is less than or equal to <see cref="TimeSpan.Zero"/>.</exception>
    public Task EnsureFirstRenderReadyAsync(TimeSpan timeout)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (_webView is null)
        {
            throw new InvalidOperationException("WebView not initialized");
        }

        return _isWebViewReady ? Task.CompletedTask : EnsureFirstRenderReadyCoreAsync(timeout);
    }

    /// <summary>
    /// Ensures that the first render of the WebView2 control is complete within the specified timeout period.
    /// </summary>
    /// <remarks>This method repeatedly checks the rendering status of the WebView2 control by executing a
    /// script in the WebView2 environment using exponential backoff for polling intervals.
    /// This reduces WebView overhead while staying responsive. If the rendering is not completed within the specified timeout,
    /// a <see cref="TimeoutException"/> is thrown.</remarks>
    /// <param name="timeout">The maximum amount of time to wait for the first render to complete. Must be a positive <see cref="TimeSpan"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">Thrown if the first render is not signaled within the specified timeout period.</exception>
    private async Task EnsureFirstRenderReadyCoreAsync(TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int attemptCount = 0; stopwatch.Elapsed < timeout; attemptCount++)
        {
            // Return only a primitive, never a Promise. WebView2 ExecuteScriptAsync doesn't support Promises.
            // WebView2 will JSON-encode strings (e.g., "\"true\"" or "\"pending\"")
            const string script = "globalThis.__renderingComplete__ === true ? 'true' : 'pending'";
            string? rawReturnValue = await ExecuteScriptAsync(script);
            if (!string.IsNullOrWhiteSpace(rawReturnValue))
            {
                string trimmedReturnValue = rawReturnValue.Trim().Trim('"');
                if (string.Equals(trimmedReturnValue, "true", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_isWebViewReady)
                    {
                        _isWebViewReady = true;
                    }
                    return;
                }
            }

            // Reduces WebView script execution overhead while staying responsive for fast renders
            int delayMs = 50 * (int)Math.Pow(2, Math.Min(attemptCount, 3));
            await Task.Delay(delayMs);
        }

        throw new TimeoutException($"First render not signaled within {timeout.TotalSeconds:0.##} seconds.");
    }

    /// <summary>
    /// Initializes the application by starting an HTTP server and navigating to it.
    /// </summary>
    /// <remarks>This method performs the following steps: 1. Starts an HTTP server to serve assets from disk on-demand.
    /// 2. Waits for the HTTP server to be ready, with a timeout of 10 seconds. 3. Navigates to the HTTP server once it is ready.
    /// If the HTTP server fails to start within the timeout period, a <see cref="TimeoutException"/> is thrown.
    /// Assets are loaded from disk on first HTTP request to minimize memory usage.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">Thrown if the HTTP server does not become ready within the 10-second timeout period.</exception>
    private async Task InitializeWithHttpServerAsync()
    {
        // Step 1: Start HTTP server (assets will be loaded from disk on-demand)
        StartHttpServer();

        // Step 2: Wait for server ready
        _logger.LogInformation("Waiting for HTTP server to be ready...");
        bool serverReady = await _serverReadySemaphore.WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        if (!serverReady)
        {
            throw new TimeoutException("HTTP server failed to start within timeout");
        }

        // Step 3: Navigate to server
        await NavigateToServerAsync();  // Navigation needs UI context, so no ConfigureAwait(false) here
    }

    /// <summary>
    /// Starts an HTTP server on an available port and begins listening for incoming requests.
    /// </summary>
    /// <remarks>This method initializes an <see cref="HttpListener"/> instance, binds it to an available port
    /// on localhost, and starts a background task to handle incoming HTTP requests asynchronously. The server runs
    /// until explicitly stopped. Implements retry logic to handle the rare race condition where the OS-assigned port
    /// becomes unavailable between assignment and binding.</remarks>
    private void StartHttpServer()
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            HttpListener? transientListener = null;
            try
            {
                _serverPort = GetAvailablePort(); // OS assigns the port

                transientListener = new HttpListener();
                transientListener.Prefixes.Add($"http://localhost:{_serverPort}/");
                transientListener.Start();

                // Success - capture to field only after Start() succeeds
                _httpListener = transientListener;
                _serverCancellation = new CancellationTokenSource();

                // Background task - _serverTask is needed for proper cleanup
                _serverTask = Task.Run(async () =>
                {
                    try
                    {
                        await HandleHttpRequestsAsync(_serverCancellation.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "HTTP server task error");
                    }
                });

                _logger.LogInformation("HTTP server started on port {ServerPort}", _serverPort);
                return;
            }
            catch (HttpListenerException ex)
            {
                // Port was grabbed by another process between GetAvailablePort() and Start()
                // Clean up the failed listener
                try
                {
                    transientListener?.Close();
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Exception during best effort cleanup of transientListener");
                }

                if (attempt == maxRetries - 1)
                {
                    throw new InvalidOperationException($"Failed to start HTTP server after {maxRetries} attempts.", ex);
                }

                _logger.LogWarning("Port {ServerPort} became unavailable, retrying... (attempt {Attempt}/{MaxRetries})",
                    _serverPort, attempt + 1, maxRetries);
            }
        }
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
            _logger.LogInformation("HTTP server ready");

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
                    _logger.LogError(ex, "Error processing HTTP request");
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
            _logger.LogError(ex, "HTTP server error");
        }
        finally
        {
            _logger.LogInformation("HTTP request handler stopped");
        }
    }

    /// <summary>
    /// Processes a single HTTP request and streams the requested asset from disk directly to the response.
    /// </summary>
    /// <remarks>
    /// Assets are streamed directly from disk to the HTTP response without loading the entire file into memory.
    /// This approach:
    /// <list type="bullet">
    ///     <item><description>Minimizes memory usage - files are never fully loaded into managed memory</description></item>
    ///     <item><description>Reduces GC pressure - no large byte[] allocations</description></item>
    ///     <item><description>Improves performance for serving static assets over HTTP</description></item>
    /// </list>
    /// Each request opens a new FileStream, streams the content, and immediately disposes the stream.
    /// </remarks>
    /// <param name="context">The HTTP request context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            const string javascriptContentType = "application/javascript; charset=utf-8";
            const string htmlContentType = "text/html; charset=utf-8";

            string requestPath = context.Request.Url?.LocalPath ?? "/";
            _logger.LogDebug("Processing request: {RequestPath}", SanitizeRequestPath(requestPath));

            // Map request path to asset name and content type
            string? assetName = null;
            string? contentType = null;

            if (string.Equals(requestPath, MermaidRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                assetName = AssetService.MermaidMinJsFilePath;
                contentType = javascriptContentType;
            }
            else if (string.Equals(requestPath, "/", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(requestPath, IndexRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                assetName = AssetService.IndexHtmlFilePath;
                contentType = htmlContentType;
            }
            else if (string.Equals(requestPath, JsYamlRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                assetName = AssetService.JsYamlFilePath;
                contentType = javascriptContentType;
            }
            else if (string.Equals(requestPath, MermaidLayoutElkRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                assetName = AssetService.MermaidLayoutElkPath;
                contentType = javascriptContentType;
            }
            else if (string.Equals(requestPath, MermaidLayoutElkChunkSP2CHFBERequestPath, StringComparison.OrdinalIgnoreCase))
            {
                assetName = AssetService.MermaidLayoutElkChunkSP2CHFBEPath;
                contentType = javascriptContentType;
            }
            else if (string.Equals(requestPath, MermaidLayoutElkRenderAVRWSH4DRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                assetName = AssetService.MermaidLayoutElkRenderAVRWSH4DPath;
                contentType = javascriptContentType;
            }

            // Stream asset from disk directly to response (no intermediate memory allocation)
            if (assetName is not null && contentType is not null)
            {
                try
                {
                    await using FileStream assetStream = await _assetService.GetAssetStreamAsync(assetName)
                        .ConfigureAwait(false);

                    // Set response headers before streaming
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = assetStream.Length;

                    // Stream file directly to HTTP response without loading into memory
                    await assetStream.CopyToAsync(context.Response.OutputStream)
                        .ConfigureAwait(false);

                    _logger.LogDebug("Streamed {RequestPath}: {SizeBytes} bytes", SanitizeRequestPath(requestPath), assetStream.Length);
                }
                catch (AssetIntegrityException aie)
                {
                    context.Response.StatusCode = 403;
                    _logger.LogError(aie, "403 for asset integrity failure: {AssetName}", assetName);
                }
                catch (SecurityException se)
                {
                    context.Response.StatusCode = 403;
                    _logger.LogError(se, "403 for security failure: {AssetName}", assetName);
                }
                catch (MissingAssetException mae)
                {
                    context.Response.StatusCode = 404;
                    _logger.LogError(mae, "404 for missing asset: {AssetName}", assetName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error streaming asset {AssetName}", assetName);
                    context.Response.StatusCode = 500;
                }
            }
            else
            {
                context.Response.StatusCode = 404;
                _logger.LogDebug("404 for: {RequestPath}", SanitizeRequestPath(requestPath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HTTP request");

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
    /// complete using a <see cref="TaskCompletionSource{TResult}"/> to convert the event-based pattern to
    /// async/await. If the navigation does not complete within 10 seconds, an <see cref="InvalidOperationException"/>
    /// is thrown. The method must be called on the UI thread as it interacts with the web view and dispatcher.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the WebView is not initialized or navigation times out.</exception>
    private async Task NavigateToServerAsync()
    {
        if (_webView is null)
        {
            throw new InvalidOperationException("WebView not initialized");
        }

        string serverUrl = $"http://localhost:{_serverPort}/";
        _logger.LogInformation("Navigating to: {ServerUrl}", serverUrl);

        TaskCompletionSource<bool> navigationCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _webView.NavigationCompleted += OnNavigationCompleted;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => _webView.Url = new Uri(serverUrl));

            // Wait for navigation with 10 second timeout (more generous for slower systems)
#if DEBUG
            // no timeout in DEBUG builds to aid debugging
            using CancellationTokenSource cts = new CancellationTokenSource();
#else
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
#endif
            await navigationCompletedTcs.Task.WaitAsync(cts.Token);

            _logger.LogInformation("Navigation completed successfully");
        }
        catch (OperationCanceledException)
        {
            // Cleanup event handler if timeout occurs
            try
            {
                _webView.NavigationCompleted -= OnNavigationCompleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup NavigationCompleted handler after timeout");
            }

            throw new InvalidOperationException($"Navigation to {serverUrl} timed out after 10 seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed");

            // Cleanup on any other error
            try
            {
                _webView.NavigationCompleted -= OnNavigationCompleted;
            }
            catch
            {
                // Ignore cleanup errors during error handling
            }

            throw;
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
        void OnNavigationCompleted(object? sender, WebViewCore.Events.WebViewUrlLoadedEventArg args)
        {
            try
            {
                navigationCompletedTcs.TrySetResult(true);
                _logger.LogWebView("navigation completed", "HTTP mode");
            }
            finally
            {
                try
                {
                    _webView.NavigationCompleted -= OnNavigationCompleted;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unsubscribe from NavigationCompleted");
                }
            }
        }
    }

    /// <summary>
    /// Gets an available port by letting the OS assign one automatically using TcpListener.
    /// </summary>
    /// <remarks>This method uses TcpListener with port 0 to let the operating system assign an available
    /// ephemeral port, which is faster and more reliable than sequential scanning. The assigned port is extracted
    /// from the listener's LocalEndpoint after binding, then the listener is stopped and the port is returned.</remarks>
    /// <returns>An available port number assigned by the OS.</returns>
    /// <exception cref="InvalidOperationException">Thrown if unable to get an OS-assigned port.</exception>
    private int GetAvailablePort()
    {
        try
        {
            // Use TcpListener with port 0 to let the OS assign an available port
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            // Get the assigned port from the local endpoint
            int assignedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            _logger.LogDebug("OS assigned port: {AssignedPort}", assignedPort);
            return assignedPort;
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("Failed to get OS-assigned port due to a socket error.", ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw new InvalidOperationException("Failed to get OS-assigned port because the listener was disposed.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to get OS-assigned port due to an unexpected error.", ex);
        }
    }

    /// <summary>
    /// Renders a Mermaid diagram in the associated WebView control using the provided Mermaid source code.
    /// </summary>
    /// <remarks>This method executes JavaScript in the WebView to render the Mermaid diagram. The source code
    /// is escaped to ensure compatibility with JavaScript string literals. If the WebView is not initialized, the
    /// method logs an error and exits without performing any action.  This method must be called on the UI thread, as
    /// it interacts with the WebView control. Exceptions during rendering or clearing are logged but do not propagate
    /// to the caller.</remarks>
    /// <param name="mermaidSource">The Mermaid source code to render. If the source is null, empty,
    /// or consists only of whitespace, the output in the WebView will be cleared instead.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RenderAsync(string mermaidSource)
    {
        ThrowIfDisposed();
        if (_webView is null)
        {
            _logger.LogError("WebView not initialized");
            return;
        }

        if (string.IsNullOrWhiteSpace(mermaidSource))
        {
            try
            {
                // Copy locals to explicit locals to make intent clear and avoid captures
                WebView webView = _webView;

                // Explicit call (lambda still captures webView local, but the static local function prevents
                // implicit capture of surrounding variables inside the function body).
                await Dispatcher.UIThread.InvokeAsync(() => ClearOutputAsync(webView));

                _logger.LogDebug("Cleared output");

                // Static local function to avoid capturing outer variables inside the function
                static Task ClearOutputAsync(WebView webViewParam) => webViewParam.ExecuteScriptAsync("clearOutput();");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clear failed");
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

                // Don't pollute the log. A Render happens every time an un-debounced key
                // in the TextEditor causes the WebView to need to render itself
                Debug.WriteLine($"Render result: {result ?? "null"}");
            }

            await Dispatcher.UIThread.InvokeAsync(RenderMermaidAsync);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Render failed");
        }

        static string EscapeSource(string source)
        {
            ReadOnlySpan<char> sourceSpan = source.AsSpan();
            StringBuilder sb = new StringBuilder(sourceSpan.Length);
            foreach (char c in sourceSpan)
            {
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
    /// <exception cref="ArgumentException">Thrown if <paramref name="script"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the WebView is not initialized.</exception>
    public Task<string?> ExecuteScriptAsync(string script)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(script);
        if (_webView is null)
        {
            throw new InvalidOperationException("WebView not initialized");
        }

        return ExecuteScriptCoreAsync(script);
    }

    /// <summary>
    /// Executes the specified JavaScript code asynchronously within the context of the web view.
    /// </summary>
    /// <remarks>This method invokes the script execution on the UI thread. If an exception occurs during
    /// execution, it is logged, and the method returns <see langword="null"/>.</remarks>
    /// <param name="script">The JavaScript code to execute. Cannot be <see langword="null"/> or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the script execution
    /// as a string, or <see langword="null"/> if the execution fails.</returns>
    private async Task<string?> ExecuteScriptCoreAsync(string script)
    {
        try
        {
            // Capture snapshot of the field to avoid a race between the caller's check and this call.
            WebView? webView = _webView;

            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Re-check on the UI thread to be defensive - use the captured reference if available,
                // otherwise read the field on the UI thread.
                webView ??= _webView;
                if (webView is null)
                {
                    _logger.LogWarning("ExecuteScriptCoreAsync: WebView is null at UI invocation");
                    return null;
                }

                string? result = await webView.ExecuteScriptAsync(script);
                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed");
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
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="callback"/> is <see langword="null"/>.</exception>
    public void RegisterExportProgressCallback(Action<string> callback)
    {
        ThrowIfDisposed();
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
    /// Asynchronously unregisters a previously registered export progress callback, stopping export status
    /// notifications for the specified callback.
    /// </summary>
    /// <remarks>If this is the last registered callback, export polling will be stopped and related resources
    /// will be disposed. This method is thread-safe and may be called concurrently with other registration or
    /// unregister operations.</remarks>
    /// <param name="callback">The callback delegate to remove from export progress notifications. If <paramref name="callback"/> is <see
    /// langword="null"/>, the method performs no action.</param>
    /// <returns>A task that represents the asynchronous unregister operation.</returns>
    [SuppressMessage("ReSharper", "MethodSupportsCancellation", Justification = "Cancellation is not needed here and introduces unnecessary complexity")]
    public async Task UnregisterExportProgressCallbackAsync(Action<string>? callback)
    {
        ThrowIfDisposed();
        if (callback is null)
        {
            return;
        }

        CancellationTokenSource? ctsToDispose = null;
        Task? pollingTaskToAwait = null;
        lock (_exportCallbackLock)
        {
            _exportProgressCallbacks.Remove(callback);

            if (_exportProgressCallbacks.Count == 0)
            {
                ctsToDispose = _exportPollingCts;
                pollingTaskToAwait = _exportPollingTask;
                _exportPollingCts = null;
                _exportPollingTask = null;
                _lastExportStatus = null;
            }
        }

        // Stop polling and dispose outside lock
        if (ctsToDispose is not null)
        {
            try
            {
                // Prefer CancelAsync with a bounded wait
                try
                {
                    Task cancelTask = ctsToDispose.CancelAsync();
                    try
                    {
                        await cancelTask.WaitAsync(CancelAsyncTimeout)
                            .ConfigureAwait(false);
                    }
                    catch (TimeoutException te)
                    {
                        _logger.LogWarning(te, "exportPollingCts.CancelAsync() timed out; proceeding with unregister");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to signal export polling CancellationTokenSource with CancelAsync");
                }

                // Wait for polling task to end (short timeout). If it completes, dispose the Task to match server-task handling.
                if (pollingTaskToAwait is not null)
                {
                    try
                    {
                        await pollingTaskToAwait.WaitAsync(PollingTaskCancellationTimeout)
                            .ConfigureAwait(false);

                        // Completed in time
                        try
                        {
                            pollingTaskToAwait.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            _logger.LogError(disposeEx, "Failed to dispose polling task after completion");
                        }
                    }
                    catch (TimeoutException te)
                    {
                        _logger.LogWarning(te, "Export polling task timed out during unregister");
                    }
#pragma warning disable S6667 // Logging in this specific catch intentionally omits the exception
                    catch (OperationCanceledException)
                    {
                        // Expected during cancellation; logging without exception object is intentional and clearer here.
                        _logger.LogDebug("Export polling task was canceled during unregister");
                    }
#pragma warning restore S6667
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Export polling did not stop cleanly during unregister");
                    }
                }
            }
            finally
            {
                try
                {
                    ctsToDispose.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispose export polling CancellationTokenSource during unregister");
                }
            }
        }
    }

    /// <summary>
    /// Starts a polling task that periodically queries the web page for the export status and notifies registered
    /// callbacks of any changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses polling rather than event-based callbacks because of WebView bridge limitations.
    /// The JavaScript export function in the browser sets global variables (<c>globalThis.__pngExportStatus__</c>)
    /// that can only be read via <see cref="ExecuteScriptAsync"/>. There is no JavaScript-to-C# callback mechanism
    /// available in AvaloniaWebView that would allow the browser to push notifications directly to C#.
    /// </para>
    /// <para>
    /// The polling pattern used here is appropriate for cross-boundary communication (JavaScript from/to C#) where:
    ///     <list type="bullet">
    ///         <item><description>The external system (JavaScript) only supports query-based access</description></item>
    ///         <item><description>No event/callback mechanism exists for the external system to notify C#</description></item>
    ///         <item><description>The overhead of polling (200ms intervals) is acceptable for user-facing export operations</description></item>
    ///     </list>
    /// </para>
    /// <para>
    /// Higher-level code converts this callback pattern to <see cref="Task"/>-based async using <see cref="TaskCompletionSource{TResult}"/>
    /// (see <see cref="ExportService.WaitForExportCompletionAsync"/>), providing the best of both patterns:
    /// polling where necessary at the boundary, and clean async/await for consumers.
    /// </para>
    /// <para>
    /// This method forwards updates to registered callbacks only when the status changes, minimizing unnecessary
    /// processing. The polling interval is 200 milliseconds by default. All access to shared state (<c>_lastExportStatus</c>)
    /// is synchronized via <c>_exportCallbackLock</c> to prevent race conditions.
    /// </para>
    /// </remarks>
    /// <param name="token">A <see cref="CancellationToken"/> used to cancel the polling task. The task will stop gracefully when
    /// cancellation is requested.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation of the polling task.</returns>
    private async Task StartExportStatusPollingAsync(CancellationToken token)
    {
        _logger.LogInformation("Starting export status polling");
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

                        // Check for changes and get callbacks atomically under lock
                        Action<string>[]? callbacksToInvoke = null;
                        lock (_exportCallbackLock)
                        {
                            // Forward only when it changes to reduce churn
                            if (statusJson != _lastExportStatus)
                            {
                                _lastExportStatus = statusJson;
                                callbacksToInvoke = _exportProgressCallbacks.ToArray();
                            }
                        }

                        // Invoke callbacks outside lock to prevent deadlock
                        InvokeExportProgressCallbacks(callbacksToInvoke, statusJson);
                    }
                }
                catch (Exception ex)
                {
                    // Ignore transient script errors but log for diagnostics
                    _logger.LogError(ex, "Export status polling script error");
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
            _logger.LogInformation("Export status polling stopped");
        }
    }

    /// <summary>
    /// Invokes each export progress callback in the specified array, passing the provided status information as a JSON string.
    /// </summary>
    /// <remarks>Exceptions thrown by individual callbacks are caught and logged; invocation continues for remaining callbacks.</remarks>
    /// <param name="callbacksToInvoke">An array of callback delegates to be invoked with the export progress status. If null or empty, no callbacks are
    /// invoked.</param>
    /// <param name="statusJson">A JSON-formatted string containing the current export progress status to be supplied to each callback.</param>
    private void InvokeExportProgressCallbacks(Action<string>[]? callbacksToInvoke, string statusJson)
    {
        if (callbacksToInvoke?.Length > 0)
        {
            foreach (Action<string> callback in callbacksToInvoke)
            {
                try
                {
                    callback(statusJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Export progress callback threw");
                }
            }
        }
    }

    /// <summary>
    /// Normalizes and sanitizes a request path string by removing control characters, trimming whitespace, converting
    /// backslashes to forward slashes, and collapsing consecutive slashes.
    /// </summary>
    /// <remarks>This method ensures the returned path is safe for use in routing or logging scenarios by
    /// removing potentially problematic characters and normalizing the format. The output will always begin with a
    /// forward slash, and all backslashes are replaced with forward slashes.</remarks>
    /// <param name="path">The request path to sanitize. May be null, empty, or contain whitespace, control characters, or backslashes.</param>
    /// <returns>A sanitized path string that starts with a single forward slash and contains no control characters or
    /// consecutive slashes. Returns "/" if the input is null, empty, or consists only of whitespace.</returns>
    private static string SanitizeRequestPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        // Trim surrounding whitespace first (fixes inputs like " /foo")
        string trimmed = path.Trim();

        // Normalize and scrub control characters efficiently:
        // - convert backslashes to forward slashes
        // - remove CR/LF, NUL, TAB and other control chars (< 0x20)
        // - collapse consecutive slashes to a single '/'
        StringBuilder sb = new StringBuilder(trimmed.Length);
        char prev = '\0';

        // Map backslashes to forward slashes in the sequence
        IEnumerable<char> normalizedChars = trimmed.Select(static ch0 => ch0 == '\\' ? '/' : ch0);
        foreach (char ch in normalizedChars)
        {
            if (ch <= '\u001F') // control characters (incl. \r, \n, \t, \0)
            {
                continue;
            }

            // collapse repeated slashes
            if (ch == '/' && prev == '/')
            {
                continue;
            }

            sb.Append(ch);
            prev = ch;
        }

        string result = sb.Length == 0 ? "/" : sb.ToString();

        // Ensure leading slash
        if (!result.StartsWith('/'))
        {
            result = "/" + result;
        }

        return result;
    }

    /// <summary>
    /// Throws an exception if the current instance has been disposed.
    /// </summary>
    /// <remarks>Call this method at the beginning of public members to ensure that operations are not
    /// performed on a disposed object. This helps prevent undefined behavior and enforces correct object lifecycle
    /// management.</remarks>
    private void ThrowIfDisposed(
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (Interlocked.CompareExchange(ref _isDisposeStarted, 0, 0) != 0)
        {
            const string unknown = "unknown";

            // Shorten file path for readability
            string fileName;
            if (callerFile is null)
            {
                fileName = unknown;
            }
            else
            {
                fileName = Path.GetFileName(callerFile) ?? unknown;
            }

            string callerInfo = caller is null ? $"(at {fileName}:{callerLine})" : $"{caller} (at {fileName}:{callerLine})";
            throw new ObjectDisposedException($"{nameof(MermaidRenderer)} instance has been disposed. Caller: {callerInfo}");
        }
    }

    /// <summary>
    /// Asynchronously releases all resources used by the instance and performs a clean shutdown of background
    /// operations.
    /// </summary>
    /// <remarks>This method ensures that any ongoing server and export polling tasks are cancelled and
    /// awaited with bounded timeouts to avoid blocking the calling thread. It is safe to call multiple times;
    /// subsequent calls after the first have no effect. This method should be called when the instance is no longer
    /// needed to ensure proper resource cleanup.</remarks>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    public async ValueTask DisposeAsync()
    {
        // One-shot, thread-safe gate
        if (Interlocked.Exchange(ref _isDisposeStarted, 1) != 0)
        {
            return;
        }

        try
        {
            // Capture and null references to avoid races with re-entrancy
            CancellationTokenSource? serverCts = Interlocked.Exchange(ref _serverCancellation, null);
            HttpListener? httpListener = Interlocked.Exchange(ref _httpListener, null);
            CancellationTokenSource? pollingCts = Interlocked.Exchange(ref _exportPollingCts, null);
            Task? pollingTask = Interlocked.Exchange(ref _exportPollingTask, null);
            Task? serverTask = Interlocked.Exchange(ref _serverTask, null);

            // Signal server cancellation using CancelAsync with a short bounded wait to avoid UI deadlock.
            if (serverCts is not null)
            {
                try
                {
                    Task cancelTask = serverCts.CancelAsync();
                    try
                    {
                        await cancelTask.WaitAsync(CancelAsyncTimeout)
                            .ConfigureAwait(false);
                    }
                    catch (TimeoutException te)
                    {
                        _logger.LogWarning(te, "serverCts.CancelAsync() timed out; continuing disposal to avoid blocking UI thread");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to signal server CancellationTokenSource with CancelAsync");
                }
            }

            // Stop and cleanup export polling if running
            try
            {
                // Signal polling cancellation using CancelAsync with a bounded wait
                if (pollingCts is not null)
                {
                    try
                    {
                        Task cancelTask = pollingCts.CancelAsync();
                        try
                        {
                            await cancelTask.WaitAsync(CancelAsyncTimeout)
                                .ConfigureAwait(false);
                        }
                        catch (TimeoutException te)
                        {
                            _logger.LogWarning(te, "pollingCts.CancelAsync() timed out; proceeding with disposal");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to signal export polling CancellationTokenSource with CancelAsync");
                    }
                }

                // Await polling task for a short timeout to ensure clean shutdown
                if (pollingTask is not null)
                {
                    try
                    {
                        await pollingTask.WaitAsync(PollingTaskCancellationTimeout)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Log but do not rethrow during dispose
                        _logger.LogError(ex, "Export polling did not stop cleanly");
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
                    pollingCts?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispose export polling CancellationTokenSource");
                }
            }

            // Wait for server task to complete (with timeout)
            if (serverTask is not null)
            {
                try
                {
                    const int maxWaitSeconds = 5;
                    await serverTask.WaitAsync(TimeSpan.FromSeconds(maxWaitSeconds))
                        .ConfigureAwait(false);

                    // server task finished in time - dispose task and CTS safely
                    serverTask.Dispose();
                    try
                    {
                        serverCts?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing server CancellationTokenSource");
                    }
                }
                catch (TimeoutException te)
                {
                    _logger.LogError(te, "Server task did not complete within timeout - will dispose when background completion finishes");

                    // NOTE: serverCts was detached from the instance field earlier using
                    // Interlocked.Exchange(ref _serverCancellation, null). That means the local
                    // `serverCts` reference is no longer reachable via the field and cannot
                    // be mutated by other threads; capturing it here for deferred disposal is
                    // therefore safe. We still defer actual disposal until the server task
                    // completes so that any cancellation callbacks can finish without racing
                    // with Dispose() on the CTS.
                    ILogger<MermaidRenderer> logger = _logger; // Capture for continuation
                    CancellationTokenSource? ctsToDispose = serverCts;

                    _ = serverTask.ContinueWith(
                        t =>
                        {
                            try
                            {
                                if (t.IsFaulted)
                                {
                                    // Exception already observed and logged by StartHttpServer's try-catch
                                    logger.LogInformation("Abandoned server task faulted (exception already logged in {MethodName})", nameof(StartHttpServer));
                                }
                                else if (t.IsCanceled)
                                {
                                    logger.LogInformation("Abandoned server task was canceled");
                                }
                                else
                                {
                                    logger.LogInformation("Abandoned server task completed successfully");
                                }

                                // Dispose CTS only after server task completion to avoid races with cancellation callbacks
                                try
                                {
                                    ctsToDispose?.Dispose();
                                }
                                catch (Exception disposeEx)
                                {
                                    logger.LogError(disposeEx, "Error disposing server CancellationTokenSource in abandoned continuation");
                                }

                                // Dispose the task
                                t.Dispose();
                                logger.LogInformation("Abandoned server task completed and disposed");
                            }
                            catch (Exception ex)
                            {
                                // This catch ensures the continuation itself doesn't have unobserved exceptions
                                logger.LogError(ex, "Error in abandoned server task continuation");
                            }
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    // Task threw exception, but we observed it
                    _logger.LogError(ex, "Error waiting for server task");
                    try
                    {
                        serverTask.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        _logger.LogError(disposeEx, "Error disposing server task");
                    }

                    // Safe to dispose CTS here because serverTask has completed (with error)
                    try
                    {
                        serverCts?.Dispose();
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError(ex2, "Error disposing server CancellationTokenSource");
                    }
                }
            }
            else
            {
                // No server task running: safe to dispose CTS immediately
                try
                {
                    serverCts?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispose server CancellationTokenSource");
                }
            }

            try
            {
                // Try to stop and close HTTP listener. It's possible the listener was already closed.
                // So, we catch ObjectDisposedException and ignore it.
                if (httpListener?.IsListening == true)
                {
                    httpListener.Stop();
                }
                httpListener?.Close();
            }
            catch (ObjectDisposedException)
            {
                // Ignore - already disposed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping/closing HttpListener");
            }
            finally
            {
                _serverReadySemaphore.Dispose();
            }

            _logger.LogInformation("MermaidRenderer disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disposal");
        }
    }
}
