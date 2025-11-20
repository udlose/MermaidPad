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

using Microsoft.Extensions.Logging;

namespace MermaidPad.Services.Platforms;

/// <summary>
/// Defines methods for encrypting, decrypting, and validating strings using platform-specific secure storage
/// mechanisms.
/// </summary>
/// <remarks>Implementations of this interface provide secure handling of sensitive data by leveraging encryption
/// and decryption operations that are tailored to the underlying platform. This interface is intended for scenarios
/// where confidential information, such as credentials or tokens, must be protected at rest. The actual security
/// guarantees and supported features may vary depending on the platform and implementation.</remarks>
public interface ISecureStorageService
{
    /// <summary>
    /// Encrypts the specified plaintext string and returns the encrypted result as a string.
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt. Cannot be null or empty.</param>
    /// <returns>A string containing the encrypted representation of the input plaintext.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="plaintext"/> is null or empty.</exception>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts the specified encrypted string and returns the original plaintext value.
    /// </summary>
    /// <param name="encrypted">The encrypted string to be decrypted. Cannot be null or empty.</param>
    /// <returns>The decrypted plaintext string corresponding to the input.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encrypted"/> is null or empty.</exception>
    string Decrypt(string encrypted);

    /// <summary>
    /// Determines whether the specified encrypted string is valid according to the expected format or criteria.
    /// </summary>
    /// <param name="encrypted">The encrypted string to validate. Cannot be null or empty.</param>
    /// <returns>true if the encrypted string is valid; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encrypted"/> is null or empty.</exception>
    bool IsValid(string encrypted);
}

/// <summary>
/// Secure storage implementation that delegates to platform-specific services.
/// - Windows: DPAPI (Data Protection API) - Best practice for Windows desktop apps
/// - Linux/macOS: AES-GCM authenticated encryption - Modern, secure, and cross-platform
/// </summary>
public sealed class SecureStorageService : ISecureStorageService
{
    private readonly ILogger<SecureStorageService> _logger;
    private readonly IPlatformServices _platformServices;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureStorageService"/> class.
    /// </summary>
    /// <param name="logger">Logger for the service.</param>
    /// <param name="platformServices">Platform-specific services for encryption/decryption</param>
    public SecureStorageService(ILogger<SecureStorageService> logger, IPlatformServices platformServices)
    {
        _platformServices = platformServices;
        _logger = logger;
    }

    /// <summary>
    /// Encrypts the specified plaintext string and returns the encrypted result.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt. Cannot be null or empty.</param>
    /// <returns>A string containing the encrypted representation of the input plaintext.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="plaintext"/> is null or empty.</exception>
    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        try
        {
            return _platformServices.EncryptString(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting string.");
            throw;
        }
    }

    /// <summary>
    /// Decrypts the specified encrypted string and returns the original plaintext value.
    /// </summary>
    /// <param name="encrypted">The encrypted string to be decrypted. Cannot be null or empty.</param>
    /// <returns>The decrypted plaintext string corresponding to the input.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encrypted"/> is null or empty.</exception>
    public string Decrypt(string encrypted)
    {
        ArgumentException.ThrowIfNullOrEmpty(encrypted);

        try
        {
            return _platformServices.DecryptString(encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting string.");
            throw;
        }
    }

    /// <summary>
    /// Determines whether the specified encrypted string can be successfully decrypted and is not empty.
    /// </summary>
    /// <param name="encrypted">The encrypted string to validate. Cannot be null or empty.</param>
    /// <returns>true if the encrypted string can be decrypted to a non-empty value; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encrypted"/> is null or empty.</exception>
    public bool IsValid(string encrypted)
    {
        ArgumentException.ThrowIfNullOrEmpty(encrypted);

        try
        {
            string decrypted = Decrypt(encrypted);
            return !string.IsNullOrEmpty(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating encrypted string.");
            return false;
        }
    }
}
