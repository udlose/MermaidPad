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
using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Security;

namespace MermaidPad.Services.Platforms;

/// <summary>
/// Provides utility methods for managing and validating assets, including retrieving assets from disk, extracting
/// embedded resources, and ensuring asset integrity and security.
/// </summary>
/// <remarks>The <see cref="AssetHelper"/> class is designed to handle asset-related operations such as: <list
/// type="bullet"> <item><description>Retrieving assets from disk with validation for security and
/// integrity.</description></item> <item><description>Extracting embedded resources to disk and ensuring their
/// presence.</description></item> <item><description>Validating asset names and paths to prevent unauthorized access or
/// path traversal attacks.</description></item> </list> This class enforces strict security measures, such as
/// validating asset names, restricting file paths to a designated directory, and verifying the integrity of assets. It
/// is intended for internal use within the application to manage assets securely and efficiently.
/// Designed for single-file publishing scenarios where Content files are unreliable.
/// IL3000-safe: Does not use Assembly.Location for single-file compatibility.</remarks>
public static class AssetHelper
{
    /// <summary>
    /// The file name for the main HTML index asset.
    /// </summary>
    internal const string IndexHtmlFileName = "index.html";

    /// <summary>
    /// The file name for the minified Mermaid JavaScript asset.
    /// </summary>
    internal const string MermaidMinJsFileName = "mermaid.min.js";

    /// <summary>
    /// The file name for the minified js-yaml JavaScript asset.
    /// </summary>
    internal const string JsYamlFileName = "js-yaml.min.js";

    /// <summary>
    /// The prefix used for embedded resource names within the assembly.
    /// </summary>
    private const string EmbeddedResourcePrefix = "MermaidPad.Assets.";

    /// <summary>
    /// The current executing assembly, used for resource extraction.
    /// </summary>
    private static readonly Assembly _currentAssembly = Assembly.GetExecutingAssembly();

