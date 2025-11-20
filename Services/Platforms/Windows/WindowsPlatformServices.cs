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
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<WindowsPlatformServices> _logger;

    /// <summary>
    /// Additional entropy for Windows DPAPI encryption to ensure application-specific encryption.
    /// </summary>
    private static readonly byte[] _additionalEntropy = "MermaidPad.AI.Encryption.v1"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of the WindowsPlatformServices class using the specified logger.
    /// </summary>
    /// <param name="logger">The logger instance used to record diagnostic and operational messages for WindowsPlatformServices. Cannot be
    /// null.</param>
    public WindowsPlatformServices(ILogger<WindowsPlatformServices> logger)
    {
        _logger = logger;
    }

    #region P/Invoke Declarations

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

    #endregion P/Invoke Declarations

    #region Native Dialog

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

    #endregion Native Dialog

    #region DPAPI Encryption/Decryption

    /// <summary>
    /// Encrypts the specified plaintext string using Windows Data Protection API (DPAPI) for the current user.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt. Cannot be null or empty.</param>
    /// <returns>A Base64-encoded string containing the encrypted data.</returns>
    /// <remarks>
    /// <para>
    /// DPAPI is used on Windows instead of AES-GCM (which is used on Linux/macOS) because it provides
    /// significantly stronger security through deep integration with the Windows security infrastructure.
    /// </para>
    /// <para><strong>Key advantages of DPAPI over AES-GCM on Windows:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Hardware Security:</strong> On systems with TPM (Trusted Platform Module),
    /// encryption keys can be hardware-backed and never exposed to application memory, providing protection
    /// against memory dumps and debugging attacks.</description></item>
    /// <item><description><strong>True Machine Binding:</strong> Keys are derived from the Windows machine SID
    /// (Security Identifier) and hardware characteristics, not just hostname (which can be spoofed). Data
    /// encrypted on one machine cannot be decrypted on another, even by the same user.</description></item>
    /// <item><description><strong>User Credential Integration:</strong> Encryption is tied to the user's Windows
    /// login credentials. When a user changes their password, Windows automatically re-encrypts master keys.</description></item>
    /// <item><description><strong>Kernel-Level Protection:</strong> DPAPI operates through the LSA (Local Security
    /// Authority) in kernel space, providing protection from user-mode attacks like process memory scraping.</description></item>
    /// <item><description><strong>Enterprise Features:</strong> Supports Active Directory integration, credential
    /// roaming across domain machines, centralized key escrow, and auditing through Windows Security logs.</description></item>
    /// <item><description><strong>Automatic Key Management:</strong> Windows handles key rotation, master key
    /// encryption, and backup automatically without requiring application-level code.</description></item>
    /// </list>
    /// <para>
    /// In contrast, AES-GCM (used on Linux/macOS) derives keys from environment variables (machine name, username)
    /// which are less secure and easier to spoof. While AES-GCM provides excellent authenticated encryption, it
    /// operates in user space and cannot leverage hardware security modules or OS-level credential management.
    /// </para>
    /// <para>
    /// For desktop applications storing sensitive data like API keys, DPAPI remains the industry-standard best
    /// practice on Windows, as recommended by Microsoft security guidelines.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the encryption operation fails due to an error with DPAPI.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="plaintext"/> is null or empty.</exception>
    public string EncryptString(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        try
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] encryptedBytes = ProtectedData.Protect(
                plaintextBytes,
                _additionalEntropy,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data with DPAPI");
            throw new InvalidOperationException("Failed to encrypt data using Windows DPAPI", ex);
        }
    }

    /// <summary>
    /// Decrypts a string that was previously encrypted using Windows Data Protection API (DPAPI) and returns the
    /// original plaintext.
    /// </summary>
    /// <remarks>This method uses the current user's data protection scope. Only the user account that
    /// performed the original encryption can successfully decrypt the string. Ensure that the input was encrypted using
    /// the same entropy and scope.</remarks>
    /// <param name="encrypted">The encrypted string to decrypt. Must be a valid Base64-encoded value
    /// produced by the corresponding encryption method. Cannot be null or empty.</param>
    /// <returns>The decrypted plaintext string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the decryption operation fails,
    /// such as when the input is not a valid encrypted value or the protected data cannot be unprotected.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="encrypted"/> is null or empty.</exception>
    public string DecryptString(string encrypted)
    {
        ArgumentException.ThrowIfNullOrEmpty(encrypted);

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encrypted);
            byte[] plaintextBytes = ProtectedData.Unprotect(
                encryptedBytes,
                _additionalEntropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data with DPAPI");
            throw new InvalidOperationException("Failed to decrypt data using Windows DPAPI", ex);
        }
    }

    #endregion DPAPI Encryption/Decryption
}
