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

using MermaidPad.Extensions;
using MermaidPad.Generated;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace MermaidPad.Services.Platforms;

/// <summary>
/// Provides methods for verifying the integrity of assets, including embedded resources and files on disk, as well as
/// validating the content of JavaScript and HTML files.
/// </summary>
/// <remarks>This service includes functionality for computing and verifying SHA-256 hashes, ensuring the
/// integrity of assets during runtime. It also provides basic validation for JavaScript and HTML content to detect
/// potential issues. The methods are designed for internal use and assume that inputs are pre-validated where
/// applicable.</remarks>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Class is a singleton by design with lifetime controlled by DI")]
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "Class is a singleton by design with lifetime controlled by DI")]
public sealed class AssetIntegrityService
{
    private readonly ILogger<AssetIntegrityService> _logger;
    private readonly SecurityService _securityService;

    /// <summary>
    /// Initializes a new instance of the AssetIntegrityService class with the specified logger and security service.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the service.</param>
    /// <param name="securityService">The security service used to perform authentication and authorization operations within the service.</param>
    public AssetIntegrityService(ILogger<AssetIntegrityService> logger, SecurityService securityService)
    {
        _logger = logger;
        _securityService = securityService;
    }

    private const int StackAllocThreshold = 8_192;  // 8KB threshold for stack vs heap allocation
    private const int HashPreviewLength = 8;        // Number of characters to show in hash previews

    /// <summary>
    /// Provides a set of string patterns commonly used to identify JavaScript code constructs.
    /// </summary>
    /// <remarks>This collection includes keywords and symbols such as "function", "const ", "var ", "let ",
    /// "=>", "class ", "import ", and "export ", which are typically found in JavaScript source code. The patterns are
    /// compared using ordinal string comparison for performance and accuracy.</remarks>
    private static readonly SearchValues<string> _jsPatterns = SearchValues.Create(
    [
        "function", "const ", "var ", "let ", "=>", "class ", "import ", "export "
    ], StringComparison.Ordinal);

