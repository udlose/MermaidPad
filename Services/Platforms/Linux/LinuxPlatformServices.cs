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

        // Fallback to console output
        ShowConsoleDialog(title, message);
    }

    /// <summary>
    /// Attempts to show dialog using zenity (GNOME).
    /// </summary>
    private static bool TryShowZenityDialog(string title, string message)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = $"--error --title=\"{EscapeShellArg(title)}\" --text=\"{EscapeShellArg(message)}\"",
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
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "kdialog",
                Arguments = $"--error --title \"{EscapeShellArg(title)}\" \"{EscapeShellArg(message)}\"",
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
            Thread.Sleep(3000);
        }
    }

    /// <summary>
    /// Escapes shell arguments to prevent injection.
    /// </summary>
    private static string EscapeShellArg(string arg)
    {
        return arg.Replace("\"", "\\\"").Replace("'", "\\'").Replace("$", "\\$").Replace("`", "\\`");
    }
}