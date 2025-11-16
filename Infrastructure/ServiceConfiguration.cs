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

using MermaidPad.Services;
using MermaidPad.Services.Export;
using MermaidPad.Services.Highlighting;
using MermaidPad.Services.Platforms;
using MermaidPad.ViewModels;
using MermaidPad.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

using Serilog.Events;

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

        // Configure logging FIRST (before any services that need ILogger)
        ConfigureLogging(services);

        Log.Information("=== MermaidPad Service Configuration Started ===");

        // Add HTTP Client Factory
        services.AddHttpClient();

        // Core singletons
        services.AddSingleton<SettingsService>();
        services.AddSingleton<SecurityService>();
        services.AddSingleton<AssetIntegrityService>();

        // Build a temporary provider to get dependencies for early asset extraction
        string assetsDirectory;
        using (ServiceProvider tempProvider = services.BuildServiceProvider())
        {
            ILogger<AssetService> assetLogger = tempProvider.GetRequiredService<ILogger<AssetService>>();
            SecurityService securityService = tempProvider.GetRequiredService<SecurityService>();
            AssetIntegrityService assetIntegrityService = tempProvider.GetRequiredService<AssetIntegrityService>();
            AssetService assetService = new AssetService(assetLogger, securityService, assetIntegrityService);

            // Extract assets ONCE to user-writable directory (same pattern as settings)
            assetsDirectory = assetService.ExtractAssets();

            // Register the AssetService instance as a singleton
            services.AddSingleton(assetService);
        }

        services.AddSingleton(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            ILogger<MermaidUpdateService> logger = sp.GetRequiredService<ILogger<MermaidUpdateService>>();
            Models.AppSettings settings = sp.GetRequiredService<SettingsService>().Settings;
            return new MermaidUpdateService(settings, assetsDirectory, httpClientFactory, logger);
        });

        services.AddSingleton<SyntaxHighlightingService>();
        services.AddSingleton<MermaidRenderer>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<IDebounceDispatcher, DebounceDispatcher>();
        services.AddSingleton<IImageConversionService, SkiaSharpImageConversionService>();
        services.AddSingleton<IDialogFactory, DialogFactory>();
        services.AddSingleton<IFileService, FileService>();

        // Main ViewModel: transient (one per window)
        services.AddTransient<MainViewModel>();

        // Dialog ViewModels: transient (one per dialog instance)
        services.AddTransient<ExportDialogViewModel>();
        services.AddTransient<ProgressDialogViewModel>();
        services.AddTransient<MessageDialogViewModel>();
        services.AddTransient<ConfirmationDialogViewModel>();

        // Note: Dialog Views (Windows) are not registered in DI
        // They are created directly with 'new' since they need special initialization
        // Only their ViewModels are created through DI

        Log.Information("Building Service Provider");
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        Log.Information("=== MermaidPad Service Configuration Completed ===");
        return serviceProvider;
    }

    /// <summary>
    /// Configures Serilog-based logging for the application.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    private static void ConfigureLogging(ServiceCollection services)
    {
        // Load settings to get logging configuration
        SettingsService settingsService = new SettingsService(logger: null);
        Models.LoggingSettings loggingSettings = settingsService.Settings.Logging;

        // Determine log file path
        string logFilePath = loggingSettings.CustomLogFilePath ?? GetDefaultLogPath();

        // Parse log level
        LogEventLevel minimumLevel = ParseLogLevel(loggingSettings.MinimumLogLevel);

        // Build Serilog configuration
        LoggerConfiguration loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithThreadName()
            .Enrich.WithProcessId()
            .MinimumLevel.Is(minimumLevel);

        // Add file sink if enabled
        if (loggingSettings.EnableFileLogging)
        {
            loggerConfig.WriteTo.File(
                path: logFilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}",
                fileSizeLimitBytes: loggingSettings.FileSizeLimitBytes,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                rollingInterval: RollingInterval.Infinite, // Allow multiple processes to write to the same log file
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: loggingSettings.RetainedFileCountLimit);
        }

        // Add debug sink if enabled
        if (loggingSettings.EnableDebugOutput)
        {
            loggerConfig.WriteTo.Debug(
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}");
        }

        // Create global logger and add to services
        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(static builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });
    }

    /// <summary>
    /// Gets the default log file path.
    /// </summary>
    /// <returns>The full path to the default log file location.</returns>
    private static string GetDefaultLogPath()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataPath, "MermaidPad");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "debug.log");
    }

    /// <summary>
    /// Parses a log level string into a Serilog LogEventLevel.
    /// </summary>
    /// <param name="level">The log level string to parse.</param>
    /// <returns>The corresponding LogEventLevel.</returns>
    private static LogEventLevel ParseLogLevel(string level)
    {
        return level switch
        {
            var l when string.Equals(l, "debug", StringComparison.InvariantCultureIgnoreCase) => LogEventLevel.Debug,
            var l when string.Equals(l, "information", StringComparison.InvariantCultureIgnoreCase) => LogEventLevel.Information,
            var l when string.Equals(l, "warning", StringComparison.InvariantCultureIgnoreCase) => LogEventLevel.Warning,
            var l when string.Equals(l, "error", StringComparison.InvariantCultureIgnoreCase) => LogEventLevel.Error,
            var l when string.Equals(l, "fatal", StringComparison.InvariantCultureIgnoreCase) => LogEventLevel.Fatal,
            _ => LogEventLevel.Debug
        };
    }
}
