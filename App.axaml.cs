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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MermaidPad.Infrastructure;
using MermaidPad.Services;
using MermaidPad.Views;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MermaidPad;

/// <summary>
/// Represents the entry point for the application, providing initialization and configuration logic.
/// </summary>
/// <remarks>The <see cref="App"/> class is responsible for setting up the application, including initializing
/// services, configuring global exception handlers, and preparing the main user interface. It extends the <see
/// cref="Application"/> class and overrides key lifecycle methods such as <see cref="Initialize"/> and <see
/// cref="OnFrameworkInitializationCompleted"/> to ensure the application is properly configured before it starts
/// running.</remarks>
public sealed partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Initializes the component and loads its associated XAML content.
    /// </summary>
    /// <remarks>This method should be called to ensure that the component's user interface is properly loaded
    /// and ready for use. It uses the AvaloniaXamlLoader to load the XAML content associated with this
    /// component.</remarks>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Sets up comprehensive global exception handlers to catch unhandled exceptions across all threading contexts.
    /// This prevents the application from crashing silently and provides proper error logging and user notification.
    /// </summary>
    /// <remarks>
    /// This method configures three levels of exception handling:
    /// <list type="number">
    /// <item><description>UI Thread exceptions via Dispatcher.UIThread.UnhandledException</description></item>
    /// <item><description>Background thread exceptions via AppDomain.CurrentDomain.UnhandledException</description></item>
    /// <item><description>Unobserved Task exceptions via TaskScheduler.UnobservedTaskException</description></item>
    /// </list>
    /// All exceptions are logged and shown to the user with a friendly error dialog.
    /// </remarks>
    private void SetupGlobalExceptionHandlers()
    {
        // Handle exceptions on the UI thread (Avalonia-specific)
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;

        // Handle exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        // Handle unobserved task exceptions
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

        SimpleLogger.Log("Global exception handlers initialized");
    }

    /// <summary>
    /// Completes the framework initialization process by configuring application services, setting up global exception
    /// handlers, and initializing the main window or view depending on the application lifetime.
    /// </summary>
    /// <remarks>This method is called automatically during the application startup sequence. It ensures that
    /// the application is properly configured before the main user interface is displayed.
    /// For desktop applications, the <see cref="MainWindow"/> is set as the main window.
    /// For single-view applications, the <see cref="MainWindow"/> is set as the main view.</remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        // Set up global exception handlers before doing anything else
        SetupGlobalExceptionHandlers();

        Services = ServiceConfiguration.BuildServiceProvider();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow();      // Set the main window for desktop applications
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainWindow(); // Set the main view for single view applications
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Disables the Avalonia Data Annotations validation by removing all instances of
    /// <see cref="DataAnnotationsValidationPlugin"/> from the binding plugins.
    /// </summary>
    /// <remarks>This method iterates through the collection of data validation plugins in
    /// <see cref="BindingPlugins.DataValidators"/> and removes all plugins of type
    /// <see cref="DataAnnotationsValidationPlugin"/>. After calling this method,
    /// Avalonia's Data Annotations validation will no longer be applied.</remarks>
    [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "<Pending>")]
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (DataAnnotationsValidationPlugin plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    /// <summary>
    /// Handles unhandled exceptions that occur on the UI thread (Avalonia Dispatcher).
    /// </summary>
    /// <remarks>
    /// This handler catches exceptions from UI operations, data binding, and event handlers.
    /// The exception is logged and a user-friendly error dialog is shown.
    /// </remarks>
    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SimpleLogger.LogError("Unhandled UI thread exception", e.Exception);

        // Show error dialog to user
        ShowErrorDialog(e.Exception, "An unexpected error occurred in the user interface.");

        // Mark as handled to prevent application crash
        e.Handled = true;
    }

    /// <summary>
    /// Handles unhandled exceptions that occur on background threads.
    /// </summary>
    /// <remarks>
    /// This handler catches exceptions from non-UI threads that aren't caught by try-catch blocks.
    /// Since the CLR will terminate the process for these exceptions, we log and notify but cannot prevent termination.
    /// </remarks>
    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            SimpleLogger.LogError($"Unhandled background thread exception (Terminating: {e.IsTerminating})", exception);

            // Try to show error dialog, but this may fail if we're terminating
            if (!e.IsTerminating)
            {
                try
                {
                    ShowErrorDialog(exception, "An unexpected error occurred in a background operation.");
                }
                catch (Exception dialogEx)
                {
                    SimpleLogger.LogError("Failed to show error dialog for background exception", dialogEx);
                }
            }
        }
        else
        {
            SimpleLogger.LogError($"Unhandled non-exception object: {e.ExceptionObject}");
        }
    }

    /// <summary>
    /// Handles task exceptions that were not observed (no await, no .Result, no .Wait()).
    /// </summary>
    /// <remarks>
    /// This handler catches exceptions from Tasks that complete with an exception but are never observed.
    /// In .NET 4.5+, these don't crash the app by default, but we should still log them.
    /// </remarks>
    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        SimpleLogger.LogError("Unobserved task exception", e.Exception);

        // Show error dialog to user
        ShowErrorDialog(e.Exception, "An unexpected error occurred during an asynchronous operation.");

        // Mark as observed to prevent default handling
        e.SetObserved();
    }

    /// <summary>
    /// Shows a user-friendly error dialog with exception details.
    /// </summary>
    /// <param name="exception">The exception to display.</param>
    /// <param name="userMessage">A user-friendly message explaining the context.</param>
    /// <remarks>
    /// This method marshals to the UI thread if necessary and shows a MessageBox with error details.
    /// For production builds, you might want to hide technical details from end users.
    /// </remarks>
    private void ShowErrorDialog(Exception exception, string userMessage)
    {
        try
        {
            // Marshal to UI thread if necessary
            if (Dispatcher.UIThread.CheckAccess())
            {
                ShowErrorDialogCore(exception, userMessage);
            }
            else
            {
                Dispatcher.UIThread.Post(() => ShowErrorDialogCore(exception, userMessage));
            }
        }
        catch (Exception ex)
        {
            // Last resort logging if we can't even show the error dialog
            SimpleLogger.LogError("Failed to show error dialog", ex);
        }
    }

    /// <summary>
    /// Core implementation of error dialog display (must be called on UI thread).
    /// </summary>
    private void ShowErrorDialogCore(Exception exception, string userMessage)
    {
        try
        {
            // Get the main window if available
            Window? mainWindow = null;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                mainWindow = desktop.MainWindow;
            }

            // Build error message with technical details
            StringBuilder errorDetails = new StringBuilder($"{userMessage}{Environment.NewLine}{Environment.NewLine}");
            errorDetails.AppendLine($"Error Type: {exception.GetType().Name}{Environment.NewLine}");
            errorDetails.AppendLine($"Message: {exception.Message}{Environment.NewLine}{Environment.NewLine}");
            errorDetails.AppendLine("Please check the log file for more details.");

            // Show message box asynchronously to avoid blocking
            _ = Task.Run(async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (mainWindow is not null)
                    {
                        // Create a simple error window
                        Window errorWindow = new Window
                        {
                            Title = "Error",
                            Width = 500,
                            Height = 300,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            CanResize = false
                        };

                        StackPanel stackPanel = new StackPanel
                        {
                            Margin = new Thickness(20)
                        };

                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = errorDetails.ToString(),
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20)
                        });

                        Button okButton = new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Width = 100
                        };

                        okButton.Click += (_, _) => errorWindow.Close();
                        stackPanel.Children.Add(okButton);

                        errorWindow.Content = stackPanel;

                        await errorWindow.ShowDialog(mainWindow);
                    }
                });
            });
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to display error dialog", ex);
        }
    }
}
