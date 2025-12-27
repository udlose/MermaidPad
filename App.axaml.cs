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
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaloniaWebView;
using MermaidPad.Infrastructure;
using MermaidPad.ViewModels;
using MermaidPad.Views;
using Serilog;
using System.Diagnostics;
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
public sealed partial class App : Application, IDisposable
{
    public static IServiceProvider Services { get; private set; } = null!;
    private static readonly string[] _newlineCharacters = ["\r\n", "\r", "\n"];

    private IClassicDesktopStyleApplicationLifetime? _desktopLifetime;
    private int _desktopLifetimeEventsHooked;
    private int _isDisposedFlag;

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

        Log.Information("Global exception handlers initialized");
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

            _desktopLifetime = desktop;
            HookDesktopLifetimeEvents(desktop);

            try
            {
                SplashWindowViewModel splashWindowViewModel = new SplashWindowViewModel();

                // Show splash screen first, then main window
                desktop.MainWindow = new SplashWindow(splashWindowViewModel, OnSplashCompletedShowMainWindow);
            }
            catch (Exception ex)
            {
                const string errorMessage = "An error occurred during application initialization.";
                //TODO - this doesn't work. need to figure out how to show a dialog before main window exists
                //ShowErrorDialog(ex, errorMessage);
                Log.Error(ex, errorMessage);
            }

            // Hook up cleanup on application exit
            desktop.ShutdownRequested += OnShutdownRequested;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainWindow(); // Set the main view for single view applications
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Registers application services required for web view functionality.
    /// </summary>
    /// <remarks>This method initializes services necessary for Avalonia WebView integration. It should be
    /// called during application startup to ensure that web view components are properly configured.</remarks>
    public override void RegisterServices()
    {
        base.RegisterServices();

        AvaloniaWebViewBuilder.Initialize(null);
    }

    /// <summary>
    /// Handles the shutdown request event, allowing the operation to be canceled if necessary.
    /// </summary>
    /// <remarks>Use this event handler to prompt the user to save work or perform other checks before
    /// shutdown. Other windows or handlers may also cancel the shutdown after this event is processed.</remarks>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">A <see cref="ShutdownRequestedEventArgs"/> that contains the event data. Set <c>e.Cancel</c> to <see
    /// langword="true"/> to cancel the shutdown request.</param>
    [SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Local", Justification = "Avalonia requires instance methods for certain operations.")]
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Do not Dispose() here: disposal of application resources is intentionally deferred to OnDesktopExit,
        // where it is performed after all shutdown cancellation opportunities (including this event and any other
        // window handlers) have been processed.
        // Use this event only to optionally set e.Cancel (e.g., unsaved work prompt), because other windows may still cancel later.
    }

    /// <summary>
    /// Displays the application's main window and sets it as the primary window for the desktop lifetime.
    /// </summary>
    /// <remarks>If the desktop lifetime is not available, the method does nothing. This method should be
    /// called when the main window needs to be shown and focused for user interaction.</remarks>
    private void OnSplashCompletedShowMainWindow()
    {
        if (_desktopLifetime is null)
        {
            return;
        }

        MainWindow mainWindow = new MainWindow();
        mainWindow.Show();
        mainWindow.Focus();

        _desktopLifetime.MainWindow = mainWindow;
    }

