using AvaloniaWebView;
using System.Diagnostics;

namespace MermaidPad.Services;

/// <summary>
/// Thin wrapper around executing Mermaid render script inside the embedded WebView.
/// </summary>
public sealed class MermaidRenderer
{
    private WebView? _webView;

    /// <summary>Attach the WebView after it is constructed (late binding for DI).</summary>
    public void Attach(WebView webView) => _webView = webView;

    public async Task RenderAsync(string mermaidSource)
    {
        if (_webView is null)
        {
            Debug.WriteLine("MermaidRenderer.Render called before WebView attached.");
            return;
        }

        if (string.IsNullOrWhiteSpace(mermaidSource))
        {
            await ExecuteAsync("clearOutput();");
            return;
        }

        string escaped = EscapeForJs(mermaidSource);
        string script = $"renderMermaid(`{escaped}`);";
        try
        {
            await ExecuteAsync(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Render failed: {ex}");
        }
    }

    private static string EscapeForJs(string text)
        => text.Replace("\\", "\\\\").Replace("`", "\\`");

    private Task<string?> ExecuteAsync(string script) => _webView!.ExecuteScriptAsync(script);
}

//using System;
//using System.Diagnostics;
//using Avalonia.Controls;
//using Avalonia.WebView;

//namespace MermaidPad.Services;

//public sealed class MermaidRenderer
//{
//    private readonly WebView _webView;

//    public MermaidRenderer(WebView webView)
//    {
//        _webView = webView;
//    }

//    public async Task RenderAsync(string mermaidSource)
//    {
//        if (string.IsNullOrWhiteSpace(mermaidSource))
//        {
//            await ExecuteAsync("clearOutput();");
//            return;
//        }

//        var escaped = EscapeForJs(mermaidSource);
//        var script = $"renderMermaid(`{escaped}`);";
//        try
//        {
//            await ExecuteAsync(script);
//        }
//        catch (Exception ex)
//        {
//            Debug.WriteLine($"Render failed: {ex}");
//        }
//    }

//    private static string EscapeForJs(string text)
//        => text.Replace("\\", "\\\\").Replace("`", "\\`");

//    private System.Threading.Tasks.Task<string?> ExecuteAsync(string script)
//    {
//        // Avalonia.WebView ExecuteScriptAsync API: adjust according to actual signature
//        return _webView.ExecuteScriptAsync(script);
//    }
//}
