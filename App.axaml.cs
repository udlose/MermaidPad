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

using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MermaidPad.Infrastructure;
using MermaidPad.Services;
using MermaidPad.Views;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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

#if DEBUG
        this.AttachDeveloperTools();
#endif
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
        LogExceptionWithContext("Unhandled UI thread exception", e.Exception, "UI Thread");

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
            LogExceptionWithContext($"Unhandled background thread exception (Terminating: {e.IsTerminating})",
                exception,
                "Background Thread");

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
        LogExceptionWithContext("Unobserved task exception", e.Exception, "Task Thread Pool");

        // Show error dialog to user
        ShowErrorDialog(e.Exception, "An unexpected error occurred during an asynchronous operation.");

        // Mark as observed to prevent default handling
        e.SetObserved();
    }

    /// <summary>
    /// Displays an error dialog to the user with the specified exception details and user-friendly message.
    /// </summary>
    /// <remarks>This method ensures that the error dialog is displayed on the UI thread. If the current
    /// thread is not the UI thread, the operation is marshaled to the UI thread. In case of a failure to display the
    /// dialog, the error is logged as a last resort.</remarks>
    /// <param name="exception">The exception that describes the error. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="userMessage">A user-friendly message to display in the error dialog. This parameter cannot be <see langword="null"/> or
    /// empty.</param>
    private void ShowErrorDialog(Exception exception, string userMessage)
    {
        try
        {
            // Marshal to UI thread if necessary
            if (Dispatcher.UIThread.CheckAccess())
            {
                ShowErrorDialogCoreAsync(exception, userMessage)
                    .SafeFireAndForget(
                        onException: static ex =>
                        {
                            SimpleLogger.LogError("Unhandled exception in ShowErrorDialogCoreAsync (UI)", ex);
                            Debug.Fail($"Unhandled exception in ShowErrorDialogCoreAsync (UI): {ex}");
                        },
                        continueOnCapturedContext: false);
            }
            else
            {
                Dispatcher.UIThread
                    .InvokeAsync(() => ShowErrorDialogCoreAsync(exception, userMessage))
                    .SafeFireAndForget(
                        onException: static ex =>
                        {
                            SimpleLogger.LogError("Unhandled exception scheduling ShowErrorDialogCoreAsync", ex);
                            Debug.Fail($"Unhandled exception scheduling ShowErrorDialogCoreAsync: {ex}");
                        },
                        continueOnCapturedContext: false);
            }
        }
        catch (Exception ex)
        {
            // Last resort logging if we can't even show the error dialog
            SimpleLogger.LogError("Failed to show error dialog", ex);
            Debug.Fail($"Failed to show error dialog: {ex}");
        }
    }

    /// <summary>
    /// Displays an error dialog with the specified user-friendly message and comprehensive technical details about the exception.
    /// </summary>
    /// <remarks>This method must be called on the UI thread. If the main application window is unavailable,
    /// the dialog will not be shown. The dialog includes user-friendly message, exception details, stack trace,
    /// and a button to copy full details to clipboard.</remarks>
    /// <param name="exception">The exception containing technical details to display in the error dialog.</param>
    /// <param name="userMessage">A user-friendly message to display at the top of the error dialog.</param>
    /// <returns>A task that represents the asynchronous operation of showing the error dialog.</returns>
    private async Task ShowErrorDialogCoreAsync(Exception exception, string userMessage)
    {
        try
        {
            // Enforce UI-thread-only contract
            Dispatcher.UIThread.VerifyAccess();

            // Get the main window if available
            Window? mainWindow = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow is null)
            {
                return;
            }

            // Build comprehensive exception details
            string fullExceptionDetails = BuildExceptionDetails(exception);

            // Build user-facing summary
            StringBuilder errorSummary = new StringBuilder();
            errorSummary.AppendLine(userMessage);
            errorSummary.AppendLine();
            errorSummary.AppendLine($"Error Type: {exception.GetType().FullName}");
            errorSummary.AppendLine($"Message: {exception.Message}");

            // Add source location if available
            string? sourceLocation = GetExceptionSourceLocation(exception);
            if (!string.IsNullOrEmpty(sourceLocation))
            {
                errorSummary.AppendLine($"Location: {sourceLocation}");
            }

            errorSummary.AppendLine();
            errorSummary.AppendLine("Full details have been logged. Click 'Copy Details' to copy technical information to clipboard.");

            // Create error window with enhanced layout
            Window errorWindow = new Window
            {
                Title = "Application Error",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true,
                MinWidth = 400,
                MinHeight = 300
            };

            StackPanel mainPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // User-facing summary (scrollable)
            ScrollViewer summaryScroller = new ScrollViewer
            {
                MaxHeight = 250,
                Margin = new Thickness(0, 0, 0, 15)
            };

            TextBlock summaryText = new TextBlock
            {
                Text = errorSummary.ToString(),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            summaryScroller.Content = summaryText;
            mainPanel.Children.Add(summaryScroller);

            // Button panel
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            // Copy Details button
            Button copyButton = new Button
            {
                Content = "Copy Details",
                Width = 120
            };

            copyButton.Click += (_, _) =>
            {
                CopyExceptionDetailsToClipboardAsync(mainWindow, copyButton, fullExceptionDetails)
                    .SafeFireAndForget(onException: ex =>
                    {
                        SimpleLogger.LogError("Failed to copy exception details to clipboard", ex);
                        Debug.WriteLine($"Failed to copy to clipboard: {ex}");
                    });
            };

            buttonPanel.Children.Add(copyButton);

            // OK button
            Button okButton = new Button
            {
                Content = "OK",
                Width = 100
            };

            okButton.Click += (_, _) => errorWindow.Close();
            buttonPanel.Children.Add(okButton);

            mainPanel.Children.Add(buttonPanel);
            errorWindow.Content = mainPanel;

            await errorWindow.ShowDialog(mainWindow);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to display error dialog", ex);
            Debug.Fail($"Failed to display error dialog: {ex}");
        }
    }

    #region Exception Detail Helpers

    /// <summary>
    /// Copies exception details to the clipboard and provides visual feedback.
    /// </summary>
    /// <param name="window">The main window (for clipboard access).</param>
    /// <param name="copyButton">The copy button to update with feedback.</param>
    /// <param name="exceptionDetails">The exception details to copy.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task CopyExceptionDetailsToClipboardAsync(Window window, Button copyButton, string exceptionDetails)
    {
        await window.Clipboard!.SetTextAsync(exceptionDetails);
        copyButton.Content = "Copied!";
        await Task.Delay(2000);
        copyButton.Content = "Copy Details";
    }

    /// <summary>
    /// Logs exception with comprehensive context including thread information and full exception chain.
    /// </summary>
    /// <param name="message">The log message describing the exception context.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="threadContext">Description of the thread context (e.g., "UI Thread", "Background Thread").</param>
    private static void LogExceptionWithContext(string message, Exception exception, string threadContext)
    {
        StringBuilder logEntry = new StringBuilder();
        logEntry.AppendLine("═══════════════════════════════════════════════════════════════");
        logEntry.AppendLine($"EXCEPTION: {message}");
        logEntry.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        logEntry.AppendLine($"Thread Context: {threadContext}");
        logEntry.AppendLine($"Thread ID: {Environment.CurrentManagedThreadId}");
        logEntry.AppendLine($"Is ThreadPool Thread: {Thread.CurrentThread.IsThreadPoolThread}");
        logEntry.AppendLine($"Is Background Thread: {Thread.CurrentThread.IsBackground}");

        string? threadName = Thread.CurrentThread.Name;
        if (!string.IsNullOrEmpty(threadName))
        {
            logEntry.AppendLine($"Thread Name: {threadName}");
        }

        logEntry.AppendLine("───────────────────────────────────────────────────────────────");
        logEntry.AppendLine(BuildExceptionDetails(exception));
        logEntry.AppendLine("═══════════════════════════════════════════════════════════════");

        SimpleLogger.LogError(logEntry.ToString());
    }

    /// <summary>
    /// Builds comprehensive exception details including full exception chain, stack traces, and context.
    /// </summary>
    /// <param name="exception">The exception to format.</param>
    /// <returns>A formatted string containing all exception details.</returns>
    private static string BuildExceptionDetails(Exception exception)
    {
        StringBuilder details = new StringBuilder();

        // Handle AggregateException specially to show all inner exceptions
        if (exception is AggregateException aggregateException)
        {
            details.AppendLine($"Exception Type: {exception.GetType().FullName}");
            details.AppendLine($"Message: {exception.Message}");
            details.AppendLine();
            details.AppendLine($"Contains {aggregateException.InnerExceptions.Count} inner exception(s):");
            details.AppendLine();

            for (int i = 0; i < aggregateException.InnerExceptions.Count; i++)
            {
                details.AppendLine($"───── Inner Exception #{i + 1} ─────");
                details.AppendLine(FormatSingleException(aggregateException.InnerExceptions[i], includeStackTrace: true));
                details.AppendLine();
            }
        }
        else
        {
            // Format exception chain
            int exceptionDepth = 0;
            Exception? currentException = exception;

            while (currentException != null)
            {
                if (exceptionDepth > 0)
                {
                    details.AppendLine();
                    details.AppendLine($"───── Inner Exception (Depth {exceptionDepth}) ─────");
                }

                details.AppendLine(FormatSingleException(currentException, includeStackTrace: exceptionDepth == 0));

                currentException = currentException.InnerException;
                exceptionDepth++;
            }

            // If we had inner exceptions, show the full chain summary
            if (exceptionDepth > 1)
            {
                details.AppendLine();
                details.AppendLine($"Exception Chain Depth: {exceptionDepth}");
            }
        }

        return details.ToString();
    }

    /// <summary>
    /// Formats a single exception with all available details.
    /// </summary>
    /// <param name="exception">The exception to format.</param>
    /// <param name="includeStackTrace">Whether to include the full stack trace.</param>
    /// <returns>Formatted exception string.</returns>
    private static string FormatSingleException(Exception exception, bool includeStackTrace)
    {
        StringBuilder details = new StringBuilder();

        details.AppendLine($"Exception Type: {exception.GetType().FullName}");
        details.AppendLine($"Message: {exception.Message}");

        // Add HResult if available
        if (exception.HResult != 0)
        {
            details.AppendLine($"HResult: 0x{exception.HResult:X8} ({exception.HResult})");
        }

        // Add source location if available
        string? sourceLocation = GetExceptionSourceLocation(exception);
        if (!string.IsNullOrEmpty(sourceLocation))
        {
            details.AppendLine($"Source Location: {sourceLocation}");
        }

        // Add source assembly
        if (!string.IsNullOrEmpty(exception.Source))
        {
            details.AppendLine($"Source: {exception.Source}");
        }

        // Add target site (method that threw)
        if (exception.TargetSite != null)
        {
            details.AppendLine($"Target Site: {exception.TargetSite.DeclaringType?.FullName}.{exception.TargetSite.Name}");
        }

        // Add custom data if present
        if (exception.Data.Count > 0)
        {
            details.AppendLine("Data:");
            foreach (System.Collections.DictionaryEntry entry in exception.Data)
            {
                details.AppendLine($"  {entry.Key}: {entry.Value}");
            }
        }

        // Add stack trace if requested
        if (includeStackTrace && !string.IsNullOrEmpty(exception.StackTrace))
        {
            details.AppendLine();
            details.AppendLine("Stack Trace:");
            details.AppendLine(exception.StackTrace);
        }

        return details.ToString();
    }

    /// <summary>
    /// Extracts the source file and line number from the exception stack trace.
    /// </summary>
    /// <param name="exception">The exception to extract source location from.</param>
    /// <returns>A string in format "FileName.cs:line 123" or null if not available.</returns>
    private static string? GetExceptionSourceLocation(Exception exception)
    {
        if (string.IsNullOrEmpty(exception.StackTrace))
        {
            return null;
        }

        // Try to extract file and line number from stack trace
        // Format is typically: "at Namespace.Class.Method() in C:\path\to\File.cs:line 123"
        string[] lines = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            // Look for " in " followed by file path
            int inIndex = line.IndexOf(" in ", StringComparison.Ordinal);
            if (inIndex > 0)
            {
                string pathPart = line[(inIndex + 4)..].Trim();

                // Extract just the filename and line number
                int lineIndex = pathPart.LastIndexOf(":line ", StringComparison.Ordinal);
                if (lineIndex > 0)
                {
                    string filePath = pathPart[..lineIndex];
                    string fileName = Path.GetFileName(filePath);
                    string lineNumber = pathPart[(lineIndex + 6)..];

                    return $"{fileName}:line {lineNumber}";
                }
                else
                {
                    // Just return the filename if no line number
                    string fileName = Path.GetFileName(pathPart);
                    return fileName;
                }
            }
        }

        return null;
    }

    #endregion
}