    /// <summary>
    /// Handles the event that occurs when the desktop application is exiting.
    /// </summary>
    /// <param name="sender">The source of the event. This is typically the application lifetime object.</param>
    /// <param name="e">An object that contains the event data for the exit event.</param>
    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        Dispose();
    }

    /// <summary>
    /// Attaches application lifetime event handlers to the specified classic desktop-style application lifetime.
    /// </summary>
    /// <param name="desktop">The application lifetime object representing the classic desktop environment to which event handlers will be
    /// attached. Cannot be null.</param>
    private void HookDesktopLifetimeEvents(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (Interlocked.Exchange(ref _desktopLifetimeEventsHooked, 1) != 0)
        {
            return;
        }

        desktop.ShutdownRequested += OnShutdownRequested;
        desktop.Exit += OnDesktopExit;
    }

    /// <summary>
    /// Detaches event handlers from the current desktop lifetime instance and releases its reference.
    /// </summary>
    /// <remarks>Call this method to clean up resources associated with the desktop lifetime. After calling
    /// this method, the desktop lifetime instance will no longer receive shutdown or exit events.</remarks>
    private void UnhookDesktopLifetimeEvents()
    {
        if (_desktopLifetime is null)
        {
            return;
        }

        _desktopLifetime.ShutdownRequested -= OnShutdownRequested;
        _desktopLifetime.Exit -= OnDesktopExit;

        _desktopLifetime = null;
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
                    Log.Error(dialogEx, "Failed to show error dialog for background exception");
                }
            }
        }
        else
        {
            Log.Error("Unhandled non-exception object: {ExceptionObject}", e.ExceptionObject);
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
                            Log.Error(ex, "Unhandled exception in ShowErrorDialogCoreAsync (UI)");
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
                            Log.Error(ex, "Unhandled exception scheduling ShowErrorDialogCoreAsync");
                            Debug.Fail($"Unhandled exception scheduling ShowErrorDialogCoreAsync: {ex}");
                        },
                        continueOnCapturedContext: false);
            }
        }
        catch (Exception ex)
        {
            // Last resort logging if we can't even show the error dialog
            Log.Error(ex, "Failed to show error dialog");
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
    [SuppressMessage("Style", "IDE0061:Use expression body for local function", Justification = "Code reads better this way in this case.")]
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

            // Store event handlers for cleanup to prevent memory leaks
            copyButton.Click += CopyClickHandler;
            buttonPanel.Children.Add(copyButton);

            // OK button
            Button okButton = new Button
            {
                Content = "OK",
                Width = 100
            };

            okButton.Click += OkClickHandler;
            buttonPanel.Children.Add(okButton);

            errorWindow.Closed += ErrorWindowClosedHandler;
            mainPanel.Children.Add(buttonPanel);
            errorWindow.Content = mainPanel;

            await errorWindow.ShowDialog(mainWindow);

            void CopyClickHandler(object? sender, RoutedEventArgs routedEventArgs)
            {
                CopyExceptionDetailsToClipboardAsync(mainWindow, copyButton, fullExceptionDetails)
                    .SafeFireAndForget(onException: static ex =>
                    {
                        Log.Error(ex, "Failed to copy exception details to clipboard");
                        Debug.WriteLine($"Failed to copy to clipboard: {ex}");
                    });
            }

            void OkClickHandler(object? sender, RoutedEventArgs routedEventArgs)
            {
                errorWindow.Close();
            }

            void ErrorWindowClosedHandler(object? sender, EventArgs e)
            {
                copyButton.Click -= CopyClickHandler;
                okButton.Click -= OkClickHandler;
                errorWindow.Closed -= ErrorWindowClosedHandler;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to display error dialog");
            Debug.Fail($"Failed to display error dialog: {ex}");
        }
    }

    #region Exception Detail Helpers

    /// <summary>
    /// Copies the specified exception details to the clipboard and provides visual feedback on the copy button.
    /// </summary>
    /// <remarks>The copy button's content is set to "Copied!" for two seconds after the details are copied,
    /// then restored to its original text. This method is intended for use in UI scenarios where immediate user
    /// feedback is required.</remarks>
    /// <param name="window">The window instance whose clipboard will be used to store the exception details. Must not be null and must have
    /// a valid clipboard service.</param>
    /// <param name="copyButton">The button that displays copy status feedback to the user. Its content will be temporarily updated to indicate
    /// success.</param>
    /// <param name="exceptionDetails">The exception details text to copy to the clipboard. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    private static async Task CopyExceptionDetailsToClipboardAsync(Window? window, Button? copyButton, string? exceptionDetails)
    {
        // Defensive checks - swallow problems early so SafeFireAndForget's handler is never relied on
        if (window is null || copyButton is null || exceptionDetails is null)
        {
            Log.Error("{MethodName} called with null argument(s)", nameof(CopyExceptionDetailsToClipboardAsync));
            return;
        }

        // Keep original content so we can always restore it
        object? originalContent = copyButton.Content;
        const int displayDurationMs = 2_000;
        try
        {
            if (window.Clipboard is null)
            {
                await SafeSetButtonContentAsync(copyButton, "Clipboard Unavailable");
                await Task.Delay(displayDurationMs);
                return;
            }

            try
            {
                // Attempt the clipboard operation first
                await window.Clipboard.SetTextAsync(exceptionDetails);

                // Only indicate success after the clipboard op completed successfully
                await SafeSetButtonContentAsync(copyButton, "Copied!");
            }
            catch (Exception ex)
            {
                // Handle clipboard-specific failures here so the button is never left in an inconsistent state.
                Log.Error(ex, "Failed to copy exception details to clipboard");
                Debug.WriteLine($"Failed to copy to clipboard: {ex}");

                // Show a transient failure state to the user before restoring original content
                await SafeSetButtonContentAsync(copyButton, "Copy Failed");
            }

            // Keep transient state visible for a short, fixed period
            await Task.Delay(displayDurationMs);
        }
        catch (Exception ex)
        {
            // Catch any unexpected error inside this helper to avoid bubbling to SafeFireAndForget's handler
            Log.Error(ex, "Unexpected error in CopyExceptionDetailsToClipboardAsync");
            Debug.WriteLine($"Unexpected error in CopyExceptionDetailsToClipboardAsync: {ex}");
        }
        finally
        {
            // Ensure original content is restored regardless of what happened above
            await SafeSetButtonContentAsync(copyButton, originalContent ?? "Copy Details");
        }

        // Helper to update button content on the UI thread without throwing
        static async Task SafeSetButtonContentAsync(Button btn, object? content)
        {
            try
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    btn.Content = content;
                    return;
                }
                await Dispatcher.UIThread.InvokeAsync(() => btn.Content = content);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update button content on UI thread");
            }
        }
    }

    /// <summary>
    /// Logs detailed information about an exception, including contextual thread data and a custom message, to the
    /// error log.
    /// </summary>
    /// <remarks>The log entry includes the exception details, timestamp, thread information, and the provided
    /// context message. This method is intended for diagnostic purposes and should be used to capture comprehensive
    /// error information in multithreaded environments.</remarks>
    /// <param name="message">A descriptive message providing context for the exception being logged.</param>
    /// <param name="exception">The exception instance containing error details to be logged. Cannot be null.</param>
    /// <param name="threadContext">A string representing the logical context or name of the thread where the exception occurred - e.g. "UI Thread", "Background Thread", "ThreadPool Thread".</param>
    private static void LogExceptionWithContext(string message, Exception exception, string threadContext)
    {
        StringBuilder logEntry = new StringBuilder(256);
        logEntry.AppendLine("---------------------------------------------------------------");
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

        logEntry.AppendLine("---------------------------------------------------------------");
        logEntry.AppendLine(BuildExceptionDetails(exception));
        logEntry.AppendLine("---------------------------------------------------------------");

        Log.Error(exception, "{LogEntry}", logEntry.ToString());
    }

    /// <summary>
    /// Builds a detailed, human-readable string representation of the specified exception, including its type, message,
    /// and any inner exceptions.
    /// </summary>
    /// <remarks>This method is useful for logging or displaying exception details in diagnostic scenarios.
    /// The output includes stack traces for the top-level exception and for each inner exception in the case of <see
    /// cref="AggregateException"/>. For other exception types, the entire inner exception chain is traversed and
    /// formatted.</remarks>
    /// <param name="exception">The exception to format. If the exception is an <see cref="AggregateException"/>, all inner exceptions are
    /// included; otherwise, the full inner exception chain is displayed.</param>
    /// <returns>A string containing detailed information about the exception and its inner exceptions, including type, message,
    /// and stack trace where applicable.</returns>
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
                details.AppendLine($"------ Inner Exception #{i + 1} ------");
                details.AppendLine(FormatSingleException(aggregateException.InnerExceptions[i], includeStackTrace: true));
                details.AppendLine();
            }
        }
        else
        {
            // Format exception chain
            int exceptionDepth = 0;
            Exception? currentException = exception;

            while (currentException is not null)
            {
                if (exceptionDepth > 0)
                {
                    details.AppendLine();
                    details.AppendLine($"------ Inner Exception (Depth {exceptionDepth}) ------");
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
    /// Formats detailed information about a single exception into a readable string, optionally including the stack
    /// trace.
    /// </summary>
    /// <remarks>The formatted output includes key exception properties and any custom data. This method does
    /// not format inner exceptions; only the provided exception is included.</remarks>
    /// <param name="exception">The exception to format. Cannot be null.</param>
    /// <param name="includeStackTrace">Specifies whether to include the stack trace in the formatted output. Set to <see langword="true"/> to include
    /// the stack trace; otherwise, <see langword="false"/>.</param>
    /// <returns>A string containing formatted details about the specified exception, including type, message, HResult, source
    /// location, source assembly, target site, custom data, and optionally the stack trace.</returns>
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
        if (exception.TargetSite is not null)
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
    /// Extracts the source file name and line number from the stack trace of the specified exception, if available.
    /// </summary>
    /// <remarks>This method parses the stack trace of the provided exception to locate source file and line
    /// number information. If the stack trace does not contain file or line details, the method returns null. The
    /// returned value is intended for diagnostic or logging purposes.</remarks>
    /// <param name="exception">The exception from which to retrieve the source location information. Must not be null.</param>
    /// <returns>A string containing the file name and line number in the format "FileName:line LineNumber" if available;
    /// otherwise, null.</returns>
    private static string? GetExceptionSourceLocation(Exception exception)
    {
        if (string.IsNullOrEmpty(exception.StackTrace))
        {
            return null;
        }

        // Try to extract file and line number from stack trace
        // Format is typically: "at Namespace.Class.Method() in C:\path\to\File.cs:line 123"
        string[] lines = exception.StackTrace.Split(_newlineCharacters, StringSplitOptions.RemoveEmptyEntries);

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
                    return Path.GetFileName(pathPart);
                }
            }
        }

        return null;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>Call this method when you are finished using the object to free unmanaged resources and
    /// perform other cleanup operations. After calling <see cref="Dispose"/>, the object should not be used.</remarks>
    [SuppressMessage("ReSharper", "GCSuppressFinalizeForTypeWithoutDestructor", Justification = "No unmanaged resources to finalize.")]
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the application, optionally disposing managed resources and unregistering global
    /// exception handlers.
    /// </summary>
    /// <remarks>This method should be called when the application is being disposed to ensure that all
    /// managed services and global exception handlers are properly released. If disposing is false, only unmanaged
    /// resources are released; however, this implementation does not hold unmanaged resources.</remarks>
    /// <param name="disposing">true to dispose managed resources and unregister event handlers; false to release only unmanaged resources.</param>
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Disposal must be synchronous in this context.")]
    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _isDisposedFlag, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                // Unregister managed handlers
                Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnTaskSchedulerUnobservedTaskException;

                UnhookDesktopLifetimeEvents();

                // Dispose managed services since we built and managed the ServiceProvider ourselves (async-aware)
                if (Services is IAsyncDisposable asyncDisposableServices)
                {
                    asyncDisposableServices.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    Log.Information("Service provider successfully disposed asynchronously");
                }
                else if (Services is IDisposable disposableServices)
                {
                    disposableServices.Dispose();
                    Log.Information("Service provider successfully disposed synchronously");
                }

                Log.Information("App disposed successfully");
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception during Dispose");
                // Don't rethrow - we're in shutdown - best effort cleanup
            }
            finally
            {
                // Close and flush Serilog after all logging is complete
                // We use dispose: false in ServiceConfiguration, so we manually control when to close
                Log.CloseAndFlush();
            }
        }
        else
        {
            // Finalizer path (just for correctness though we don't have unmanaged resources)
            // No unmanaged resources to free here.
        }
    }

    #endregion IDisposable
}
