using MermaidPad.Services;
using MermaidPad.Services.Platforms;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MermaidPad.Infrastructure;
public static class ServiceConfiguration
{
    public static ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new ServiceCollection();

        // Core singletons
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp =>
        {
            Models.AppSettings settings = sp.GetRequiredService<SettingsService>().Settings;
            string assetsDir = PlatformServiceFactory.Instance.GetAssetsDirectory();
            return new MermaidUpdateService(settings, assetsDir);
        });

        // Renderer (WebView assigned later)
        services.AddSingleton<MermaidRenderer>();

        // Debounce dispatcher
        services.AddSingleton<IDebounceDispatcher, DebounceDispatcher>();

        // ViewModel: transient (one per window)
        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
