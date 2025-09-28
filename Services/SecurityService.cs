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

using JetBrains.Annotations;
using MermaidPad.Exceptions.Assets;
using System.Diagnostics.CodeAnalysis;
using System.Security;

namespace MermaidPad.Services;
/// <summary>
/// Provides a set of methods for validating file paths, file names, and directories to ensure secure file operations.
/// </summary>
/// <remarks>The <see cref="SecurityService"/> class is designed to help developers implement secure file handling
/// practices by providing utilities for validating file paths, detecting symbolic links, enforcing directory
/// boundaries, and creating secure file streams. It includes methods to prevent common security vulnerabilities such as
/// path traversal attacks, unauthorized access via symbolic links, and improper file name validation.</remarks>
[SuppressMessage("ReSharper", "ConvertToStaticClass", Justification = "Class is a singleton by design with lifetime controlled by DI")]
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Class is a singleton by design with lifetime controlled by DI")]
public sealed class SecurityService
{
    private const string SecurityLogCategory = "Security: ";
    private const int DefaultBufferSize = 81_920; // 80KB buffer size for file operations

    /// <summary>
    /// Determines whether the specified file path is secure based on a series of validation checks.
    /// </summary>
    /// <remarks>This method performs several security checks, including:
    ///     <list type="bullet">
    ///         <item><description>Ensures the file exists.</description></item>
    ///         <item><description>Validates that the file resides within the specified <paramref name="allowedDirectory"/>, if provided.</description></item>
    ///         <item><description>Checks for symbolic links or reparse points to prevent unauthorized access.</description></item>
    ///         <item><description>Verifies that the file path is rooted.</description></item>
    ///     </list>
    ///
    /// If any of these checks fail, the method returns <see langword="false"/>
    /// along with a reason describing the failure. In the event of an exception during validation, the method logs the
    /// error and returns <see langword="false"/> to ensure secure failure.</remarks>
    /// <param name="filePath">The file path to validate. This parameter cannot be null, empty, or consist only of white-space characters.</param>
    /// <param name="allowedDirectory">An optional directory path that the file must reside within to be considered secure. If null or empty, this
    /// check is skipped.</param>
    /// <param name="isAssetFile">A boolean indicating whether the file is an asset file. If <see langword="true"/>, a missing file will result in
    /// a <see cref="MissingAssetException"/> being thrown.</param>
    /// <returns>A tuple containing a boolean value and an optional reason string. The boolean value indicates whether the file
    /// path is secure. If the file path is not secure, the reason string provides additional details about the failure.</returns>
    /// <exception cref="MissingAssetException">Thrown if <paramref name="isAssetFile"/> is <see langword="true"/> and the file does not exist.</exception>
    public static (bool IsSecure, string? Reason) IsFilePathSecure(string filePath, string? allowedDirectory = null, bool isAssetFile = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Layer 1: Basic existence and file info validation
        FileInfo fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            if (isAssetFile)
            {
                throw new MissingAssetException($"Asset not found at expected path. Path: {filePath}, Allowed Directory: {allowedDirectory}");
            }

            return (false, $"{SecurityLogCategory} File does not exist: {filePath}");
        }

        // Layer 2: Invalid path characters
        if (filePath.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return (false, $"{SecurityLogCategory} File path '{filePath}' contains invalid characters");
        }

        // Step 2: Directory boundary validation (if specified)
        if (!string.IsNullOrWhiteSpace(allowedDirectory) && !IsPathWithinDirectory(filePath, allowedDirectory))
        {
            return (false, $"{SecurityLogCategory} Path '{filePath}' is outside allowed directory '{allowedDirectory}'");
        }

        // Step 3: Cross-platform symlink detection
        if (IsSymbolicLink(fileInfo))
        {
            return (false, $"{SecurityLogCategory} File is a symbolic link or reparse point: {filePath}");
        }

        // Layer 4: Check for rooted paths
        if (!Path.IsPathRooted(filePath))
        {
            return (false, $"{SecurityLogCategory} File path '{filePath}' is not rooted");
        }

