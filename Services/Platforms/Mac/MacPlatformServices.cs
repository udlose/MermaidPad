// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

[SupportedOSPlatform("macos")]
public sealed partial class MacPlatformServices : IPlatformServices
{
    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string className);

    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string selectorName);

    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static partial IntPtr objc_msgSend(IntPtr target, IntPtr selector);

    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static partial IntPtr objc_msgSend(IntPtr target, IntPtr selector, IntPtr arg1);

    [LibraryImport("/System/Library/Frameworks/Foundation.framework/Foundation", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_msgSend(IntPtr target, IntPtr selector, string arg1);

    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static partial void objc_msgSend_void(IntPtr target, IntPtr selector, long arg1);

    /// <summary>
    /// Shows a macOS NSAlert with the specified title and message.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    public void ShowNativeDialog(string title, string message)
    {
        try
        {
            // Get NSAlert class
            IntPtr alertClass = objc_getClass("NSAlert");
            IntPtr allocSelector = sel_registerName("alloc");
            IntPtr initSelector = sel_registerName("init");
            IntPtr runModalSelector = sel_registerName("runModal");
            IntPtr setMessageTextSelector = sel_registerName("setMessageText:");
            IntPtr setInformativeTextSelector = sel_registerName("setInformativeText:");
            IntPtr setAlertStyleSelector = sel_registerName("setAlertStyle:");

            // Create NSAlert instance
            IntPtr alert = objc_msgSend(alertClass, allocSelector);
            alert = objc_msgSend(alert, initSelector);

            // Set alert style to critical (2 = NSAlertStyleCritical)
            objc_msgSend_void(alert, setAlertStyleSelector, 2);

            // Set title and message
            objc_msgSend(alert, setMessageTextSelector, title);
            objc_msgSend(alert, setInformativeTextSelector, message);

            // Show modal dialog
            objc_msgSend(alert, runModalSelector);
        }
        catch (Exception ex)
        {
            // Fallback to console if P/Invoke fails
            Console.WriteLine($"macOS Dialog Error: {ex.Message}");
            Console.WriteLine($"{title}: {message}");
        }
    }
}