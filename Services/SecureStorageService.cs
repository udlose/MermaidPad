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

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MermaidPad.Services;

/// <summary>
/// Provides secure storage for sensitive data like API keys using platform-specific encryption.
/// </summary>
public interface ISecureStorageService
{
    /// <summary>
    /// Encrypts a plaintext string using platform-specific secure storage.
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts an encrypted string using platform-specific secure storage.
    /// </summary>
    string Decrypt(string encrypted);

    /// <summary>
    /// Checks if the encrypted string is valid and can be decrypted.
    /// </summary>
    bool IsValid(string encrypted);
}

/// <summary>
/// Secure storage implementation using platform-specific encryption.
/// - Windows: DPAPI (Data Protection API)
/// - Linux/macOS: AES with machine-specific entropy
/// </summary>
public sealed class SecureStorageService : ISecureStorageService
{
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("MermaidPad.AI.Encryption.v1");

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return EncryptWindows(plaintext);
            }
            else
            {
                // Linux and macOS: Use AES encryption with machine-specific key
                return EncryptCrossPlatform(plaintext);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to encrypt data", ex);
        }
    }

    public string Decrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
            return string.Empty;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DecryptWindows(encrypted);
            }
            else
            {
                return DecryptCrossPlatform(encrypted);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decrypt data", ex);
        }
    }

    public bool IsValid(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
            return false;

        try
        {
            var decrypted = Decrypt(encrypted);
            return !string.IsNullOrEmpty(decrypted);
        }
        catch
        {
            return false;
        }
    }

    private static string EncryptWindows(string plaintext)
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] encryptedBytes = ProtectedData.Protect(
            plaintextBytes,
            AdditionalEntropy,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    private static string DecryptWindows(string encrypted)
    {
        byte[] encryptedBytes = Convert.FromBase64String(encrypted);
        byte[] plaintextBytes = ProtectedData.Unprotect(
            encryptedBytes,
            AdditionalEntropy,
            DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static string EncryptCrossPlatform(string plaintext)
    {
        using var aes = Aes.Create();

        // Derive key from machine-specific information
        var machineKey = GetMachineSpecificKey();
        aes.Key = machineKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Combine IV and encrypted data
        byte[] result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    private static string DecryptCrossPlatform(string encrypted)
    {
        byte[] combined = Convert.FromBase64String(encrypted);

        using var aes = Aes.Create();

        // Derive key from machine-specific information
        var machineKey = GetMachineSpecificKey();
        aes.Key = machineKey;

        // Extract IV
        byte[] iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
        aes.IV = iv;

        // Extract encrypted data
        byte[] encryptedBytes = new byte[combined.Length - iv.Length];
        Buffer.BlockCopy(combined, iv.Length, encryptedBytes, 0, encryptedBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        byte[] plaintextBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] GetMachineSpecificKey()
    {
        // Use machine name and user as entropy source
        var entropy = $"{Environment.MachineName}:{Environment.UserName}:{AdditionalEntropy}";
        var entropyBytes = Encoding.UTF8.GetBytes(entropy);

        // Derive a 256-bit key using SHA256
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(entropyBytes);
    }
}
