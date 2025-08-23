using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.WebView.Desktop;
using MermaidPad.Services.Platforms;

namespace MermaidPad;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // CRITICAL: Check platform compatibility FIRST, before any other initialization
        // This prevents crashes from architecture mismatches (e.g., x64 app on ARM64 via Rosetta)
        PlatformCompatibilityChecker.CheckCompatibility();

        // If we get here, platform compatibility is OK
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure(() => new App())
            .UsePlatformDetect()
            .UseDesktopWebView() // this handles cross-platform WebView support
            .UseReactiveUI()
            .LogToTrace();
}