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
/// Provides macOS-specific platform services and interop functionality for managed code, including methods for
/// interacting with Objective-C runtime APIs and displaying native dialogs.
/// </summary>
/// <remarks>This class is intended for use on macOS platforms and enables advanced interoperability scenarios
/// with native Objective-C APIs. It exposes methods for low-level messaging, class and selector retrieval, and native
/// dialog presentation. All functionality is restricted to macOS and may not be available or supported on other
/// operating systems.</remarks>
[SupportedOSPlatform("macos")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "Readability")]
public sealed partial class MacPlatformServices : IPlatformServices
{
    private const int KeyLength = 32; // 256 bits
    private readonly ILogger<MacPlatformServices> _logger;

    /// <summary>
    /// Additional entropy for encryption to ensure application-specific encryption.
    /// </summary>
    private static readonly byte[] _additionalEntropy = "MermaidPad.AI.Encryption.v1"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of the MacPlatformServices class using the specified logger.
    /// </summary>
    /// <param name="logger">The logger instance used to record diagnostic and operational messages for MacPlatformServices. Cannot be null.</param>
    public MacPlatformServices(ILogger<MacPlatformServices> logger)
    {
        _logger = logger;
    }

    #region Native Objective-C Interop

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

    #endregion Native Objective-C Interop

    #region Native Dialog

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
            _logger.LogError(ex, "Failed to show native macOS dialog");

