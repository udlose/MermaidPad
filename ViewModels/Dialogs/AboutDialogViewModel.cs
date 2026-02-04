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

using Serilog;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace MermaidPad.ViewModels.Dialogs;

/// <summary>
/// Represents the view model for the About dialog, providing application identity, version,
/// build, environment, and third-party library information.
/// </summary>
/// <remarks>
/// All properties are populated at construction time from <see cref="AppMetadata"/> and
/// <see cref="System.Environment"/> and are read-only for the lifetime of the dialog.
/// This ViewModel requires no subscriptions, event handlers, or disposal.
/// </remarks>
[SuppressMessage("Maintainability", "S1075:URIs should not be hardcoded")]
internal sealed class AboutDialogViewModel : ViewModelBase
{
    /// <summary>
    /// Represents a third-party library used by MermaidPad.
    /// </summary>
    /// <param name="Name">The display name of the library.</param>
    /// <param name="Description">A brief description of what the library provides.</param>
    /// <param name="Url">The project homepage URL.</param>
    public sealed record ThirdPartyLibrary(string Name, string Description, string Url);

    /// <summary>
    /// Represents a clickable link displayed in the About dialog.
    /// </summary>
    /// <param name="Label">The display text for the link.</param>
    /// <param name="Url">The target URL.</param>
    public sealed record AboutLink(string Label, string Url);

    /// <summary>
    /// Represents the base URL for the MermaidPad GitHub repository.
    /// </summary>
    private const string GitHubBaseUrl = "https://github.com/udlose/MermaidPad";

    #region App Identity

    /// <summary>
    /// Gets the product name (e.g., "MermaidPad").
    /// </summary>
    public string ProductName { get; } = AppMetadata.Product;

    /// <summary>
    /// Gets the application tagline/description.
    /// </summary>
    public string Tagline { get; } = AppMetadata.Tagline;

    #endregion App Identity

    #region Version Info

    /// <summary>
    /// Gets the informational version string, which typically includes the version number
    /// and commit SHA (e.g., "1.2.3+abcd1234").
    /// </summary>
    public string InformationalVersion { get; } = AppMetadata.InformationalVersion;

    /// <summary>
    /// Gets the file version string (e.g., "1.0.0").
    /// </summary>
    public string FileVersion { get; } = AppMetadata.FileVersion;

    #endregion Version Info

    #region Build Info

    /// <summary>
    /// Gets the build date, or "Local Build" if not available (CI builds only).
    /// </summary>
    public string BuildDate { get; } = AppMetadata.BuildDate ?? "Local Build";

    /// <summary>
    /// Gets the commit SHA, or "N/A" if not available (CI builds only).
    /// </summary>
    public string CommitSha { get; } = AppMetadata.CommitSha ?? "N/A";

    #endregion Build Info

    #region Copyright & License

    /// <summary>
    /// Gets the copyright notice.
    /// </summary>
    public string Copyright { get; } = AppMetadata.Copyright;

    /// <summary>
    /// Gets the license type identifier.
    /// </summary>
    public string LicenseType { get; } = "MIT License";

    #endregion Copyright & License

    #region Environment Info

    /// <summary>
    /// Gets the .NET runtime version string (e.g., "9.0.1").
    /// </summary>
    public string DotNetVersion { get; } = Environment.Version.ToString();

    /// <summary>
    /// Gets the Avalonia framework version string.
    /// </summary>
    public string AvaloniaVersion { get; } = GetAvaloniaVersion();

    /// <summary>
    /// Gets the operating system description (e.g., "Microsoft Windows 11.0.26100").
    /// </summary>
    public string OsDescription { get; } = RuntimeInformation.OSDescription;

    /// <summary>
    /// Gets the process architecture (e.g., "X64", "Arm64").
    /// </summary>
    public string ProcessArchitecture { get; } = RuntimeInformation.ProcessArchitecture.ToString();

    #endregion Environment Info

    #region Links

    /// <summary>
    /// Gets the collection of About dialog links (GitHub, Issues, Discussions).
    /// </summary>
    public List<AboutLink> Links { get; } =
    [
        new("MermaidPad Homepage", GitHubBaseUrl),
        new("Report an Issue", $"{GitHubBaseUrl}/issues"),
        new("Discussions", $"{GitHubBaseUrl}/discussions"),
        new("MermaidPad on gitter/matrix", $"https://matrix.to/#/#mermaidpad:matrix.org")
    ];

