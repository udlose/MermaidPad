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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides Windows-specific platform services, including the ability to display native MessageBox dialogs.
/// </summary>
/// <remarks>This class is intended for use on Windows operating systems and exposes functionality that interacts
/// directly with Windows APIs. It implements the <see cref="IPlatformServices"/> interface to provide
/// platform-dependent features for Windows environments. All members are supported only on Windows; attempting to use
/// them on other platforms will result in a platform compatibility error.</remarks>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Win32 API naming conventions")]
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatformServices : IPlatformServices
{
    /// <summary>
    /// Additional entropy for Windows DPAPI encryption to ensure application-specific encryption.
    /// </summary>
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("MermaidPad.AI.Encryption.v1");

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
    [SuppressMessage("Minor Code Smell", "S1905:Redundant casts should not be used", Justification = "Clarifies intent of conversion to IntPtr")]
    public void ShowNativeDialog(string title, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(message);

        // Make the message box modal to the main application window if possible by passing its handle
        // Even though the cast is redundant, it clarifies the intent that we are converting to IntPtr
        IntPtr mainWindowHandle = (IntPtr)Process.GetCurrentProcess().MainWindowHandle;
        MessageBox(mainWindowHandle, message, title, MB_OK | MB_ICONERROR);
    }

    /// <summary>
    /// Encrypts a plaintext string using Windows DPAPI (Data Protection API).
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt</param>
    /// <returns>Base64-encoded encrypted string</returns>
    /// <exception cref="InvalidOperationException">Thrown if encryption fails</exception>
    public string EncryptString(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        try
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] encryptedBytes = ProtectedData.Protect(
                plaintextBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to encrypt data using Windows DPAPI", ex);
        }
    }

    /// <summary>
    /// Decrypts an encrypted string using Windows DPAPI (Data Protection API).
    /// </summary>
    /// <param name="encrypted">The Base64-encoded encrypted string</param>
    /// <returns>Decrypted plaintext string</returns>
    /// <exception cref="InvalidOperationException">Thrown if decryption fails</exception>
    public string DecryptString(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
            return string.Empty;

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encrypted);
            byte[] plaintextBytes = ProtectedData.Unprotect(
                encryptedBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decrypt data using Windows DPAPI", ex);
        }
    }
}
