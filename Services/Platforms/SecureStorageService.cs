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

namespace MermaidPad.Services.Platforms;

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
/// Secure storage implementation that delegates to platform-specific services.
/// - Windows: DPAPI (Data Protection API) - Best practice for Windows desktop apps
/// - Linux/macOS: AES-GCM authenticated encryption - Modern, secure, and cross-platform
/// </summary>
public sealed class SecureStorageService : ISecureStorageService
{
    private readonly IPlatformServices _platformServices;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureStorageService"/> class.
    /// </summary>
    /// <param name="platformServices">Platform-specific services for encryption/decryption</param>
    public SecureStorageService(IPlatformServices platformServices)
    {
        _platformServices = platformServices ?? throw new ArgumentNullException(nameof(platformServices));
    }

    /// <summary>
    /// Encrypts a plaintext string using platform-specific secure storage.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt</param>
    /// <returns>Base64-encoded encrypted string</returns>
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        return _platformServices.EncryptString(plaintext);
    }

    /// <summary>
    /// Decrypts an encrypted string using platform-specific secure storage.
    /// </summary>
    /// <param name="encrypted">The Base64-encoded encrypted string</param>
    /// <returns>Decrypted plaintext string</returns>
    public string Decrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
            return string.Empty;

        return _platformServices.DecryptString(encrypted);
    }

    /// <summary>
    /// Checks if the encrypted string is valid and can be decrypted.
    /// </summary>
    /// <param name="encrypted">The encrypted string to validate</param>
    /// <returns>True if the string can be decrypted; otherwise, false</returns>
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
}
