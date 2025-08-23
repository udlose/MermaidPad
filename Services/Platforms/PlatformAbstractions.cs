using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Services.Platforms;

public interface IPlatformServices
{
    /// <summary>
    /// Shows a native OS dialog with the specified title and message.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    void ShowNativeDialog(string title, string message);
}

public static class PlatformServiceFactory
{
    public static IPlatformServices Instance { get; } = Create();

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The factory pattern allows for easy extension and platform-specific implementations.")]
    private static IPlatformServices Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatformServices();
        }
        if (OperatingSystem.IsLinux())
        {
            return new LinuxPlatformServices();
        }
        if (OperatingSystem.IsMacOS())
        {
            return new MacPlatformServices();
        }

        Debug.Fail("Unsupported operating system. Only Windows, Linux, and macOS are supported.");
        throw new PlatformNotSupportedException("Unsupported operating system. Only Windows, Linux, and macOS are supported.");
    }
}
