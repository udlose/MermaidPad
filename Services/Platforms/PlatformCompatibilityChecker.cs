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
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "Hardcoded to specific github download url")]
    private const string DownloadUrl = "https://github.com/udlose/MermaidPad/releases";

    /// <summary>
    /// Checks whether the current platform and architecture are compatible with the application's requirements, and
    /// terminates the process if an unsupported or mismatched configuration is detected.
    /// </summary>
    /// <remarks>This method should be called at application startup to ensure that the runtime environment
    /// matches the expected platform and architecture. If an unsupported platform (such as linux-arm64) or a mismatch
    /// between the current and target platform is detected, a warning is displayed to the user and the application
    /// exits with an error code. The method may show a native dialog or print a message to the console, depending on
    /// platform capabilities.</remarks>
    public static void CheckCompatibility()
    {
        PlatformInfo currentInfo = GetCurrentPlatformInfo();
        PlatformInfo targetInfo = GetBuildTargetInfo();

        // Check for unsupported platforms - linux-arm64 is not supported
        if (string.Equals(currentInfo.OS, "linux", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(currentInfo.Architecture, "arm64", StringComparison.OrdinalIgnoreCase))
        {
            string message = $"The linux-arm64 version is not supported at this time due to CefGlue limitations. See the releases page for available versions:{Environment.NewLine}{Environment.NewLine}{DownloadUrl}";

            bool dialogShown = TryShowNativeDialog("Warning: Unsupported Platform Detected", message);
            ExitWithErrorDelay(dialogShown);
        }

        // Check for platform/architecture mismatch
        if (IsMismatch(currentInfo, targetInfo))
        {
            string message = CreateMismatchMessage(currentInfo, targetInfo);
            bool dialogShown = TryShowNativeDialog("Warning: Platform Mismatch Detected", message);
            ExitWithErrorDelay(dialogShown);
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
    [SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "Compile-time constants for build targets")]
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
                    return true; // Likely running x64 under Rosetta on arm64 hardware
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
        if (string.Equals(target.Architecture, "x86", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(current.Architecture, "x64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // All other architecture mismatches are problematic
        return !string.Equals(current.Architecture, target.Architecture, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks for architecture mismatches specific to macOS.
    /// Flags running x64 apps under Rosetta on arm64 hardware and direct architecture mismatches.
    /// </summary>
    /// <param name="current">The current platform information.</param>
    /// <param name="target">The build target platform information.</param>
    /// <returns>
    /// True if a problematic mismatch is detected; otherwise, false.
    /// </returns>
    private static bool IsMacOSArchitectureMismatch(PlatformInfo current, PlatformInfo target)
    {
        // Flag Rosetta translation (x64 app on arm64 hardware)
        if (current.IsTranslated &&
            string.Equals(target.Architecture, "x64", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(current.Architecture, "x64", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Also flag direct architecture mismatches
        return !string.Equals(current.Architecture, target.Architecture, StringComparison.OrdinalIgnoreCase);
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
        return !string.Equals(current.Architecture, target.Architecture, StringComparison.OrdinalIgnoreCase);
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
        if (string.Equals(current.OS, "osx", StringComparison.OrdinalIgnoreCase) && current.IsTranslated)
        {
            // If we're under Rosetta, the hardware is actually arm64
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

    #region Filesystem Permission Checks

    /// <summary>
    /// Verifies filesystem permissions required for app operation on Mac/Linux.
    /// Shows native dialog and exits if critical permissions are missing.
    /// </summary>
    /// <remarks>
    /// This method checks:
    /// <list type="bullet">
    ///     <item><description>Configuration directory access (for settings.json)</description></item>
    ///     <item><description>Assets directory access (for extracted resources)</description></item>
    ///     <item><description>Log file write permissions (for debug.log)</description></item>
    ///     <item><description>Temp directory access (for update downloads)</description></item>
    ///     <item><description>Display environment availability on Linux (for GUI dialogs)</description></item>
    /// </list>
    /// On Windows, this check is skipped as permission issues are rare.
    /// </remarks>
    public static void CheckFilesystemPermissions()
    {
        // Skip on Windows - permissions rarely an issue with normal user accounts
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        List<string> errors = new List<string>(4);

        // 1. Check config directory access
        if (!CanAccessConfigDirectory(out string? configError))
        {
            errors.Add(configError);
        }

        // 2. Check assets directory (will be created by AssetService)
        if (!CanAccessAssetsDirectory(out string? assetsError))
        {
            errors.Add(assetsError);
        }

        // 3. Check log file write
        if (!CanWriteLogFile(out string? logError))
        {
            errors.Add(logError);
        }

        // 4. Check temp directory
        if (!CanAccessTempDirectory(out string? tempError))
        {
            errors.Add(tempError);
        }

        // 5. Linux-specific: Check display environment (warning only, not fatal)
        if (OperatingSystem.IsLinux() && !HasDisplayEnvironment(out string? displayWarning))
        {
            // This is a warning, not an error (can fall back to console dialogs)
            Console.WriteLine($"Warning: {displayWarning}");
        }

        if (errors.Count > 0)
        {
            string message = CreatePermissionErrorMessage(errors);
            bool dialogShown = TryShowNativeDialog("Permission Error", message);
            ExitWithErrorDelay(dialogShown);
        }
    }

    /// <summary>
    /// Checks if the application can access the configuration directory.
    /// </summary>
    /// <param name="error">Error message if check fails, otherwise null.</param>
    /// <returns>True if directory is accessible, false otherwise.</returns>
    private static bool CanAccessConfigDirectory([NotNullWhen(false)] out string? error)
    {
        try
        {
            string configDir = GetConfigDirectory();

            // Try to create directory
            Directory.CreateDirectory(configDir);

            // Verify we can write/read a test file
            string testFile = Path.Combine(configDir, ".permission_test");
            File.WriteAllText(testFile, "test");
            _ = File.ReadAllText(testFile);
            File.Delete(testFile);

            error = null;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"No permission to access config directory: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Config directory access failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if the application can create and write to the assets directory.
    /// </summary>
    /// <param name="error">Error message if check fails, otherwise null.</param>
    /// <returns>True if assets directory is accessible, false otherwise.</returns>
    private static bool CanAccessAssetsDirectory([NotNullWhen(false)] out string? error)
    {
        try
        {
            string configDir = GetConfigDirectory();
            string assetsDir = Path.Combine(configDir, "Assets");

            Directory.CreateDirectory(assetsDir);

            // Test write/read of a file similar to what AssetService will extract
            string testFile = Path.Combine(assetsDir, ".test.html");
            File.WriteAllText(testFile, "<html></html>");
            _ = File.ReadAllText(testFile);
            File.Delete(testFile);

            error = null;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"No permission to create/write asset files: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Assets directory access failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if the application can write to the log file.
    /// </summary>
    /// <param name="error">Error message if check fails, otherwise null.</param>
    /// <returns>True if log file is writable, false otherwise.</returns>
    private static bool CanWriteLogFile([NotNullWhen(false)] out string? error)
    {
        try
        {
            string configDir = GetConfigDirectory();
            string logFile = Path.Combine(configDir, "debug.log");

            // Test append mode with FileShare.ReadWrite to match Serilog's exact behavior.
            // We must use FileStream instead of File.AppendAllText() because:
            // - Serilog is configured with shared:true (see ServiceConfiguration.cs:160)
            // - This means FileShare.ReadWrite, allowing multiple processes/threads to write
            // - File.AppendAllText() uses FileShare.Read by default, which would fail this test
            //   even though Serilog would succeed, creating a false negative.
            using (FileStream fs = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter writer = new StreamWriter(fs))
            {
                writer.WriteLine($"# Permission test {DateTime.UtcNow:O}");
            }

            // Clean up test entry if file is small (< 1KB)
            const int maxTestLogSize = 1_024;
            if (File.Exists(logFile) && new FileInfo(logFile).Length < maxTestLogSize)
            {
                File.Delete(logFile);
            }

            error = null;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"No permission to write log file: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Log file write test failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if the application can access the system temp directory.
    /// </summary>
    /// <param name="error">Error message if check fails, otherwise null.</param>
    /// <returns>True if temp directory is accessible, false otherwise.</returns>
    private static bool CanAccessTempDirectory([NotNullWhen(false)] out string? error)
    {
        try
        {
            // Try to create and delete a temp file
            // This test purposely uses Path.GetTempPath() to match actual temp file usage instead of Path.GetTempFileName()
            string tempDir = Path.GetTempPath();
            string testFile = Path.Combine(tempDir, $"mermaidpad_test_{Guid.NewGuid():N}.tmp");

            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            error = null;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"No permission to write temp files: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Temp directory access failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if a graphical display environment is available on Linux.
    /// </summary>
    /// <param name="warning">Warning message if no display is available, otherwise null.</param>
    /// <returns>True if display environment is available, false otherwise.</returns>
    private static bool HasDisplayEnvironment([NotNullWhen(false)] out string? warning)
    {
        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        string? waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

        if (string.IsNullOrEmpty(display) && string.IsNullOrEmpty(waylandDisplay))
        {
            warning = "No graphical environment detected (DISPLAY/WAYLAND_DISPLAY not set). GUI dialogs may not work.";
            return false;
        }

        warning = null;
        return true;
    }

    /// <summary>
    /// Gets the application configuration directory path.
    /// </summary>
    /// <returns>Full path to the MermaidPad config directory.</returns>
    private static string GetConfigDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MermaidPad");
    }

    /// <summary>
    /// Creates a user-friendly error message describing filesystem permission failures.
    /// </summary>
    /// <param name="errors">List of permission errors encountered.</param>
    /// <returns>Formatted error message with troubleshooting guidance.</returns>
    private static string CreatePermissionErrorMessage(List<string> errors)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MermaidPad Filesystem Permission Error");
        sb.AppendLine();
        sb.AppendLine("The application cannot start due to insufficient filesystem permissions:");
        sb.AppendLine();

        foreach (string error in errors)
        {
            sb.AppendLine($"• {error}");
        }

        sb.AppendLine();
        sb.AppendLine("On Linux/macOS, ensure:");
        sb.AppendLine("1. You have write access to your home directory");
        sb.AppendLine("2. ~/.config/ (Linux) or ~/Library/Application Support/ (macOS) is writable");
        sb.AppendLine("3. SELinux/AppArmor policies don't block the application");
        sb.AppendLine("4. Disk is not full or read-only");
        sb.AppendLine();
        sb.AppendLine($"Config directory: {GetConfigDirectory()}");

        return sb.ToString();
    }

    #endregion Filesystem Permission Checks

    #region Shared Dialog Helpers

    /// <summary>
    /// Attempts to show a native OS dialog with the specified title and message.
    /// Falls back to console output if the dialog cannot be shown.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The dialog message.</param>
    /// <returns>True if the native dialog was shown successfully; false if console fallback was used.</returns>
    private static bool TryShowNativeDialog(string title, string message)
    {
        try
        {
            PlatformServiceFactory.Instance.ShowNativeDialog(title, message);
            return true; // Dialog was shown and dismissed by user
        }
        catch (Exception ex)
        {
            // Fallback if platform service fails
            Console.WriteLine($"{title}: {message}");
            Console.WriteLine($"Dialog error: {ex.Message}");
            return false; // Dialog failed, used console fallback
        }
    }

    /// <summary>
    /// Exits the application with error code 1, optionally pausing to allow console message reading.
    /// </summary>
    /// <param name="dialogWasShown">Whether a native dialog was successfully shown to the user.</param>
    /// <remarks>
    /// Only sleeps if console fallback was used (no user interaction).
    /// Native dialogs already block until dismissed, so no sleep is needed in that case.
    /// </remarks>
    private static void ExitWithErrorDelay(bool dialogWasShown)
    {
        // Only sleep if we used console fallback (no user interaction)
        // Native dialogs already block until dismissed, so no sleep needed
        if (!dialogWasShown)
        {
            try
            {
                Thread.Sleep(10_000); // Give user time to read console message
            }
            catch (ThreadInterruptedException)
            {
                // Handle interruption gracefully
            }
        }

        Environment.Exit(1);
    }

    #endregion Shared Dialog Helpers
}
