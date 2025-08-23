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
    /// Checks if the current runtime platform and architecture match the build target.
    /// If a mismatch is detected, displays a native OS dialog with details and terminates the application.
    /// </summary>
    public static void CheckCompatibility()
    {
        PlatformInfo currentInfo = GetCurrentPlatformInfo();
        PlatformInfo targetInfo = GetBuildTargetInfo();

        // Check for platform/architecture mismatch
        if (IsMismatch(currentInfo, targetInfo))
        {
            string message = CreateMismatchMessage(currentInfo, targetInfo);
            try
            {
                PlatformServiceFactory.Instance.ShowNativeDialog("Warning: Platform Mismatch Detected", message);
            }
            catch (Exception ex)
            {
                // Fallback if platform service fails
                Console.WriteLine("Warning: Platform Mismatch Detected: " + message);
                Console.WriteLine($"Dialog error: {ex.Message}");
            }

            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Retrieves information about the current operating system, processor architecture,
    /// and whether the application is running under translation/emulation (e.g., Rosetta on macOS).
    /// </summary>
    /// <returns>
    /// A <see cref="PlatformInfo"/> struct containing the OS identifier, architecture, and translation status.
    /// </returns>
    private static PlatformInfo GetCurrentPlatformInfo()
    {
        string os = GetCurrentOS();
        string arch = GetCurrentArchitecture();
        bool isTranslated = IsRunningUnderTranslation();

        return new PlatformInfo(os, arch, isTranslated);
    }

    /// <summary>
    /// Retrieves the build target platform and architecture as determined at compile time.
    /// If unavailable or in an unexpected format, falls back to the current runtime platform.
    /// </summary>
    /// <returns>
    /// A <see cref="PlatformInfo"/> struct representing the build target.
    /// </returns>
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
    /// Determines the build target Runtime Identifier (RID) using compile-time constants.
    /// The RID is used to identify the intended OS and architecture for the build.
    /// </summary>
    /// <returns>
    /// A string representing the build target RID (e.g., "win-x64", "osx-arm64"), or an empty string if not specified.
    /// </returns>
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
    /// Identifies the current operating system as a short string identifier.
    /// </summary>
    /// <returns>
    /// "win" for Windows, "linux" for Linux, "osx" for macOS, or "unknown" if undetectable.
    /// </returns>
    [SuppressMessage("Style", "IDE0046:Convert to conditional expression", Justification = "Code is more clear without a double-ternary")]
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
    /// Identifies the current processor architecture as a short string identifier.
    /// </summary>
    /// <returns>
    /// "x64", "x86", "arm64", "arm", or "unknown" if undetectable.
    /// </returns>
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
    /// Detects if the application is running under translation or emulation,
    /// such as Rosetta on Apple Silicon Macs.
    /// </summary>
    /// <returns>
    /// True if running under translation/emulation; otherwise, false.
    /// </returns>
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
    /// Determines if there is a platform or architecture mismatch between the current runtime and the build target.
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <param name="target">The build target platform information.</param>
    /// <returns>
    /// True if a mismatch is detected; otherwise, false.
    /// </returns>
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
    /// Checks for architecture mismatches specific to Windows.
    /// Allows x86 applications to run on x64 systems (WOW64).
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <param name="target">The build target platform information.</param>
    /// <returns>
    /// True if a problematic mismatch is detected; otherwise, false.
    /// </returns>
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
    /// Checks for architecture mismatches specific to macOS.
    /// Flags running x64 apps under Rosetta on ARM64 hardware and direct architecture mismatches.
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <param name="target">The build target platform information.</param>
    /// <returns>
    /// True if a problematic mismatch is detected; otherwise, false.
    /// </returns>
    private static bool IsMacOSArchitectureMismatch(PlatformInfo current, PlatformInfo target)
    {
        // Flag Rosetta translation (x64 app on arm64 hardware)
        if (current.IsTranslated && target.Architecture == "x64" && current.Architecture == "x64")
        {
            return true;
        }

        // Also flag direct architecture mismatches
        return current.Architecture != target.Architecture;
    }

    /// <summary>
    /// Checks for architecture mismatches specific to Linux.
    /// Linux generally does not support cross-architecture execution.
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <param name="target">The build target platform information.</param>
    /// <returns>
    /// True if a problematic mismatch is detected; otherwise, false.
    /// </returns>
    private static bool IsLinuxArchitectureMismatch(PlatformInfo current, PlatformInfo target)
    {
        // Linux typically doesn't have as robust cross-architecture support as Windows
        return current.Architecture != target.Architecture;
    }

    /// <summary>
    /// Generates a detailed, user-friendly message describing the detected platform mismatch,
    /// including recommended download instructions for the correct version.
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <param name="target">The build target platform information.</param>
    /// <returns>
    /// A string containing the mismatch details and download instructions.
    /// </returns>
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
    /// Determines the native Runtime Identifier (RID) for the current system,
    /// accounting for translation/emulation scenarios (e.g., Rosetta on macOS).
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <returns>
    /// A string representing the native RID (e.g., "osx-arm64").
    /// </returns>
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
    /// Gets the recommended Runtime Identifier (RID) to download for the current system.
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <returns>
    /// A string representing the recommended RID.
    /// </returns>
    private static string GetRecommendedRid(PlatformInfo current)
    {
        return GetNativeRidForCurrent(current);
    }

    /// <summary>
    /// Represents platform information including OS identifier, architecture, and translation/emulation status.
    /// </summary>
    private readonly record struct PlatformInfo(string OS, string Architecture, bool IsTranslated);
}