    /// <summary>
    /// Provides a set of string patterns used to identify potentially suspicious or unsafe content in input data.
    /// </summary>
    /// <remarks>The patterns include common indicators of embedded scripts, server-side code, or malformed
    /// input. This set is intended for use in case-insensitive searches to detect possible security risks such as
    /// cross-site scripting or code injection attempts.</remarks>
    private static readonly SearchValues<string> _suspiciousPatterns = SearchValues.Create(
    [
        "<script", "<?php", "<?xml", "\0"
    ], StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Provides a set of string patterns used to identify HTML content in a case-insensitive manner.
    /// </summary>
    /// <remarks>This value is intended for use in operations that need to quickly determine whether a given
    /// input contains HTML markup, such as content type detection or input validation. The patterns include common HTML
    /// document start sequences and are compared using ordinal case-insensitive matching.</remarks>
    private static readonly SearchValues<string> _htmlPatterns = SearchValues.Create(
    [
        "<!DOCTYPE", "<html", "<HTML"
    ], StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifies the integrity of an embedded asset by comparing its computed SHA-256 hash against a pre-stored hash
    /// value.
    /// </summary>
    /// <remarks>This method computes the SHA-256 hash of the provided asset content and validates it  against
    /// a pre-stored hash associated with the specified asset name. If the hash matches, the asset is considered valid,
    /// and a log entry is created to indicate successful verification. If the hash does not match, an error is logged,
    /// and the method returns <see langword="false"/>. <para> In the event of an exception during the verification
    /// process, the method logs the error  and returns <see langword="false"/>. </para></remarks>
    /// <param name="assetName">The name of the asset to verify. Cannot be null, empty, or whitespace.</param>
    /// <param name="content">The binary content of the asset. Cannot be null.</param>
    /// <returns><see langword="true"/> if the asset's computed hash matches the expected hash;  otherwise, <see
    /// langword="false"/>.</returns>
    internal bool VerifyEmbeddedAssetIntegrity(string assetName, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new ArgumentException("Value cannot be an empty collection.", nameof(content));
        }

        try
        {
            string actualHash = ComputeSha256Hash(content);
            bool isValid = AssetHashes.VerifyHash(assetName, actualHash);

            if (isValid)
            {
                _logger.LogAsset("embedded asset integrity verified", assetName, isValid, content.Length);
                _logger.LogInformation("Embedded Asset integrity verified: {AssetName} (SHA-256: {ActualHashPreview}...)", assetName, actualHash[..HashPreviewLength]);
            }
            else
            {
                _logger.LogError("Embedded Asset integrity check FAILED for {AssetName}. Hash mismatch detected.", assetName);
                Debug.WriteLine($"Expected hash for {assetName} not found or doesn't match. Actual: {actualHash}");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify integrity for Embedded {AssetName}", assetName);
            return false;
        }
    }

    /// <summary>
    /// Verifies the integrity of a file by comparing its SHA-256 hash to an expected hash value.
    /// </summary>
    /// <remarks>This method reads the file's contents asynchronously and computes its SHA-256 hash. If the
    /// file does not exist,  the method logs an error and returns <see langword="false"/>. The comparison is
    /// case-insensitive.</remarks>
    /// <param name="filePath">The full path to the file whose integrity is to be verified.
    /// Cannot be null, empty, or whitespace.</param>
    /// <param name="expectedHash">The expected SHA-256 hash of the file, represented as a hexadecimal string.
    /// Cannot be null, empty, or whitespace.</param>
    /// <returns><see langword="true"/> if the file's computed SHA-256 hash matches the <paramref name="expectedHash"/>;
    /// otherwise, <see langword="false"/>.</returns>
    internal async Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        string actualHash = await ComputeFileHashAsync(filePath);

        bool isValid = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        string fileName = Path.GetFileName(filePath);
        if (isValid)
        {
            _logger.LogAsset("file integrity verified", filePath, isValid);
            _logger.LogInformation("File integrity verified: {FileName} (SHA-256: {AssetHashPreview}...)", fileName, actualHash[..HashPreviewLength]);
        }
        else
        {
            int expectedHashPreviewLength = Math.Min(HashPreviewLength, expectedHash.Length);
            _logger.LogError("File integrity check FAILED for {FileName} from {FilePath}. Expected: {ExpectedHash}..., Actual: {ActualHash}...",
                fileName, filePath, expectedHash[..expectedHashPreviewLength], actualHash[..HashPreviewLength]);
        }

        return isValid;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified file.
    /// </summary>
    /// <remarks>This method reads the file asynchronously and computes its hash using the SHA-256 algorithm.
    /// Ensure the file exists and is accessible before calling this method.</remarks>
    /// <param name="filePath">The absolute path of the file to compute the hash for.
    /// The path must not contain traversal sequences.</param>
    /// <returns>A hexadecimal string representation of the computed hash.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/>
    /// is null, empty, consists only of white-space characters,  is not an
    /// absolute path, or contains directory traversal sequences.</exception>
    internal async Task<string> ComputeFileHashAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        (bool isSecure, string? reason) = _securityService.IsFilePathSecure(filePath);
        if (!isSecure && !string.IsNullOrEmpty(reason))
        {
            string errorMessage = $"Insecure file path detected: {filePath}. Reason: {reason}";
            _logger.LogError("Insecure file path detected: {FilePath}. Reason: {Reason}", filePath, reason);
            throw new SecurityException(errorMessage);
        }

        await using FileStream stream = _securityService.CreateSecureFileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = await sha256.ComputeHashAsync(stream)
            .ConfigureAwait(false);

        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Validates the provided content to determine if it represents valid JavaScript.
    /// </summary>
    /// <remarks>The validation checks for common JavaScript patterns such as the presence of keywords like
    /// <c>function</c>, <c>const</c>, <c>var</c>, <c>let</c>, <c>=></c>, or <c>class</c>. It also ensures  that the
    /// content does not contain suspicious patterns such as HTML or PHP injection markers, or null bytes.</remarks>
    /// <param name="content">The content to validate, represented as a byte array encoded in UTF-8.</param>
    /// <returns><see langword="true"/> if the content appears to be valid JavaScript; otherwise, <see langword="false"/>.</returns>
    internal bool ValidateJavaScriptContent(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new ArgumentException("Value cannot be an empty collection.", nameof(content));
        }

        try
        {
            // Performance optimization: Use Span<char> to avoid string allocation for large content
            // and leverage SearchValues for O(1) pattern detection instead of multiple O(n) Contains() calls

            // Basic sanity checks first (fastest path for obviously invalid content)
            if (content.Length < 100)
            {
                return false;
            }

            // Convert to string only once and work with ReadOnlySpan for efficiency
            string text = Encoding.UTF8.GetString(content);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            ReadOnlySpan<char> textSpan = text.AsSpan();

            // Check for suspicious patterns (security-critical check)
            bool hasSuspiciousPatterns = ContainsAnyPattern(textSpan, _suspiciousPatterns);
            if (hasSuspiciousPatterns)
            {
                return false;
            }

            // Check for JavaScript patterns (positive validation)
            return ContainsAnyPattern(textSpan, _jsPatterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JavaScript content validation failed");
            return false;
        }
    }

    /// <summary>
    /// Validates the provided HTML content to ensure it meets basic structural and content requirements.
    /// </summary>
    /// <remarks>The validation checks for the presence of basic HTML patterns, such as a DOCTYPE declaration
    /// or an <c>&lt;html&gt;</c> tag, as well as specific elements like <c>&lt;head&gt;</c>, <c>&lt;body&gt;</c>, and
    /// references to "mermaid". The method returns <see langword="false"/> if the content is null, empty, too short, or
    /// fails these checks.</remarks>
    /// <param name="content">The HTML content to validate, represented as a UTF-8 encoded byte array.</param>
    /// <returns><see langword="true"/> if the content contains valid HTML structure and expected elements; otherwise, <see
    /// langword="false"/>.</returns>
    internal bool ValidateHtmlContent(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new ArgumentException("Value cannot be an empty collection.", nameof(content));
        }

        try
        {
            // Basic sanity checks first (fastest path for obviously invalid content)
            if (content.Length < 50)
            {
                return false;
            }

            string text = Encoding.UTF8.GetString(content);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            ReadOnlySpan<char> textSpan = text.AsSpan();

            // Check for required HTML patterns (structural validation)
            bool hasHtmlPatterns = ContainsAnyPattern(textSpan, _htmlPatterns);
            if (!hasHtmlPatterns)
            {
                return false;
            }

            // Check for the specific elements we expect in our index.html
            // All required elements must be present
            return textSpan.Contains("<head", StringComparison.OrdinalIgnoreCase) &&
                    textSpan.Contains("<body", StringComparison.OrdinalIgnoreCase) &&
                    textSpan.Contains("mermaid", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTML content validation failed");
            return false;
        }
    }

    /// <summary>
    /// Performance-optimized helper method to check if text contains any of the specified patterns.
    /// Uses SearchValues for O(1) pattern detection instead of multiple O(n) string searches.
    /// </summary>
    /// <param name="textSpan">The text to search in.</param>
    /// <param name="patterns">The SearchValues containing patterns to search for.</param>
    /// <returns>True if any pattern is found, false otherwise.</returns>
    private static bool ContainsAnyPattern(ReadOnlySpan<char> textSpan, SearchValues<string> patterns) => textSpan.ContainsAny(patterns);

    /// <summary>
    /// Retrieves the stored hash for the specified asset, if available.
    /// </summary>
    /// <param name="assetName">The name of the asset for which to retrieve the hash.</param>
    /// <returns>The stored hash of the asset as a string, or <see langword="null"/> if no hash is available for the specified
    /// asset.</returns>
    internal string? GetStoredHashForAsset(string assetName)
    {
        //TODO This will be expanded in Phase 2 to store hashes for updated assets. For now, we'll use the build-time hashes for existing embedded resources
        return AssetHashes.EmbeddedAssetHashes.TryGetValue(assetName, out string? hash) ? hash : null;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified byte array and returns the result as a hexadecimal string.
    /// </summary>
    /// <param name="content">The byte array to compute the hash for. Must not be <see langword="null"/> or empty.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash.</returns>
    private static string ComputeSha256Hash(byte[] content)
    {
        const int expectedByteLength = 32; // SHA-256 produces a 32-byte hash

        // Performance optimization: Use stack allocation for hash bytes if content is small enough
        // This avoids heap allocation for the hash result array
        if (content.Length <= StackAllocThreshold)
        {
            Span<byte> hashBytes = stackalloc byte[expectedByteLength]; // SHA-256 produces 32 bytes
            bool success = SHA256.TryHashData(content, hashBytes, out int bytesWritten);
            if (!success || bytesWritten != expectedByteLength)
            {
                throw new CryptographicException($"Failed to compute SHA-256 hash: {nameof(SHA256.TryHashData)} did not succeed or wrote an unexpected number of bytes. Expected number of bytes: {expectedByteLength}");
            }

            return Convert.ToHexString(hashBytes);
        }
        else
        {
            // For larger content, use the standard heap-allocated approach
            byte[] hashBytes = SHA256.HashData(content);
            return Convert.ToHexString(hashBytes);
        }
    }
}
