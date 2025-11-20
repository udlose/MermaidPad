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
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides Linux-specific platform services, including displaying native dialogs using available graphical tools or
/// falling back to console output when necessary.
/// </summary>
/// <remarks>This class is intended for use on Linux systems and is marked with the <see
/// cref="SupportedOSPlatformAttribute"/> for "linux". It attempts to use common graphical dialog utilities such as
/// zenity, kdialog, yad, Xdialog, or gxmessage if a graphical environment is detected. If no graphical environment is
/// available or no supported dialog tool is found, dialogs are displayed in the console. This class is sealed and
/// cannot be inherited.</remarks>
[SupportedOSPlatform("linux")]
public sealed class LinuxPlatformServices : IPlatformServices
{
    private const int KeyLength = 32; // 256 bits
    private readonly ILogger<LinuxPlatformServices> _logger;

    /// <summary>
    /// Additional entropy for encryption to ensure application-specific encryption.
    /// </summary>
    private static readonly byte[] _additionalEntropy = "MermaidPad.AI.Encryption.v1"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of the LinuxPlatformServices class using the specified logger.
    /// </summary>
    /// <param name="logger">The logger instance used to record diagnostic and operational messages for Linux platform services. Cannot be
    /// null.</param>
    public LinuxPlatformServices(ILogger<LinuxPlatformServices> logger)
    {
        _logger = logger;
    }

    #region Native Dialogs

    /// <summary>
    /// Shows a native Linux dialog using zenity, kdialog, yad, Xdialog, or gxmessage if available, otherwise falls back to console output.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="title"/> or <paramref name="message"/> is null or empty.</exception>
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

            if (TryShowYadDialog(title, message))
            {
                return;
            }

            if (TryShowXDialogDialog(title, message))
            {
                return;
            }

