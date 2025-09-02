using MermaidPad.Services;
using MermaidPad.Services.Platforms;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MermaidPad.Infrastructure;

/// <summary>
/// Provides methods for configuring and building the application's dependency injection service provider.
/// Handles asset extraction and validation for MermaidPad.
/// </summary>
public static class ServiceConfiguration
{
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
        string assetsDirectory = AssetHelper.ExtractAssets();

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
}
