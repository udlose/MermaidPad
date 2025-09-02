using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Security;

namespace MermaidPad.Services.Platforms;

/// <summary>
/// Provides utility methods for managing and accessing application assets, including embedded resources and disk-based
/// files. This class handles asset extraction, validation, and retrieval to ensure that required assets are available
/// and up-to-date.
/// </summary>
/// <remarks>
/// The <see cref="AssetHelper"/> class is designed to support scenarios where application assets, such
/// as JavaScript libraries or HTML files, need to be embedded in the assembly or stored on disk. It includes methods
/// for extracting embedded resources, validating asset integrity, and retrieving asset content. This class is
/// primarily intended for internal use within the application and is not designed for direct consumption by external
/// callers. Designed for single-file publishing scenarios where Content files are unreliable.
/// IL3000-safe: Does not use Assembly.Location for single-file compatibility.
/// </remarks>
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
    /// Strict whitelist of allowed asset file names.
    /// </summary>
    private static readonly HashSet<string> _allowedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        IndexHtmlFileName,
        MermaidMinJsFileName,
        JsYamlFileName
    };

    /// <summary>
    /// Set of forbidden characters for asset names to prevent security issues.
    /// </summary>
    private static readonly SearchValues<char> _forbiddenCharacters = SearchValues.Create(
        '~', '$', '%', '|', '>', '<', '*', '?', '"', '\0', '\n', '\r'
    );

    #region Get assets from disk

    /// <summary>
    /// Asynchronously retrieves the contents of an asset file from the disk as a byte array.
    /// Performs comprehensive security validation on the asset name and file path.
    /// </summary>
    /// <param name="assetName">The name of the asset file to retrieve.</param>
    /// <returns>A byte array containing the asset file's contents.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="assetName"/> is null or whitespace.</exception>
    /// <exception cref="SecurityException">Thrown if the asset name or path fails security validation.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the asset file does not exist.</exception>
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
            throw new FileNotFoundException($"Asset '{validatedAssetName}' not found", assetPath);
        }

        // Check for symbolic links / reparse points
        if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            SimpleLogger.LogError($"Security: Asset '{validatedAssetName}' is a symbolic link or reparse point");
            throw new SecurityException("Symbolic links are not allowed for assets");
        }

        // Check file size to prevent resource exhaustion
        const long maxFileSize = 50 * 1_024 * 1_024; // 50MB max
        if (fileInfo.Length > maxFileSize)
        {
            SimpleLogger.LogError($"Security: Asset '{validatedAssetName}' exceeds maximum size ({fileInfo.Length} > {maxFileSize})");
            throw new SecurityException($"Asset file exceeds maximum allowed size of {maxFileSize} bytes");
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
            await stream.ReadExactlyAsync(buffer);

            SimpleLogger.Log($"Successfully read asset '{validatedAssetName}' ({buffer.Length} bytes)");
            return buffer;
        }
        catch (UnauthorizedAccessException ex)
        {
            SimpleLogger.LogError($"Security: Access denied to asset '{validatedAssetName}'", ex);
            throw new SecurityException($"Access denied to asset '{validatedAssetName}'", ex);
        }
    }

    #endregion Get assets from disk

    #region Asset Extraction

    /// <summary>
    /// Extracts embedded assets to the assets directory if required.
    /// Validates asset existence and version, and logs timing information.
    /// </summary>
    /// <returns>The path to the assets directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown if asset extraction fails or required assets are missing.</exception>
    internal static string ExtractAssets()
    {
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

                stopwatch.Stop();
                SimpleLogger.LogTiming("Asset extraction", stopwatch.Elapsed, success: true);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                SimpleLogger.LogTiming("Asset extraction", stopwatch.Elapsed, success: false);
                SimpleLogger.LogError("Asset extraction failed", ex);
                throw;
            }
        }
        else
        {
            stopwatch.Stop();
            SimpleLogger.LogTiming("Asset extraction (skipped)", stopwatch.Elapsed);
        }

        // Validate critical files exist
        ValidateAssets(assetsDirectory);

        return assetsDirectory;
    }

    /// <summary>
    /// Asynchronously retrieves the content of an embedded resource as a byte array.
    /// </summary>
    /// <param name="resourceName">The name of the embedded resource to retrieve.</param>
    /// <returns>A byte array containing the resource's contents.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="resourceName"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the resource is not found in the assembly.</exception>
    internal static async Task<byte[]> GetEmbeddedResourceAsync(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        string fullResourceName = $"{EmbeddedResourcePrefix}{resourceName}";
        await using Stream? stream = _currentAssembly.GetManifestResourceStream(fullResourceName);
        if (stream is null)
        {
            string available = string.Join(", ", _currentAssembly.GetManifestResourceNames());
            throw new InvalidOperationException($"Resource '{fullResourceName}' not found. Available: {available}");
        }

        byte[] buffer = new byte[stream.Length];
        await stream.ReadExactlyAsync(buffer);
        return buffer;
    }

    /// <summary>
    /// Extracts all required embedded assets to the specified directory.
    /// Always overwrites existing files to ensure current versions.
    /// </summary>
    /// <param name="targetDirectory">The directory to extract assets to.</param>
    private static void ExtractEmbeddedAssetsToDisk(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        SimpleLogger.Log($"Extracting embedded assets to: {targetDirectory}");

        // Extract all required assets
        foreach (string asset in _allowedAssets)
        {
            ExtractResourceToDisk($"{EmbeddedResourcePrefix}{asset}", Path.Combine(targetDirectory, asset));
        }

        // Write version marker for future cache validation
        WriteVersionMarker(targetDirectory);

        SimpleLogger.Log("Asset extraction completed");
    }

    /// <summary>
    /// Extracts a single embedded resource to the specified target path with enhanced security.
    /// </summary>
    /// <param name="resourceName">The name of the embedded resource to extract.</param>
    /// <param name="targetPath">The file path to write the extracted resource to.</param>
    /// <exception cref="SecurityException">Thrown if the target path fails security validation.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the resource is not found in the assembly.</exception>
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

                // Atomic move to final location
                File.Move(tempFile, targetPath, overwrite: true);

                SimpleLogger.Log($"Extracted: {Path.GetFileName(targetPath)} ({stream.Length:N0} bytes)");
            }
            finally
            {
                // Clean up temp file if it still exists
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { /* Best effort */ }
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
    /// Gets the assets directory using the same pattern as SettingsService.
    /// Ensures cross-platform compatibility and user-writable storage.
    /// </summary>
    /// <returns>The full path to the assets directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the ApplicationData folder cannot be determined.</exception>
    /// <exception cref="SecurityException">Thrown if the assets directory is not under ApplicationData.</exception>
    private static string GetAssetsDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException("Could not determine ApplicationData folder");
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
            SimpleLogger.LogError($"Security: Assets directory '{fullPath}' is not under AppData '{fullAppData}'");
            throw new SecurityException("Assets directory must be under ApplicationData");
        }

        return fullPath;
    }

    /// <summary>
    /// Determines whether assets should be extracted based on existence and version.
    /// </summary>
    /// <param name="assetsDir">The assets directory to check.</param>
    /// <returns><c>true</c> if assets should be extracted; otherwise, <c>false</c>.</returns>
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
    /// Checks if the assets in the specified directory are current by comparing version markers.
    /// </summary>
    /// <param name="assetsDir">The assets directory to check.</param>
    /// <returns><c>true</c> if the assets are current; otherwise, <c>false</c>.</returns>
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
                SimpleLogger.LogError("Could not determine assembly version for ServiceConfiguration");
                throw new InvalidOperationException("Could not determine assembly version for ServiceConfiguration.");
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
    /// Validates that all required asset files exist in the specified directory.
    /// Throws an exception if any critical asset is missing.
    /// </summary>
    /// <param name="assetsDirectory">The directory containing the assets to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if any required asset file is missing.</exception>
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
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Writes a version marker file to the assets directory for cache validation.
    /// </summary>
    /// <param name="assetsDirectory">The directory to write the version marker to.</param>
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
    /// Validates and sanitizes an asset name to prevent path traversal attacks.
    /// Uses multiple validation layers for defense in depth.
    /// </summary>
    /// <param name="assetName">The asset name to validate.</param>
    /// <returns>The validated asset name.</returns>
    /// <exception cref="SecurityException">Thrown if the asset name fails any security validation layer.</exception>
    private static string ValidateAssetName(string assetName)
    {
        // Layer 1: Whitelist check (fastest, most secure)
        if (!_allowedAssets.Contains(assetName))
        {
            SimpleLogger.LogError($"Security: Asset '{assetName}' not in whitelist");
            throw new SecurityException($"Asset '{assetName}' is not allowed");
        }

        // Layer 2: Check for parent directory traversal
        if (assetName.Contains("..", StringComparison.Ordinal))
        {
            SimpleLogger.LogError("Security: Asset name contains parent directory traversal");
            throw new SecurityException("Asset name contains forbidden pattern");
        }

        // Layer 3: Check for forbidden characters using SearchValues (fast in .NET 9)
        if (assetName.AsSpan().ContainsAny(_forbiddenCharacters))
        {
            SimpleLogger.LogError("Security: Asset name contains forbidden characters");
            throw new SecurityException("Asset name contains forbidden characters");
        }

        // Layer 4: Path character validation
        if (assetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            SimpleLogger.LogError("Security: Asset name contains invalid filename characters");
            throw new SecurityException("Asset name contains invalid characters");
        }

        // Layer 5: Ensure it's just a filename, not a path
        if (Path.GetFileName(assetName) != assetName)
        {
            SimpleLogger.LogError("Security: Asset name appears to be a path, not a filename");
            throw new SecurityException("Asset name must be a filename only, not a path");
        }

        // Layer 6: Check for rooted paths
        if (Path.IsPathRooted(assetName))
        {
            SimpleLogger.LogError("Security: Asset name is a rooted path");
            throw new SecurityException("Asset name cannot be a rooted path");
        }

        return assetName;
    }

    /// <summary>
    /// Validates that a full path is within the expected assets directory.
    /// This is the final validation before any file operation.
    /// </summary>
    /// <param name="fullPath">The full file path to validate.</param>
    /// <param name="assetsDirectory">The expected assets directory.</param>
    /// <exception cref="SecurityException">Thrown if the path is outside the assets directory or not in its root.</exception>
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
            SimpleLogger.LogError($"Security: Path traversal detected. Path '{normalizedPath}' is outside '{normalizedAssetsDir}'");
            throw new SecurityException("Access to path outside assets directory is forbidden");
        }

        // Additional check: Ensure the file is directly in the assets directory (no subdirectories)
        string directory = Path.GetDirectoryName(normalizedPath) ?? string.Empty;
        if (!string.Equals(directory, normalizedAssetsDir.TrimEnd(Path.DirectorySeparatorChar), comparison))
        {
            SimpleLogger.LogError("Security: Asset not in root of assets directory");
            throw new SecurityException("Assets must be in the root assets directory");
        }
    }

    #endregion Security - Path Traversal Protection
}
