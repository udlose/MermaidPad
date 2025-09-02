using MermaidPad.Infrastructure;
using System.Diagnostics;
using System.Reflection;

namespace MermaidPad.Services.Platforms;

/// <summary>
/// Provides utility methods for managing and accessing application assets, including embedded resources and disk-based
/// files. This class handles asset extraction, validation, and retrieval to ensure that required assets are available
/// and up-to-date.
/// </summary>
/// <remarks>The <see cref="AssetHelper"/> class is designed to support scenarios where application assets, such
/// as JavaScript libraries or HTML files, need to be embedded in the assembly or stored on disk. It includes methods
/// for extracting embedded resources, validating asset integrity, and retrieving asset content.  This class is
/// primarily intended for internal use within the application and is not designed for direct consumption by external
/// callers. Designed for single-file publishing scenarios where Content files are unreliable.
/// IL3000-safe: Does not use Assembly.Location for single-file compatibility.</remarks>
public static class AssetHelper
{
    internal const string IndexHtmlFileName = "index.html";
    internal const string MermaidMinJsFileName = "mermaid.min.js";
    internal const string JsYamlFileName = "js-yaml.min.js";

    private const string EmbeddedResourcePrefix = "MermaidPad.Assets.";
    private static readonly Assembly _currentAssembly = Assembly.GetExecutingAssembly();
    private static readonly string[] _requiredAssets =
    [
        IndexHtmlFileName,
        MermaidMinJsFileName,
        JsYamlFileName
    ];

    #region Get assets from disk

    /// <summary>
    /// Asynchronously retrieves the contents of an asset file from the disk as a byte array.
    /// </summary>
    /// <param name="assetName">The name of the asset file to retrieve. Cannot be null, empty, or consist only of whitespace.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the contents of the asset file as a
    /// byte array.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the specified asset file does not exist in the assets directory.</exception>
    internal static Task<byte[]> GetAssetFromDiskAsync(string assetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

        string assetsDirectory = GetAssetsDirectory();
        string assetPath = Path.Combine(assetsDirectory, assetName);
        if (!File.Exists(assetPath))
        {
            // Return a faulted task to avoid async/await overhead
            return Task.FromException<byte[]>(new FileNotFoundException($"Asset '{assetName}' not found in assets directory '{assetsDirectory}'. Ensure assets have been extracted.", assetPath));
        }
        return File.ReadAllBytesAsync(assetPath);
    }

    #endregion Get assets from disk

    #region Asset Extraction

    /// <summary>
    /// Extracts embedded assets to the assets directory if required.
    /// Validates asset existence and version, and logs timing information.
    /// </summary>
    /// <returns>The path to the assets directory.</returns>
    internal static string ExtractAssets()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        SimpleLogger.Log("Asset extraction process starting...");

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
    /// <remarks>The method constructs the full resource name by appending the provided <paramref
    /// name="resourceName"/> to a predefined resource prefix. If the resource is not found, an exception is thrown.
    /// Ensure that the resource name matches exactly, including case sensitivity, and that the resource is properly
    /// embedded in the assembly.</remarks>
    /// <param name="resourceName">The name of the embedded resource to retrieve. This value cannot be <see langword="null"/> or whitespace.</param>
    /// <returns>A byte array containing the UTF-8 encoded content of the specified embedded resource.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified resource cannot be found in the assembly. The exception message includes a list of
    /// available resources.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="resourceName"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>This method is provided as an alternative to reading assets from disk for scenarios where embedded resources are preferred.</remarks>
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
    /// Optimized for DI-level caching (called once per app session).
    /// </summary>
    /// <param name="targetDirectory">Directory where assets will be extracted</param>
    private static void ExtractEmbeddedAssetsToDisk(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        Debug.WriteLine($"Extracting embedded assets to: {targetDirectory}");

        // Extract all required assets
        foreach (string asset in _requiredAssets)
        {
            ExtractResourceToDisk($"{EmbeddedResourcePrefix}{asset}", Path.Combine(targetDirectory, asset));
        }

        // Write version marker for future cache validation
        WriteVersionMarker(targetDirectory);

        Debug.WriteLine("Asset extraction completed");
    }

    /// <summary>
    /// Extracts a single embedded resource to the specified target path.
    /// </summary>
    /// <param name="resourceName">The full name of the embedded resource.</param>
    /// <param name="targetPath">The file system path to write the resource to.</param>
    /// <exception cref="InvalidOperationException">Thrown if the resource cannot be found.</exception>
    private static void ExtractResourceToDisk(string resourceName, string targetPath)
    {
        try
        {
            using Stream? stream = _currentAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                string available = string.Join(", ", _currentAssembly.GetManifestResourceNames());
                throw new InvalidOperationException($"Resource '{resourceName}' not found. Available: {available}");
            }

            using FileStream fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);

            Debug.WriteLine($"Extracted: {Path.GetFileName(targetPath)} ({stream.Length:N0} bytes)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to extract {resourceName}: {ex.Message}");
            throw;
        }
    }

    #endregion Asset Extraction

    #region Asset Validation

    /// <summary>
    /// Gets the assets directory using the same pattern as SettingsService.
    /// Ensures cross-platform compatibility and user-writable storage.
    /// </summary>
    /// <returns>The path to the assets directory.</returns>
    private static string GetAssetsDirectory()
    {
        // Use same pattern as SettingsService.GetConfigDirectory()
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDir = Path.Combine(appData, "MermaidPad");
        return Path.Combine(baseDir, "Assets");
    }

    /// <summary>
    /// Determines whether assets should be extracted based on existence and version.
    /// </summary>
    /// <param name="assetsDir">The assets directory to check.</param>
    /// <returns><c>true</c> if extraction is required; otherwise, <c>false</c>.</returns>
    private static bool ShouldExtractAssets(string assetsDir)
    {
        // If directory doesn't exist, definitely extract
        if (!Directory.Exists(assetsDir))
        {
            SimpleLogger.Log($"Expected Assets directory '{assetsDir}' does not exist, extraction required");
            return true;
        }

        // Check if required files exist
        foreach (string requiredFile in _requiredAssets)
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
    /// <returns><c>true</c> if assets are current; otherwise, <c>false</c>.</returns>
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
            Version? version = typeof(ServiceConfiguration).Assembly.GetName().Version;
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
    /// <param name="assetsDirectory">The assets directory to validate.</param>
    private static void ValidateAssets(string assetsDirectory)
    {
        List<string> missingFiles = new List<string>();
        foreach (string fileName in _requiredAssets)
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
    /// <param name="assetsDirectory">The directory where the version marker will be written.</param>
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
}
