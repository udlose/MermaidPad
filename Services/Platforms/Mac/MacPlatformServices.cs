// Replace Services/Platforms/Mac/MacPlatformServices.cs with this

// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

[SupportedOSPlatform("macos")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
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

    // For objc_msgSend with string parameter (creates NSString from UTF8)
    [LibraryImport("/System/Library/Frameworks/Foundation.framework/Foundation", EntryPoint = "objc_msgSend", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_msgSend_string(IntPtr target, IntPtr selector, string utf8String);

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
            // Get required classes and selectors
            IntPtr alertClass = objc_getClass("NSAlert");
            IntPtr nsStringClass = objc_getClass("NSString");

            IntPtr allocSelector = sel_registerName("alloc");
            IntPtr initSelector = sel_registerName("init");
            IntPtr runModalSelector = sel_registerName("runModal");
            IntPtr setMessageTextSelector = sel_registerName("setMessageText:");
            IntPtr setInformativeTextSelector = sel_registerName("setInformativeText:");
            IntPtr setAlertStyleSelector = sel_registerName("setAlertStyle:");
            IntPtr stringWithUTF8Selector = sel_registerName("stringWithUTF8String:");
            IntPtr releaseSelector = sel_registerName("release");

            // Create NSString objects from C# strings
            IntPtr titleNSString = objc_msgSend_string(nsStringClass, stringWithUTF8Selector, title);
            IntPtr messageNSString = objc_msgSend_string(nsStringClass, stringWithUTF8Selector, message);

            // Create NSAlert instance
            IntPtr alert = objc_msgSend(alertClass, allocSelector);
            alert = objc_msgSend(alert, initSelector);

            // Set alert style to critical (2 = NSAlertStyleCritical)
            objc_msgSend_void(alert, setAlertStyleSelector, 2);

            // Set title and message using NSString objects
            objc_msgSend(alert, setMessageTextSelector, titleNSString);
            objc_msgSend(alert, setInformativeTextSelector, messageNSString);

            // Show modal dialog
            objc_msgSend(alert, runModalSelector);

            // Clean up - release the alert (NSStrings created with stringWithUTF8String are auto-released)
            objc_msgSend(alert, releaseSelector);
        }
        catch (Exception ex)
        {
            // Fallback to console if P/Invoke fails
            Console.WriteLine($"macOS Dialog Error: {ex.Message}");
            Console.WriteLine($"{title}: {message}");
        }
    }
}