using AvaloniaWebView;
using System.Net;
using System.Text;

namespace MermaidPad.Services;

/// <summary>
/// Production version that uses local HTTP server to bypass single-file file:// restrictions.
/// Based on diagnostic evidence showing WebView2 blocks file:// navigation in single-file apps.
/// </summary>
public sealed class MermaidRenderer : IDisposable
{
    private WebView? _webView;
    private int _renderAttemptCount = 0;
    private HttpListener? _httpListener;
    private string? _htmlContent;
    private int _serverPort = 0;
    private bool _useFileMode = false;

    public MermaidRenderer()
    {
        SimpleLogger.Log("MermaidRenderer initialized");
    }

    public async Task InitializeAsync(WebView webView, string assetsDir)
    {
        SimpleLogger.Log("=== MermaidRenderer Initialization ===");

        _webView = webView;

        // Detect if we're running in single-file mode by checking working directory
        bool isSingleFile = Environment.CurrentDirectory.Contains("win-x64") ||
                            Environment.CurrentDirectory.Contains("win-x86") ||
                            Environment.CurrentDirectory.Contains("win-arm64") ||
                            Environment.CurrentDirectory.Contains("linux-arm64") ||
                            Environment.CurrentDirectory.Contains("linux-x64") ||
                            Environment.CurrentDirectory.Contains("osx-arm64") ||
                            Environment.CurrentDirectory.Contains("osx-x64");

        SimpleLogger.Log($"Detected single-file mode: {isSingleFile}");

        if (isSingleFile)
        {
            SimpleLogger.Log("Using HTTP server approach for single-file compatibility");
            await InitializeWithHttpServerAsync(assetsDir);
        }
        else
        {
            SimpleLogger.Log("Using direct file approach for multi-file mode");
            await InitializeWithFileAsync(assetsDir);
        }
    }

    private async Task InitializeWithFileAsync(string assetsDir)
    {
        _useFileMode = true;
        string indexPath = Path.Combine(assetsDir, "index.html");

        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException($"Required asset not found: {indexPath}");
        }

        bool navigationCompleted = false;
        _webView!.NavigationCompleted += (s, e) =>
        {
            navigationCompleted = true;
            SimpleLogger.LogWebView("navigation completed", "File mode");
        };

        Uri indexUri = new Uri(indexPath);
        SimpleLogger.Log($"Navigating to file: {indexUri}");
        _webView.Url = indexUri;

        // Wait for navigation
        for (int i = 0; i < 50 && !navigationCompleted; i++)
        {
            await Task.Delay(100);
        }

        if (navigationCompleted)
        {
            SimpleLogger.Log("File navigation completed successfully");
        }
        else
        {
            throw new InvalidOperationException("File navigation failed");
        }
    }

    private async Task InitializeWithHttpServerAsync(string assetsDir)
    {
        _useFileMode = false;

        // Read and prepare content
        string indexPath = Path.Combine(assetsDir, "index.html");
        string mermaidPath = Path.Combine(assetsDir, "mermaid.min.js");

        if (!File.Exists(indexPath) || !File.Exists(mermaidPath))
        {
            throw new FileNotFoundException("Required assets not found");
        }

        string htmlTemplate = await File.ReadAllTextAsync(indexPath);
        string mermaidJs = await File.ReadAllTextAsync(mermaidPath);

        // Create self-contained HTML
        _htmlContent = htmlTemplate.Replace(
            "<script src=\"mermaid.min.js\" defer></script>",
            $"<script>{mermaidJs}</script>"
        );

        SimpleLogger.Log($"Prepared self-contained HTML: {_htmlContent.Length} characters");

        // Start HTTP server
        _serverPort = GetAvailablePort();
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{_serverPort}/");
        _httpListener.Start();

        SimpleLogger.Log($"HTTP server started on port {_serverPort}");

        // Handle requests in background
        _ = Task.Run(HandleHttpRequests);

        // Navigate WebView to HTTP server
        bool navigationCompleted = false;
        _webView!.NavigationCompleted += (s, e) =>
        {
            navigationCompleted = true;
            SimpleLogger.LogWebView("navigation completed", "HTTP mode");
        };

        string serverUrl = $"http://localhost:{_serverPort}/";
        SimpleLogger.Log($"Navigating to HTTP server: {serverUrl}");
        _webView.Url = new Uri(serverUrl);

        // Wait for navigation
        for (int i = 0; i < 50 && !navigationCompleted; i++)
        {
            await Task.Delay(100);
        }

        if (navigationCompleted)
        {
            SimpleLogger.Log("HTTP navigation completed successfully");
        }
        else
        {
            throw new InvalidOperationException("HTTP navigation failed");
        }
    }

    private static int GetAvailablePort()
    {
        for (int port = 8080; port < 8200; port++)
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
            }
        }
        throw new InvalidOperationException("No available ports found");
    }

    private async Task HandleHttpRequests()
    {
        try
        {
            while (_httpListener?.IsListening == true)
            {
                HttpListenerContext context = await _httpListener.GetContextAsync();

                byte[] responseBytes = Encoding.UTF8.GetBytes(_htmlContent ?? "<html><body>Content not ready</body></html>");

                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.Headers.Add("Cache-Control", "no-cache");

                await context.Response.OutputStream.WriteAsync(responseBytes);
                context.Response.OutputStream.Close();
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            SimpleLogger.LogError("HTTP server error", ex);
        }
    }

    public async Task RenderAsync(string mermaidSource)
    {
        _renderAttemptCount++;
        SimpleLogger.Log($"Render attempt #{_renderAttemptCount} ({(_useFileMode ? "File" : "HTTP")} mode)");

        if (_webView == null)
        {
            SimpleLogger.LogError("WebView not initialized");
            return;
        }

        if (string.IsNullOrWhiteSpace(mermaidSource))
        {
            try
            {
                await _webView.ExecuteScriptAsync("clearOutput();");
                SimpleLogger.Log("clearOutput() succeeded");
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("clearOutput() failed", ex);
            }
            return;
        }

        string escaped = mermaidSource.Replace("\\", "\\\\").Replace("`", "\\`");
        string script = $"renderMermaid(`{escaped}`);";

        try
        {
            string? result = await _webView.ExecuteScriptAsync(script);
            SimpleLogger.Log($"renderMermaid result: {result ?? "(null)"}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("renderMermaid failed", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            _httpListener?.Stop();
            _httpListener?.Close();
            if (!_useFileMode)
            {
                SimpleLogger.Log("HTTP server stopped");
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error stopping HTTP server", ex);
        }
    }
}