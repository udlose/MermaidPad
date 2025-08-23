using Avalonia.Threading;
using AvaloniaWebView;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace MermaidPad.Services;
/// <summary>
/// Clean HTTP server approach for WebView content.
/// Uses separate HTML and JS files to avoid JavaScript injection issues.
/// </summary>
public sealed class MermaidRenderer : IAsyncDisposable
{
    private const string MermaidMinJsFileName = "mermaid.min.js";
    private const string MermaidRequestPath = $"/{MermaidMinJsFileName}";
    private const string IndexHtmlFileName = "index.html";
    private const string IndexRequestPath = $"/{IndexHtmlFileName}";
    private WebView? _webView;
    private int _renderAttemptCount = 0;
    private HttpListener? _httpListener;
    private string? _htmlContent;
    private string? _mermaidJs;
    private int _serverPort = 0;
    private readonly SemaphoreSlim _serverReadySemaphore = new(0, 1);
    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;

    public async Task InitializeAsync(WebView webView, string assetsDir)
    {
        SimpleLogger.Log("=== MermaidRenderer Initialization ===");
        _webView = webView;

        await InitializeWithHttpServerAsync(assetsDir);
    }

    private async Task InitializeWithHttpServerAsync(string assetsDir)
    {
        // Step 1: Prepare content (HTML and JS separately)
        await PrepareContentAsync(assetsDir);

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

    private async Task PrepareContentAsync(string assetsDir)
    {
        string indexPath = Path.Combine(assetsDir, IndexHtmlFileName);
        string mermaidPath = Path.Combine(assetsDir, MermaidMinJsFileName);

        if (!File.Exists(indexPath) || !File.Exists(mermaidPath))
        {
            throw new FileNotFoundException("Required assets not found");
        }

        try
        {
            // Read files in parallel
            Task<string> indexHtmlTask = File.ReadAllTextAsync(indexPath);
            Task<string> jsTask = File.ReadAllTextAsync(mermaidPath);
            await Task.WhenAll(indexHtmlTask, jsTask);
            _htmlContent = await indexHtmlTask;
            _mermaidJs = await jsTask;
        }
        catch (Exception e)
        {
            SimpleLogger.LogError("Error reading asset files", e);
            return;
        }

        SimpleLogger.Log($"Prepared HTML: {_htmlContent.Length} characters");
        SimpleLogger.Log($"Prepared JS: {_mermaidJs.Length} characters");
    }

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

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            string requestPath = context.Request.Url?.LocalPath ?? "/";
            SimpleLogger.Log($"Processing request: {requestPath}");

            // Separate file handling is needed to avoid JavaScript injection issues
            switch (requestPath)
            {
                case MermaidRequestPath when _mermaidJs is not null:
                    {
                        // Serve mermaid.js efficiently
                        byte[] jsBytes = Encoding.UTF8.GetBytes(_mermaidJs);
                        context.Response.ContentType = "application/javascript; charset=utf-8";
                        context.Response.ContentLength64 = jsBytes.Length;
                        await context.Response.OutputStream.WriteAsync(jsBytes);
                        SimpleLogger.Log($"Served JS: {jsBytes.Length} bytes");
                        break;
                    }
                case MermaidRequestPath:
                    {
                        context.Response.StatusCode = 404;
                        break;
                    }
                case "/" or IndexRequestPath:
                    {
                        // Serve HTML efficiently
                        byte[] htmlBytes = Encoding.UTF8.GetBytes(_htmlContent ?? "<html><body>Content not ready</body></html>");
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = htmlBytes.Length;
                        await context.Response.OutputStream.WriteAsync(htmlBytes);
                        SimpleLogger.Log($"Served HTML: {htmlBytes.Length} bytes");
                        break;
                    }
                default:
                    {
                        context.Response.StatusCode = 404;
                        SimpleLogger.Log($"404 for: {requestPath}");
                        break;
                    }
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
        {
            ReadOnlySpan<char> sourceSpan = mermaidSource.AsSpan();
            StringBuilder sb = new StringBuilder(sourceSpan.Length);
            foreach (char c in sourceSpan)
            {
                if (c == '\\')
                {
                    sb.Append(@"\\");
                }
                else if (c == '`')
                {
                    sb.Append("\\`");
                }
                else
                {
                    sb.Append(c);
                }
            }
            escaped = sb.ToString();
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
    }

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
