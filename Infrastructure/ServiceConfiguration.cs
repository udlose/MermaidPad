using MermaidPad.Services;
using MermaidPad.Services.Platforms;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace MermaidPad.Infrastructure;
/// <summary>
/// Provides methods for configuring and building the application's dependency injection service provider.
/// Handles asset extraction and validation for MermaidPad.
/// </summary>
public static class ServiceConfiguration
{
    private const string MermaidMinJsFileName = "mermaid.min.js";
    private const string IndexHtmlFileName = "index.html";

    /// <summary>
    /// Builds and configures the application's service provider.
    /// Registers core services, asset extraction, and view models.
    /// </summary>
    /// <returns>A fully configured <see cref="ServiceProvider"/> instance.</returns>
    public static ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new ServiceCollection();

        SimpleLogger.Log("=== MermaidPad Service Configuration Started ===");

        // Extract assets ONCE to user-writable directory (same pattern as settings)
        string assetsDirectory = ExtractAssetsOnce();

        // Core singletons
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp =>
        {
            Models.AppSettings settings = sp.GetRequiredService<SettingsService>().Settings;
            return new MermaidUpdateService(settings, assetsDirectory);
        });

        // Renderer (WebView assigned later)
        services.AddSingleton<MermaidRenderer>();

        // Debounce dispatcher
        services.AddSingleton<IDebounceDispatcher, DebounceDispatcher>();

        // ViewModel: transient (one per window)
        services.AddTransient<MainViewModel>();

        SimpleLogger.Log("=== MermaidPad Service Configuration Completed ===");
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Extracts embedded assets to the assets directory if required.
    /// Validates asset existence and version, and logs timing information.
    /// </summary>
    /// <returns>The path to the assets directory.</returns>
    private static string ExtractAssetsOnce()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        SimpleLogger.Log("Asset extraction process starting...");

        // Use the SAME directory pattern as SettingsService for consistency
        string assetsDir = GetAssetsDirectory();
        SimpleLogger.Log($"Assets directory: {assetsDir}");

        // Check if extraction is needed
        if (ShouldExtractAssets(assetsDir))
        {
            SimpleLogger.Log("Assets require extraction/update");
            Directory.CreateDirectory(assetsDir);

            try
            {
                EmbeddedResourceHelper.ExtractEmbeddedAssets(assetsDir);

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
        ValidateAssets(assetsDir);

        return assetsDir;
    }

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
        string indexPath = Path.Combine(assetsDir, IndexHtmlFileName);
        if (!File.Exists(indexPath))
        {
            SimpleLogger.Log($"Missing critical asset: {IndexHtmlFileName}");
            return true;
        }

        string mermaidPath = Path.Combine(assetsDir, MermaidMinJsFileName);
        if (!File.Exists(mermaidPath))
        {
            SimpleLogger.Log($"Missing critical asset: {MermaidMinJsFileName}");
            return true;
        }

        // Use assembly version for cache validation (IL3000-safe)
        bool isCurrent = IsAssetsCurrent(assetsDir);
        SimpleLogger.Log($"Asset currency check result: {isCurrent}");
        return !isCurrent;
    }

    /// <summary>
    /// Checks if the assets in the specified directory are current by comparing version markers.
    /// </summary>
    /// <param name="assetsDir">The assets directory to check.</param>
    /// <returns><c>true</c> if assets are current; otherwise, <c>false</c>.</returns>
    private static bool IsAssetsCurrent(string assetsDir)
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
    /// <param name="assetsDir">The assets directory to validate.</param>
    private static void ValidateAssets(string assetsDir)
    {
        string[] requiredFiles = [IndexHtmlFileName, MermaidMinJsFileName];
        bool allValid = true;

        foreach (string fileName in requiredFiles)
        {
            string filePath = Path.Combine(assetsDir, fileName);
            if (File.Exists(filePath))
            {
                FileInfo fileInfo = new FileInfo(filePath);
                SimpleLogger.LogAsset("validated", fileName, true, fileInfo.Length);
            }
            else
            {
                SimpleLogger.LogAsset("validated", fileName, false);
                allValid = false;
            }
        }

        if (allValid)
        {
            SimpleLogger.Log("All required assets validated successfully");
        }
        else
        {
            SimpleLogger.LogError("Asset validation failed - some required files are missing");
            throw new InvalidOperationException("Critical assets are missing after extraction");
        }
    }
}
