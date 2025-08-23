// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Diagnostics;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

[SupportedOSPlatform("linux")]
public sealed class LinuxPlatformServices : IPlatformServices
{
    /// <summary>
    /// Shows a Linux dialog using zenity, falls back to console output.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    public void ShowNativeDialog(string title, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(message);

        // Check for graphical environment before attempting GUI dialogs
        if (IsGraphicalEnvironment())
        {
            // Try zenity first (GUI dialog)
            if (TryShowZenityDialog(title, message))
            {
                return;
            }

            // Try other common GUI dialog tools
            if (TryShowKDialogDialog(title, message))
            {
                return;
            }
        }

        // Fallback to console output
        ShowConsoleDialog(title, message);
    }

    /// <summary>
    /// Checks if a graphical environment is available.
    /// </summary>
    private static bool IsGraphicalEnvironment()
    {
        // Check for DISPLAY or WAYLAND_DISPLAY environment variable
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    /// <summary>
    /// Attempts to show dialog using zenity (GNOME).
    /// </summary>
    private static bool TryShowZenityDialog(string title, string message)
    {
        if (!IsToolAvailable("zenity"))
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = $"--error --title={EscapeShellArg(title)} --text={EscapeShellArg(message)}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to show dialog using kdialog (KDE).
    /// </summary>
    private static bool TryShowKDialogDialog(string title, string message)
    {
        if (!IsToolAvailable("kdialog"))
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "kdialog",
                Arguments = $"--error --title {EscapeShellArg(title)} {EscapeShellArg(message)}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a tool is available in the PATH.
    /// </summary>
    private static bool IsToolAvailable(string toolName)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = toolName,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process != null && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Shows dialog in console as fallback.
    /// </summary>
    private static void ShowConsoleDialog(string title, string message)
    {
        Console.WriteLine();
        Console.WriteLine($"={new string('=', title.Length + 2)}=");
        Console.WriteLine($" {title} ");
        Console.WriteLine($"={new string('=', title.Length + 2)}=");
        Console.WriteLine();
        Console.WriteLine(message);
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");

        try
        {
            Console.ReadKey(true);
        }
        catch
        {
            // In case console input is not available
            Thread.Sleep(3_000);
        }
    }

    /// <summary>
    /// Escapes shell arguments to prevent injection.
    /// </summary>
    private static string EscapeShellArg(string? arg)
    {
        // Wrap in single quotes and escape single quotes inside the argument
        if (arg is null)
        {
            return "''";
        }

        return $"'{arg.Replace("'", "'\\''")}'";
    }
}