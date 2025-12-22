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
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MermaidPad.Exceptions.Assets;
using MermaidPad.Extensions;
using MermaidPad.Services;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Main application window that contains the MermaidEditorView and preview WebView.
/// Manages window lifecycle events, WebView initialization, and coordinates the MermaidRenderer.
/// </summary>
/// <remarks>
/// Editor-specific functionality (clipboard, intellisense, syntax highlighting, etc.) has been
/// moved to the MermaidEditorView UserControl. This class focuses on window-level concerns:
/// <list type="bullet">
///     <item><description>Window lifecycle (opening, closing, activation)</description></item>
///     <item><description>WebView initialization and management</description></item>
///     <item><description>File save prompts on close</description></item>
///     <item><description>Coordinating the MermaidRenderer</description></item>
/// </list>
/// </remarks>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly MermaidRenderer _renderer;
    private readonly MermaidUpdateService _updateService;
    private readonly ILogger<MainWindow> _logger;

    private bool _isClosingApproved;
    private bool _areAllEventHandlersCleanedUp;

    private const int WebViewReadyTimeoutSeconds = 30;

    // Event handlers stored for proper cleanup
    private EventHandler? _activatedHandler;
    private EventHandler? _openedHandler;
    private EventHandler<WindowClosingEventArgs>? _closingHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor resolves required services from the application's DI container and sets up
    /// window lifecycle event handlers. Editor-specific initialization is handled by MermaidEditorView.
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _renderer = sp.GetRequiredService<MermaidRenderer>();
        _vm = sp.GetRequiredService<MainWindowViewModel>();
        _updateService = sp.GetRequiredService<MermaidUpdateService>();
        _logger = sp.GetRequiredService<ILogger<MainWindow>>();
        DataContext = _vm;

        _logger.LogInformation("=== MainWindow Initialization Started ===");

        // Store event handlers for proper cleanup
        _openedHandler = OnOpened;
        Opened += _openedHandler;

        _closingHandler = OnClosing;
        Closing += _closingHandler;

        _activatedHandler = OnActivated;
        Activated += _activatedHandler;

        _logger.LogInformation("=== MainWindow Initialization Completed ===");
    }

    /// <summary>
    /// Handles the window activated event, bringing focus to the editor and updating clipboard state.
    /// </summary>
    /// <remarks>
    /// Updates the CanPaste state when the window gains focus, allowing the app to
    /// detect if the user copied text from another application.
    /// </remarks>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnActivated(object? sender, EventArgs e)
    {
        // Delegate focus and clipboard state updates to the MermaidEditorView
        MermaidEditor.BringFocusToEditor();
        MermaidEditor.UpdateClipboardStateOnActivation();
    }

    /// <summary>
    /// Handles the event that occurs when the window has been opened.
    /// </summary>
    /// <remarks>If an error occurs during the window opening process, an error is logged and a modal error
    /// dialog is displayed to the user. The error is also communicated to the ViewModel for UI updates.</remarks>
    /// <param name="sender">The source of the event. This is typically the window instance that was opened.</param>
    /// <param name="e">An object that contains the event data.</param>
    private void OnOpened(object? sender, EventArgs e)
    {
        OnOpenedCoreAsync()
            .SafeFireAndForget(onException: ex =>
            {
                _logger.LogError(ex, "Unhandled exception in OnOpened");

                // Surface the error to the ViewModel and show a simple modal error dialog on the UI thread.
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        // Communicate error to the ViewModel so bound UI elements can react
                        const string errorMessage = "An error occurred while opening the application. Please try again.";
                        _vm.LastError = errorMessage;

                        // Build a minimal, self-contained error dialog so we don't depend on external packages
                        StackPanel messagePanel = new StackPanel { Margin = new Thickness(12) };
                        messagePanel.Children.Add(new TextBlock
                        {
                            Text = errorMessage,
                            TextWrapping = TextWrapping.Wrap
                        });
                        messagePanel.Children.Add(new TextBlock
                        {
                            Text = ex.Message,
                            Foreground = Brushes.Red,
                            Margin = new Thickness(0, 8, 0, 0),
                            TextWrapping = TextWrapping.Wrap
                        });

                        Button okButton = new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Width = 80,
                            Margin = new Thickness(0, 12, 0, 0)
                        };
                        messagePanel.Children.Add(okButton);

                        Window dialog = new Window
                        {
                            Title = "Error",
                            Width = 380,
                            Height = 180,
                            Content = messagePanel,
                            CanResize = false,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };

                        // No reason to unsubscribe - dialog is a local variable and will be disposed after close
                        okButton.Click += (_, _) => dialog.Close();

                        await dialog.ShowDialog(this);
                    }
                    catch (Exception uiEx)
                    {
                        _logger.LogError(uiEx, "Failed to show error dialog after OnOpened failure");
                    }
                }, DispatcherPriority.Normal)
                .SafeFireAndForget(onException: uiEx => _logger.LogError(uiEx, "Failed to marshal error dialog to UI thread"));
            });
    }

    /// <summary>
    /// Handles the core logic to be executed when the window is opened asynchronously.
    /// </summary>
    /// <remarks>This method logs the window open event, invokes additional asynchronous operations, and ensures the
    /// editor receives focus. It is intended to be called as part of the window opening lifecycle.</remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task OnOpenedCoreAsync()
    {
        await OnOpenedAsync();
        MermaidEditor.BringFocusToEditor();
    }

    /// <summary>
    /// Performs the longer-running open sequence: check for updates, initialize the WebView, and update command states.
    /// </summary>
    /// <returns>A task representing the asynchronous open sequence.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required update assets cannot be resolved.</exception>
    /// <remarks>
    /// This method logs timing information, performs an update check by calling <see cref="MainWindowViewModel.CheckForMermaidUpdatesAsync"/>,
    /// initializes the renderer via <see cref="InitializeWebViewAsync"/>, and notifies commands to refresh their CanExecute state.
    /// Exceptions are propagated for higher-level handling.
    /// </remarks>
    private async Task OnOpenedAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("=== Window Opened Sequence Started ===");

        try
        {
            // TODO - re-enable this once a more complete update mechanism is in place
            // Step 1: Check for Mermaid updates
            //_logger.LogInformation("Step 1: Checking for Mermaid updates...");
            //await _vm.CheckForMermaidUpdatesAsync();
            //_logger.LogInformation("Mermaid update check completed");

            // Step 2: Initialize WebView (editor state is already synchronized via constructor)
            _logger.LogInformation("Step 2: Initializing WebView...");
            string? assetsPath = Path.GetDirectoryName(_updateService.BundledMermaidPath);
            if (assetsPath is null)
            {
                const string error = "BundledMermaidPath does not contain a directory component";
                _logger.LogError(error);
                throw new InvalidOperationException(error);
            }

            // Needs to be on UI thread
            await InitializeWebViewAsync();

            // Step 3: Update command states
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.RenderCommand.NotifyCanExecuteChanged();
                _vm.ClearCommand.NotifyCanExecuteChanged();
            });

            stopwatch.Stop();
            _logger.LogTiming("Window opened sequence", stopwatch.Elapsed, success: true);
            _logger.LogInformation("=== Window Opened Sequence Completed Successfully ===");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogTiming("Window opened sequence", stopwatch.Elapsed, success: false);
            _logger.LogError(ex, "Window opened sequence failed");
            throw;
        }
    }

    /// <summary>
    /// Handles the Click event for the Exit menu item and closes the current window.
    /// </summary>
    /// <param name="sender">The source of the event, typically the Exit menu item.</param>
    /// <param name="e">The event data associated with the Click event.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Handles the window closing event, prompting the user to save unsaved changes and performing necessary cleanup
    /// before the window closes.
    /// </summary>
    /// <remarks>If there are unsaved changes, the method prompts the user before allowing the window to
    /// close. Cleanup and state persistence are only performed if the close operation is not cancelled by this or other
    /// event handlers.</remarks>
    /// <param name="sender">The source of the event, typically the window that is being closed.</param>
    /// <param name="e">A <see cref="CancelEventArgs"/> that contains the event data, including a flag
    /// to cancel the closing operation.</param>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Check for unsaved changes (only if not already approved)
        if (!_isClosingApproved && _vm.IsDirty && !string.IsNullOrWhiteSpace(_vm.DiagramText))
        {
            e.Cancel = true;
            PromptAndCloseAsync()
                .SafeFireAndForget(onException: [SuppressMessage("ReSharper", "HeapView.ImplicitCapture")] (ex) =>
                {
                    _logger.LogError(ex, "Failed during close prompt");
                    _isClosingApproved = false; // Reset on error
                });
            return; // Don't clean up - close was cancelled
        }

        // Reset approval flag if it was set
        if (_isClosingApproved)
        {
            _isClosingApproved = false;
        }

        // Check if close was cancelled by another handler or the system
        if (e.Cancel)
        {
            return; // Don't clean up - window is not actually closing
        }

        try
        {
            // Only unsubscribe when we're actually closing (e.Cancel is still false)
            UnsubscribeAllEventHandlers();

            // Save state
            _vm.Persist();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during window closing cleanup");

            // I don't want silent failures here - rethrow to let higher-level handlers know
            throw;
        }
    }

    /// <summary>
    /// Unsubscribes all event handlers that were previously attached to window events.
    /// </summary>
    /// <remarks>Call this method to detach all event handlers managed by the instance, typically during
    /// cleanup or disposal. After calling this method, the instance will no longer respond to the associated events
    /// until handlers are reattached. This helps prevent memory leaks and unintended event processing.</remarks>
    private void UnsubscribeAllEventHandlers()
    {
        // Delegate to MermaidEditorView for editor-specific cleanup
        // MermaidEditorView handles its own event subscriptions internally to protect against double-unsubscribe
        MermaidEditor.UnsubscribeAllEventHandlers();

        // Prevent double-unsubscribe
        if (!_areAllEventHandlersCleanedUp)
        {
            if (_openedHandler is not null)
            {
                Opened -= _openedHandler;
                _openedHandler = null;
            }

            if (_closingHandler is not null)
            {
                Closing -= _closingHandler;
                _closingHandler = null;
            }

            if (_activatedHandler is not null)
            {
                Activated -= _activatedHandler;
                _activatedHandler = null;
            }

            _logger.LogInformation("All MainWindow event handlers unsubscribed successfully");

            _areAllEventHandlersCleanedUp = true;
        }
        else
        {
            _logger.LogWarning($"{nameof(UnsubscribeAllEventHandlers)} called multiple times; skipping subsequent call");
        }
    }

    /// <summary>
    /// Prompts the user to save changes if there are unsaved modifications, and closes the window if the user confirms
    /// or no changes need to be saved.
    /// </summary>
    /// <remarks>If the window is closed, any unsaved changes are either saved or discarded based on the
    /// user's response to the prompt. The method ensures that the close operation does not trigger the save prompt
    /// again. This method should be called when attempting to close the window to prevent accidental loss of unsaved
    /// data.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task completes when the prompt and close sequence has
    /// finished.</returns>
    private async Task PromptAndCloseAsync()
    {
        try
        {
            bool canClose = await _vm.PromptSaveIfDirtyAsync(StorageProvider);
            if (canClose)
            {
                _isClosingApproved = true;
                Close(); // Triggers OnClosing, which resets the flag
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during close prompt");
            _isClosingApproved = false; // Reset on exception
            throw;
        }
    }

    /// <summary>
    /// Initializes the WebView and performs the initial render of the current diagram text.
    /// </summary>
    /// <returns>A task that completes when initialization and initial render have finished.</returns>
    /// <exception cref="OperationCanceledException">Propagated if initialization is canceled.</exception>
    /// <exception cref="AssetIntegrityException">Propagated for asset integrity errors.</exception>
    /// <exception cref="MissingAssetException">Propagated when required assets are missing.</exception>
    /// <remarks>
    /// Temporarily disables live preview while initialization is in progress to prevent unwanted renders.
    /// Performs renderer initialization, waits briefly for content to load, and then triggers an initial render.
    /// Re-enables the live preview setting in a finally block to ensure UI state consistency.
    /// </remarks>
    private async Task InitializeWebViewAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("=== WebView Initialization Started ===");

        // Temporarily disable live preview during WebView initialization
        bool originalLivePreview = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            bool current = _vm.LivePreviewEnabled;
            _vm.LivePreviewEnabled = false;
            _logger.LogInformation("Temporarily disabled live preview (was: {Current})", current);
            return current;
        }, DispatcherPriority.Normal);

        bool success = false;
        try
        {
            // Step 1: Initialize renderer (starts HTTP server + navigate)
            await _renderer.InitializeAsync(Preview);

            // Step 2: Kick first render; index.html sets globalThis.__renderingComplete__ in hideLoadingIndicator()
            await _renderer.RenderAsync(_vm.DiagramText);

            // Step 3: Await readiness
            try
            {
                await _renderer.EnsureFirstRenderReadyAsync(TimeSpan.FromSeconds(WebViewReadyTimeoutSeconds));
                await Dispatcher.UIThread.InvokeAsync(() => _vm.IsWebViewReady = true);
                _logger.LogInformation("WebView readiness observed");
            }
            catch (TimeoutException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _vm.IsWebViewReady = true;
                    _vm.LastError = $"WebView initialization timed out after {WebViewReadyTimeoutSeconds} seconds. Some features may not work correctly.";
                });
                _logger.LogWarning("WebView readiness timed out after {TimeoutSeconds}s; enabling commands with warning", WebViewReadyTimeoutSeconds);
            }

            success = true;
            _logger.LogInformation("=== WebView Initialization Completed Successfully ===");
        }
        catch (OperationCanceledException)
        {
            // Treat cancellations distinctly; still propagate
            _logger.LogInformation("WebView initialization was canceled.");
            throw;
        }
        catch (Exception ex) when (ex is AssetIntegrityException or MissingAssetException)
        {
            // Let asset-related exceptions bubble up for higher-level handling
            throw;
        }
        catch (Exception ex)
        {
            // Log and rethrow so OnOpenedAsync observes the failure and can abort the sequence
            _logger.LogError(ex, "WebView initialization failed");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogTiming("WebView initialization", stopwatch.Elapsed, success);

            // Re-enable live preview after WebView is ready (or on failure)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.LivePreviewEnabled = originalLivePreview;
                _logger.LogInformation("Re-enabled live preview: {OriginalLivePreview}", originalLivePreview);
            });
        }
    }
}