            // Fallback to console if P/Invoke fails
            Console.WriteLine($"macOS Dialog Error: {ex.Message}");
            Console.WriteLine($"{title}: {message}");
        }
    }

    #endregion Native Dialog

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
    /// Retrieves a machine-specific cryptographic key for use in secure operations.
    /// </summary>
    /// <remarks>This method attempts to obtain a persistent key from the system keychain or a local file. If
    /// neither is available, it generates a new key and tries to persist it. As a last resort, it derives a
    /// deterministic key based on machine and user information. The returned key is intended for use in scenarios
    /// requiring machine-level uniqueness and should be handled securely to prevent leakage.</remarks>
    /// <returns>A byte array containing the machine-specific key. The caller is responsible for securely erasing the key from
    /// memory after use.</returns>
    private byte[] GetMachineSpecificKey()
    {
        const string serviceName = "MermaidPad Machine Key";

        // 1) Try Keychain
        if (TryGetKeyFromKeychain(serviceName, out byte[]? keyFromKeychain))
        {
            return keyFromKeychain; // caller must zero
        }

        // 2) Try file (existing)
        string configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MermaidPad");
        string keyPath = Path.Combine(configDir, "machine_key.bin");

        if (TryGetKeyFromFile(keyPath, out byte[] keyFromFile))
        {
            return keyFromFile; // caller must zero
        }

        // 3) Generate a new key and try to persist (keychain first, then file)
        byte[] newKey = new byte[KeyLength];
        RandomNumberGenerator.Fill(newKey);

        if (TryStoreKeyInKeychain(serviceName, Environment.UserName, newKey) ||
            TryStoreKeyToFile(keyPath, newKey))
        {
            return newKey; // caller must zero
        }

        // 4) Last-resort deterministic fallback (zero temporary key first)
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

    /// <summary>
    /// Attempts to retrieve a cryptographic key associated with the specified service name from the macOS Keychain.
    /// </summary>
    /// <remarks>The returned key is expected to be in base64 format and must match the required key length.
    /// If the key is not found or is invalid, the out parameter is set to an empty array. This method does not throw
    /// exceptions; errors are logged and result in a return value of false.</remarks>
    /// <param name="serviceName">The name of the service for which to retrieve the key from the Keychain. Cannot be null or empty.</param>
    /// <param name="key">When this method returns, contains the key as a byte array if found and valid; otherwise, an empty array.</param>
    /// <returns>true if a valid key was found and retrieved from the Keychain; otherwise, false.</returns>
    private bool TryGetKeyFromKeychain(string serviceName, out byte[] key)
    {
        // ensure out param always assigned (empty array used when not found)
        key = Array.Empty<byte>();
        try
        {
            ProcessStartInfo findInfo = new ProcessStartInfo
            {
                FileName = "security",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            findInfo.ArgumentList.Add("find-generic-password");
            findInfo.ArgumentList.Add("-s");
            findInfo.ArgumentList.Add(serviceName);
            findInfo.ArgumentList.Add("-w");

            using Process? find = Process.Start(findInfo);
            if (find is null)
            {
                return false;
            }

            string output = find.StandardOutput.ReadToEnd();
            find.WaitForExit();
            if (find.ExitCode != 0)
            {
                return false;
            }

            string storedBase64 = output.Trim();
            if (string.IsNullOrEmpty(storedBase64))
            {
                return false;
            }

            try
            {
                byte[] existing = Convert.FromBase64String(storedBase64);
                if (existing.Length == KeyLength)
                {
                    key = existing;
                    return true;
                }

                CryptographicOperations.ZeroMemory(existing);
            }
            catch (Exception ex)
            {
                // ignore parse/format errors
                _logger.LogError(ex, "Failed to parse key from Keychain");
            }
        }
        catch (Exception ex)
        {
            // swallow and return false to allow fallback
            _logger.LogError(ex, "Failed to retrieve key from Keychain");
        }

        return false;
    }

    /// <summary>
    /// Attempts to store the specified key in the macOS Keychain for the given service and account.
    /// </summary>
    /// <remarks>This method uses the macOS 'security' command-line tool to add or update a generic password
    /// entry in the Keychain. If the operation fails, the method returns false and logs the error. This method is
    /// intended for use on macOS systems; it will not function on other platforms.</remarks>
    /// <param name="serviceName">The name of the service under which the key will be stored in the Keychain. Cannot be null or empty.</param>
    /// <param name="account">The account identifier associated with the key. Cannot be null or empty.</param>
    /// <param name="key">The key to store, as a byte array. Cannot be null or empty.</param>
    /// <returns>true if the key was successfully stored in the Keychain; otherwise, false.</returns>
    private bool TryStoreKeyInKeychain(string serviceName, string account, byte[] key)
    {
        try
        {
            string base64 = Convert.ToBase64String(key);
            ProcessStartInfo addInfo = new ProcessStartInfo
            {
                FileName = "security",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            addInfo.ArgumentList.Add("add-generic-password");
            addInfo.ArgumentList.Add("-s");
            addInfo.ArgumentList.Add(serviceName);
            addInfo.ArgumentList.Add("-a");
            addInfo.ArgumentList.Add(account);
            addInfo.ArgumentList.Add("-w");
            addInfo.ArgumentList.Add(base64);
            addInfo.ArgumentList.Add("-U"); // update if exists

            using Process? add = Process.Start(addInfo);
            if (add is null)
            {
                return false;
            }

            add.WaitForExit();
            return add.ExitCode == 0;
        }
        catch (Exception ex)
        {
            // swallow and return false to allow fallback
            _logger.LogError(ex, "Failed to store key in Keychain");
            return false;
        }
    }

    /// <summary>
    /// Attempts to retrieve a cryptographic key from the specified file path.
    /// </summary>
    /// <remarks>If the file does not exist, is inaccessible, or does not contain a key of the required
    /// length, the method returns false and the out parameter is set to an empty array. Errors encountered during file
    /// access are logged but do not throw exceptions.</remarks>
    /// <param name="keyPath">The path to the file containing the key. The file must exist and contain a key of the expected length.</param>
    /// <param name="key">When this method returns, contains the key as a byte array if retrieval was successful; otherwise, contains an
    /// empty array.</param>
    /// <returns>true if a key of the expected length was successfully read from the file; otherwise, false.</returns>
    private bool TryGetKeyFromFile(string keyPath, out byte[] key)
    {
        // ensure out param always assigned (empty array used when not found)
        key = Array.Empty<byte>();
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
            // ignore errors and return false
            _logger.LogError(ex, "Failed to retrieve key from file");
        }

        return false;
    }

    /// <summary>
    /// Attempts to securely write the specified encryption key to a file at the given path.
    /// </summary>
    /// <remarks>The method writes the key to a temporary file and then atomically moves it to the target
    /// location to reduce the risk of partial writes. On supported platforms, file permissions are restricted to allow
    /// only user read and write access. If an error occurs during the process, the method logs the error and returns
    /// false.</remarks>
    /// <param name="keyPath">The full file path where the encryption key should be stored. If the directory does not exist, it will be
    /// created.</param>
    /// <param name="key">The encryption key data to write to the file.</param>
    /// <returns>true if the key was successfully written to the file; otherwise, false.</returns>
    private bool TryStoreKeyToFile(string keyPath, byte[] key)
    {
        try
        {
            string configDir = Path.GetDirectoryName(keyPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            string tempPath = keyPath + ".tmp";
            File.WriteAllBytes(tempPath, key);

            try
            {
                // Restrict permissions where supported (POSIX)
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // ignore on unsupported platforms
            }

            File.Move(tempPath, keyPath, true);

            try
            {
                File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // ignore
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store encryption key to file");
            return false;
        }
    }

    #endregion AES-GCM Encryption/Decryption
}
