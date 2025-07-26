
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Services.Platforms;
public interface IPlatformServices
{
    string GetAssetsDirectory();
}

public static class PlatformServiceFactory
{
    public static IPlatformServices Instance { get; } = Create();

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The factory pattern allows for easy extension and platform-specific implementations.")]
    private static IPlatformServices Create()
    {
#if WINDOWS
        return new WindowsPlatformServices();
#elif LINUX
        return new LinuxPlatformServices(); //TODO - add implementation
#elif MACOS
        return new MacPlatformServices();     //TODO - add implementation
#else
        return new WindowsPlatformServices();
#endif
    }
}
