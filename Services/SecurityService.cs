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

using MermaidPad.Exceptions.Assets;

namespace MermaidPad.Services;

/// <summary>
/// Provides centralized, cross-platform file security validation services.
/// Handles symlink detection, path traversal prevention, and file integrity checks.
/// </summary>
public sealed class SecurityService
{
    private const string SecurityLogCategory = "Security: ";
    private const int DefaultBufferSize = 81_920; // 80KB buffer size for file operations

    /// <summary>
    /// Determines whether the specified file path is secure based on a series of validation checks.
    /// </summary>
    /// <remarks>The method performs the following security checks:
    ///     <list type="bullet">
    ///         <item><description>Ensures the file exists.</description></item>
    ///         <item><description>Validates that the file resides within the specified <paramref name="allowedDirectory"/>, if provided.</description></item>
    ///         <item><description>Checks for symbolic links or reparse points to prevent unauthorized access.</description></item>
    ///     </list>
    ///
    /// If any of these checks fail, the method logs the failure and returns <see
    /// langword="false"/>. In the case of an exception, the method also logs the error and returns <see
    /// langword="false"/> to ensure secure failure.</remarks>
    /// <param name="filePath">The full path of the file to validate. This parameter cannot be null, empty, or consist only of whitespace.</param>
    /// <param name="allowedDirectory">An optional directory path that the file must reside within. If specified, the method ensures that the file path
    /// is within this directory.</param>
    /// <param name="isAssetFile">A value indicating whether the file is considered an asset file. If <see langword="true"/>, the method throws a
    /// <see cref="MissingAssetException"/> if the file does not exist.</param>
    /// <returns><see langword="true"/> if the file path passes all security checks; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="MissingAssetException">Thrown if <paramref name="isAssetFile"/> is <see langword="true"/> and the file does not exist at the specified
    /// <paramref name="filePath"/>.</exception>
    public static bool IsFilePathSecure(string filePath, string? allowedDirectory = null, bool isAssetFile = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            // Step 1: Basic existence and file info validation
            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                SimpleLogger.Log($"File does not exist: {filePath}");
                if (isAssetFile)
                {
                    throw new MissingAssetException($"Asset not found at expected path. Path: {filePath}, Allowed Directory: {allowedDirectory}");
                }

                return false;
            }

            // Step 2: Directory boundary validation (if specified)
            if (!string.IsNullOrWhiteSpace(allowedDirectory) && !IsPathWithinDirectory(filePath, allowedDirectory))
            {
                SimpleLogger.LogError($"{SecurityLogCategory} Path '{filePath}' is outside allowed directory '{allowedDirectory}'");
                return false;
            }

