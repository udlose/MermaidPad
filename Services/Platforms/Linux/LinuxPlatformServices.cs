// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides platform-specific services for Linux, including native dialog display.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxPlatformServices : IPlatformServices
{
    /// <summary>
    /// Shows a native Linux dialog using zenity, kdialog, yad, Xdialog, or gxmessage if available, otherwise falls back to console output.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="title"/> or <paramref name="message"/> is null or empty.</exception>
    [SuppressMessage("Style", "IDE0011:Add braces", Justification = "<Pending>")]
    public void ShowNativeDialog(string title, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(message);

        // Check for graphical environment before attempting GUI dialogs
        if (IsGraphicalEnvironment())
        {
            // Try zenity first (GUI dialog)
            if (TryShowZenityDialog(title, message)) return;

            // Try other common GUI dialog tools
            if (TryShowKDialogDialog(title, message)) return;
            if (TryShowYadDialog(title, message)) return;
            if (TryShowXDialogDialog(title, message)) return;
            if (TryShowGxmessageDialog(title, message)) return;
        }

        // Fallback to console output
        ShowConsoleDialog(title, message);
    }

    /// <summary>
    /// Determines whether a graphical environment is available by checking environment variables.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a graphical environment is available; otherwise, <c>false</c>.
    /// </returns>
    private static bool IsGraphicalEnvironment()
    {
        // Check for DISPLAY or WAYLAND_DISPLAY environment variable
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    /// <summary>
    /// Attempts to show a dialog using zenity (GNOME).
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <returns>
    /// <c>true</c> if the dialog was shown successfully; otherwise, <c>false</c>.
    /// </returns>
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
    /// Attempts to show a dialog using kdialog (KDE).
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <returns>
    /// <c>true</c> if the dialog was shown successfully; otherwise, <c>false</c>.
    /// </returns>
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
    /// Attempts to show a dialog using yad.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <returns>
    /// <c>true</c> if the dialog was shown successfully; otherwise, <c>false</c>.
    /// </returns>
    private static bool TryShowYadDialog(string title, string message)
    {
        if (!IsToolAvailable("yad"))
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "yad",
                Arguments = $"--title={EscapeShellArg(title)} --text={EscapeShellArg(message)} --button=OK",
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
    /// Attempts to show a dialog using Xdialog.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <returns>
    /// <c>true</c> if the dialog was shown successfully; otherwise, <c>false</c>.
    /// </returns>
    private static bool TryShowXDialogDialog(string title, string message)
    {
        if (!IsToolAvailable("Xdialog"))
        {
            return false;
        }

        try
        {
            // Xdialog requires geometry: --msgbox "Message" height width
            // We'll use a default geometry
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "Xdialog",
                Arguments = $"--msgbox {EscapeShellArg(message)} 10 40 --title {EscapeShellArg(title)}",
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
    /// Attempts to show a dialog using gxmessage.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <returns>
    /// <c>true</c> if the dialog was shown successfully; otherwise, <c>false</c>.
    /// </returns>
    private static bool TryShowGxmessageDialog(string title, string message)
    {
        if (!IsToolAvailable("gxmessage"))
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "gxmessage",
                Arguments = $"-title {EscapeShellArg(title)} {EscapeShellArg(message)}",
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
    /// Checks if a tool is available in the system's PATH.
    /// </summary>
    /// <param name="toolName">The name of the tool to check.</param>
    /// <returns>
    /// <c>true</c> if the tool is available; otherwise, <c>false</c>.
    /// </returns>
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
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Shows a dialog in the console as a fallback if no GUI dialog tools are available.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
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
    /// Escapes a shell argument to prevent injection by wrapping in single quotes and escaping internal single quotes.
    /// </summary>
    /// <param name="arg">The argument to escape.</param>
    /// <returns>
    /// The escaped shell argument.
    /// </returns>
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