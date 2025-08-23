using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace MermaidPad.Services.Platforms;
/// <summary>
/// Static platform compatibility checker that validates the running application 
/// matches the target platform and architecture. Shows native OS dialogs on mismatches.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class PlatformCompatibilityChecker
{
    private const string DownloadUrl = "https://github.com/udlose/MermaidPad/releases";

    /// <summary>
    /// Performs platform compatibility check. If a mismatch is detected,
    /// shows a native dialog and exits the application.
    /// </summary>
    public static void CheckCompatibility()
    {
        PlatformInfo currentInfo = GetCurrentPlatformInfo();
        PlatformInfo targetInfo = GetBuildTargetInfo();

        // Check for platform/architecture mismatch
        if (IsMismatch(currentInfo, targetInfo))
        {
            string message = CreateMismatchMessage(currentInfo, targetInfo);
            ShowNativeDialog("Platform Mismatch Detected", message);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Gets information about the current runtime platform and architecture.
    /// </summary>
    private static PlatformInfo GetCurrentPlatformInfo()
    {
        string os = GetCurrentOS();
        string arch = GetCurrentArchitecture();
        bool isTranslated = IsRunningUnderTranslation();

        return new PlatformInfo(os, arch, isTranslated);
    }

    /// <summary>
    /// Gets information about the build target embedded at compile time.
    /// </summary>
    private static PlatformInfo GetBuildTargetInfo()
    {
        // These constants are defined by Directory.Build.props based on RuntimeIdentifier
        string targetRid = GetBuildTargetRid();

        if (string.IsNullOrEmpty(targetRid))
        {
            // Fallback - no specific target detected, assume current platform is correct
            return GetCurrentPlatformInfo();
        }

        // Parse RID format like "win-x64", "osx-arm64", "linux-x64"
        string[] parts = targetRid.Split('-');
        if (parts.Length != 2)
        {
            return GetCurrentPlatformInfo(); // Fallback for unexpected format
        }

        Debug.Assert(parts.Length == 2, "Unexpected RID format");
        return new PlatformInfo(parts[0], parts[1], false);
    }

    /// <summary>
    /// Gets the build target RID based on compile-time constants.
    /// </summary>
    private static string GetBuildTargetRid()
    {
#if BUILT_FOR_WIN_X64
        return "win-x64";
#elif BUILT_FOR_WIN_X86
        return "win-x86";
#elif BUILT_FOR_WIN_ARM64
        return "win-arm64";
#elif BUILT_FOR_LINUX_X64
        return "linux-x64";
#elif BUILT_FOR_LINUX_ARM64
        return "linux-arm64";
#elif BUILT_FOR_OSX_X64
        return "osx-x64";
#elif BUILT_FOR_OSX_ARM64
        return "osx-arm64";
#else
        return ""; // No specific target detected
#endif
    }

    /// <summary>
    /// Determines the current operating system identifier.
    /// </summary>
    private static string GetCurrentOS()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        return OperatingSystem.IsMacOS() ? "osx" : "unknown";
    }

    /// <summary>
    /// Determines the current processor architecture.
    /// </summary>
    private static string GetCurrentArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Detects if the application is running under translation (like Rosetta on macOS).
    /// </summary>
    private static bool IsRunningUnderTranslation()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            // On macOS, we can detect Rosetta translation by checking if:
            // 1. The process architecture is x64 but hardware architecture is arm64
            // 2. Or by checking environment variables set by Rosetta

            // Method 1: Check environment variable set by Rosetta
            string? rosettaEnv = Environment.GetEnvironmentVariable("ROSETTA_NATIVE_ARCH");
            if (!string.IsNullOrEmpty(rosettaEnv))
            {
                return true;
            }

            // Method 2: Process arch vs native arch mismatch detection
            // If we're running x64 but hardware is actually arm64, we're under Rosetta
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                // We can infer hardware architecture from the OS version or other methods
                // For now, if we're x64 on macOS 11+ (which supports Apple Silicon), likely Rosetta
                Version osVersion = Environment.OSVersion.Version;
                if (osVersion.Major >= 11) // macOS 11+ supports Apple Silicon
                {
                    return true; // Likely running x64 under Rosetta on ARM64 hardware
                }
            }

            return false;
        }
        catch
        {
            return false; // If we can't determine, assume no translation
        }
    }

    /// <summary>
    /// Determines if there's a platform/architecture mismatch that should be flagged.
    /// </summary>
    private static bool IsMismatch(PlatformInfo current, PlatformInfo target)
    {
        // Cross-OS mismatches are always problematic
        if (current.OS != target.OS)
        {
            return true;
        }

        // Architecture-specific rules by platform
        return current.OS switch
        {
            "win" => IsWindowsArchitectureMismatch(current, target),
            "osx" => IsMacOSArchitectureMismatch(current, target),
            "linux" => IsLinuxArchitectureMismatch(current, target),
            _ => false // Unknown platform, don't flag
        };
    }

    /// <summary>
    /// Windows-specific architecture compatibility rules.
    /// </summary>
    private static bool IsWindowsArchitectureMismatch(PlatformInfo current, PlatformInfo target)
    {
        // Windows x86 can run on x64 (WOW64), so don't flag this
        if (target.Architecture == "x86" && current.Architecture == "x64")
        {
            return false;
        }

        // All other architecture mismatches are problematic
        return current.Architecture != target.Architecture;
    }

    /// <summary>
    /// macOS-specific architecture compatibility rules.
    /// </summary>
    private static bool IsMacOSArchitectureMismatch(PlatformInfo current, PlatformInfo target)
    {
        // Flag Rosetta translation (x64 app on arm64 hardware) - this was the original issue!
        if (current.IsTranslated && target.Architecture == "x64" && current.Architecture == "x64")
        {
            return true;
        }

        // Also flag direct architecture mismatches
        return current.Architecture != target.Architecture;
    }

    /// <summary>
    /// Linux-specific architecture compatibility rules.
    /// </summary>
    private static bool IsLinuxArchitectureMismatch(PlatformInfo current, PlatformInfo target)
    {
        // Linux typically doesn't have as robust cross-architecture support as Windows
        return current.Architecture != target.Architecture;
    }

    /// <summary>
    /// Creates a user-friendly message describing the platform mismatch.
    /// </summary>
    private static string CreateMismatchMessage(PlatformInfo current, PlatformInfo target)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("Warning: Platform Mismatch Detected");
        sb.AppendLine();

        string targetRid = $"{target.OS}-{target.Architecture}";
        string currentRid = $"{current.OS}-{current.Architecture}";

        if (current.IsTranslated)
        {
            sb.AppendLine($"You're running the {targetRid} version under translation/emulation.");
            sb.AppendLine($"Your system needs the native {GetNativeRidForCurrent(current)} version.");
        }
        else if (current.OS != target.OS)
        {
            sb.AppendLine($"You downloaded: {targetRid} version.");
            sb.AppendLine($"Your system needs: {currentRid} version.");
        }
        else
        {
            sb.AppendLine($"You downloaded: {targetRid} version.");
            sb.AppendLine($"Your system needs: {currentRid} version.");
        }

        sb.AppendLine();
        sb.AppendLine($"Please download the latest version for {GetRecommendedRid(current)} from:");
        sb.AppendLine(DownloadUrl);

        return sb.ToString();
    }

    /// <summary>
    /// Gets the native RID for the current system (detecting hardware architecture on macOS).
    /// </summary>
    private static string GetNativeRidForCurrent(PlatformInfo current)
    {
        if (current.OS == "osx" && current.IsTranslated)
        {
            // If we're under Rosetta, the hardware is actually ARM64
            return "osx-arm64";
        }

        return $"{current.OS}-{current.Architecture}";
    }

    /// <summary>
    /// Gets the recommended RID to download for the current system.
    /// </summary>
    private static string GetRecommendedRid(PlatformInfo current)
    {
        return GetNativeRidForCurrent(current);
    }

    /// <summary>
    /// Shows a platform-native dialog with the specified title and message.
    /// </summary>
    private static void ShowNativeDialog(string title, string message)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsNativeDialog.Show(title, message);
            }
            else if (OperatingSystem.IsMacOS())
            {
                MacOSNativeDialog.Show(title, message);
            }
            else if (OperatingSystem.IsLinux())
            {
                LinuxNativeDialog.Show(title, message);
            }
            else
            {
                // Fallback to console
                Console.WriteLine($"{title}: {message}");
            }
        }
        catch (Exception ex)
        {
            // Fallback if native dialog fails
            Console.WriteLine($"{title}: {message}");
            Console.WriteLine($"Dialog error: {ex.Message}");
        }
    }

    /// <summary>
    /// Simple record to hold platform information.
    /// </summary>
    private readonly record struct PlatformInfo(string OS, string Architecture, bool IsTranslated);
}