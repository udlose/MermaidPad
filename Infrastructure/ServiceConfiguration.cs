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
        services.AddSingleton<SecurityService>();
        services.AddSingleton(sp =>
        {
            Models.AppSettings settings = sp.GetRequiredService<SettingsService>().Settings;
            return new MermaidUpdateService(settings, assetsDirectory);
        });

        services.AddSingleton<SyntaxHighlightingService>();
        services.AddSingleton<MermaidRenderer>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<IDebounceDispatcher, DebounceDispatcher>();
        services.AddSingleton<IImageConversionService, SkiaSharpImageConversionService>();
        services.AddSingleton<IDialogFactory, DialogFactory>();

        // Main ViewModel: transient (one per window)
        services.AddTransient<MainViewModel>();

        // Dialog ViewModels: transient (one per dialog instance)
        services.AddTransient<ExportDialogViewModel>();
        services.AddTransient<ProgressDialogViewModel>();
        services.AddTransient<MessageDialogViewModel>();

        // Note: Dialog Views (Windows) are not registered in DI
        // They are created directly with 'new' since they need special initialization
        // Only their ViewModels are created through DI

        SimpleLogger.Log("=== MermaidPad Service Configuration Completed ===");
        return services.BuildServiceProvider();
    }
}
