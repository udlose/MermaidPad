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

// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides macOS-specific platform services and interop functionality for managed code, including methods for
/// interacting with Objective-C runtime APIs and displaying native dialogs.
/// </summary>
/// <remarks>This class is intended for use on macOS platforms and enables advanced interoperability scenarios
/// with native Objective-C APIs. It exposes methods for low-level messaging, class and selector retrieval, and native
/// dialog presentation. All functionality is restricted to macOS and may not be available or supported on other
/// operating systems.</remarks>
[SupportedOSPlatform("macos")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public sealed partial class MacPlatformServices : IPlatformServices
{
    /// <summary>
    /// Additional entropy for encryption to ensure application-specific encryption.
    /// </summary>
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("MermaidPad.AI.Encryption.v1");

    /// <summary>
    /// Retrieves a pointer to the Objective-C class definition for the specified class name.
    /// </summary>
    /// <remarks>This method is typically used when interoperating with Objective-C APIs from managed code.
    /// The returned pointer can be used with other Objective-C runtime functions to interact with the class.</remarks>
    /// <param name="className">The name of the Objective-C class to retrieve. Must not be null.</param>
    /// <returns>An <see cref="IntPtr"/> representing a pointer to the class definition, or <see cref="IntPtr.Zero"/> if the
    /// class does not exist.</returns>
    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string className);

    /// <summary>
    /// Registers a selector name with the Objective-C runtime and returns a pointer to the selector.
    /// </summary>
    /// <remarks>This method is typically used when interoperating with Objective-C APIs that require selector
    /// pointers. The returned pointer can be used in subsequent Objective-C method invocations.</remarks>
    /// <param name="selectorName">The name of the Objective-C selector to register. Cannot be null.</param>
    /// <returns>A pointer to the registered selector. Returns IntPtr.Zero if registration fails.</returns>
    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string selectorName);

    /// <summary>
    /// Invokes an Objective-C method on the specified target object using the given selector.
    /// </summary>
    /// <remarks>This method provides low-level access to Objective-C messaging and should be used with care.
    /// Incorrect usage may result in runtime errors or application instability. The caller is responsible for ensuring
    /// that the selector and target are valid and that the method signature matches the expected usage.</remarks>
    /// <param name="target">A pointer to the Objective-C object on which to invoke the method. Must not be zero.</param>
    /// <param name="selector">A pointer to the selector that identifies the method to invoke. Must not be zero.</param>
    /// <returns>A pointer to the result of the Objective-C method invocation. The meaning of the return value depends on the
    /// method called.</returns>
    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static partial IntPtr objc_msgSend(IntPtr target, IntPtr selector);

    /// <summary>
    /// Invokes an Objective-C method on the specified target object with a single argument using the Objective-C
    /// runtime messaging system.
    /// </summary>
    /// <remarks>This method provides low-level access to the Objective-C runtime and should be used with
    /// care. Incorrect usage may result in runtime errors or application instability. It is the caller's responsibility
    /// to ensure that the arguments and return value are correctly marshaled and that the selector matches the expected
    /// method signature.</remarks>
    /// <param name="target">A pointer to the Objective-C object that will receive the message. Must not be <c>IntPtr.Zero</c>.</param>
    /// <param name="selector">A pointer to the selector representing the method to invoke. Must correspond to a valid method of the target
    /// object.</param>
    /// <param name="arg1">A pointer to the argument to pass to the method. The meaning and type depend on the method being called.</param>
    /// <returns>A pointer representing the result of the Objective-C method invocation. The actual type and meaning depend on
    /// the method signature.</returns>
    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static partial IntPtr objc_msgSend(IntPtr target, IntPtr selector, IntPtr arg1);

    /// <summary>
    /// Invokes the specified Objective-C method on the given target object, passing a UTF-8 encoded string as an
    /// NSString parameter.
    /// </summary>
    /// <remarks>This method marshals the provided .NET string as a UTF-8 encoded NSString before passing it
    /// to the Objective-C runtime. The caller is responsible for ensuring that the target and selector pointers are
    /// valid and compatible with the expected method signature. Incorrect usage may result in runtime errors or
    /// undefined behavior.</remarks>
    /// <param name="target">A pointer to the Objective-C object that will receive the message.</param>
    /// <param name="selector">A pointer to the selector identifying the method to invoke on the target object.</param>
    /// <param name="utf8String">The string value to pass as an NSString argument to the Objective-C method. The string is marshalled as UTF-8.</param>
    /// <returns>A pointer to the result returned by the Objective-C method invocation. The meaning of the return value depends
    /// on the method being called.</returns>
    // For objc_msgSend with string parameter (creates NSString from UTF8)
    [LibraryImport("/System/Library/Frameworks/Foundation.framework/Foundation", EntryPoint = "objc_msgSend", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_msgSend_string(IntPtr target, IntPtr selector, string utf8String);

    /// <summary>
    /// Invokes an Objective-C method on the specified target with a single 64-bit integer argument, using the given
    /// selector.
    /// </summary>
    /// <remarks>This method provides low-level interop with Objective-C APIs and should be used with care.
    /// The caller is responsible for ensuring that the selector and argument types match the expected Objective-C
    /// method signature. Incorrect usage may result in runtime errors or application instability.</remarks>
    /// <param name="target">A pointer to the Objective-C object that will receive the message.</param>
    /// <param name="selector">A pointer to the selector identifying the method to invoke on the target object.</param>
    /// <param name="arg1">The 64-bit integer argument to pass to the Objective-C method.</param>
    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static partial void objc_msgSend_void(IntPtr target, IntPtr selector, long arg1);

    /// <summary>
    /// Shows a macOS NSAlert with the specified title and message.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="title"/> or <paramref name="message"/> is null or empty.</exception>
    public void ShowNativeDialog(string title, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(message);

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

            // Show modal dialog (blocks until dismissed)
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

    /// <summary>
    /// Encrypts a plaintext string using AES-GCM authenticated encryption with a machine-specific key.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt</param>
    /// <returns>Base64-encoded encrypted string containing nonce, tag, and ciphertext</returns>
    /// <exception cref="InvalidOperationException">Thrown if encryption fails</exception>
    public string EncryptString(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        try
        {
            byte[] key = GetMachineSpecificKey();
            byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = new byte[plaintextBytes.Length];

            RandomNumberGenerator.Fill(nonce);

            using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Combine nonce + tag + ciphertext
            byte[] result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to encrypt data using AES-GCM", ex);
        }
    }

    /// <summary>
    /// Decrypts an encrypted string using AES-GCM authenticated encryption with a machine-specific key.
    /// </summary>
    /// <param name="encrypted">The Base64-encoded encrypted string containing nonce, tag, and ciphertext</param>
    /// <returns>Decrypted plaintext string</returns>
    /// <exception cref="InvalidOperationException">Thrown if decryption fails</exception>
    public string DecryptString(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
            return string.Empty;

        try
        {
            byte[] combined = Convert.FromBase64String(encrypted);
            byte[] key = GetMachineSpecificKey();

            int nonceSize = AesGcm.NonceByteSizes.MaxSize;
            int tagSize = AesGcm.TagByteSizes.MaxSize;

            // Extract nonce, tag, and ciphertext
            byte[] nonce = new byte[nonceSize];
            byte[] tag = new byte[tagSize];
            byte[] ciphertext = new byte[combined.Length - nonceSize - tagSize];

            Buffer.BlockCopy(combined, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(combined, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(combined, nonceSize + tagSize, ciphertext, 0, ciphertext.Length);

            byte[] plaintext = new byte[ciphertext.Length];

            using var aesGcm = new AesGcm(key, tagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decrypt data using AES-GCM", ex);
        }
    }

    /// <summary>
    /// Generates a machine-specific encryption key using SHA256 hash of machine name, username, and additional entropy.
    /// </summary>
    /// <returns>256-bit encryption key</returns>
    private static byte[] GetMachineSpecificKey()
    {
        string entropy = $"{Environment.MachineName}:{Environment.UserName}:{Convert.ToBase64String(AdditionalEntropy)}";
        byte[] entropyBytes = Encoding.UTF8.GetBytes(entropy);
        return SHA256.HashData(entropyBytes);
    }
}
