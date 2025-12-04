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
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MermaidPad.Exceptions.Assets;
using MermaidPad.Extensions;
using MermaidPad.Services;
using MermaidPad.Services.Highlighting;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Main application window that contains the editor and preview WebView.
/// Manages synchronization between the editor control and the <see cref="MainWindowViewModel"/>,
/// initializes and manages the <see cref="MermaidRenderer"/>, and handles window lifecycle events.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly MermaidRenderer _renderer;
    private readonly MermaidUpdateService _updateService;
    private readonly SyntaxHighlightingService _syntaxHighlightingService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly ILogger<MainWindow> _logger;

    private bool _isClosingApproved;
    private bool _suppressEditorTextChanged;
    private bool _suppressEditorStateSync; // Prevent circular updates

    private const int WebViewReadyTimeoutSeconds = 30;

    // Event handlers stored for proper cleanup
    private EventHandler? _activatedHandler;
    private EventHandler? _openedHandler;
    private EventHandler<WindowClosingEventArgs>? _closingHandler;
    private EventHandler? _editorTextChangedHandler;
    private EventHandler? _editorSelectionChangedHandler;
    private EventHandler? _editorCaretPositionChangedHandler;
    private EventHandler? _themeChangedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor resolves required services from the application's DI container,
    /// initializes the editor state from the view model, and hooks up synchronization and lifecycle handlers.
    /// No long-running or blocking work is performed here; heavier initialization happens during the window open sequence.
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _editorDebouncer = sp.GetRequiredService<IDebounceDispatcher>();
        _renderer = sp.GetRequiredService<MermaidRenderer>();
        _vm = sp.GetRequiredService<MainWindowViewModel>();
        _updateService = sp.GetRequiredService<MermaidUpdateService>();
        _syntaxHighlightingService = sp.GetRequiredService<SyntaxHighlightingService>();
        _logger = sp.GetRequiredService<ILogger<MainWindow>>();
        DataContext = _vm;

        _logger.LogInformation("=== MainWindow Initialization Started ===");

        // Initialize syntax highlighting before wiring up OnThemeChanged
        InitializeSyntaxHighlighting();

        // Store event handlers for proper cleanup
        _openedHandler = OnOpened;
        Opened += _openedHandler;

        _closingHandler = OnClosing;
        Closing += _closingHandler;

        _themeChangedHandler = OnThemeChanged;
        ActualThemeVariantChanged += _themeChangedHandler;

        _activatedHandler = (_, _) => BringFocusToEditor();
        Activated += _activatedHandler;

        // Initialize editor with ViewModel data using validation
        SetEditorStateWithValidation(
            _vm.DiagramText,
            _vm.EditorSelectionStart,
            _vm.EditorSelectionLength,
            _vm.EditorCaretOffset
        );

        // Set up two-way synchronization between Editor and ViewModel
        SetupEditorViewModelSync();

        // Wire up clipboard and edit actions to ViewModel
        WireUpClipboardActions();

        _logger.LogInformation("=== MainWindow Initialization Completed ===");
    }

    /// <summary>
    /// Sets the editor text, selection, and caret position while validating bounds and preventing circular updates.
    /// </summary>
    /// <param name="text">The text to set into the editor. Must not be <see langword="null"/>.</param>
    /// <param name="selectionStart">Requested selection start index.</param>
    /// <param name="selectionLength">Requested selection length.</param>
    /// <param name="caretOffset">Requested caret offset.</param>
    private void SetEditorStateWithValidation(string text, int selectionStart, int selectionLength, int caretOffset)
    {
        _suppressEditorStateSync = true; // Prevent circular updates during initialization
        try
        {
            Editor.Text = text;

            // Ensure selection bounds are valid
            int textLength = text.Length;
            int validSelectionStart = Math.Max(0, Math.Min(selectionStart, textLength));
            int validSelectionLength = Math.Max(0, Math.Min(selectionLength, textLength - validSelectionStart));
            int validCaretOffset = Math.Max(0, Math.Min(caretOffset, textLength));
            Editor.SelectionStart = validSelectionStart;
            Editor.SelectionLength = validSelectionLength;
            Editor.CaretOffset = validCaretOffset;

            // Since this is yaml/diagram text, convert tabs to spaces for correct rendering
            Editor.Options.ConvertTabsToSpaces = true;
            Editor.Options.HighlightCurrentLine = true;
            Editor.Options.IndentationSize = 2;

            _logger.LogInformation("Editor state set with {CharacterCount} characters", textLength);
        }
        finally
        {
            _suppressEditorStateSync = false;
        }
    }

    /// <summary>
    /// Wires up synchronization between the editor control and the view model.
    /// </summary>
    /// <remarks>
    /// - Subscribes to editor text/selection/caret events and updates the view model using a debounce dispatcher.
    /// - Subscribes to view model property changes and applies them to the editor.
    /// - Suppresses reciprocal updates to avoid feedback loops.
    /// </remarks>
    private void SetupEditorViewModelSync()
    {
        // Editor -> ViewModel synchronization (text)
        _editorTextChangedHandler = (_, _) =>
        {
            if (_suppressEditorTextChanged)
            {
                return;
            }

            // Update undo/redo states immediately (not debounced)
            _vm.CanUndo = Editor.CanUndo;
            _vm.CanRedo = Editor.CanRedo;

            // Notify commands that their CanExecute state may have changed
            _vm.UndoCommand.NotifyCanExecuteChanged();
            _vm.RedoCommand.NotifyCanExecuteChanged();

            // Debounce to avoid excessive updates
            _editorDebouncer.DebounceOnUI("editor-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
            {
                if (_vm.DiagramText != Editor.Text)
                {
                    _suppressEditorStateSync = true;
                    try
                    {
                        _vm.DiagramText = Editor.Text;
                    }
                    finally
                    {
                        _suppressEditorStateSync = false;
                    }
                }
            },
            DispatcherPriority.Background);
        };
        Editor.TextChanged += _editorTextChangedHandler;

        // Editor selection/caret -> ViewModel: subscribe to both, coalesce into one update
        _editorSelectionChangedHandler = (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            // Update cut/copy states immediately based on selection
            bool hasSelection = Editor.SelectionLength > 0;
            _vm.CanCutClipboard = hasSelection;
            _vm.CanCopyClipboard = hasSelection;

            // Notify commands that their CanExecute state may have changed
            _vm.CutCommand.NotifyCanExecuteChanged();
            _vm.CopyCommand.NotifyCanExecuteChanged();

            ScheduleEditorStateSyncIfNeeded();
        };
        Editor.TextArea.SelectionChanged += _editorSelectionChangedHandler;

        _editorCaretPositionChangedHandler = (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            ScheduleEditorStateSyncIfNeeded();
        };
        Editor.TextArea.Caret.PositionChanged += _editorCaretPositionChangedHandler;

        // ViewModel -> Editor synchronization
        _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
        _vm.PropertyChanged += _viewModelPropertyChangedHandler;
    }

    /// <summary>
    /// Coalesces caret and selection updates and schedules a debounced update of the view model's editor state.
    /// </summary>
    /// <remarks>
    /// The method compares the current editor state with the view model and only schedules an update
    /// when a change is detected. Values are read again at the time the debounced action runs to coalesce
    /// multiple rapid events into a single update.
    /// </remarks>
    private void ScheduleEditorStateSyncIfNeeded()
    {
        int selectionStart = Editor.SelectionStart;
        int selectionLength = Editor.SelectionLength;
        int caretOffset = Editor.CaretOffset;

        if (selectionStart == _vm.EditorSelectionStart &&
            selectionLength == _vm.EditorSelectionLength &&
            caretOffset == _vm.EditorCaretOffset)
        {
            return; // nothing changed
        }

        _editorDebouncer.DebounceOnUI("editor-state", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
        {
            _suppressEditorStateSync = true;
            try
            {
                // Take the latest values at execution time to coalesce multiple events
                _vm.EditorSelectionStart = Editor.SelectionStart;
                _vm.EditorSelectionLength = Editor.SelectionLength;
                _vm.EditorCaretOffset = Editor.CaretOffset;
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        },
        DispatcherPriority.Background);
    }

    /// <summary>
    /// Handles property changes on the view model and synchronizes relevant values to the editor control.
    /// </summary>
    /// <param name="sender">The object raising the property changed event (typically the view model).</param>
    /// <param name="e">Property changed event arguments describing which property changed.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressEditorStateSync)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(_vm.DiagramText):
                if (Editor.Text != _vm.DiagramText)
                {
                    _editorDebouncer.DebounceOnUI("vm-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
                    {
                        _suppressEditorTextChanged = true;
                        _suppressEditorStateSync = true;
                        try
                        {
                            Editor.Text = _vm.DiagramText;
                        }
                        finally
                        {
                            _suppressEditorTextChanged = false;
                            _suppressEditorStateSync = false;
                        }
                    },
                    DispatcherPriority.Background);
                }
                break;

            case nameof(_vm.EditorSelectionStart):
            case nameof(_vm.EditorSelectionLength):
            case nameof(_vm.EditorCaretOffset):
                _editorDebouncer.DebounceOnUI("vm-selection", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
                {
                    _suppressEditorStateSync = true;
                    try
                    {
                        // Validate bounds before setting
                        int textLength = Editor.Text.Length;
                        int validSelectionStart = Math.Max(0, Math.Min(_vm.EditorSelectionStart, textLength));
                        int validSelectionLength = Math.Max(0, Math.Min(_vm.EditorSelectionLength, textLength - validSelectionStart));
                        int validCaretOffset = Math.Max(0, Math.Min(_vm.EditorCaretOffset, textLength));

                        if (Editor.SelectionStart != validSelectionStart ||
                            Editor.SelectionLength != validSelectionLength ||
                            Editor.CaretOffset != validCaretOffset)
                        {
                            Editor.SelectionStart = validSelectionStart;
                            Editor.SelectionLength = validSelectionLength;
                            Editor.CaretOffset = validCaretOffset;
                        }
                    }
                    finally
                    {
                        _suppressEditorStateSync = false;
                    }
                },
                DispatcherPriority.Background);
                break;
        }
    }

    /// <summary>
    /// Brings focus to the editor control and adjusts visuals for caret and selection.
    /// </summary>
    /// <remarks>
    /// This method executes on the UI thread via the dispatcher and temporarily suppresses
    /// editor <see cref="_suppressEditorStateSync"/> to avoid generating spurious model updates.
    /// </remarks>
    private void BringFocusToEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Suppress event reactions during programmatic focus/caret adjustments
            _suppressEditorStateSync = true;
            try
            {
                // Make sure caret is visible:
                Editor.TextArea.Caret.CaretBrush = Brushes.Red;

                // Ensure selection is visible
                Editor.TextArea.SelectionBrush = Brushes.SteelBlue;
                if (!Editor.IsFocused)
                {
                    Editor.Focus();
                }
                Editor.TextArea.Caret.BringCaretToView();
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        }, DispatcherPriority.Background);
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
    /// <remarks>This method logs the window open event, invokes additional asynchronous operations,  and ensures the
    /// editor receives focus. It is intended to be called as part of the  window opening lifecycle.</remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task OnOpenedCoreAsync()
    {
        _suppressEditorStateSync = true;
        try
        {
            await OnOpenedAsync();
            BringFocusToEditor();
        }
        finally
        {
            _suppressEditorStateSync = false;
        }
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
            _logger.LogInformation("Step 3: Updating command states...");
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

        // Perform async cleanup
        // Capture logger for use in lambda in case 'this' is disposed before the async work completes
        ILogger<MainWindow> logger = _logger;
        OnClosingAsync()
            .SafeFireAndForget(onException: [SuppressMessage("ReSharper", "HeapView.ImplicitCapture")] (ex) =>
                logger.LogError(ex, "Failed during window close cleanup"));
    }

    /// <summary>
    /// Unsubscribes all event handlers that were previously attached to window and editor events.
    /// </summary>
    /// <remarks>Call this method to detach all event handlers managed by the instance, typically during
    /// cleanup or disposal. After calling this method, the instance will no longer respond to the associated events
    /// until handlers are reattached. This helps prevent memory leaks and unintended event processing.</remarks>
    private void UnsubscribeAllEventHandlers()
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

        if (_themeChangedHandler is not null)
        {
            ActualThemeVariantChanged -= _themeChangedHandler;
            _themeChangedHandler = null;
        }

        if (_editorTextChangedHandler is not null)
        {
            Editor.TextChanged -= _editorTextChangedHandler;
            _editorTextChangedHandler = null;
        }

        if (_editorSelectionChangedHandler is not null)
        {
            Editor.TextArea.SelectionChanged -= _editorSelectionChangedHandler;
            _editorSelectionChangedHandler = null;
        }

        if (_editorCaretPositionChangedHandler is not null)
        {
            Editor.TextArea.Caret.PositionChanged -= _editorCaretPositionChangedHandler;
            _editorCaretPositionChangedHandler = null;
        }

        if (_viewModelPropertyChangedHandler is not null)
        {
            _vm.PropertyChanged -= _viewModelPropertyChangedHandler;
            _viewModelPropertyChangedHandler = null;
        }

        _logger.LogInformation("All event handlers unsubscribed successfully");
    }


    private async Task OnClosingAsync()
    {
        _logger.LogInformation("Window closing, cleaning up...");

        if (_renderer is IAsyncDisposable disposableRenderer)
        {
            await disposableRenderer.DisposeAsync();
            _logger.LogInformation("MermaidRenderer disposed");
        }

        _logger.LogInformation("Window cleanup completed successfully");
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

    #region Clipboard and Edit Methods

    /// <summary>
    /// Wires up the clipboard and edit action delegates in the ViewModel to their implementations.
    /// </summary>
    /// <remarks>
    /// This method connects the ViewModel's Action properties to the actual implementation methods,
    /// enabling proper MVVM separation while allowing the View to implement UI-specific operations.
    /// </remarks>
    private void WireUpClipboardActions()
    {
        _vm.CutAction = CutToClipboardAsync;
        _vm.CopyAction = CopyToClipboard;
        _vm.PasteAction = PasteFromClipboard;
        _vm.UndoAction = UndoEdit;
        _vm.RedoAction = RedoEdit;
        _vm.SelectAllAction = SelectAllText;
        _vm.OpenFindAction = OpenFindPanel;
        _vm.FindNextAction = FindNextMatch;
        _vm.FindPreviousAction = FindPreviousMatch;
    }

    /// <summary>
    /// Asynchronously cuts the selected text to the clipboard and removes it from the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs a two-phase atomic operation:
    /// 1. Copy selected text to clipboard (awaits completion)
    /// 2. Remove text from editor only if clipboard operation succeeded
    /// </para>
    /// <para>
    /// The entire operation is atomic - the command won't complete until both phases finish,
    /// preventing race conditions where selection might change mid-operation.
    /// </para>
    /// <para>
    /// THREADING: Avalonia's Clipboard.SetTextAsync must be called on the UI thread.
    /// The operation runs entirely on UI thread to avoid cross-thread access issues.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous cut operation.</returns>
    private async Task CutToClipboardAsync()
    {
        // Fast-path checks that don't require UI thread
        if (Clipboard is null)
        {
            return;
        }

        try
        {
            // Marshal the entire two-phase operation to the UI thread because
            // - Avalonia's clipboard APIs must be called on the UI thread, and
            // - we read/write Editor state (selection, document) which is a UI object.
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Editor.SelectionLength <= 0)
                {
                    return;
                }

                string selectedText = Editor.SelectedText;
                if (string.IsNullOrEmpty(selectedText))
                {
                    return;
                }

                // Store selection position before async operation
                int selectionStart = Editor.SelectionStart;
                int selectionLength = Editor.SelectionLength;

                try
                {
                    // Phase 1: Copy to clipboard (must run on UI thread)
                    await Clipboard.SetTextAsync(selectedText);

                    // Phase 2: Remove text only if clipboard succeeded and selection unchanged
                    if (Editor.SelectionStart == selectionStart && Editor.SelectionLength == selectionLength)
                    {
                        Editor.Document.Remove(selectionStart, selectionLength);
                        _logger.LogInformation("Cut {CharCount} characters to clipboard", selectedText.Length);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Selection changed during cut operation (was {Start}:{Length}, now {NewStart}:{NewLength}) - text not removed",
                            selectionStart, selectionLength, Editor.SelectionStart, Editor.SelectionLength);
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to cut text to clipboard (UI thread)");
                }
            }, DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cut text to clipboard");
            // Don't re-throw - keep method safe for command handlers
            // The text remains in the editor, which is the safe fallback
        }
    }

    /// <summary>
    /// Copies the selected text to the clipboard.
    /// </summary>
    /// <remarks>
    /// This method must be called on the UI thread as it accesses both the TextEditor and the clipboard.
    /// </remarks>
    private void CopyToClipboard()
    {
        if (Editor.SelectionLength <= 0 || Clipboard is null)
        {
            return;
        }

        try
        {
            string selectedText = Editor.SelectedText;
            if (string.IsNullOrEmpty(selectedText))
            {
                return;
            }

            Clipboard.SetTextAsync(selectedText)
                .SafeFireAndForget(onException: ex => _logger.LogError(ex, "Failed to set clipboard text during copy"));

            _logger.LogInformation("Copied {CharCount} characters to clipboard", selectedText.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy text to clipboard");
        }
    }

    /// <summary>
    /// Pastes text from the clipboard at the current caret position.
    /// </summary>
    /// <remarks>
    /// This method must be called on the UI thread as it accesses both the TextEditor and the clipboard.
    /// The pasted text replaces any current selection, and the caret moves to the end of the pasted text.
    /// </remarks>
    private void PasteFromClipboard()
    {
        if (Clipboard is null)
        {
            return;
        }

        try
        {
            // Start async paste operation - SafeFireAndForget handles exceptions
            PasteFromClipboardAsync()
                .SafeFireAndForget(onException: ex => _logger.LogError(ex, "Failed to paste from clipboard"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate paste from clipboard");
        }
    }

    /// <summary>
    /// Asynchronously pastes text from the clipboard at the current caret position.
    /// </summary>
    /// <returns>A task representing the asynchronous paste operation.</returns>
    private async Task PasteFromClipboardAsync()
    {
        try
        {
            string? clipboardText = await GetTextFromClipboardAsync(this)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(clipboardText))
            {
                return;
            }

            // Marshal back to UI thread to update the editor
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    int insertPosition = Editor.SelectionStart;
                    int removeLength = Editor.SelectionLength;

                    // Replace selection with clipboard text
                    Editor.Document.Replace(insertPosition, removeLength, clipboardText);

                    // Move caret to end of pasted text (standard behavior)
                    Editor.CaretOffset = insertPosition + clipboardText.Length;

                    _logger.LogInformation("Pasted {CharCount} characters from clipboard", clipboardText.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to insert clipboard text into editor");
                }
            }, DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to paste from clipboard");
        }
    }

    /// <summary>
    /// Undoes the last edit operation in the editor.
    /// </summary>
    /// <remarks>
    /// This method calls the AvaloniaEdit TextEditor's built-in Undo functionality.
    /// Must be called on the UI thread.
    /// </remarks>
    private void UndoEdit()
    {
        try
        {
            if (Editor.CanUndo)
            {
                Editor.Undo();
                _logger.LogInformation("Undo operation performed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform undo operation");
        }
    }

    /// <summary>
    /// Redoes the last undone edit operation in the editor.
    /// </summary>
    /// <remarks>
    /// This method calls the AvaloniaEdit TextEditor's built-in Redo functionality.
    /// Must be called on the UI thread.
    /// </remarks>
    private void RedoEdit()
    {
        try
        {
            if (Editor.CanRedo)
            {
                Editor.Redo();
                _logger.LogInformation("Redo operation performed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform redo operation");
        }
    }

    /// <summary>
    /// Selects all text in the editor.
    /// </summary>
    /// <remarks>
    /// This method calls the AvaloniaEdit TextEditor's built-in SelectAll functionality.
    /// Must be called on the UI thread.
    /// </remarks>
    private void SelectAllText()
    {
        try
        {
            Editor.SelectAll();
            _logger.LogInformation("Select all operation performed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform select all operation");
        }
    }

    /// <summary>
    /// Opens the find panel in the editor.
    /// </summary>
    /// <remarks>
    /// This method opens the AvaloniaEdit TextEditor's built-in SearchPanel.
    /// If text is currently selected, it pre-fills the search pattern with the selected text.
    /// Must be called on the UI thread.
    /// </remarks>
    private void OpenFindPanel()
    {
        try
        {
            // Pre-fill search with selected text if available
            if (Editor.SelectionLength > 0 && Editor.SelectionLength < 100)
            {
                Editor.SearchPanel.SearchPattern = Editor.SelectedText;
            }

            Editor.SearchPanel.Open();
            _logger.LogInformation("Find panel opened");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open find panel");
        }
    }

    /// <summary>
    /// Finds the next match in the editor using the current search pattern.
    /// </summary>
    /// <remarks>
    /// This method calls the AvaloniaEdit TextEditor's SearchPanel FindNext functionality.
    /// Must be called on the UI thread.
    /// </remarks>
    private void FindNextMatch()
    {
        try
        {
            Editor.SearchPanel.FindNext();
            _logger.LogInformation("Find next operation performed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform find next operation");
        }
    }

    /// <summary>
    /// Finds the previous match in the editor using the current search pattern.
    /// </summary>
    /// <remarks>
    /// This method calls the AvaloniaEdit TextEditor's SearchPanel FindPrevious functionality.
    /// Must be called on the UI thread.
    /// </remarks>
    private void FindPreviousMatch()
    {
        try
        {
            Editor.SearchPanel.FindPrevious();
            _logger.LogInformation("Find previous operation performed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform find previous operation");
        }
    }

    /// <summary>
    /// Determines the enabled state of context menu clipboard commands based on the current editor selection and
    /// clipboard availability.
    /// </summary>
    /// <remarks>This method is intended to be used as an event handler for context menu opening events. It
    /// updates the clipboard-related command states to reflect whether copy and paste actions are currently
    /// available.</remarks>
    /// <param name="sender">The source of the event, typically the control that triggered the context menu opening.</param>
    /// <param name="e">A <see cref="CancelEventArgs"/> instance that can be used to cancel the context menu opening.</param>
    private void GetContextMenuState(object? sender, CancelEventArgs e)
    {
        // Update clipboard states
        _vm.CanCopyClipboard = _vm.EditorSelectionLength > 0;
        _vm.CanCutClipboard = _vm.EditorSelectionLength > 0;

        // Update undo/redo states
        _vm.CanUndo = Editor.CanUndo;
        _vm.CanRedo = Editor.CanRedo;

        UpdateCanPasteClipboardAsync()
            .SafeFireAndForget(onException: ex => _logger.LogError(ex, "Failed to update CanPasteClipboard"));
    }

    /// <summary>
    /// Asynchronously retrieves the current text content from the clipboard associated with the specified window.
    /// </summary>
    /// <remarks>If the clipboard is unavailable or does not contain text, the method returns null. The
    /// operation is performed on the appropriate UI thread as required by the window's clipboard
    /// implementation.</remarks>
    /// <param name="window">The window whose clipboard is accessed to retrieve text. Must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the clipboard text if available;
    /// otherwise, null.</returns>
    private static async Task<string?> GetTextFromClipboardAsync(Window window)
    {
        // Access Window.Clipboard on the UI thread
        IClipboard? clipboard = Dispatcher.UIThread.CheckAccess()
            ? window.Clipboard
            : await Dispatcher.UIThread.InvokeAsync(() => window.Clipboard, DispatcherPriority.Background);

        if (clipboard is null)
        {
            return null;
        }

        // Perform the read without capturing the UI context (no UI touched afterward)
        string? clipboardText = await clipboard.TryGetTextAsync()
            .ConfigureAwait(false);
        return clipboardText;
    }

    /// <summary>
    /// Asynchronously updates the ViewModel to reflect whether clipboard text is available for pasting.
    /// </summary>
    /// <remarks>This method reads the clipboard text off the UI thread and updates the CanPasteClipboard
    /// property on the ViewModel. If clipboard access fails or the clipboard contains only whitespace,
    /// CanPasteClipboard is set to false. The update is marshaled back to the UI thread to ensure thread
    /// safety.</remarks>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation of updating the ViewModel's
    /// <see cref="MainWindowViewModel.CanPasteClipboard"/> property based on the current clipboard contents.
    /// The task completes when the property has been updated.
    /// </returns>
    private async Task UpdateCanPasteClipboardAsync()
    {
        string? clipboardText = null;

        try
        {
            // Perform clipboard I/O off the UI context
            clipboardText = await GetTextFromClipboardAsync(this)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log and treat as no pasteable text
            _logger.LogError(ex, "Error reading clipboard text");
        }

        bool canPaste = !string.IsNullOrWhiteSpace(clipboardText);

        // Marshal back to UI thread to update the ViewModel property
        await Dispatcher.UIThread.InvokeAsync(() => _vm.CanPasteClipboard = canPaste, DispatcherPriority.Normal);
    }

    #endregion Clipboard and Edit Methods

    #region Syntax Highlighting methods

    /// <summary>
    /// Initializes syntax highlighting for the text editor.
    /// </summary>
    /// <remarks>
    /// This method initializes the syntax highlighting service and applies Mermaid syntax highlighting
    /// to the editor. The theme is automatically selected based on the current Avalonia theme variant.
    /// </remarks>
    private void InitializeSyntaxHighlighting()
    {
        try
        {
            // Initialize the service (verifies grammar resources exist)
            _syntaxHighlightingService.Initialize();

            // Apply Mermaid syntax highlighting with automatic theme detection
            _syntaxHighlightingService.ApplyTo(Editor);

            _logger.LogInformation("Syntax highlighting initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize syntax highlighting");
            // Non-fatal: Continue without syntax highlighting rather than crash the application
        }
    }

    /// <summary>
    /// Handles theme variant changes to update syntax highlighting theme.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        try
        {
            // Get syntax highlighting service from App.Services
            bool isDarkTheme = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

            // Update syntax highlighting theme to match
            _syntaxHighlightingService.UpdateThemeForVariant(isDarkTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling theme change");
            // Non-fatal: Continue with current theme
        }
    }

    #endregion Syntax Highlighting methods
}
