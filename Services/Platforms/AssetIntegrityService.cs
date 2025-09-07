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

using System.Buffers;
using System.Diagnostics;
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
internal static class AssetIntegrityService
{
    private const int StackAllocThreshold = 8_192; // 8KB threshold for stack vs heap allocation
    private const int DefaultBufferSize = 81_920; // 80KB buffer size for file operations
    private static readonly SearchValues<string> _jsPatterns = SearchValues.Create(
    [
        "function", "const ", "var ", "let ", "=>", "class ", "import ", "export "
    ], StringComparison.Ordinal);

    private static readonly SearchValues<string> _suspiciousPatterns = SearchValues.Create(
    [
        "<script", "<?php", "<?xml", "\0"
    ], StringComparison.OrdinalIgnoreCase);

    private static readonly SearchValues<string> _htmlPatterns = SearchValues.Create(
    [
        "<!DOCTYPE", "<html", "<HTML"
    ], StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifies the integrity of an embedded asset by comparing its computed SHA-256 hash  against a pre-stored hash
    /// value.
    /// </summary>
    /// <remarks>This method computes the SHA-256 hash of the provided asset content and validates it  against
    /// a pre-stored hash associated with the specified asset name. If the hash matches,  the asset is considered valid,
    /// and a log entry is created to indicate successful verification.  If the hash does not match, an error is logged,
    /// and the method returns <see langword="false"/>. <para> In the event of an exception during the verification
    /// process, the method logs the error  and returns <see langword="false"/>. </para></remarks>
    /// <param name="assetName">The name of the asset to verify. Cannot be null, empty, or whitespace.</param>
    /// <param name="content">The binary content of the asset. Cannot be null.</param>
    /// <returns><see langword="true"/> if the asset's computed hash matches the expected hash;  otherwise, <see
    /// langword="false"/>.</returns>
    internal static bool VerifyEmbeddedAssetIntegrity(string assetName, byte[] content)
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
                SimpleLogger.Log($"Asset integrity verified: {assetName} (SHA-256: {actualHash[..8]}...)");
            }
            else
            {
                SimpleLogger.LogError($"Asset integrity check FAILED for {assetName}. Hash mismatch detected.");
                Debug.WriteLine($"Expected hash for {assetName} not found or doesn't match. Actual: {actualHash}");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Failed to verify integrity for {assetName}", ex);
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
    internal static async Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        if (!File.Exists(filePath))
        {
            SimpleLogger.LogError($"File not found for integrity check: {filePath}");
            return false;
        }

        // Performance optimization: Use streaming hash computation instead of loading entire file into memory
        // This is more memory-efficient for large files and uses the same optimized I/O pattern
        string actualHash;
        await using (FileStream stream = new FileStream(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: DefaultBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream)
                .ConfigureAwait(false);

            actualHash = Convert.ToHexString(hashBytes);
        }

        bool isValid = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        string fileName = Path.GetFileName(filePath);
        if (isValid)
        {
            SimpleLogger.Log($"File integrity verified: {fileName} (SHA-256: {actualHash[..8]}...)");
        }
        else
        {
            SimpleLogger.LogError($"File integrity check FAILED for {fileName} from {filePath}. Expected: {expectedHash[..8]}..., Actual: {actualHash[..8]}...");
        }

        return isValid;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified byte array and returns the result as a hexadecimal string.
    /// </summary>
    /// <param name="content">The byte array to compute the hash for. Must not be <see langword="null"/> or empty.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash.</returns>
    private static string ComputeSha256Hash(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfZero(content.Length);

        // Performance optimization: Use stack allocation for hash bytes if content is small enough
        // This avoids heap allocation for the hash result array
        if (content.Length <= StackAllocThreshold)
        {
            Span<byte> hashBytes = stackalloc byte[32]; // SHA-256 produces 32 bytes
            bool success = SHA256.TryHashData(content, hashBytes, out int bytesWritten);
            Debug.Assert(success && bytesWritten == 32);
            return Convert.ToHexString(hashBytes);
        }
        else
        {
            // For larger content, use the standard heap-allocated approach
            byte[] hashBytes = SHA256.HashData(content);
            return Convert.ToHexString(hashBytes);
        }
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
    internal static async Task<string> ComputeFileHashAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Validate that the file path is absolute and does not contain traversal sequences
        if (!Path.IsPathRooted(filePath) || filePath.Contains(".."))
        {
            throw new ArgumentException("Invalid file path. Only absolute paths without traversal are allowed.", nameof(filePath));
        }

        // Performance optimization: Use FileStream with async read for better memory efficiency
        // and configure SHA256 for streaming computation
        await using FileStream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: DefaultBufferSize,
            options: FileOptions.SequentialScan | FileOptions.Asynchronous);

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
    internal static bool ValidateJavaScriptContent(byte[] content)
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
            SimpleLogger.LogError("JavaScript content validation failed", ex);
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
    internal static bool ValidateHtmlContent(byte[] content)
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
            SimpleLogger.LogError("HTML content validation failed", ex);
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
    /// <param name="settingsService">The settings service used to manage application settings.</param>
    /// <returns>The stored hash of the asset as a string, or <see langword="null"/> if no hash is available for the specified
    /// asset.</returns>
    internal static string? GetStoredHashForAsset(string assetName, SettingsService settingsService)
    {
        //TODO This will be expanded in Phase 2 to store hashes for updated assets. For now, we'll use the build-time hashes for existing embedded resources
        return AssetHashes.EmbeddedAssetHashes.TryGetValue(assetName, out string? hash) ? hash : null;

        //TODO In Phase 2, we'll check settingsService.Settings.AssetHashes dictionary
        // Consider using a combined lookup strategy or caching layer for multiple hash sources
    }
}