    #endregion Links

    #region Third-Party Libraries

    /// <summary>
    /// Gets the collection of third-party libraries used by MermaidPad.
    /// </summary>
    public List<ThirdPartyLibrary> ThirdPartyLibraries { get; } =
    [
        new ThirdPartyLibrary("Microsoft .NET", "Cross-platform application framework", "https://github.com/dotnet"),
        new ThirdPartyLibrary("AsyncAwaitBestPractices", "Async/await helpers", "https://github.com/brminnick/AsyncAwaitBestPractices"),
        new ThirdPartyLibrary("Avalonia UI", "Cross-platform desktop UI framework", "https://github.com/AvaloniaUI/Avalonia"),
        new ThirdPartyLibrary("Avalonia.AvaloniaEdit", "Text Editor component", "https://github.com/AvaloniaUI/AvaloniaEdit"),
        new ThirdPartyLibrary("CommunityToolkit.Mvvm", "MVVM patterns and source generators", "https://github.com/CommunityToolkit/dotnet"),
        new ThirdPartyLibrary("Dock.Avalonia", "Dockable panel layout system", "https://github.com/wieslawsoltes/Dock"),
        new ThirdPartyLibrary("JetBrains Resharper Annotations", "C# code analysis tooling", "https://github.com/JetBrains/JetBrains.Annotations"),
        new ThirdPartyLibrary("mermaid-js", "Diagram rendering engine", "https://github.com/mermaid-js/mermaid"),
        new ThirdPartyLibrary("mermaid-js/layout-elk", "ELK layout algorithm support", "https://github.com/mermaid-js/mermaid/tree/develop/packages/mermaid-layout-elk"),
        new ThirdPartyLibrary("mermaid-js/layout-tidy-tree", "Bidirectional Tidy Tree layout algorithm support", "https://github.com/mermaid-js/mermaid/tree/develop/packages/mermaid-layout-tidy-tree"),
        new ThirdPartyLibrary("panzoom", "Zoom and pan interactions", "https://github.com/timmywil/panzoom"),
        new ThirdPartyLibrary("Roslynator","C# code analysis tooling", "https://github.com/dotnet/roslynator"),
        new ThirdPartyLibrary("Serilog", "Structured logging framework", "https://github.com/serilog/serilog"),
        new ThirdPartyLibrary("SkiaSharp", "2D graphics and PNG export conversion", "https://github.com/nicknamenamenick/SkiaSharp"),
        new ThirdPartyLibrary("SVG.Skia", "SVG rendering engine", "https://github.com/wieslawsoltes/Svg.Skia"),
        new ThirdPartyLibrary("TextMateSharp", "Syntax highlighting in AvaloniaEdit", "https://github.com/danipen/TextMateSharp"),
        new ThirdPartyLibrary("vscode-mermaid-syntax-highlight", "Mermaid diagram syntax definition", "https://github.com/bpruitt-goddard/vscode-mermaid-syntax-highlight"),
        new ThirdPartyLibrary("Webview.Avalonia","Cross-platform browser engine","https://github.com/MicroSugarDeveloperOrg/Webviews.Avalonia"),
    ];

    #endregion Third-Party Libraries

    #region Commands

    /// <summary>
    /// Opens the specified URL in the system's default browser.
    /// </summary>
    /// <remarks>
    /// Uses the same cross-platform pattern as <c>ViewLogs()</c> and <c>OpenFileLocationAsync()</c>.
    /// </remarks>
    /// <param name="url">The URL to open. If null or empty, this method is a no-op.</param>
    public static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true,
            };
            using Process? process = Process.Start(processStartInfo);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open URL: {Url}, from {ViewModel}",
                url, nameof(AboutDialogViewModel));
        }
    }

    #endregion Commands

    #region Helpers

    /// <summary>
    /// Gets the Avalonia framework version from the assembly metadata.
    /// </summary>
    /// <returns>The Avalonia version string, or "Unknown" if not available.</returns>
    private static string GetAvaloniaVersion() =>
        typeof(Avalonia.Application).Assembly.GetName().Version?.ToString(3) ?? "Unknown";

    #endregion Helpers
}
