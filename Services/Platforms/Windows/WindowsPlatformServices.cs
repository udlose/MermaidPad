// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Win32 API naming conventions")]
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatformServices : IPlatformServices
{
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;

    /// <summary>
    /// Shows a Windows MessageBox with the specified title and message.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    public void ShowNativeDialog(string title, string message)
    {
        MessageBox(IntPtr.Zero, message, title, MB_OK | MB_ICONERROR);
    }
}