    /// <summary>
    /// Represents a collection of asset file names that are allowed for processing.
    /// </summary>
    /// <remarks>The collection is case-insensitive, as determined by <see
    /// cref="StringComparer.OrdinalIgnoreCase"/>. It includes predefined file names such as <c>IndexHtmlFileName</c>,
    /// <c>MermaidMinJsFileName</c>, and <c>JsYamlFileName</c>.</remarks>
    private static readonly HashSet<string> _allowedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        IndexHtmlFileName,
        MermaidMinJsFileName,
        JsYamlFileName
    };

    /// <summary>
    /// Represents a collection of characters that are considered invalid or forbidden in certain contexts.
    /// </summary>
    /// <remarks><![CDATA[This collection includes characters such as '~', '$', '%', '|', '>', '<', '*', '?', '"',
    /// null characters, newline characters, and path-related symbols like '/', '\', and ':'.  These characters may be
    /// used to validate input or restrict certain operations where such  characters are not allowed.]]></remarks>
    private static readonly SearchValues<char> _forbiddenCharacters = SearchValues.Create(
        '~', '$', '%', '|', '>', '<', '*', '?', '"', '\0', '\n', '\r', '/', '\\', ':'
    );

    private const string SecurityLogCategory = "Security: ";

    #region Get assets from disk

    /// <summary>
    /// Asynchronously retrieves the binary content of an asset from disk by its name.
    /// </summary>
    /// <remarks>This method performs several validations to ensure the asset is safe to access: <list
    /// type="bullet"> <item><description>Validates that the asset name is not null, empty, or
    /// invalid.</description></item> <item><description>Ensures the asset path is within the designated assets
    /// directory.</description></item> <item><description>Checks that the asset is a regular file and not a symbolic
    /// link or reparse point.</description></item> <item><description>Enforces a maximum file size limit of 50 MB to
    /// prevent resource exhaustion.</description></item> <item><description>Optionally verifies the file's integrity
    /// against a known hash, if available.</description></item> </list> If the asset cannot be found, is inaccessible,
    /// or fails validation, an appropriate exception is thrown.</remarks>
    /// <param name="assetName">The name of the asset to retrieve. Must not be null, empty, or consist only of whitespace.</param>
    /// <returns>A byte array containing the binary content of the asset. The array will contain the full content of the file.</returns>
    /// <exception cref="MissingAssetException">Thrown if the specified asset does not exist on disk.</exception>
    /// <exception cref="SecurityException">Thrown if the asset is a symbolic link, exceeds the maximum allowed size, or fails integrity verification.</exception>
    /// <exception cref="AssetIntegrityException"></exception>
    internal static async Task<byte[]> GetAssetFromDiskAsync(string assetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        SimpleLogger.Log($"Requesting asset from disk: {assetName}");

        // Step 1: Validate the asset name
        string validatedAssetName = ValidateAssetName(assetName);

        // Step 2: Get the assets directory
        string assetsDirectory = GetAssetsDirectory();

        // Step 3: Build the full path using Path.Combine (safe API)
        string assetPath = Path.Combine(assetsDirectory, validatedAssetName);

        // Step 4: Validate the final path is within bounds
        ValidatePathIsWithinAssetsDirectory(assetPath, assetsDirectory);

        // Step 5: Additional validation - ensure it's a file, not a directory or symlink
        FileInfo fileInfo = new FileInfo(assetPath);
        if (!fileInfo.Exists)
        {
            throw new MissingAssetException($"Asset '{validatedAssetName}' not found at '{assetPath}'");
        }

        // Check for symbolic links / reparse points
        if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            string errorMessage = $"{SecurityLogCategory} Asset '{validatedAssetName}' is a symbolic link or reparse point";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Check file size to prevent resource exhaustion
        const long maxFileSize = 50 * 1_024 * 1_024; // 50MB max
        if (fileInfo.Length > maxFileSize)
        {
            string errorMessage = $"{SecurityLogCategory} Asset '{validatedAssetName}' exceeds maximum size ({fileInfo.Length} > {maxFileSize})";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Step 6: Read the file with proper sharing mode
        try
        {
            // Use FileShare.Read to allow other processes to read but not write
            await using FileStream stream = new FileStream(
                assetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            );

            byte[] buffer = new byte[stream.Length];
            await stream.ReadExactlyAsync(buffer)
                .ConfigureAwait(false);

            // Step 7: Verify integrity if we have a known hash
            // For disk assets, we check against stored hashes (will be expanded in Phase 2)
            if (App.Services.GetService(typeof(SettingsService)) is SettingsService settingsService)
            {
                string? expectedHash = AssetIntegrityService.GetStoredHashForAsset(validatedAssetName, settingsService);
                if (expectedHash != null)
                {
                    bool integrityValid = await AssetIntegrityService.VerifyFileIntegrityAsync(assetPath, expectedHash)
                        .ConfigureAwait(false);
                    if (!integrityValid)
                    {
                        SimpleLogger.LogError($"Integrity check failed for asset '{validatedAssetName}'. File may be corrupted or tampered.");
                        throw new AssetIntegrityException($"Asset '{validatedAssetName}' failed integrity verification");
                    }
                }
            }

            SimpleLogger.Log($"Successfully read asset '{validatedAssetName}' ({buffer.Length} bytes)");
            return buffer;
        }
        catch (UnauthorizedAccessException ex)
        {
            string errorMessage = $"{SecurityLogCategory} Access denied to asset '{validatedAssetName}'";
            SimpleLogger.LogError(errorMessage, ex);
            throw new SecurityException(errorMessage, ex);
        }
    }

    #endregion Get assets from disk

    #region Asset Extraction

    /// <summary>
    /// Extracts embedded assets to the disk if necessary and validates their presence.
    /// </summary>
    /// <remarks>This method ensures that the required assets are available on the disk by extracting them
    /// from embedded resources if they are missing or outdated. The extraction process is logged,  and the method
    /// validates the presence of critical files after extraction. If the assets are  already up-to-date, the extraction
    /// process is skipped.</remarks>
    /// <returns>The path to the directory containing the extracted assets.</returns>
    internal static string ExtractAssets()
    {
        const string timingMessage = "Asset extraction";
        bool skippedExtraction = false;

        SimpleLogger.Log("Asset extraction process starting...");
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Use the SAME directory pattern as SettingsService for consistency
        string assetsDirectory = GetAssetsDirectory();
        SimpleLogger.Log($"Assets directory: {assetsDirectory}");

        // Check if extraction is needed
        if (ShouldExtractAssets(assetsDirectory))
        {
            SimpleLogger.Log("Assets require extraction/update");
            Directory.CreateDirectory(assetsDirectory);

            try
            {
                ExtractEmbeddedAssetsToDisk(assetsDirectory);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                SimpleLogger.LogTiming(timingMessage, stopwatch.Elapsed, success: false);
                SimpleLogger.LogError("Asset extraction failed", ex);
                throw;
            }
        }
        else
        {
            skippedExtraction = true;
        }

        // Validate critical files exist
        ValidateAssets(assetsDirectory);

        stopwatch.Stop();
        if (skippedExtraction)
        {
            SimpleLogger.LogTiming(timingMessage + " (skipped)", stopwatch.Elapsed, success: true);
        }
        else
        {
            SimpleLogger.LogTiming(timingMessage, stopwatch.Elapsed, success: true);
        }

        return assetsDirectory;
    }

    /// <summary>
    /// Asynchronously retrieves an embedded resource as a byte array.
    /// </summary>
    /// <remarks>The method constructs the full resource name using a predefined prefix and attempts to locate
    /// the resource within the current assembly. If the resource is not found, a <see cref="MissingAssetException"/> is
    /// thrown.  The method also verifies the integrity of the retrieved resource using an integrity service. If the
    /// integrity check fails, an <see cref="AssetIntegrityException"/> is thrown.</remarks>
    /// <param name="resourceName">The name of the embedded resource to retrieve. This value cannot be null, empty, or whitespace.</param>
    /// <returns>A byte array containing the contents of the embedded resource.</returns>
    /// <exception cref="MissingAssetException">Thrown if the specified resource cannot be found in the assembly.</exception>
    /// <exception cref="AssetIntegrityException">Thrown if the integrity check for the retrieved resource fails.</exception>
    internal static async Task<byte[]> GetEmbeddedResourceAsync(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        string fullResourceName = $"{EmbeddedResourcePrefix}{resourceName}";
        await using Stream? stream = _currentAssembly.GetManifestResourceStream(fullResourceName);
        if (stream is null)
        {
            string available = string.Join(", ", _currentAssembly.GetManifestResourceNames());
            throw new MissingAssetException($"Resource '{fullResourceName}' not found. Available: {available}");
        }

        byte[] buffer = new byte[stream.Length];
        await stream.ReadExactlyAsync(buffer)
            .ConfigureAwait(false);

        // Verify integrity of embedded resource
        bool integrityValid = AssetIntegrityService.VerifyEmbeddedAssetIntegrity(resourceName, buffer);
        if (!integrityValid)
        {
            SimpleLogger.LogError($"Embedded resource '{resourceName}' failed integrity check. This may indicate a corrupted assembly.");
            throw new AssetIntegrityException($"Embedded resource '{resourceName}' integrity check failed");
        }

        return buffer;
    }

    /// <summary>
    /// Extracts embedded assets to the specified directory on disk.
    /// </summary>
    /// <remarks>This method extracts a predefined set of embedded assets to the specified directory.  It
    /// ensures that the directory exists before extraction and writes a version marker  to the directory for future
    /// cache validation. The method logs the progress and timing  of the extraction process.</remarks>
    /// <param name="targetDirectory">The path to the directory where the embedded assets will be extracted.  The directory will be created if it does
    /// not already exist.</param>
    private static void ExtractEmbeddedAssetsToDisk(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        SimpleLogger.Log($"Extracting embedded assets to: {targetDirectory}");

        Stopwatch stopwatch = Stopwatch.StartNew();
        SimpleLogger.Log("Asset extraction: Beginning extraction to disk");

        // Extract all required assets
        foreach (string asset in _allowedAssets)
        {
            ExtractResourceToDisk($"{EmbeddedResourcePrefix}{asset}", Path.Combine(targetDirectory, asset));
        }

        // Write version marker for future cache validation
        WriteVersionMarker(targetDirectory);

        stopwatch.Stop();
        SimpleLogger.LogTiming("Completed asset extraction to disk", stopwatch.Elapsed, success: true);
        SimpleLogger.Log("Asset extraction: Completed");
    }

    /// <summary>
    /// Extracts an embedded resource from the assembly and writes it to the specified file path on disk.
    /// </summary>
    /// <remarks>This method ensures the integrity and security of the extracted resource by performing the
    /// following steps: <list type="bullet"> <item>Validates that the <paramref name="targetPath"/> is within the
    /// assets directory.</item> <item>Writes the resource to a temporary file before moving it to the final
    /// location.</item> <item>Verifies the integrity of the extracted resource using the <see
    /// cref="AssetIntegrityService"/>.</item> <item>Performs additional content validation for specific file types,
    /// such as JavaScript and HTML.</item> </list> If the resource fails any validation step, an exception is thrown,
    /// and the operation is aborted.</remarks>
    /// <param name="resourceName">The name of the embedded resource to extract. This must match the resource name in the assembly.</param>
    /// <param name="targetPath">The full file path where the resource will be written. The path must be within the assets directory.</param>
    /// <exception cref="InvalidOperationException">Thrown if the resource is not found in the assembly, if the resource fails integrity verification,  or if the
    /// resource fails content validation.</exception>
    /// <exception cref="SecurityException">Thrown if the temporary file becomes a symbolic link during the extraction process.</exception>
    /// <exception cref="AssetIntegrityException">Thrown if the content validation for JavaScript or HTML files fails.</exception>
    private static void ExtractResourceToDisk(string resourceName, string targetPath)
    {
        // Validate the target path is within assets directory
        string assetsDirectory = GetAssetsDirectory();
        ValidatePathIsWithinAssetsDirectory(targetPath, assetsDirectory);

        try
        {
            using Stream? stream = _currentAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                string available = string.Join(", ", _currentAssembly.GetManifestResourceNames());
                throw new InvalidOperationException($"Resource '{resourceName}' not found. Available: {available}");
            }

            // Use a secure temporary file name
            string tempDir = Path.GetTempPath();
            string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());

            try
            {
                // Write to temp file first
                using (FileStream fileStream = File.Create(tempFile))
                {
                    stream.CopyTo(fileStream);
                }

                // Validate temp file before moving
                FileInfo tempInfo = new FileInfo(tempFile);
                if ((tempInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new SecurityException("Temporary file became a symbolic link");
                }

                // Read the temp file to verify integrity before moving
                byte[] extractedContent = File.ReadAllBytes(tempFile);
                string assetName = Path.GetFileName(targetPath);

                // Verify integrity of the extracted asset
                bool integrityValid = AssetIntegrityService.VerifyEmbeddedAssetIntegrity(assetName, extractedContent);
                if (!integrityValid)
                {
                    throw new InvalidOperationException($"Extracted resource '{assetName}' failed integrity verification");
                }

                // Additional content validation for extra security
                if (assetName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                {
                    if (!AssetIntegrityService.ValidateJavaScriptContent(extractedContent))
                    {
                        throw new AssetIntegrityException($"Extracted JavaScript '{assetName}' failed content validation");
                    }
                }
                else if (assetName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                {
                    if (!AssetIntegrityService.ValidateHtmlContent(extractedContent))
                    {
                        throw new AssetIntegrityException($"Extracted HTML '{assetName}' failed content validation");
                    }
                }

                // Atomic move to final location
                File.Move(tempFile, targetPath, overwrite: true);

                SimpleLogger.Log($"Extracted and verified: {assetName} ({stream.Length:N0} bytes)");
            }
            finally
            {
                // Clean up temp file if it still exists
                if (File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError($"Failed to delete temp file '{tempFile}': {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Failed to extract {resourceName}: {ex.Message}");
            throw;
        }
    }

    #endregion Asset Extraction

    #region Asset Validation

    /// <summary>
    /// Retrieves the full path to the application's assets directory located under the user's Application Data folder.
    /// </summary>
    /// <remarks>This method constructs the path to the "Assets" directory under the "MermaidPad" folder
    /// within the user's Application Data directory. It validates that the resulting path is within the Application
    /// Data folder to prevent directory traversal attacks. If the Application Data folder cannot be determined, or if
    /// the validation fails, an exception is thrown.</remarks>
    /// <returns>The full path to the assets directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Application Data folder cannot be determined.</exception>
    /// <exception cref="SecurityException">Thrown if the assets directory is not located within the Application Data folder.</exception>
    private static string GetAssetsDirectory()
    {
        const Environment.SpecialFolder appDataSpecialFolder = Environment.SpecialFolder.ApplicationData;
        string appData = Environment.GetFolderPath(appDataSpecialFolder);

        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException($"Could not determine {nameof(Environment.SpecialFolder.ApplicationData)} folder '{appData}'");
        }

        string baseDir = Path.Combine(appData, "MermaidPad");
        string assetsDir = Path.Combine(baseDir, "Assets");

        // Validate the path hasn't been tampered with
        string fullPath = Path.GetFullPath(assetsDir);
        string fullAppData = Path.GetFullPath(appData);

        // Ensure assets directory is under AppData
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(fullAppData, comparison))
        {
            string errorMessage = $"{SecurityLogCategory} Assets directory '{fullPath}' is not under AppData '{fullAppData}'";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        return fullPath;
    }

    /// <summary>
    /// Determines whether the assets in the specified directory need to be extracted.
    /// </summary>
    /// <remarks>This method checks the existence of the specified directory and verifies that all required
    /// asset files are present. If the directory does not exist or any required file is missing, extraction is deemed
    /// necessary. Additionally, the method validates the currency of the assets using an internal cache validation
    /// mechanism.</remarks>
    /// <param name="assetsDir">The path to the directory containing the assets.</param>
    /// <returns><see langword="true"/> if the assets need to be extracted; otherwise, <see langword="false"/>.</returns>
    private static bool ShouldExtractAssets(string assetsDir)
    {
        // If directory doesn't exist, definitely extract
        if (!Directory.Exists(assetsDir))
        {
            SimpleLogger.Log($"Expected Assets directory '{assetsDir}' does not exist, extraction required");
            return true;
        }

        // Check if required files exist
        foreach (string requiredFile in _allowedAssets)
        {
            string filePath = Path.Combine(assetsDir, requiredFile);
            if (!File.Exists(filePath))
            {
                SimpleLogger.Log($"Missing critical asset: {requiredFile}");
                return true;
            }
        }

        // Use assembly version for cache validation (IL3000-safe)
        bool isCurrent = AreAssetsCurrent(assetsDir);
        SimpleLogger.Log($"Asset currency check result: {isCurrent}");
        return !isCurrent;
    }

    /// <summary>
    /// Determines whether the assets in the specified directory are up-to-date based on a version marker file.
    /// </summary>
    /// <remarks>This method checks for the presence of a version marker file named <c>.version</c> in the
    /// specified directory. The file is expected to contain the version of the assets. The method compares this version
    /// with the current assembly version of the <c>AssetHelper</c> class. If the versions do not match, or if the
    /// version marker file is missing or unreadable, the method assumes the assets are not current.</remarks>
    /// <param name="assetsDir">The path to the directory containing the assets to check.</param>
    /// <returns><see langword="true"/> if the assets are current; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the assembly version of the <c>AssetHelper</c> class cannot be determined.</exception>
    private static bool AreAssetsCurrent(string assetsDir)
    {
        try
        {
            string versionMarkerPath = Path.Combine(assetsDir, ".version");
            if (!File.Exists(versionMarkerPath))
            {
                SimpleLogger.Log("Version marker file not found, assets need update");
                return false;
            }

            string storedVersion = File.ReadAllText(versionMarkerPath).Trim();
            Version? version = typeof(AssetHelper).Assembly.GetName().Version;
            if (version is null)
            {
                const string errorMessage = $"Could not determine assembly version for {nameof(AssetHelper)}. This may indicate a build or deployment issue.";
                SimpleLogger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            string currentVersion = version.ToString();

            bool isCurrent = storedVersion == currentVersion;
            SimpleLogger.Log($"Version comparison: stored={storedVersion}, current={currentVersion}, isCurrent={isCurrent}");

            return isCurrent;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Version check failed, assuming assets need update", ex);
            return false; // if we can't read version, re-extract to be safe
        }
    }

    /// <summary>
    /// Validates the presence of required asset files in the specified directory.
    /// </summary>
    /// <remarks>This method checks for the existence of a predefined set of required asset files in the
    /// specified directory. For each file, it logs whether the file is present and, if present, its size. If any
    /// required files are missing, an error is logged, and a <see cref="MissingAssetException"/> is thrown.</remarks>
    /// <param name="assetsDirectory">The path to the directory containing the asset files to validate.</param>
    /// <exception cref="MissingAssetException">Thrown if one or more required asset files are missing from the specified directory.</exception>
    private static void ValidateAssets(string assetsDirectory)
    {
        List<string> missingFiles = new List<string>();
        foreach (string fileName in _allowedAssets)
        {
            string filePath = Path.Combine(assetsDirectory, fileName);
            if (File.Exists(filePath))
            {
                FileInfo fileInfo = new FileInfo(filePath);
                SimpleLogger.LogAsset("validate", fileName, true, fileInfo.Length);
            }
            else
            {
                SimpleLogger.LogAsset("validate", fileName, false);
                missingFiles.Add(fileName);
            }
        }

        if (missingFiles.Count == 0)
        {
            SimpleLogger.Log("All required assets validated successfully");
        }
        else
        {
            string errorMessage = $"Asset validation after extraction failed: One or more required asset files are missing from {assetsDirectory}: {string.Join(", ", missingFiles)}";
            SimpleLogger.LogError(errorMessage);
            throw new MissingAssetException(errorMessage);
        }
    }

    /// <summary>
    /// Writes a version marker file to the specified directory.
    /// </summary>
    /// <remarks>The version marker file is named <c>.version</c> and contains the version of the current
    /// assembly. If the assembly version cannot be determined, an <see cref="InvalidOperationException"/> is
    /// thrown.</remarks>
    /// <param name="assetsDirectory">The directory where the version marker file will be created. This directory must exist and be writable.</param>
    /// <exception cref="InvalidOperationException">Thrown if the assembly version cannot be determined, which may indicate a build or deployment issue.</exception>
    private static void WriteVersionMarker(string assetsDirectory)
    {
        try
        {
            string versionMarkerPath = Path.Combine(assetsDirectory, ".version");
            Version versionObj = _currentAssembly.GetName().Version ??
                throw new InvalidOperationException("Assembly version could not be determined. This may indicate a build or deployment issue.");

            string version = versionObj.ToString();
            File.WriteAllText(versionMarkerPath, version);
            Debug.WriteLine($"Version marker written: {version}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not write version marker: {ex.Message}");
        }
    }

    #endregion Asset Validation

    #region Security - Path Traversal Protection

    /// <summary>
    /// Validates the specified asset name to ensure it adheres to security and naming constraints.
    /// </summary>
    /// <remarks>This method performs multiple layers of validation to ensure the asset name is secure and
    /// conforms to expected naming conventions. It is designed to prevent path traversal attacks and enforce strict
    /// filename constraints.</remarks>
    /// <param name="assetName">The name of the asset to validate. This should be a simple filename, not a path.</param>
    /// <returns>The validated asset name if it passes all security and naming checks.</returns>
    /// <exception cref="SecurityException">Thrown if the asset name fails any of the following validations: <list type="bullet"> <item><description>The
    /// asset name is not in the allowed assets list.</description></item> <item><description>The asset name contains
    /// parent directory traversal sequences (e.g., "..").</description></item> <item><description>The asset name
    /// contains forbidden characters.</description></item> <item><description>The asset name contains invalid filename
    /// characters.</description></item> <item><description>The asset name is a path rather than a simple
    /// filename.</description></item> <item><description>The asset name is a rooted path.</description></item> </list></exception>
    private static string ValidateAssetName(string assetName)
    {
        // Layer 1: Whitelist check (fastest, most secure)
        if (!_allowedAssets.Contains(assetName))
        {
            string errorMessage = $"Security: Asset '{assetName}' is not in the allowed assets list";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Layer 2: Check for parent directory traversal
        if (assetName.Contains("..", StringComparison.Ordinal))
        {
            string errorMessage = $"Security: Asset name '{assetName}' contains parent directory traversal";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Layer 3: Check for forbidden characters using SearchValues (fast in .NET 9)
        if (assetName.AsSpan().ContainsAny(_forbiddenCharacters))
        {
            string errorMessage = $"Security: Asset name '{assetName}' contains forbidden characters";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Layer 4: Path character validation
        if (assetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            string errorMessage = $"Security: Asset name '{assetName}' contains invalid filename characters";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Layer 5: Ensure it's just a filename, not a path
        if (Path.GetFileName(assetName) != assetName)
        {
            string errorMessage = $"Security: Asset name '{assetName}' appears to be a path, not a filename";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Layer 6: Check for rooted paths
        if (Path.IsPathRooted(assetName))
        {
            string errorMessage = $"Security: Asset name '{assetName}' is a rooted path";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        return assetName;
    }

    /// <summary>
    /// Validates that the specified file path is located within the root of the given assets directory.
    /// </summary>
    /// <remarks>This method ensures that the file path is securely contained within the root of the assets
    /// directory  to prevent path traversal attacks. The comparison is case-insensitive on Windows and case-sensitive
    /// on Unix-based systems.</remarks>
    /// <param name="fullPath">The full path of the file to validate.</param>
    /// <param name="assetsDirectory">The root directory of the assets to validate against.</param>
    /// <exception cref="SecurityException">Thrown if the specified <paramref name="fullPath"/> is outside the <paramref name="assetsDirectory"/>
    /// or if it is located in a subdirectory of the assets directory.</exception>
    private static void ValidatePathIsWithinAssetsDirectory(string fullPath, string assetsDirectory)
    {
        // Normalize both paths to absolute paths
        string normalizedPath = Path.GetFullPath(fullPath);
        string normalizedAssetsDir = Path.GetFullPath(assetsDirectory);

        // Ensure the assets directory ends with a separator to prevent prefix attacks
        if (!normalizedAssetsDir.EndsWith(Path.DirectorySeparatorChar))
        {
            normalizedAssetsDir += Path.DirectorySeparatorChar;
        }

        // Case-insensitive comparison on Windows, case-sensitive on Unix
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!normalizedPath.StartsWith(normalizedAssetsDir, comparison))
        {
            string errorMessage = $"Security: Path traversal detected. Path '{normalizedPath}' is outside '{normalizedAssetsDir}'";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Additional check: Ensure the file is directly in the assets directory (no subdirectories)
        string directory = Path.GetDirectoryName(normalizedPath) ?? string.Empty;
        if (!string.Equals(directory, normalizedAssetsDir.TrimEnd(Path.DirectorySeparatorChar), comparison))
        {
            string errorMessage = $"Security: Asset '{fullPath}' is not in the root of the assets directory '{normalizedAssetsDir}'";
            SimpleLogger.LogError(errorMessage);
            throw new SecurityException(errorMessage);
        }
    }

    #endregion Security - Path Traversal Protection
}
