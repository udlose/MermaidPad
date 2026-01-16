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
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MermaidPad.Extensions;
using MermaidPad.Infrastructure.Messages;
using MermaidPad.ViewModels;
using MermaidPad.Views.Docking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Main application window that contains the dockable <see cref="MermaidEditorToolView"/> and <see cref="DiagramToolView"/> panels.
/// Manages window lifecycle events and coordinates between the editor and diagram preview.
/// </summary>
/// <remarks>
/// Editor-specific functionality (clipboard, intellisense, syntax highlighting, etc.) has been
/// moved to the <see cref="UserControls.MermaidEditorView"/> UserControl. WebView-specific functionality (initialization,
/// rendering) has been moved to the <see cref="UserControls.DiagramView"/> UserControl. This class focuses on window-level concerns:
/// <list type="bullet">
///     <item><description>Window lifecycle (opening, closing, activation)</description></item>
///     <item><description>File save prompts on close</description></item>
///     <item><description>Coordinating initialization between dockable tool panels</description></item>
///     <item><description>Managing dock layout persistence</description></item>
/// </list>
/// </remarks>
public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly ILogger<MainWindow> _logger;

    private bool _isClosingApproved;
    private int _isClosedFlag;
    private int _closePromptInFlight;
    private int _openErrorDialogShown;
    private bool _areAllEventHandlersCleanedUp;

    // Event handlers stored for proper cleanup
    private EventHandler? _activatedHandler;

    // Cached references to tool views for focus and cleanup operations
    private MermaidEditorToolView? _editorToolView;
    private DiagramToolView? _diagramToolView;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor resolves required services from the application's DI container and sets up
    /// window lifecycle event handlers. Editor-specific initialization is handled by <see cref="UserControls.MermaidEditorView"/>.
    /// WebView-specific initialization is handled by <see cref="UserControls.DiagramView"/>.
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _vm = sp.GetRequiredService<MainWindowViewModel>();
        _logger = sp.GetRequiredService<ILogger<MainWindow>>();
        DataContext = _vm;

        // Store event handlers for proper cleanup
        _activatedHandler = OnActivated;
        Activated += _activatedHandler;

        // Subscribe to ViewModel property changes for WordWrap and ShowLineNumbers
        _vm.PropertyChanged += OnMainViewModelPropertyChanged;

        // Apply initial editor settings
        MermaidEditor.SetWordWrap(_vm.WordWrapEnabled);
        MermaidEditor.SetShowLineNumbers(_vm.ShowLineNumbers);

        // Add debug-specific menu items in debug builds. Note that this is conditionally compiled.
        AddDebugViewMenuItems();

        _logger.LogInformation("=== MainWindow Initialization Completed ===");
    }

    /// <summary>
    /// Handles property changes from the MainWindowViewModel to apply editor settings.
    /// </summary>
    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.WordWrapEnabled):
                MermaidEditor.SetWordWrap(_vm.WordWrapEnabled);
                break;
            case nameof(MainWindowViewModel.ShowLineNumbers):
                MermaidEditor.SetShowLineNumbers(_vm.ShowLineNumbers);
                break;
        }
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
        // Find and cache tool views on first activation if needed
        EnsureToolViewsCached();

        // Delegate focus and clipboard state updates to the MermaidEditorToolView
        _editorToolView?.BringFocusToEditor();
        _editorToolView?.UpdateClipboardStateOnActivation();
    }

    /// <summary>
    /// Ensures that tool view references are cached for focus and cleanup operations.
    /// </summary>
    /// <remarks>
    /// This method searches the visual tree for the tool views created by the dock system's
    /// DataTemplate instantiation. It only searches once and caches the results.
    /// </remarks>
    private void EnsureToolViewsCached()
    {
        if (_editorToolView is null)
        {
            _editorToolView = this.GetVisualDescendants()
                .OfType<MermaidEditorToolView>()
                .FirstOrDefault();

            if (_editorToolView is not null)
            {
                _logger.LogDebug("Cached MermaidEditorToolView reference");
            }
        }

        if (_diagramToolView is null)
        {
            _diagramToolView = this.GetVisualDescendants()
                .OfType<DiagramToolView>()
                .FirstOrDefault();

            if (_diagramToolView is not null)
            {
                _logger.LogDebug("Cached DiagramToolView reference");
            }
        }
    }

    #region Overrides

    /// <summary>
    /// Called when the DataContext property changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This override provides defensive logging when the DataContext changes.
    /// In normal operation, the DataContext is set once in the constructor to
    /// <see cref="MainWindowViewModel"/> and never changes. However, this override
    /// logs any unexpected changes for diagnostic purposes.
    /// </para>
    /// <para>
    /// The dock system may manipulate DataContext on child controls, but the
    /// MainWindow's DataContext should remain stable throughout the application lifecycle.
    /// </para>
    /// </remarks>
    /// <param name="e">The event arguments.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        // Call base implementation first
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel)
        {
            _logger.LogDebug("{ViewName} DataContext set to {ViewModel}", nameof(MainWindow), nameof(MainWindowViewModel));
        }
        else if (DataContext is null)
        {
            _logger.LogWarning("{ViewName} DataContext was set to null - this is unexpected", nameof(MainWindow));
        }
        else
        {
            _logger.LogWarning("{ViewName} DataContext was set to unexpected type: {Type}", nameof(MainWindow), DataContext.GetType().Name);
        }
    }

    /// <summary>
    /// Handles additional cleanup and state management when the window is closed.
    /// </summary>
    /// <remarks>This override ensures that event handlers are unsubscribed and view model state is persisted
    /// before the window is fully closed. It also resets internal state flags to prepare for future window operations.
    /// Call the base method to ensure standard close behavior is preserved.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> instance containing the event data associated with the window close event.</param>
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            // Notify ViewModel to prepare for shutdown (e.g., close floating windows, stop background tasks, etc.)
            _vm.PrepareForShutdown();

            UnsubscribeAllEventHandlers();
        }
        finally
        {
            Volatile.Write(ref _isClosedFlag, 1);
            _isClosingApproved = false;     // Reset for future opens
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Handles the window closing event, allowing cancellation if there are unsaved changes and prompting the user for
    /// confirmation before closing.
    /// </summary>
    /// <remarks>If there are unsaved changes, the closing operation is canceled and the user is prompted to
    /// confirm closing. The method ensures that the base implementation is always called so that other event
    /// subscribers can observe the cancellation state.</remarks>
    /// <param name="e">A <see cref="WindowClosingEventArgs"/> that contains the event data for the closing operation.</param>
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Base method guarantees non-null e.")]
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // If already approved, allow close
        if (_isClosingApproved)
        {
            base.OnClosing(e);

            // If someone else canceled closing, approval must not "stick"
            if (e.Cancel)
            {
                _isClosingApproved = false;
            }

            return;
        }

        try
        {
            // Persist settings here in OnClosing. Using OnClosed is too late in
            // the lifecycle because we need to save Layout state before floating windows are destroyed.
            _vm.Persist();

            bool hasUnsavedChanges = _vm is { IsDirty: true, EditorHasText: true };
            if (hasUnsavedChanges)
            {
                // Cancel the close attempt and call base.OnClosing(e) so the Closing event is raised and subscribers
                // can observe Cancel==true. Then post the async prompt so it runs after the current closing callback
                // unwinds back to the UI loop (avoids reentrancy during the close pipeline).
                e.Cancel = true; // Cancel now; close later if user approves

                // Allow other subscribers to observe Cancel==true
                base.OnClosing(e);

                // Only prompt once
                if (Interlocked.Exchange(ref _closePromptInFlight, 1) == 0)
                {
                    // Run prompt AFTER OnClosing returns. Using Post (not InvokeAsync) ensures this is queued
                    // back to the UI loop and does not re-enter the close pipeline while it is still unwinding.
                    Dispatcher.UIThread.Post(PostedPromptAndClose);
                }

                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OnClosing");

            //TODO - DaveBlack: uncomment e.Cancel = true when an appropriate error dialog is implemented
            // Cancel the close operation if persistence fails, so the user does not lose unsaved state
            //e.Cancel = true;

            // Allow other subscribers to observe Cancel==true, mirroring the unsaved-changes path
            base.OnClosing(e);

            //TODO - DaveBlack: Consider showing an error dialog here to inform the user of the failure
            return;
        }

        base.OnClosing(e); // Normal close path
    }

    /// <summary>
    /// Initiates the prompt-and-close workflow if the window is not already closed.
    /// </summary>
    /// <remarks>If the window has already been closed, this method does not prompt the user and ensures that
    /// any in-flight close prompt state is reset. This method is intended to be called when a close operation is
    /// requested and should not be called concurrently from multiple threads.</remarks>
    private void PostedPromptAndClose()
    {
        // If the window is already closed, don't prompt and don't leave _closePromptInFlight stuck at 1
        if (Volatile.Read(ref _isClosedFlag) != 0)
        {
            Interlocked.Exchange(ref _closePromptInFlight, 0);
            _isClosingApproved = false;
            return;
        }

        PromptAndCloseAsync()
            .SafeFireAndForget(onException: OnPromptAndCloseFireAndForgetException);
    }

    /// <summary>
    /// Handles exceptions that occur during the prompt-and-close operation by logging the error and resetting the close
    /// state.
    /// </summary>
    /// <remarks>This method is intended to be called when an exception occurs in a fire-and-forget
    /// prompt-and-close workflow. It ensures that the internal close state is reset to prevent inconsistent application
    /// state.</remarks>
    /// <param name="ex">The exception that was thrown during the prompt-and-close operation.</param>
    private void OnPromptAndCloseFireAndForgetException(Exception ex)
    {
        _logger.LogError(ex, "Failed during close prompt");
        _isClosingApproved = false;

        // Extra safety: PromptAndCloseAsync has a finally that clears this,
        // but keep this in case the task never actually starts/completes.
        Interlocked.Exchange(ref _closePromptInFlight, 0);
    }

    /// <summary>
    /// Handles additional logic when the window is opened.
    /// </summary>
    /// <remarks>This method is called after the window has been opened. It initiates asynchronous operations
    /// related to the window opening process and handles any exceptions that may occur during those operations.
    /// Override this method to provide custom behavior when the window is opened, but ensure to call the base
    /// implementation to maintain expected behavior.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> that contains the event data associated with the window opening event.</param>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        OnOpenedCoreAsync()
            .SafeFireAndForget(onException: ex =>
            {
                _logger.LogError(ex, "Unhandled exception in OnOpened");
                if (Interlocked.Exchange(ref _openErrorDialogShown, 1) != 0)
                {
                    return;
                }

                // Show a simple modal error dialog on the UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    ShowOpenedErrorDialogAsync(ex)
                        .SafeFireAndForget(onException: uiEx => _logger.LogError(uiEx, "Failed to show open failure dialog"));
                });
            });
    }

    #endregion Overrides

    /// <summary>
    /// Handles the core logic to be executed when the window is opened asynchronously.
    /// </summary>
    /// <remarks>This method logs the window open event, invokes additional asynchronous operations, and ensures the
    /// editor receives focus. It is intended to be called as part of the window opening lifecycle.</remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task OnOpenedCoreAsync()
    {
        await OnOpenedAsync();

        // Ensure tool views are cached before trying to focus
        EnsureToolViewsCached();
        _editorToolView?.BringFocusToEditor();
    }

    /// <summary>
    /// Performs the longer-running open sequence: check for updates, initialize the DiagramView, and update command states.
    /// </summary>
    /// <returns>A task representing the asynchronous open sequence.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required update assets cannot be resolved.</exception>
    /// <remarks>
    /// This method logs timing information, performs an update check by calling <see cref="MainWindowViewModel.CheckForMermaidUpdatesAsync"/>,
    /// initializes the diagram view via <see cref="MainWindowViewModel.InitializeDiagramAsync"/>, and notifies commands to refresh their CanExecute state.
    /// Exceptions are propagated for higher-level handling.
    /// </remarks>
    private async Task OnOpenedAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("=== Window Opened Sequence Started ===");

        bool isSuccess = false;
        try
        {
            // TODO - re-enable this once a more complete update mechanism is in place
            // Step 1: Check for Mermaid updates
            //_logger.LogInformation("Step 1: Checking for Mermaid updates...");
            //await _vm.CheckForMermaidUpdatesAsync();
            //_logger.LogInformation("Mermaid update check completed");

            // Initialize DiagramView (WebView initialization is now encapsulated there)
            // Note: Only initialize if the DiagramView is attached (i.e., visible in the dock layout).
            // When the diagram panel is pinned/hidden, the View isn't created, so InitializeActionAsync
            // won't be set. In that case, we set _isPendingDiagramInitialization=true so that when
            // DiagramView attaches and sends DiagramViewReadyMessage, MainWindowViewModel.Receive()
            // will trigger initialization.
            if (_vm.Diagram.InitializeActionAsync is not null)
            {
                _logger.LogInformation($"Initializing {nameof(DiagramToolView)}...");

                // Temporarily disable live preview during initialization
                bool originalLivePreview = _vm.LivePreviewEnabled;
                _vm.LivePreviewEnabled = false;

                try
                {
                    // Initialize and render the diagram view through the ViewModel
                    await _vm.InitializeDiagramAsync();
                }
                finally
                {
                    // Re-enable live preview after initialization
                    _vm.LivePreviewEnabled = originalLivePreview;
                }
            }
            else
            {
                // DiagramView hasn't attached yet (pinned/floating layout).
                // Set the pending flag so MainWindowViewModel.Receive(DiagramViewReadyMessage)
                // will trigger initialization when the View becomes ready.
                _vm.SetPendingDiagramInitialization();

                _logger.LogInformation(
                    "DiagramView not yet attached - panel may be pinned/floating. " +
                    "Initialization will be triggered when {MessageName} is received.",
                    nameof(DiagramViewReadyMessage));
            }

            // Step 3: Update command states
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.RenderCommand.NotifyCanExecuteChanged();
                _vm.ClearCommand.NotifyCanExecuteChanged();
            });

            _logger.LogInformation("=== Window Opened Sequence Completed Successfully ===");
            isSuccess = true;
        }
        catch (Exception ex)
        {
            isSuccess = false;
            _logger.LogError(ex, "Window opened sequence failed");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogTiming("Window opened sequence", stopwatch.Elapsed, success: isSuccess);
        }
    }

    /// <summary>
    /// Displays an asynchronous error dialog indicating that an error occurred while opening the application.
    /// </summary>
    /// <param name="ex">The exception that caused the error.</param>
    /// <remarks>The dialog presents a general error message along with details from the underlying exception,
    /// if available. The dialog must be closed by the user before the calling code continues execution.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task completes when the error dialog is closed.</returns>
    private async Task ShowOpenedErrorDialogAsync(Exception ex)
    {
        // Build a minimal, self-contained error dialog so we don't depend on external packages
        StackPanel messagePanel = new StackPanel { Margin = new Thickness(12) };
        messagePanel.Children.Add(new TextBlock
        {
            Text = "An error occurred while opening the application. Please try again.",
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

        okButton.Click += OkButtonClick;
        try
        {
            await dialog.ShowDialog(this);
        }
        finally
        {
            okButton.Click -= OkButtonClick;
        }

        void OkButtonClick(object? sender, RoutedEventArgs e) => dialog.Close();
    }

    /// <summary>
    /// Handles the Click event for the Exit menu item and closes the current window.
    /// </summary>
    /// <param name="sender">The source of the event, typically the Exit menu item.</param>
    /// <param name="e">The event data associated with the Click event.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Unsubscribes all event handlers that were previously attached to window events.
    /// </summary>
    /// <remarks>Call this method to detach all event handlers managed by the instance, typically during
    /// cleanup or disposal. After calling this method, the instance will no longer respond to the associated events
    /// until handlers are reattached. This helps prevent memory leaks and unintended event processing.</remarks>
    private void UnsubscribeAllEventHandlers()
    {
        // Delegate to tool views for cleanup (they handle their own nested controls)
        _editorToolView?.UnsubscribeAllEventHandlers();
        _diagramToolView?.UnsubscribeAllEventHandlers();

        // Prevent double-unsubscribe
        if (!_areAllEventHandlersCleanedUp)
        {
            if (_activatedHandler is not null)
            {
                Activated -= _activatedHandler;
                _activatedHandler = null;
            }

            // Unsubscribe from ViewModel property changes
            _vm.PropertyChanged -= OnMainViewModelPropertyChanged;

            _logger.LogInformation("All {ViewName} event handlers unsubscribed successfully", nameof(MainWindow));

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
                await CloseOnUIThreadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during close prompt");
            _isClosingApproved = false; // Reset on exception
            // Do not rethrow during close; best-effort shutdown
        }
        finally
        {
            Interlocked.Exchange(ref _closePromptInFlight, 0);
        }

        Task CloseOnUIThreadAsync()
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Close(); // Triggers OnClosing
                return Task.CompletedTask;
            }

            return Dispatcher.UIThread.InvokeAsync(Close).GetTask();
        }
    }

    /// <summary>
    /// Adds debug-specific diagnostic menu items to the View menu when running in a debug build.
    /// </summary>
    /// <remarks>This method is only included in builds where the DEBUG symbol is defined. It augments the
    /// View menu with additional options useful for debugging and diagnostics, and has no effect in release
    /// builds.</remarks>
    [Conditional("DEBUG")]
    private void AddDebugViewMenuItems()
    {
        MenuItem? viewMenu = this.FindControl<MenuItem>("ViewMenu");
        if (viewMenu == null)
        {
            return;
        }

        viewMenu.Items.Add(new Separator());
        MenuItem dumpDockDiagnosticsMenuItem = new MenuItem
        {
            Header = "Dump Dock _Diagnostics"
        };

        dumpDockDiagnosticsMenuItem.Bind(MenuItem.CommandProperty, new Binding("DumpDockDiagnosticsCommand"));
        viewMenu.Items.Add(dumpDockDiagnosticsMenuItem);
    }
}
