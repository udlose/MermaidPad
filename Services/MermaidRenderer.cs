using Avalonia.Threading;
using AvaloniaWebView;
using MermaidPad.Services.Platforms;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace MermaidPad.Services;

/// <summary>
/// Provides rendering of Mermaid diagrams using a local HTTP server and Avalonia WebView.
/// Serves separate HTML and JS files to avoid JavaScript injection issues.
/// </summary>
public sealed class MermaidRenderer : IAsyncDisposable
{
    private const string MermaidRequestPath = $"/{AssetHelper.MermaidMinJsFileName}";
    private const string IndexRequestPath = $"/{AssetHelper.IndexHtmlFileName}";
    private const string JsYamlRequestPath = $"/{AssetHelper.JsYamlFileName}";
    private WebView? _webView;
    private int _renderAttemptCount;
    private HttpListener? _httpListener;
    private byte[]? _htmlContent;
    private byte[]? _mermaidJs;
    private byte[]? _jsYamlJs;
    private int _serverPort;
    private readonly SemaphoreSlim _serverReadySemaphore = new SemaphoreSlim(0, 1);
    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;

    /// <summary>
    /// Initializes the MermaidRenderer with the specified WebView and assets directory.
    /// </summary>
    /// <param name="webView">The WebView to render Mermaid diagrams in.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync(WebView webView)
    {
        SimpleLogger.Log("=== MermaidRenderer Initialization ===");
        _webView = webView;

        await InitializeWithHttpServerAsync();
    }

    /// <summary>
    /// Prepares content and starts the local HTTP server for serving Mermaid assets.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task InitializeWithHttpServerAsync()
    {
        // Step 1: Prepare content (HTML and JS separately)
        await PrepareContentFromDiskAsync();

        // Step 2: Start HTTP server
        StartHttpServer();

        // Step 3: Wait for server ready
        SimpleLogger.Log("Waiting for HTTP server to be ready...");
        bool serverReady = await _serverReadySemaphore.WaitAsync(TimeSpan.FromSeconds(10));
        if (!serverReady)
        {
            throw new TimeoutException("HTTP server failed to start within timeout");
        }

        // Step 4: Navigate to server
        await NavigateToServerAsync();
    }

    /// <summary>
    /// Reads the required HTML and JS asset files from disk.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PrepareContentFromDiskAsync()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Get assets in parallel
            Task<byte[]> indexHtmlTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.IndexHtmlFileName);
            Task<byte[]> jsTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.MermaidMinJsFileName);
            Task<byte[]> jsYamlTask = AssetHelper.GetAssetFromDiskAsync(AssetHelper.JsYamlFileName);
            await Task.WhenAll(indexHtmlTask, jsTask, jsYamlTask);
            _htmlContent = await indexHtmlTask;
            _mermaidJs = await jsTask;
            _jsYamlJs = await jsYamlTask;
        }
        catch (Exception e)
        {
            SimpleLogger.LogError("Error reading asset files", e);
            return;
        }

        sw.Stop();
        SimpleLogger.Log($"Prepared HTML: {_htmlContent.Length} characters");
        SimpleLogger.Log($"Prepared JS: {_mermaidJs.Length} characters");

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
                    context = await _httpListener.GetContextAsync().WaitAsync(cancellationToken);
                    await ProcessRequestAsync(context);
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
            switch (requestPath)
            {
                case MermaidRequestPath when _mermaidJs is not null:
                    {
                        responseBytes = _mermaidJs;
                        contentType = "application/javascript; charset=utf-8";
                        break;
                    }
                case MermaidRequestPath:
                    {
                        context.Response.StatusCode = 404;
                        break;
                    }
                case "/" or IndexRequestPath:
                    {
                        responseBytes = _htmlContent ?? "<html><body>Content not ready</body></html>"u8.ToArray();
                        contentType = "text/html; charset=utf-8";
                        break;
                    }
                case JsYamlRequestPath when _jsYamlJs is not null:
                    {
                        responseBytes = _jsYamlJs;
                        contentType = "application/javascript; charset=utf-8";
                        break;
                    }
                default:
                    {
                        context.Response.StatusCode = 404;
                        SimpleLogger.Log($"404 for: {requestPath}");
                        break;
                    }
            }

            if (responseBytes?.Length > 0 && !string.IsNullOrWhiteSpace(contentType))
            {
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = responseBytes.Length;
                await context.Response.OutputStream.WriteAsync(responseBytes);
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
            await Task.Delay(100);
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

        // Simple JavaScript execution - no unnecessary complexity
        string escaped;
        if (!mermaidSource.AsSpan().Contains('\\') && !mermaidSource.AsSpan().Contains('`'))
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
                await _serverCancellation.CancelAsync();
            }

            // Stop and close HTTP listener
            if (_httpListener?.IsListening == true)
            {
                _httpListener.Stop();
            }
            _httpListener?.Close();

            // Wait for server task to complete (with timeout)
            if (_serverTask is not null)
            {
                try
                {
                    const int maxWaitSeconds = 5;
                    await _serverTask.WaitAsync(TimeSpan.FromSeconds(maxWaitSeconds));
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
