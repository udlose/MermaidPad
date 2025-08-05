using MermaidPad.Services;
using MermaidPad.Services.Platforms;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace MermaidPad.Infrastructure;
public static class ServiceConfiguration
{
    public static ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new ServiceCollection();

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

        return services.BuildServiceProvider();
    }

    private static string ExtractAssetsOnce()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Use the SAME directory pattern as SettingsService for consistency
        string assetsDir = GetAssetsDirectory();

        // Check if extraction is needed
        if (ShouldExtractAssets(assetsDir))
        {
            Directory.CreateDirectory(assetsDir);
            EmbeddedResourceHelper.ExtractEmbeddedAssets(assetsDir);

            stopwatch.Stop();
            Debug.WriteLine($"Asset extraction completed: {stopwatch.ElapsedMilliseconds}ms");
        }
        else
        {
            stopwatch.Stop();
            Debug.WriteLine($"Assets current, skipped extraction: {stopwatch.ElapsedMilliseconds}ms");
        }

        return assetsDir;
    }

    /// <summary>
    /// Gets the assets directory using the same pattern as SettingsService.
    /// This ensures cross-platform compatibility and user-writable storage.
    /// </summary>
    private static string GetAssetsDirectory()
    {
        // Use same pattern as SettingsService.GetConfigDirectory()
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDir = Path.Combine(appData, "MermaidPad");
        return Path.Combine(baseDir, "Assets");
    }

    private static bool ShouldExtractAssets(string assetsDir)
    {
        // If directory doesn't exist, definitely extract
        if (!Directory.Exists(assetsDir))
        {
            return true;
        }

        // Check if required files exist
        string indexPath = Path.Combine(assetsDir, "index.html");
        string mermaidPath = Path.Combine(assetsDir, "mermaid.min.js");

        if (!File.Exists(indexPath) || !File.Exists(mermaidPath))
        {
            return true;
        }

        // Use assembly version for cache validation (IL3000-safe)
        return !IsAssetsCurrent(assetsDir);
    }

    private static bool IsAssetsCurrent(string assetsDir)
    {
        try
        {
            string versionMarkerPath = Path.Combine(assetsDir, ".version");
            if (!File.Exists(versionMarkerPath))
            {
                return false;
            }

            string storedVersion = File.ReadAllText(versionMarkerPath).Trim();
            string currentVersion = typeof(ServiceConfiguration).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

            bool isCurrent = storedVersion == currentVersion;
            Debug.WriteLine($"Version check: stored={storedVersion}, current={currentVersion}, isCurrent={isCurrent}");

            return isCurrent;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Version check failed: {ex.Message}");
            return false; // if we can't read version, re-extract to be safe
        }
    }
}