            // Step 3: Cross-platform symlink detection
            if (IsSymbolicLink(fileInfo))
            {
                SimpleLogger.LogError($"{SecurityLogCategory} File '{filePath}' is a symbolic link or reparse point");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"{SecurityLogCategory} Security validation failed for '{filePath}'", ex);
            return false; // Fail secure
        }
    }

    /// <summary>
    /// Cross-platform symlink detection using multiple validation layers.
    /// </summary>
    private static bool IsSymbolicLink(FileInfo fileInfo)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        try
        {
            // Layer 1: FileAttributes.ReparsePoint (works on all platforms in .NET 5+)
            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                SimpleLogger.Log($"File {fileInfo.FullName} detected as reparse point via FileAttributes");
                return true;
            }

            // Layer 2: .NET 6+ LinkTarget property (cross-platform)
            if (fileInfo.LinkTarget != null)
            {
                SimpleLogger.Log($"File {fileInfo.FullName} has LinkTarget: {fileInfo.LinkTarget}");
                return true;
            }

            // Layer 3: Cross-platform path resolution comparison
            return HasPathResolutionMismatch(fileInfo.FullName);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Symlink detection failed for {fileInfo.FullName}", ex);
            return true; // Fail secure - assume it's a symlink if we can't determine
        }
    }

    /// <summary>
    /// Validates that a path is within the specified directory boundary.
    /// Prevents path traversal attacks.
    /// </summary>
    public static bool IsPathWithinDirectory(string filePath, string allowedDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedDirectory);
        try
        {
            string normalizedFilePath = Path.GetFullPath(filePath);
            string normalizedDirectory = Path.GetFullPath(allowedDirectory);

            // Ensure directory ends with separator to prevent prefix attacks
            if (!normalizedDirectory.AsSpan().EndsWith(Path.DirectorySeparatorChar))
            {
                normalizedDirectory += Path.DirectorySeparatorChar;
            }

            // Platform-appropriate string comparison
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return normalizedFilePath.AsSpan().StartsWith(normalizedDirectory, comparison);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Path boundary validation failed for '{filePath}' in '{allowedDirectory}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Validates file name against a whitelist and forbidden characters.
    /// </summary>
    public static bool IsFileNameSecure(string fileName, HashSet<string> allowedFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(allowedFiles);
        if (allowedFiles.Count == 0)
        {
            throw new ArgumentException("Allowed files set cannot be empty", nameof(allowedFiles));
        }

        try
        {
            // Whitelist check (most secure)
            if (!allowedFiles.Contains(fileName))
            {
                SimpleLogger.LogError($"{SecurityLogCategory} File '{fileName}' not in allow-list");
                return false;
            }

            ReadOnlySpan<char> fileNameSpan = fileName.AsSpan();

            // Path traversal check
            if (fileNameSpan.Contains("..", StringComparison.Ordinal))
            {
                SimpleLogger.LogError($"{SecurityLogCategory} File name '{fileName}' contains path traversal");
                return false;
            }

            // Invalid filename characters
            if (fileNameSpan.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                SimpleLogger.LogError($"{SecurityLogCategory} File name '{fileName}' contains invalid characters");
                return false;
            }

            // Ensure it's just a filename, not a path
            if (Path.GetFileName(fileName) != fileName)
            {
                SimpleLogger.LogError($"{SecurityLogCategory} File name '{fileName}' appears to be a path");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Filename validation failed for '{fileName}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Creates a secure FileStream with appropriate options for the platform.
    /// </summary>
    public static FileStream CreateSecureFileStream(string path, FileMode mode, FileAccess access, FileShare share = FileShare.Read, int bufferSize = DefaultBufferSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        return new FileStream(
            path,
            mode,
            access,
            share,
            bufferSize: bufferSize,
            FileOptions.SequentialScan | FileOptions.Asynchronous
        );
    }

    #region Private Helper Methods

    private static bool HasPathResolutionMismatch(string originalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        try
        {
            string resolvedPath = Path.GetFullPath(originalPath);

            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!string.Equals(originalPath, resolvedPath, comparison))
            {
                SimpleLogger.Log($"Path resolution mismatch: original={originalPath}, resolved={resolvedPath}");
                return true;
            }

            // Additional Unix validation
            if (!OperatingSystem.IsWindows())
            {
                return ValidateUnixPathResolution(originalPath, resolvedPath);
            }

            return false;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Path resolution validation failed for '{originalPath}'", ex);
            return true; // Assume mismatch if we can't validate
        }
    }

    private static bool ValidateUnixPathResolution(string originalPath, string resolvedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedPath);

        try
        {
            // Canonical path validation for Unix systems
            string canonicalPath = Path.GetFullPath(resolvedPath);
            if (!string.Equals(resolvedPath, canonicalPath, StringComparison.Ordinal))
            {
                SimpleLogger.Log($"Unix canonical path mismatch: resolved={resolvedPath}, canonical={canonicalPath}");
                return true; // Indicates mismatch (potential symlink)
            }

            return false; // No mismatch detected
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Unix path resolution validation failed for '{originalPath}'", ex);
            return true; // Assume mismatch if validation fails
        }
    }

    //private static bool ValidateFileIntegrity(FileInfo fileInfo)
    //{
    //    try
    //    {
    //        // Basic integrity checks that work cross-platform

    //        // Check for reasonable file size (prevent resource exhaustion)
    //        const long maxReasonableSize = 100 * 1_024 * 1_024; // 100MB
    //        if (fileInfo.Length > maxReasonableSize)
    //        {
    //            SimpleLogger.Log($"File {fileInfo.FullName} exceeds reasonable size limit");
    //            return false;
    //        }

    //        // Ensure file timestamps are reasonable (not far future/past)
    //        DateTime now = DateTime.UtcNow;
    //        DateTime twoYearsAgo = now.AddYears(-2);
    //        DateTime oneYearFromNow = now.AddYears(1);

    //        if (fileInfo.LastWriteTimeUtc < twoYearsAgo || fileInfo.LastWriteTimeUtc > oneYearFromNow)
    //        {
    //            SimpleLogger.Log($"File {fileInfo.FullName} has suspicious timestamp: {fileInfo.LastWriteTimeUtc}");
    //            // Don't fail for this - just log it
    //        }

    //        return true;
    //    }
    //    catch
    //    {
    //        return false;
    //    }
    //}

    #endregion
}
