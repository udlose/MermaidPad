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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MermaidPad.Exceptions.Assets;
using MermaidPad.Services;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Main application window that contains the editor and preview WebView.
/// Manages synchronization between the editor control and the <see cref="MainViewModel"/>,
/// initializes and manages the <see cref="MermaidRenderer"/>, and handles window lifecycle events.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly MermaidRenderer _renderer;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;

    private bool _suppressEditorTextChanged;
    private bool _suppressEditorStateSync; // Prevent circular updates

    private const int WebViewReadyTimeoutSeconds = 30;

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
        _vm = sp.GetRequiredService<MainViewModel>();
        _updateService = sp.GetRequiredService<MermaidUpdateService>();
        DataContext = _vm;

        SimpleLogger.Log("=== MainWindow Initialization Started ===");

        Opened += OnOpened;
        Closing += OnClosing;

        // Focus the editor when the window is activated
        Activated += (_, _) => BringFocusToEditor();

        // Initialize editor with ViewModel data using validation
        SetEditorStateWithValidation(
            _vm.DiagramText,
            _vm.EditorSelectionStart,
            _vm.EditorSelectionLength,
            _vm.EditorCaretOffset
        );

        SimpleLogger.Log($"Editor initialized with {_vm.DiagramText.Length} characters");

        // Set up two-way synchronization between Editor and ViewModel
        SetupEditorViewModelSync();

        SimpleLogger.Log("=== MainWindow Initialization Completed ===");
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

            SimpleLogger.Log($"Editor state set: Start={validSelectionStart}, Length={validSelectionLength}, Caret={validCaretOffset} (text length: {textLength})");
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
        Editor.TextChanged += (_, _) =>
        {
            if (_suppressEditorTextChanged)
            {
                return;
            }

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

        // Editor selection/caret -> ViewModel: subscribe to both, coalesce into one update
        Editor.TextArea.SelectionChanged += (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            ScheduleEditorStateSyncIfNeeded();
        };

        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            ScheduleEditorStateSyncIfNeeded();
        };

        // ViewModel -> Editor synchronization
        _vm.PropertyChanged += OnViewModelPropertyChanged;
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
            case nameof(_vm.CanCopyClipboard):
            case nameof(_vm.CanPasteClipboard):
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
                Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Colors.Red);

                // Ensure selection is visible
                Editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.SteelBlue);
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
    /// Handles the window <see cref="OnOpened"/> event and starts the asynchronous open sequence.
    /// </summary>
    /// <param name="sender">Event sender (window).</param>
    /// <param name="e">Event arguments (unused).</param>
    /// <remarks>
    /// This method delegates to <see cref="OnOpenedCoreAsync"/> to perform asynchronous initialization,
    /// subscribe to renderer events, and start a failsafe timeout to enable UI if the WebView never becomes ready.
    /// Uses SafeFireAndForget to handle the async operation without blocking the event handler.
    /// </remarks>
    private void OnOpened(object? sender, EventArgs e)
    {
        OnOpenedCoreAsync()
            .SafeFireAndForget(onException: static ex =>
            {
                SimpleLogger.LogError("Unhandled exception in OnOpened", ex);
                //TODO - show a message to the user (this would need UI thread!)
                //Dispatcher.UIThread.Post(async () =>
                //{
                //    await MessageBox.ShowAsync(this, "An error occurred while opening the window. Please try again.", "Error", MessageBox.MessageBoxButtons.Ok, MessageBox.MessageBoxIcon.Error);
                //});
            }
            //TODO - re-enable this if I add UI operations in the future
            //continueOnCapturedContext: true  // Needed for UI operations and event subscriptions
            );
    }

    /// <summary>
    /// Handles the core logic to be executed when the window is opened asynchronously.
    /// </summary>
    /// <remarks>This method logs the window open event, invokes additional asynchronous operations,  and ensures the
    /// editor receives focus. It is intended to be called as part of the  window opening lifecycle.</remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task OnOpenedCoreAsync()
    {
        await OnOpenedAsync();
        BringFocusToEditor();
    }

    /// <summary>
    /// Performs the longer-running open sequence: check for updates, initialize the WebView, and update command states.
    /// </summary>
    /// <returns>A task representing the asynchronous open sequence.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required update assets cannot be resolved.</exception>
    /// <remarks>
    /// This method logs timing information, performs an update check by calling <see cref="MainViewModel.CheckForMermaidUpdatesAsync"/>,
    /// initializes the renderer via <see cref="InitializeWebViewAsync"/>, and notifies commands to refresh their CanExecute state.
    /// Exceptions are propagated for higher-level handling.
    /// </remarks>
    private async Task OnOpenedAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        SimpleLogger.Log("=== Window Opened Sequence Started ===");

        try
        {
            // TODO - re-enable this once a more complete update mechanism is in place
            // Step 1: Check for Mermaid updates
            //SimpleLogger.Log("Step 1: Checking for Mermaid updates...");
            //await _vm.CheckForMermaidUpdatesAsync();
            //SimpleLogger.Log("Mermaid update check completed");

            // Step 2: Initialize WebView (editor state is already synchronized via constructor)
            SimpleLogger.Log("Step 2: Initializing WebView...");
            string? assetsPath = Path.GetDirectoryName(_updateService.BundledMermaidPath);
            if (assetsPath is null)
            {
                const string error = "BundledMermaidPath does not contain a directory component";
                SimpleLogger.LogError(error);
                throw new InvalidOperationException(error);
            }

            // Needs to be on UI thread
            await InitializeWebViewAsync();

            // Step 3: Update command states
            SimpleLogger.Log("Step 3: Updating command states...");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.RenderCommand.NotifyCanExecuteChanged();
                _vm.ClearCommand.NotifyCanExecuteChanged();
            });

            stopwatch.Stop();
            SimpleLogger.LogTiming("Window opened sequence", stopwatch.Elapsed, success: true);
            SimpleLogger.Log("=== Window Opened Sequence Completed Successfully ===");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            SimpleLogger.LogTiming("Window opened sequence", stopwatch.Elapsed, success: false);
            SimpleLogger.LogError("Window opened sequence failed", ex);
            throw;
        }
    }

    /// <summary>
    /// Handles the window close event and initiates the cleanup sequence.
    /// </summary>
    /// <param name="sender">Event sender (window).</param>
    /// <param name="e">Cancel event args allowing the close to be canceled (not used here).</param>
    /// <remarks>
    /// This method delegates to <see cref="OnClosingAsync"/> to perform asynchronous cleanup operations.
    /// Uses SafeFireAndForget to handle the async cleanup without blocking the window close event.
    /// </remarks>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        OnClosingAsync()
            .SafeFireAndForget(onException: static ex => SimpleLogger.LogError("Failed during window close cleanup", ex));
    }

    /// <summary>
    /// Performs cleanup operations when the window is closing, including persisting state and disposing of resources
    /// asynchronously.
    /// </summary>
    /// <remarks>This method ensures that the application state is saved and any resources, such as the renderer, are
    /// properly disposed of before the window is closed. It logs the progress of the cleanup process for diagnostic
    /// purposes.</remarks>
    /// <returns>A <see cref="Task"/> that represents the asynchronous cleanup operation.</returns>
    private async Task OnClosingAsync()
    {
        SimpleLogger.Log("Window closing, cleaning up...");

        // Save state
        _vm.Persist();

        if (_renderer is IAsyncDisposable disposableRenderer)
        {
            await disposableRenderer.DisposeAsync();
            SimpleLogger.Log("MermaidRenderer disposed");
        }

        SimpleLogger.Log("Window cleanup completed successfully");
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
        SimpleLogger.Log("=== WebView Initialization Started ===");

        // Temporarily disable live preview during WebView initialization
        bool originalLivePreview = _vm.LivePreviewEnabled;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _vm.LivePreviewEnabled = false;
            SimpleLogger.Log($"Temporarily disabled live preview (was: {originalLivePreview})");
        });

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
                SimpleLogger.Log("WebView readiness observed");
            }
            catch (TimeoutException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _vm.IsWebViewReady = true;
                    _vm.LastError = $"WebView initialization timed out after {WebViewReadyTimeoutSeconds} seconds. Some features may not work correctly.";
                });
                SimpleLogger.Log($"WebView readiness timed out after {WebViewReadyTimeoutSeconds}s; enabling commands with warning");
            }

            success = true;
            SimpleLogger.Log("=== WebView Initialization Completed Successfully ===");
        }
        catch (OperationCanceledException)
        {
            // Treat cancellations distinctly; still propagate
            SimpleLogger.Log("WebView initialization was canceled.");
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
            SimpleLogger.LogError("WebView initialization failed", ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            SimpleLogger.LogTiming("WebView initialization", stopwatch.Elapsed, success);

            // Re-enable live preview after WebView is ready (or on failure)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.LivePreviewEnabled = originalLivePreview;
                SimpleLogger.Log($"Re-enabled live preview: {originalLivePreview}");
            });
        }
    }

    /// <summary>
    /// Handler for the close button click. Closes the window.
    /// </summary>
    /// <param name="sender">Event sender (button).</param>
    /// <param name="e">Routed event arguments.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        SimpleLogger.Log("Close button clicked");
        Close();
    }

    /// <summary>
    /// Handler for the Context Menu, to get updated Clipboard State
    /// </summary>
    /// <param name="sender">Event sender (Context Menu).</param>
    /// <param name="e">Cancel Event Arguments.</param>
    private void GetContextMenuState(object? sender, CancelEventArgs e)
    {
        // Get Clipboard state
        _vm.CanCopyClipboard = _vm.EditorSelectionLength > 0;

        UpdateCanPasteClipboardAsync()
            .SafeFireAndForget(onException: static ex => SimpleLogger.LogError("Failed to update CanPasteClipboard", ex));
    }

    /// <summary>
    /// Task that returns the text data format from the Clipboard.
    /// </summary>
    /// <param name="window">The window instance</param>
    private static async Task<string?> GetTextFromClipboardAsync(Window window)
    {
        IClipboard? clipboard = window.Clipboard;

        if (clipboard is null)
        {
            return null;
        }
        
        string? clipboardText = await clipboard.TryGetTextAsync();

        return clipboardText;
    }

    /// <summary>
    /// Reads clipboard text asynchronously and updates the ViewModel's CanPasteClipboard on the UI thread.
    /// </summary>
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
            SimpleLogger.LogError("Error reading clipboard text", ex);
        }

        bool canPaste = !string.IsNullOrWhiteSpace(clipboardText);

        // Marshal back to UI thread to update the ViewModel property
        await Dispatcher.UIThread.InvokeAsync(() => _vm.CanPasteClipboard = canPaste, DispatcherPriority.Normal);
    }
}