            if (TryShowGxmessageDialog(title, message))
            {
                return;
            }
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
    private bool TryShowZenityDialog(string title, string message)
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
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--error");
            startInfo.ArgumentList.Add($"--title={title}");
            startInfo.ArgumentList.Add($"--text={message}");

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show zenity dialog");
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
    private bool TryShowKDialogDialog(string title, string message)
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
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--error");
            startInfo.ArgumentList.Add("--title");
            startInfo.ArgumentList.Add(title);
            startInfo.ArgumentList.Add(message);

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show kdialog dialog");
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
    private bool TryShowYadDialog(string title, string message)
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
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add($"--title={title}");
            startInfo.ArgumentList.Add($"--text={message}");
            startInfo.ArgumentList.Add("--button=OK");

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show yad dialog");
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
    private bool TryShowXDialogDialog(string title, string message)
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
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--msgbox");
            startInfo.ArgumentList.Add(message);
            startInfo.ArgumentList.Add("10");
            startInfo.ArgumentList.Add("40");
            startInfo.ArgumentList.Add("--title");
            startInfo.ArgumentList.Add(title);

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show Xdialog dialog");
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
    private bool TryShowGxmessageDialog(string title, string message)
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
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-title");
            startInfo.ArgumentList.Add(title);
            startInfo.ArgumentList.Add(message);

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show gxmessage dialog");
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
    private bool IsToolAvailable(string toolName)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "which",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(toolName);

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check tool availability");
            return false;
        }
    }

    /// <summary>
    /// Shows a dialog in the console as a fallback if no GUI dialog tools are available.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    private void ShowConsoleDialog(string title, string message)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read console input for dialog");

            // In case console input is not available
            Thread.Sleep(3_000);
        }
    }

    #endregion Native Dialogs

    #region AES-GCM Encryption/Decryption

    /// <summary>
    /// Encrypts the specified plaintext string using AES-GCM and returns the encrypted data as a Base64-encoded string.
    /// </summary>
    /// <remarks>The encryption uses a machine-specific key and generates a unique nonce for each operation.
    /// The output combines the nonce, authentication tag, and ciphertext, all encoded in Base64. The method is not
    /// intended for cross-machine decryption, as the key is specific to the current machine.</remarks>
    /// <param name="plaintext">The plaintext string to encrypt. If null or empty, the method throws an exception.</param>
    /// <returns>A Base64-encoded string containing the encrypted representation of the input plaintext
    /// or throws an exception if encryption fails.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the encryption operation fails due to an internal error.</exception>
    /// <exception cref="ArgumentException">Thrown when the input plaintext is null or empty.</exception>
    [SuppressMessage("Style", "IDE0011:Add braces", Justification = "Compact style for single-line statements")]
    public string EncryptString(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        byte[]? key = null;
        byte[]? nonce = null;
        byte[]? tag = null;
        byte[]? plaintextBytes = null;
        byte[]? ciphertext = null;
        byte[]? result = null;
        try
        {
            key = GetMachineSpecificKey();
            nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            tag = new byte[AesGcm.TagByteSizes.MaxSize];
            plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            ciphertext = new byte[plaintextBytes.Length];

            RandomNumberGenerator.Fill(nonce);

            using AesGcm aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Combine nonce + tag + ciphertext
            result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed");
            throw new InvalidOperationException("Failed to encrypt data using AES-GCM", ex);
        }
        finally
        {
            // Zero out sensitive data
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (plaintextBytes is not null) CryptographicOperations.ZeroMemory(plaintextBytes);
            if (ciphertext is not null) CryptographicOperations.ZeroMemory(ciphertext);
            if (result is not null) CryptographicOperations.ZeroMemory(result);
            if (nonce is not null) CryptographicOperations.ZeroMemory(nonce);
            if (tag is not null) CryptographicOperations.ZeroMemory(tag);
        }
    }

    /// <summary>
    /// Decrypts a Base64-encoded string that was encrypted using AES-GCM with a machine-specific key.
    /// </summary>
    /// <remarks>The decryption uses a key derived from the current machine. Only data encrypted on the same
    /// machine with the corresponding method can be successfully decrypted. This method is not suitable for
    /// cross-machine decryption scenarios.</remarks>
    /// <param name="encrypted">A Base64-encoded string containing the encrypted data, including nonce and authentication tag.
    /// Cannot be null or empty.</param>
    /// <returns>The decrypted plaintext string, or throws an exception if decryption fails.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the input cannot be decrypted using AES-GCM,
    /// such as if the data is corrupted or the key is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when the input is null or empty.</exception>
    [SuppressMessage("Style", "IDE0011:Add braces", Justification = "Compact style for single-line statements")]
    public string DecryptString(string encrypted)
    {
        ArgumentException.ThrowIfNullOrEmpty(encrypted);

        byte[]? combined = null;
        byte[]? key = null;
        byte[]? nonce = null;
        byte[]? tag = null;
        byte[]? ciphertext = null;
        byte[]? plaintext = null;
        try
        {
            combined = Convert.FromBase64String(encrypted);
            key = GetMachineSpecificKey();

            int nonceSize = AesGcm.NonceByteSizes.MaxSize;
            int tagSize = AesGcm.TagByteSizes.MaxSize;

            // Extract nonce, tag, and ciphertext
            nonce = new byte[nonceSize];
            tag = new byte[tagSize];

            if (combined.Length < nonceSize + tagSize)
            {
                throw new InvalidOperationException("Invalid encrypted data format");
            }

            ciphertext = new byte[combined.Length - nonceSize - tagSize];

            Buffer.BlockCopy(combined, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(combined, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(combined, nonceSize + tagSize, ciphertext, 0, ciphertext.Length);

            plaintext = new byte[ciphertext.Length];

            using AesGcm aesGcm = new AesGcm(key, tagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed");
            throw new InvalidOperationException("Failed to decrypt data using AES-GCM", ex);
        }
        finally
        {
            // Zero out sensitive data
            if (combined is not null) CryptographicOperations.ZeroMemory(combined);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (ciphertext is not null) CryptographicOperations.ZeroMemory(ciphertext);
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
            if (nonce is not null) CryptographicOperations.ZeroMemory(nonce);
            if (tag is not null) CryptographicOperations.ZeroMemory(tag);
        }
    }

    /// <summary>
    /// Retrieves a 256-bit machine-specific cryptographic key, persisting it securely to disk if necessary. If secure
    /// storage is unavailable, derives a fallback key based on machine and user information.
    /// </summary>
    /// <remarks>The key is stored in the user's application data directory and is intended to be unique and
    /// stable for the current machine and user. If the persisted key file is missing or invalid, a new key is generated
    /// and saved. If file operations fail, a deterministic key is derived from machine and user identifiers, which may
    /// be less secure. Callers are responsible for zeroing the returned key when it is no longer needed to prevent
    /// sensitive data from remaining in memory.</remarks>
    /// <returns>A byte array containing the 32-byte (256-bit) machine-specific key. The same key is returned on subsequent calls
    /// unless the key file is deleted or corrupted.</returns>
    [SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Purposely zeroed in finally block for security")]
    private byte[] GetMachineSpecificKey()
    {
        string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MermaidPad");
        string keyPath = Path.Combine(configDir, "machine_key.bin");

        // 1) Try read existing persisted key
        if (TryGetKeyFromFile(keyPath, out byte[]? existing) && existing is not null)
        {
            return existing; // caller must zero
        }

        // 2) Generate new key and attempt to persist
        byte[] newKey = new byte[KeyLength];
        RandomNumberGenerator.Fill(newKey);

        if (TryStoreKeyToFile(keyPath, newKey))
        {
            return newKey; // caller must zero
        }

        // 3) Persistence failed — zero generated key and fall back to deterministic derivation
        CryptographicOperations.ZeroMemory(newKey);

        string entropy = $"{Environment.MachineName}:{Environment.UserName}:{Convert.ToBase64String(_additionalEntropy)}";
        byte[] entropyBytes = Encoding.UTF8.GetBytes(entropy);
        try
        {
            return SHA256.HashData(entropyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropyBytes);
        }
    }

    private bool TryGetKeyFromFile(string keyPath, out byte[]? key)
    {
        key = null;
        try
        {
            if (!File.Exists(keyPath))
            {
                return false;
            }

            byte[] existing = File.ReadAllBytes(keyPath);
            if (existing.Length == KeyLength)
            {
                key = existing;
                return true;
            }

            CryptographicOperations.ZeroMemory(existing);
        }
        catch (Exception ex)
        {
            // Ignore and allow fallback
            _logger.LogError(ex, "Failed to read machine-specific key file");
        }
        return false;
    }

    private bool TryStoreKeyToFile(string keyPath, byte[] key)
    {
        try
        {
            string? configDir = Path.GetDirectoryName(keyPath);
            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            string tempPath = keyPath + ".tmp";
            File.WriteAllBytes(tempPath, key);

            try
            {
                // Attempt to restrict permissions to user read/write only (POSIX).
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // ignore on unsupported platforms
            }

            File.Move(tempPath, keyPath, overwrite: true);

            try
            {
                // Ensure final file has safe permissions where supported.
                File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Ignore on platforms that don't support Unix file modes.
            }

            return true;
        }
        catch (Exception ex)
        {
            // Log at caller site if desired; return false to trigger fallback
            _logger.LogError(ex, "Failed to create or read machine-specific key file");
            return false;
        }
    }

    #endregion AES-GCM Encryption/Decryption
}
