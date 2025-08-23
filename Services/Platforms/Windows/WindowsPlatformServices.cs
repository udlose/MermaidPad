// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides Windows-specific platform services, including native dialog display.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Win32 API naming conventions")]
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatformServices : IPlatformServices
{
    /// <summary>
    /// Displays a native Windows MessageBox dialog.
    /// </summary>
    /// <param name="hWnd">A handle to the owner window of the message box.</param>
    /// <param name="text">The message to be displayed.</param>
    /// <param name="caption">The title of the message box.</param>
    /// <param name="type">The type of message box to display (button and icon options).</param>
    /// <returns>
    /// An integer value indicating which button was pressed by the user.
    /// </returns>
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>
    /// Specifies the OK button for the message box.
    /// </summary>
    private const uint MB_OK = 0x00000000;

    /// <summary>
    /// Specifies the error icon for the message box.
    /// </summary>
    private const uint MB_ICONERROR = 0x00000010;

    /// <summary>
    /// Shows a Windows MessageBox with the specified title and message.
    /// The dialog is modal to the application's main window if available.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog message.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="title"/> or <paramref name="message"/> is null or empty.</exception>
    public void ShowNativeDialog(string title, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(message);

        // Make the message box modal to the main application window if possible by passing its handle
        // Even though the cast is redundant, it clarifies the intent that we are converting to IntPtr
        IntPtr mainWindowHandle = (IntPtr)Process.GetCurrentProcess().MainWindowHandle;
        MessageBox(mainWindowHandle, message, title, MB_OK | MB_ICONERROR);
    }
}