        return (true, null);
    }

    /// <summary>
    /// Determines whether the specified file is a symbolic link.
    /// </summary>
    /// <remarks>This method uses multiple layers of detection to determine if the file is a symbolic link:
    ///     <list type="bullet">
    ///         <item><description>It first checks if the file has the <see cref="FileAttributes.ReparsePoint"/> attribute.</description></item>
    ///         <item><description>It then checks the <see cref="FileInfo.LinkTarget"/> property, if available.</description></item>
    ///         <item><description>Finally, it performs a cross-platform path resolution comparison.</description></item>
    ///     </list>
    ///
    /// If an error occurs during detection, the method assumes the file is a symbolic link and returns <see langword="true"/>.</remarks>
    /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to check. Cannot be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the file is a symbolic link; otherwise, <see langword="false"/>.</returns>
    private static bool IsSymbolicLink(FileInfo fileInfo)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        try
        {
            // Layer 1: FileAttributes.ReparsePoint
            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                SimpleLogger.Log($"File {fileInfo.FullName} detected as reparse point via FileAttributes");
                return true;
            }

            // Layer 2: .NET 6+ LinkTarget property
            if (fileInfo.LinkTarget is not null)
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
    /// Determines whether the specified file name is secure based on a set of allowed file names and security checks.
    /// </summary>
    /// <remarks>The method performs the following checks to determine if the file name is secure:
    ///     <list type="bullet">
    ///         <item><description>Ensures the file name is included in the <paramref name="allowedFiles"/> set.</description></item>
    ///         <item><description>Checks for path traversal sequences (e.g., "..").</description></item>
    ///         <item><description>Validates that the file name does not contain invalid characters as defined by <see cref="Path.GetInvalidFileNameChars"/>.</description></item>
    ///         <item><description>Ensures the input represents only a file name and not a full or partial path.</description></item>
    ///     </list>
    /// </remarks>
    /// <param name="fileName">The file name to validate. This must be a simple file name, not a path.</param>
    /// <param name="allowedFiles">A set of allowed file names. The set cannot be null or empty.</param>
    /// <returns>A tuple containing a boolean value indicating whether the file name is secure and a string providing the reason
    /// if it is not secure. If the file name is secure, the reason will be <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="fileName"/> is null, empty, or consists only of whitespace, or if <paramref
    /// name="allowedFiles"/> is empty.</exception>
    public static (bool IsSecure, string? Reason) IsFileNameSecure(string fileName, HashSet<string> allowedFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(allowedFiles);
        if (allowedFiles.Count == 0)
        {
            throw new ArgumentException("Allowed files set cannot be empty", nameof(allowedFiles));
        }

        // Whitelist check
        if (!allowedFiles.Contains(fileName))
        {
            return (false, $"{SecurityLogCategory} File '{fileName}' not in allow-list");
        }

        ReadOnlySpan<char> fileNameSpan = fileName.AsSpan();

        // Path traversal check
        if (fileNameSpan.Contains("..", StringComparison.Ordinal))
        {
            return (false, $"{SecurityLogCategory} File name '{fileName}' contains path traversal");
        }

        // Invalid filename characters
        if (fileNameSpan.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return (false, $"{SecurityLogCategory} File name '{fileName}' contains invalid characters");
        }

        // Ensure it's just a filename, not a path
        if (Path.GetFileName(fileName) != fileName)
        {
            return (false, $"{SecurityLogCategory} File name '{fileName}' appears to be a path");
        }

        return (true, null);
    }

    /// <summary>
    /// Creates a secure <see cref="FileStream"/> with specified access, sharing, and buffering options.
    /// </summary>
    /// <remarks>The returned <see cref="FileStream"/> is configured with <see
    /// cref="FileOptions.SequentialScan"/> and <see cref="FileOptions.Asynchronous"/> to optimize performance for
    /// sequential and asynchronous file operations.</remarks>
    /// <param name="path">The file path to open. The path cannot be null, empty, or consist only of white-space characters.</param>
    /// <param name="mode">A <see cref="FileMode"/> value that specifies how the operating system should open the file.</param>
    /// <param name="access">A <see cref="FileAccess"/> value that specifies the type of access to the file (read, write, or both).</param>
    /// <param name="share">A <see cref="FileShare"/> value that specifies the type of access other threads or processes have to the file.
    /// The default is <see cref="FileShare.Read"/>.</param>
    /// <param name="bufferSize">The size of the buffer, in bytes. Must be greater than 0. The default value is <c>DefaultBufferSize</c>.</param>
    /// <returns>A <see cref="FileStream"/> configured with the specified options, optimized for sequential access and
    /// asynchronous operations.</returns>
    [MustDisposeResource]
    public static FileStream CreateSecureFileStream(string path, FileMode mode, FileAccess access, FileShare share = FileShare.Read, int bufferSize = DefaultBufferSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        // verify the path is secure before opening
        (bool isSecure, string? reason) = IsFilePathSecure(path);
        if (!isSecure && !string.IsNullOrEmpty(reason))
        {
            string errorMessage = $"Requested path for FileStream is not secure: {reason}";
            SimpleLogger.Log(errorMessage);
            throw new SecurityException(errorMessage);
        }

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

    /// <summary>
    /// Determines whether the specified file path is located within the specified directory.
    /// </summary>
    /// <remarks>This method normalizes both the file path and the directory path to ensure consistent
    /// comparison.  On Windows, the comparison is case-insensitive; on other platforms, it is case-sensitive. The
    /// method appends a directory separator to the directory path if it does not already end with one,  to prevent
    /// prefix-based attacks (e.g., a file path starting with a directory name but not actually within it).</remarks>
    /// <param name="filePath">The full path of the file to check. Cannot be null, empty, or whitespace.</param>
    /// <param name="allowedDirectory">The directory to validate against. Cannot be null, empty, or whitespace.</param>
    /// <returns><see langword="true"/> if the file path is within the specified directory; otherwise, <see langword="false"/>.</returns>
    private static bool IsPathWithinDirectory(string filePath, string allowedDirectory)
    {
        try
        {
            // Normalize both paths to absolute paths
            string normalizedFilePath = Path.GetFullPath(filePath);
            string normalizedDirectory = Path.GetFullPath(allowedDirectory);

            // Ensure directory ends with separator to prevent prefix attacks
            if (!normalizedDirectory.AsSpan().EndsWith(Path.DirectorySeparatorChar))
            {
                normalizedDirectory += Path.DirectorySeparatorChar;
            }

            // Case-insensitive comparison on Windows, case-sensitive on Unix
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
    /// Determines whether the specified path has a resolution mismatch when compared to its fully resolved path.
    /// </summary>
    /// <remarks>On Windows, the comparison is case-insensitive. On Unix-based systems, the comparison is
    /// case-sensitive, and additional validation may be performed to account for platform-specific path resolution
    /// behavior. If an error occurs during validation, the method assumes a mismatch and returns <see
    /// langword="true"/>.</remarks>
    /// <param name="originalPath">The original file or directory path to validate. Cannot be null, empty, or consist only of whitespace.</param>
    /// <returns><see langword="true"/> if the resolved path differs from the original path; otherwise, <see langword="false"/>.
    /// On Unix-based systems, additional validation is performed to ensure path consistency.</returns>
    private static bool HasPathResolutionMismatch(string originalPath)
    {
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

    /// <summary>
    /// Validates whether the resolved Unix path matches its canonical form, indicating potential symlink usage.
    /// </summary>
    /// <remarks>This method is designed for Unix systems and checks whether the resolved path differs from
    /// its canonical representation, which may indicate the presence of symbolic links or other path inconsistencies.
    /// If an exception occurs during validation, the method assumes a mismatch and returns <see
    /// langword="true"/>.</remarks>
    /// <param name="originalPath">The original path provided by the user. Must not be null, empty, or whitespace.</param>
    /// <param name="resolvedPath">The resolved path to validate. Must not be null, empty, or whitespace.</param>
    /// <returns><see langword="true"/> if the resolved path does not match its canonical form or if validation fails; otherwise,
    /// <see langword="false"/>.</returns>
    private static bool ValidateUnixPathResolution(string originalPath, string resolvedPath)
    {
        try
        {
            // Canonical, case-sensitive path validation for Unix systems
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

    #endregion
}
