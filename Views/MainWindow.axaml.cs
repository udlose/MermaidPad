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
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using MermaidPad.Exceptions.Assets;
using MermaidPad.Extensions;
using MermaidPad.Models.Editor;
using MermaidPad.Services;
using MermaidPad.Services.Editor;
using MermaidPad.Services.Highlighting;
using MermaidPad.Services.Theming;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using TextMateSharp.Grammars;

namespace MermaidPad.Views;

/// <summary>
/// Main application window that contains the editor and preview WebView.
/// Manages synchronization between the editor control and the <see cref="MainWindowViewModel"/>,
/// initializes and manages the <see cref="MermaidRenderer"/>, and handles window lifecycle events.
/// </summary>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
public sealed partial class MainWindow : Window
{
    private readonly Lock _subscriptionsLock = new Lock();
    private readonly MainWindowViewModel _vm;
    private readonly MermaidRenderer _renderer;
    private readonly MermaidUpdateService _updateService;
    private readonly SyntaxHighlightingService _syntaxHighlightingService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly DocumentAnalyzer _documentAnalyzer;
    private readonly ILogger<MainWindow> _logger;
    private readonly IThemeService _themeService;

    private bool _isClosingApproved;
    private bool _suppressEditorTextChanged;
    private bool _suppressEditorStateSync; // Prevent circular updates
    private readonly SemaphoreSlim _contextMenuSemaphore = new SemaphoreSlim(1, 1);

    private const int WebViewReadyTimeoutSeconds = 30;

    // Track disposables for cleanup
    private CompositeDisposable? _subscriptions;

    #region Theme-related members

    private readonly Dictionary<ApplicationTheme, MenuItem> _applicationThemeMenuItems = new Dictionary<ApplicationTheme, MenuItem>();
    private readonly Dictionary<ThemeName, MenuItem> _editorThemeMenuItems = new Dictionary<ThemeName, MenuItem>();
    private ApplicationTheme? _previousApplicationTheme;
    private ThemeName? _previousEditorTheme;
    private const string CheckmarkGeometryPath = "M4,6 L8,10 L14,4 L14,6 L8,12 L4,8 Z";     // SVG-like path data for a 14x12 checkmark icon
    private static readonly Geometry _checkmarkGeometry = Geometry.Parse(CheckmarkGeometryPath);

    #endregion Theme-related members

    /// <summary>
    /// Initializes a new instance of the MainWindow class and sets up application services, data context, and editor state.
    /// This is where we resolve services, set DataContext, initialize passive components, and wire subscriptions that don't depend on the visual tree.
    /// </summary>
    /// <remarks>This constructor configures the main window by retrieving required services from the
    /// application's service provider, initializing syntax highlighting, and establishing synchronization between the
    /// editor and the view model. Logging is performed to indicate the start and completion of initialization. The
    /// window's data context is set to the associated view model, enabling data binding for UI elements.</remarks>
    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _editorDebouncer = sp.GetRequiredService<IDebounceDispatcher>();
        _renderer = sp.GetRequiredService<MermaidRenderer>();
        _vm = sp.GetRequiredService<MainWindowViewModel>();
        _updateService = sp.GetRequiredService<MermaidUpdateService>();
        _syntaxHighlightingService = sp.GetRequiredService<SyntaxHighlightingService>();
        _documentAnalyzer = sp.GetRequiredService<DocumentAnalyzer>();
        _logger = sp.GetRequiredService<ILogger<MainWindow>>();
        _themeService = sp.GetRequiredService<IThemeService>();
        DataContext = _vm;

        _subscriptions = new CompositeDisposable();
        _logger.LogInformation("=== MainWindow Initialization Started ===");

        // Initialize syntax highlighting before wiring up OnThemeChanged
        InitializeSyntaxHighlighting();

        InitializeIntellisense();

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
        WireUpEditorActions();

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
            Editor.TextArea.IndentationStrategy = new MermaidIndentationStrategy(_documentAnalyzer, Editor.Options);

            _logger.LogInformation("Editor state set with {CharacterCount} characters", textLength);
        }
        finally
        {
            _suppressEditorStateSync = false;
        }
    }

    /// <summary>
    /// Initializes synchronization between the editor and the view model, ensuring that changes in text, selection, and
    /// caret position are reflected in both components.
    /// </summary>
    /// <remarks>This method sets up event handlers to keep the editor and view model in sync. It manages
    /// subscriptions so that updates to the editor's text, selection, and caret position are propagated to the view
    /// model, and changes in the view model are reflected in the editor. All event subscriptions are tracked for proper
    /// disposal, helping to prevent memory leaks.</remarks>
    private void SetupEditorViewModelSync()
    {
        // Editor -> ViewModel synchronization (text)
        Editor.TextChanged += OnEditorTextChanged;
        IDisposable dText = Disposable.Create(UnsubscribeEditorTextChanged);
        AddOrDisposeSubscription(dText);

        // Editor selection/caret -> ViewModel: subscribe to both, coalesce into one update
        Editor.TextArea.SelectionChanged += OnEditorSelectionChanged;
        IDisposable dSelection = Disposable.Create(UnsubscribeEditorSelectionChanged);
        AddOrDisposeSubscription(dSelection);

        // Editor caret position changed -> ViewModel synchronization
        Editor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        IDisposable dCaret = Disposable.Create(UnsubscribeEditorCaretPositionChanged);
        AddOrDisposeSubscription(dCaret);

        // ViewModel -> Editor synchronization
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        IDisposable dVm = Disposable.Create(UnsubscribeViewModelPropertyChanged);
        AddOrDisposeSubscription(dVm);
    }

    #region Unsubscribe helpers to avoid closure allocations

    // These method-group targets eliminate lambda closures in Disposable.Create(...)
    /// <summary>
    /// Detaches the event handler for editor text changes to stop receiving notifications when the editor's text is
    /// modified.
    /// </summary>
    /// <remarks>Call this method to unsubscribe from the editor's TextChanged event when text change
    /// notifications are no longer needed, such as during cleanup or disposal. This helps prevent memory leaks and
    /// unintended side effects from lingering event subscriptions.</remarks>
    private void UnsubscribeEditorTextChanged()
    {
        if (Editor is not null)
        {
            Editor.TextChanged -= OnEditorTextChanged;
        }
    }

    /// <summary>
    /// Detaches the handler for editor selection change events from the text area.
    /// </summary>
    /// <remarks>Call this method to stop receiving notifications when the editor's selection changes. This is
    /// typically used to clean up event subscriptions and prevent memory leaks when the editor is no longer
    /// needed.</remarks>
    private void UnsubscribeEditorSelectionChanged()
    {
        if (Editor is not null)
        {
            Editor.TextArea.SelectionChanged -= OnEditorSelectionChanged;
        }
    }

    /// <summary>
    /// Detaches the handler for property change notifications from the associated view model.
    /// </summary>
    /// <remarks>Call this method to stop receiving property change events from the view model. This is
    /// typically used to prevent memory leaks or unwanted updates when the view model is no longer needed or the
    /// containing object is being disposed.</remarks>
    private void UnsubscribeViewModelPropertyChanged() => _vm.PropertyChanged -= OnViewModelPropertyChanged;

    /// <summary>
    /// Unsubscribes the handler from the ActualThemeVariantChanged event to stop receiving notifications when the theme
    /// variant changes.
    /// </summary>
    /// <remarks>Call this method to detach the event handler when theme variant change notifications are no
    /// longer needed, such as during cleanup or disposal. This helps prevent memory leaks and unintended side effects
    /// from lingering event subscriptions.</remarks>
    private void UnsubscribeThemeVariantChanged() => ActualThemeVariantChanged -= OnThemeVariantChanged;

    #endregion Unsubscribe helpers to avoid closure allocations

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
                // NOTE: Accessing .Text allocates a string. This is unavoidable with standard AvaloniaEdit API
                string currentText = Editor.Text;
                if (currentText != _vm.DiagramText)
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
                        int textLength = Editor.Document.TextLength;
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

            case nameof(_vm.CurrentApplicationTheme):
                UpdateApplicationThemeCheckmarks();
                break;

            case nameof(_vm.CurrentEditorTheme):
                ApplyEditorTheme(_vm.CurrentEditorTheme);
                UpdateEditorThemeCheckmarks();
                break;
        }
    }

    /// <summary>
    /// Handles the window activated event, bringing focus to the editor and updating clipboard state.
    /// </summary>
    /// <remarks>
    /// Updates the CanPasteClipboard state when the window gains focus, allowing the app to
    /// detect if the user copied text from another application.
    /// </remarks>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnActivated(object? sender, EventArgs e)
    {
        BringFocusToEditor();

        // Update clipboard state when window gains focus (user might have copied from another app)
        UpdateCanPasteClipboardAsync()
            .SafeFireAndForget(onException: ex =>
                _logger.LogError(ex, "Failed to update clipboard state on activation"));
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
        // Check if we're on the ui thread first
        if (Dispatcher.UIThread.CheckAccess())
        {
            SetFocusToEditor();
            return;
        }

        Dispatcher.UIThread.Post(SetFocusToEditor, DispatcherPriority.Input);

        void SetFocusToEditor()
        {
            if (Editor?.TextArea is not null)
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
            }
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
            _logger.LogInformation("Initializing WebView...");
            string? assetsPath = Path.GetDirectoryName(_updateService.BundledMermaidPath);
            if (assetsPath is null)
            {
                const string error = $"{nameof(MermaidUpdateService.BundledMermaidPath)} does not contain a directory component";
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

        // Dispose of the context menu semaphore here, as all operations using it have completed
        _contextMenuSemaphore.Dispose();

        _logger.LogInformation("All event handlers unsubscribed successfully");
    }

    //TODO - DaveBlack: consider re-adding this method for close prompt functionality if we need it
    /*
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
    */
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
            catch (TimeoutException te)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _vm.IsWebViewReady = true;
                    _vm.LastError = $"WebView initialization timed out after {WebViewReadyTimeoutSeconds} seconds. Some features may not work correctly.";
                });
                _logger.LogWarning(te, "WebView readiness timed out after {TimeoutSeconds}s; enabling commands with warning", WebViewReadyTimeoutSeconds);
            }

            success = true;
            _logger.LogInformation("=== WebView Initialization Completed Successfully ===");
        }
        catch (OperationCanceledException oce)
        {
            // Treat cancellations distinctly; still propagate
            _logger.LogInformation(oce, "WebView initialization was canceled.");
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


    /// <summary>
    /// Adds the specified subscription to the internal collection if it is available; otherwise, disposes the
    /// subscription immediately.
    /// </summary>
    /// <remarks>If the internal subscription collection has already been cleared or disposed, the provided
    /// subscription will be disposed immediately to prevent resource leaks. This method is thread-safe.</remarks>
    /// <param name="subscription">The subscription to add or dispose.</param>
    private void AddOrDisposeSubscription(IDisposable subscription)
    {
        lock (_subscriptionsLock)
        {
            if (_subscriptions is not null)
            {
                _subscriptions.Add(subscription);
                return;
            }
        }

        // Subscriptions were already cleared/disposed - dispose the provided subscription.
        // Dispose outside the lock to avoid running user code while holding the lock.
        subscription.Dispose();
    }

    /// <summary>
    /// Releases resources and clears theme-related menu item collections during the unload or disposal process.
    /// </summary>
    /// <remarks>This method atomically disposes of all tracked subscriptions and clears internal collections
    /// for application and editor theme menu items. It is intended to be called when the containing object is being
    /// unloaded or disposed to ensure proper cleanup and prevent resource leaks. Command bindings are automatically
    /// cleaned by Avalonia and do not require manual intervention.</remarks>
    private void PerformUnloadCleanup()
    {
        // Take ownership of _subscriptions under lock to prevent races with additions.
        CompositeDisposable? toDispose;
        lock (_subscriptionsLock)
        {
            toDispose = _subscriptions;
            _subscriptions = null;
        }
        toDispose?.Dispose();

        _applicationThemeMenuItems.Clear();
        _editorThemeMenuItems.Clear();
        
        // Dispose of the context menu semaphore here, as all operations using it have completed
        _contextMenuSemaphore.Dispose();

        _logger.LogInformation("Composite disposables disposed and Theme menu dictionaries cleared (Command bindings auto-cleaned by Avalonia)");
    }

    #region Clipboard and Edit Methods

    /// <summary>
    /// Wires up the clipboard and edit action delegates in the ViewModel to their implementations.
    /// </summary>
    /// <remarks>
    /// This method connects the ViewModel's Action properties to the actual implementation methods,
    /// enabling proper MVVM separation while allowing the View to implement UI-specific operations.
    /// </remarks>
    private void WireUpEditorActions()
    {
        _vm.CutAction = CutToClipboardAsync;
        _vm.CopyAction = CopyToClipboardAsync;
        _vm.PasteAction = PasteFromClipboardAsync;
        _vm.UndoAction = UndoEdit;
        _vm.RedoAction = RedoEdit;
        _vm.SelectAllAction = SelectAllText;
        _vm.OpenFindAction = OpenFindPanel;
        _vm.FindNextAction = FindNextMatch;
        _vm.FindPreviousAction = FindPreviousMatch;
        _vm.GetCurrentEditorContextFunc = GetCurrentEditorContext;
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
        try
        {
            // If we're not on the UI thread, dispatch just the core logic (not the whole method)
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(CutToClipboardCoreAsync, DispatcherPriority.Normal);
                return;
            }

            if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(Editor.SelectedText) || Clipboard is null)
            {
                return;
            }

            // We're on the UI thread, proceed directly
            await CutToClipboardCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cut text to clipboard");
            // Don't re-throw - keep method safe for command handlers
            // The text remains in the editor, which is the safe fallback
        }
    }

    /// <summary>
    /// Core logic for cutting text to clipboard. Must be called on the UI thread.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CutToClipboardCoreAsync()
    {
        // UI thread validation checks
        string selectedText = Editor.SelectedText;
        if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(selectedText) || Clipboard is null)
        {
            return;
        }

        // Since this operation is asynchronous but should be atomic, we need to
        // store the selection position before the async operation and verify it hasn't changed
        int selectionStart = Editor.SelectionStart;
        int selectionLength = Editor.SelectionLength;

        try
        {
            // Phase 1: Copy to clipboard (async operation on UI thread)
            await Clipboard.SetTextAsync(selectedText);

            // Phase 2: Remove text only if:
            // - Clipboard operation succeeded (we got here)
            // - Selection hasn't changed
            // - Document is still large enough (boundary validation)
            if (Editor.SelectionStart == selectionStart &&
                Editor.SelectionLength == selectionLength &&
                selectionStart + selectionLength <= Editor.Document.TextLength)
            {
                Editor.Document.Remove(selectionStart, selectionLength);
                _logger.LogInformation("Cut {CharCount} characters to clipboard", selectedText.Length);

                // We just put text on clipboard, so paste *should* be enabled.
                // For our purpose, we'll optimistically assume it is.
                _vm.CanPasteClipboard = true;
                _vm.PasteCommand.NotifyCanExecuteChanged();
            }
            else
            {
                // Log why we didn't remove the text
                if (Editor.SelectionStart != selectionStart || Editor.SelectionLength != selectionLength)
                {
                    _logger.LogWarning(
                        "Selection changed during cut operation (was {Start}:{Length}, now {NewStart}:{NewLength}) - text not removed",
                        selectionStart, selectionLength, Editor.SelectionStart, Editor.SelectionLength);
                }
                else if (selectionStart + selectionLength > Editor.Document.TextLength)
                {
                    _logger.LogWarning(
                        "Document size changed during cut operation (was {OldLength}, now {NewLength}) - text not removed to prevent exception",
                        selectionStart + selectionLength, Editor.Document.TextLength);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cut text to clipboard");
            // Don't re-throw - keep method safe for command handlers
            // The text remains in the editor, which is the safe fallback
        }
    }

    /// <summary>
    /// Asynchronously copies the selected text to the clipboard.
    /// </summary>
    /// <remarks>
    /// This method must be called on the UI thread as it accesses both the TextEditor and the clipboard.
    /// </remarks>
    /// <returns>A task representing the asynchronous copy operation.</returns>
    private async Task CopyToClipboardAsync()
    {
        try
        {
            // If we're not on the UI thread, dispatch just the core logic (not the whole method)
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(CopyToClipboardCoreAsync, DispatcherPriority.Normal);
                return;
            }

            // Now safe to check UI properties - we're guaranteed to be on UI thread
            if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(Editor.SelectedText) || Clipboard is null)
            {
                return;
            }

            // We're on the UI thread, proceed directly
            await CopyToClipboardCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy text to clipboard");
        }
    }

    /// <summary>
    /// Core logic for copying text to clipboard. Must be called on the UI thread.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CopyToClipboardCoreAsync()
    {
        // UI thread validation checks
        string selectedText = Editor.SelectedText;
        if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(selectedText) || Clipboard is null)
        {
            return;
        }

        try
        {
            await Clipboard.SetTextAsync(selectedText);
            _logger.LogInformation("Copied {CharCount} characters to clipboard", selectedText.Length);

            // We just put text on clipboard, so paste *should* be enabled.
            // For our purpose, we'll optimistically assume it is.
            _vm.CanPasteClipboard = true;
            _vm.PasteCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy text to clipboard");
        }
    }

    /// <summary>
    /// Asynchronously pastes text from the clipboard into the current context, if available.
    /// </summary>
    /// <remarks>This method ensures thread safety by dispatching clipboard operations to the UI thread when
    /// necessary. If the clipboard is unavailable, the method completes without performing any action. Exceptions are
    /// logged but not propagated, making this method safe to use in command handlers.</remarks>
    /// <returns>A task that represents the asynchronous paste operation. The task completes when the clipboard content has been
    /// processed.</returns>
    private async Task PasteFromClipboardAsync()
    {
        try
        {
            // If we're not on the UI thread, dispatch just the core logic (not the whole method)
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(PasteFromClipboardCoreAsync, DispatcherPriority.Normal);
                return;
            }

            if (Clipboard is null)
            {
                return;
            }

            // We're on the UI thread, proceed directly
            await PasteFromClipboardCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to paste text from clipboard");
            // Don't re-throw - keep method safe for command handlers
        }
    }

    /// <summary>
    /// Asynchronously pastes text from the clipboard at the current caret position.
    /// </summary>
    /// <remarks>
    /// This method must be called on the UI thread as it accesses both the TextEditor and the clipboard.
    /// The pasted text replaces any current selection, and the caret moves to the end of the pasted text.
    /// </remarks>
    /// <returns>A task representing the asynchronous paste operation.</returns>
    private async Task PasteFromClipboardCoreAsync()
    {
        // UI thread validation checks
        if (Clipboard is null)
        {
            return;
        }

        try
        {
            string? clipboardText = await GetTextFromClipboardAsync(this);
            if (string.IsNullOrEmpty(clipboardText))
            {
                return;
            }

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
            _logger.LogError(ex, "Failed to paste text from clipboard");
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
            //TODO: there is a known issue where SelectAll does not update. See: https://github.com/AvaloniaUI/AvaloniaEdit/issues/512
            // we need to monitor that issue for a new release and remove this comment when it is fixed.
            // I am tracking it here: https://github.com/udlose/MermaidPad/issues/258
            //Editor.SelectAll();

            // As a temporary workaround, we manually create the selection.
            Selection selection = Selection.Create(Editor.TextArea, 0, Editor.Document.TextLength);
            Editor.TextArea.Selection = selection;
            Editor.CaretOffset = Editor.Document.TextLength;

            // Make sure caret is visible after selection
            Editor.TextArea.Caret.BringCaretToView();

            _logger.LogInformation("Select all operation performed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform select all operation");
        }
    }

    /// <summary>
    /// Gets a value indicating whether the Select All operation can be performed in the editor.
    /// </summary>
    private bool CanSelectAll => Editor?.Document?.TextLength > 0;

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
            // The maximum length of selected text to pre-fill the search pattern in the find panel.
            // This limit prevents excessively long selections from being used as the search pattern.
            const int maxSearchPatternPrefillLength = 100;

            // Pre-fill search with selected text if available
            if (Editor.SelectionLength is > 0 and < maxSearchPatternPrefillLength)
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
    /// Ensures context-menu commands (Copy, Cut, Undo, Redo, Paste) are enabled/disabled based on the
    /// current editor selection and clipboard contents when the context menu opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an async-void event handler; it is safe here because:
    ///     1. It is an event handler (the only valid use of async void).
    ///     2. All exceptions are caught and logged.
    ///     3. It performs an awaited clipboard read so the menu doesn't render with stale state.
    /// </para>
    /// <para>
    /// Synchronization semantics:
    ///     * Uses a non-blocking, async-aware semaphore acquire (<c>_contextMenuSemaphore.WaitAsync(TimeSpan.Zero)</c>)
    ///       to implement "skip-on-busy" behavior: if a state-check is already running, new invocations
    ///       return immediately to keep the UI responsive.
    ///     * A local <c>acquired</c> flag tracks whether the 'acquire' succeeded; <c>Release()</c> is called
    ///       in the <c>finally</c> block only when the semaphore was actually acquired.
    /// </para>
    /// <para>
    /// Threading and safety:
    ///     * The method assumes it starts on the UI thread (context-menu open event) but is defensive:
    ///       all ViewModel updates are marshaled to the UI thread when required.
    ///     * The semaphore prevents overlapping starts; it does not serialize callers that were never granted the semaphore.
    /// </para>
    /// <para>
    /// Trade-offs and when to change:
    ///     * Skip-on-busy (current) is fast and avoids allocations/queueing, but callers can be dropped and the menu
    ///       may occasionally render with slightly stale state. This is acceptable for cheap, infrequent clipboard checks.
    ///     * If you must guarantee every invocation runs (no drops) replace the try-acquire with a full
    ///       <c>await _contextMenuSemaphore.WaitAsync()</c> so callers queue and are executed in order.
    /// </para>
    /// <para>
    /// Diagnostics:
    ///     * Unexpected semaphore release errors are surfaced via <c>Debug.Fail</c> in DEBUG and logged as Error in RELEASE.
    /// </para>
    /// </remarks>
    /// <param name="sender">Source of the event (may be null).</param>
    /// <param name="e">CancelEventArgs for the opening event; if <see cref="CancelEventArgs.Cancel"/> is true no work is done.</param>
    [SuppressMessage("ReSharper", "AsyncVoidEventHandlerMethod", Justification = "This is an event handler, so async void is appropriate here.")]
    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "This is an event handler, so async void is appropriate here.")]
    private async void GetContextMenuState(object? sender, CancelEventArgs e)
    {
        if (e.Cancel)
        {
            return; // nothing to do
        }

        // We want to avoid overlapping executions of this method if the user somehow triggers multiple context menu openings quickly
        bool acquired = false;
        try
        {
            // Non-blocking try-acquire: prefer skip-on-busy semantics (no UI blocking)
            acquired = await _contextMenuSemaphore.WaitAsync(TimeSpan.Zero);
            if (!acquired)
            {
                return; // skip if busy
            }

            // We should already be on the UI thread since this method is an event handler,
            // but be defensive: marshal ViewModel updates when not on UI thread.
            if (Dispatcher.UIThread.CheckAccess())
            {
                _vm.CanCopyClipboard = _vm.EditorSelectionLength > 0;
                _vm.CanCutClipboard = _vm.EditorSelectionLength > 0;
                _vm.CanUndo = Editor.CanUndo;
                _vm.CanRedo = Editor.CanRedo;
                _vm.CanSelectAll = CanSelectAll;
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _vm.CanCopyClipboard = _vm.EditorSelectionLength > 0;
                    _vm.CanCutClipboard = _vm.EditorSelectionLength > 0;
                    _vm.CanUndo = Editor.CanUndo;
                    _vm.CanRedo = Editor.CanRedo;
                    _vm.CanSelectAll = CanSelectAll;
                }, DispatcherPriority.Normal);
            }

            string? clipboardText = null;
            try
            {
                // AWAIT the async clipboard check to minimize the possibility of the menu rendering with a stale state
                clipboardText = await GetTextFromClipboardAsync(this);
            }
            catch (Exception ex)
            {
                // Treat as no text available
                _logger.LogDebug(ex, "Clipboard read failed in context menu state check");
            }

            // We should be able to paste whitespace, so only check for null or empty
            if (Dispatcher.UIThread.CheckAccess())
            {
                _vm.CanPasteClipboard = !string.IsNullOrEmpty(clipboardText);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => _vm.CanPasteClipboard = !string.IsNullOrEmpty(clipboardText), DispatcherPriority.Normal);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update CanPasteClipboard in context menu");
        }
        finally
        {
            // Only release if we acquired the semaphore.
            if (acquired)
            {
                try
                {
                    _contextMenuSemaphore.Release();
                }
                catch (SemaphoreFullException ex)
                {
#if DEBUG
                    Debug.Fail("Context menu semaphore released without a matching WaitAsync", ex.ToString());
                    Debugger.Break();
#else
                    // Log as error for triage but avoid re-throwing here because it will crash the process from an async-void
                    _logger.LogError(ex, "Context menu semaphore release failed (already released) - indicates a logic bug");
#endif
                }
            }
        }
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
        // Get Window.Clipboard on UI thread when necessary; don't capture caller context for the dispatch.
        // If we end up on a background thread, we need to ensure we await Dispatcher calls without capturing the UI context
        // If caller is on UI thread, read directly and resume on UI thread.
        if (Dispatcher.UIThread.CheckAccess())
        {
            IClipboard? clipboard = window.Clipboard;
            if (clipboard is null)
            {
                return null;
            }

            // If caller is on UI thread, allow resuming on UI thread.
            return await clipboard.TryGetTextAsync();
        }

        // Off-UI caller: get the clipboard reference on the UI thread (synchronous lambda),
        // then perform the async read without capturing the UI context to avoid deadlocks.
        // If we don't await Dispatcher calls *in this case*, we run into possible race conditions where the Clipboard property isn't ready yet.
        IClipboard? uiClipboard = await Dispatcher.UIThread.InvokeAsync(() => window.Clipboard, DispatcherPriority.Background);
        if (uiClipboard is null)
        {
            return null;
        }

        // If caller is background, avoid capturing the UI context to reduce deadlock risk.
        return await uiClipboard.TryGetTextAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously updates the ViewModel to reflect whether clipboard text is available for pasting.
    /// </summary>
    /// <remarks>This method reads the clipboard text off the UI thread and updates the CanPasteClipboard
    /// property on the ViewModel. If clipboard access fails or the clipboard is empty,
    /// <see cref="MainWindowViewModel.CanPasteClipboard"/> is set to false.
    /// The update is marshaled back to the UI thread to ensure thread safety.</remarks>
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

        // We should be able to paste whitespace, so only check for null or empty
        bool canPaste = !string.IsNullOrEmpty(clipboardText);

        // Marshal back to UI thread to update the ViewModel property
        await Dispatcher.UIThread.InvokeAsync(() => _vm.CanPasteClipboard = canPaste, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Extracts the current editor context from the TextEditor control.
    /// </summary>
    /// <remarks>
    /// This method retrieves the current state of the editor including the document, selection,
    /// and caret position. It validates the editor state and returns null if the editor is not
    /// in a valid state for commenting operations. This is called on-demand by the ViewModel
    /// when comment/uncomment commands execute, ensuring fresh, accurate state.
    /// </remarks>
    /// <returns>
    /// An <see cref="EditorContext"/> containing the current editor state if valid; otherwise, null.
    /// </returns>
    private EditorContext? GetCurrentEditorContext()
    {
        // Validate editor is loaded and has a document
        if (!Editor.IsLoaded)
        {
            _logger.LogWarning("{MethodName}: Editor is not loaded", nameof(GetCurrentEditorContext));
            return null;
        }

        TextDocument? document = Editor.Document;
        if (document is null)
        {
            _logger.LogWarning("{MethodName}: Editor document is null", nameof(GetCurrentEditorContext));
            return null;
        }

        // Extract current editor state
        int selectionStart = Editor.SelectionStart;
        int selectionLength = Editor.SelectionLength;
        int caretOffset = Editor.CaretOffset;

        // Validate selection and caret are within bounds
        if (selectionStart < 0 || selectionLength < 0 || caretOffset < 0 || (selectionStart + selectionLength) > document.TextLength)
        {
            _logger.LogWarning("{MethodName}: Invalid editor state - SelectionStart={SelectionStart}, SelectionLength={SelectionLength}, CaretOffset={CaretOffset}, TextLength={TextLength}",
                nameof(GetCurrentEditorContext), selectionStart, selectionLength, caretOffset, document.TextLength);

            return new EditorContext(document, selectionStart, selectionLength, caretOffset)
            {
                IsValid = false
            };
        }

        // Emulate what AvaloniaEdit.TextEditor.SelectedText does - see: https://github.com/AvaloniaUI/AvaloniaEdit/blob/8dea781b49b09dedcf98ee7496d4e4a10b410ef0/src/AvaloniaEdit/TextEditor.cs#L971-L978
        // We'll get the text from the whole surrounding segment. This is done to ensure that SelectedText.Length == SelectionLength.
        string selectedText = string.Empty;
        if (!Editor.TextArea.Selection.IsEmpty)
        {
            selectedText = Editor.TextArea.Document.GetText(Editor.TextArea.Selection.SurroundingSegment);
        }

        return new EditorContext(document, selectionStart, selectionLength, caretOffset)
        {
            SelectedText = selectedText,
            IsValid = true
        };
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
    /// Handles changes to the application's theme by updating the syntax highlighting service to match the current
    /// theme variant.
    /// </summary>
    /// <remarks>This method synchronizes the syntax highlighting appearance with the application's active
    /// theme. It is typically called when the theme variant changes, ensuring consistent visual styling throughout the
    /// application.</remarks>
    private void OnThemeChanged()
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

    /// <summary>
    /// Handles the event when the application's theme variant changes.
    /// </summary>
    /// <param name="sender">The source of the event, typically the object that triggered the theme variant change.</param>
    /// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
    private void OnThemeVariantChanged(object? sender, EventArgs e) => OnThemeChanged();

    #endregion Syntax Highlighting methods

    #region Theme methods

    /// <summary>
    /// Applies the specified theme to the editor component.
    /// </summary>
    /// <param name="theme">The name of the theme to apply to the editor.</param>
    private void ApplyEditorTheme(ThemeName theme)
{
    try
    {
        _themeService.ApplyEditorTheme(Editor, theme);
        _logger.LogInformation("Applied editor theme in response to ViewModel change: {Theme}", theme);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to apply editor theme: {Theme}", theme);
        _vm.LastError = $"Failed to apply editor theme: {ex.Message}";
    }
}

/// <summary>
/// Populates the specified menu with items representing all available application themes, enabling users to select
/// and apply a theme.
/// </summary>
/// <remarks>Each menu item is bound to the application's theme selection command and reflects the current
/// theme state. The method updates menu item checkmarks for efficient theme switching and uses MVVM command binding
/// to avoid direct event handlers.</remarks>
/// <param name="parentMenu">The parent menu to which theme selection items will be added. Must not be null.</param>
private void PopulateApplicationThemeMenu(MenuItem parentMenu)
{
    ApplicationTheme currentTheme = _themeService.CurrentApplicationTheme;
    _applicationThemeMenuItems.Clear();

    // Initialize previous theme tracker
    _previousApplicationTheme = currentTheme;

    foreach (ApplicationTheme theme in _vm.GetAvailableApplicationThemes())
    {
        MenuItem menuItem = new MenuItem
        {
            Header = _vm.GetApplicationThemeDisplayName(theme),
            Tag = theme,
            IsChecked = theme == currentTheme
        };

        // Add Bind Command and CommandParameter so we can avoid Click handlers
        BindMenuItemCommand(menuItem, nameof(MainWindowViewModel.SetApplicationThemeCommand), theme);

        // Add checkmark icon for checked state (Avalonia MenuItem displays Icon when IsChecked is true)
        if (theme == currentTheme)
        {
            menuItem.Icon = CreateCheckmarkIcon();
        }

        // Track menu item checkmark updates
        _applicationThemeMenuItems[theme] = menuItem;

        parentMenu.Items.Add(menuItem);
    }

    _logger.LogInformation("Application theme menu populated with {Count} themes using MVVM Command binding", _applicationThemeMenuItems.Count);
}

/// <summary>
/// Updates the checkmark states of application theme menu items to reflect the currently selected theme.
/// </summary>
/// <remarks>This method ensures that only the menu item corresponding to the active application theme is
/// checked, and any previously checked theme menu item is unchecked. It should be called whenever the application
/// theme changes to keep the menu UI in sync with the current theme selection.</remarks>
private void UpdateApplicationThemeCheckmarks()
{
    ApplicationTheme currentTheme = _vm.CurrentApplicationTheme;

    // Uncheck the previously selected theme (if it exists and changed)
    if (_previousApplicationTheme.HasValue && _previousApplicationTheme.Value != currentTheme &&
        _applicationThemeMenuItems.TryGetValue(_previousApplicationTheme.Value, out MenuItem? previousItem))
    {
        previousItem.IsChecked = false;
        previousItem.Icon = null; // Remove checkmark
    }

    // Check the currently selected theme
    if (_applicationThemeMenuItems.TryGetValue(currentTheme, out MenuItem? currentItem))
    {
        currentItem.IsChecked = true;
        currentItem.Icon = CreateCheckmarkIcon(); // Add checkmark
    }

    // Update tracker for next change
    _previousApplicationTheme = currentTheme;
}

/// <summary>
/// Populates the specified menu with items representing available editor themes, enabling users to select and apply
/// a theme via MVVM command binding.
/// </summary>
/// <remarks>Each menu item is bound to the ViewModel's SetEditorThemeCommand and represents a selectable
/// editor theme. The currently active theme is indicated with a checkmark. This method should be called when the
/// list of available themes or the current theme changes to ensure the menu reflects the latest state.</remarks>
/// <param name="parentMenu">The parent menu to which editor theme menu items will be added. Must not be null.</param>
private void PopulateEditorThemeMenu(MenuItem parentMenu)
{
    ThemeName currentTheme = _themeService.CurrentEditorTheme;
    _editorThemeMenuItems.Clear();

    // Initialize previous theme tracker
    _previousEditorTheme = currentTheme;

    foreach (ThemeName theme in _vm.GetAvailableEditorThemes())
    {
        MenuItem menuItem = new MenuItem
        {
            Header = _vm.GetEditorThemeDisplayName(theme),
            Tag = theme,
            IsChecked = theme == currentTheme
        };

        // Add Bind Command and CommandParameter so we can avoid Click handlers
        BindMenuItemCommand(menuItem, nameof(MainWindowViewModel.SetEditorThemeCommand), theme);

        // Add checkmark icon for checked state (Avalonia MenuItem displays Icon when IsChecked is true)
        if (theme == currentTheme)
        {
            menuItem.Icon = CreateCheckmarkIcon();
        }

        // Track menu item for checkmark updates
        _editorThemeMenuItems[theme] = menuItem;

        parentMenu.Items.Add(menuItem);
    }

    _logger.LogInformation("Editor theme menu populated with {Count} themes using MVVM Command binding", _editorThemeMenuItems.Count);
}

/// <summary>
/// Updates the checkmark states of editor theme menu items to reflect the currently selected theme.
/// </summary>
/// <remarks>This method ensures that only the menu item corresponding to the active editor theme is
/// checked, and any previously checked theme is unchecked. It should be called whenever the editor theme changes to
/// keep the menu state in sync with the application's theme selection.</remarks>
private void UpdateEditorThemeCheckmarks()
{
    ThemeName currentTheme = _vm.CurrentEditorTheme;

    // Uncheck the previously selected theme (if it exists and changed)
    if (_previousEditorTheme.HasValue && _previousEditorTheme.Value != currentTheme &&
        _editorThemeMenuItems.TryGetValue(_previousEditorTheme.Value, out MenuItem? previousItem))
    {
        previousItem.IsChecked = false;
        previousItem.Icon = null; // Remove checkmark
    }

    // Check the currently selected theme
    if (_editorThemeMenuItems.TryGetValue(currentTheme, out MenuItem? currentItem))
    {
        currentItem.IsChecked = true;
        currentItem.Icon = CreateCheckmarkIcon(); // Add checkmark
    }

    // Update tracker for next change
    _previousEditorTheme = currentTheme;
}

/// <summary>
/// Creates a new checkmark icon as a PathIcon with predefined geometry and size.
/// </summary>
/// <returns>A PathIcon representing a checkmark, with a width of 14 and height of 12 units.</returns>
private static PathIcon CreateCheckmarkIcon()
{
    // Create a fresh PathIcon instance each time (controls cannot be shared across the visual tree).
    return new PathIcon
    {
        Data = _checkmarkGeometry,
        Width = 14,
        Height = 12,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        IsHitTestVisible = false        // Prevents icon from intercepting input events
    };
}

/// <summary>
/// Binds a command to the specified menu item using the provided command name and sets the command parameter.
/// </summary>
/// <remarks>This method sets up data binding for the menu item's command, enabling command-based handling
/// of menu actions in MVVM scenarios. Ensure that the command name matches a property in the menu item's data
/// context that implements the ICommand interface.</remarks>
/// <param name="menuItem">The menu item to which the command will be bound. Cannot be null.</param>
/// <param name="commandName">The name of the command property to bind to the menu item. Must correspond to a valid command property in the
/// data context.</param>
/// <param name="parameter">An optional parameter to pass to the command when it is executed. Can be null if no parameter is required.</param>
private static void BindMenuItemCommand(MenuItem menuItem, string commandName, object? parameter)
{
    // Explicit OneWay binding avoids unintended updates if DataContext changes (pitfall: unintended TwoWay)
    menuItem.Bind(MenuItem.CommandProperty, new Binding(commandName) { Mode = BindingMode.OneWay });
    menuItem.CommandParameter = parameter;
}

#endregion Theme methods

#region Window overrides

/// <summary>
/// Raises the Opened event and initiates any additional asynchronous operations when the window is opened.
/// This is where we can kick off long-running async initialization (e.g., WebView setup), command state updates, focus changes.
/// </summary>
/// <remarks>Overrides the base window's OnOpened method to perform custom logic when the window is
/// opened. Asynchronous operations started by this method are not awaited and exceptions are logged
/// internally.</remarks>
/// <param name="e">An <see cref="EventArgs"/> instance that contains the event data.</param>
protected override void OnOpened(EventArgs e)
{
    base.OnOpened(e);

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
/// Handles the window closing event, performing cleanup and persisting state before the window is closed.
/// </summary>
/// <remarks>If the closing operation is cancelled by another handler or the system, no cleanup or state
/// persistence occurs. Cleanup actions and state persistence are only performed when the window is actually
/// closing. Exceptions during cleanup are logged and rethrown to ensure higher-level handlers are
/// notified.</remarks>
/// <param name="e">The event data for the window closing operation. If <paramref name="e"/>.Cancel is <see langword="true"/>, the
/// closing process is aborted and cleanup is not performed.</param>
[SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "WindowClosingEventArgs is provided by the framework")]
protected override void OnClosing(WindowClosingEventArgs e)
{
    // Since the DiagramText is saved when the VM is persisted, there is no need to prompt the user here

    // Reset approval flag if it was set
    if (_isClosingApproved)
    {
        _isClosingApproved = false;
    }

    // Check if close was cancelled by another handler or the system
    if (e.Cancel)
    {
        base.OnClosing(e);
        return; // Don't clean up - window is not actually closing
    }

    try
    {
        // Save state
        _vm.Persist();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during window closing cleanup");

        // I don't want silent failures here - rethrow to let higher-level handlers know
        throw;
    }

    // Cleanup to prevent resource leaks just in case OnUnloaded was not called
    PerformUnloadCleanup();

    ILogger<MainWindow> logger = _logger;
    OnClosingAsync()
        .SafeFireAndForget(onException: ex => logger.LogError(ex, "Failed during window close cleanup"));

    // Call base.OnClosing(e) last to ensure this class's cleanup logic runs before the base class's logic
    base.OnClosing(e);
}

/// <summary>
/// Handles the Loaded event for the control, applying the saved editor theme and initializing theme selection menus.
/// This is where we perform work that requires the visual tree (e.g., applying editor theme, populating menus, accessing Editor parts).
/// </summary>
/// <remarks>This method is called when the control is loaded into the visual tree. It ensures that the
/// editor theme is applied and that theme selection menus are populated if their controls are available. If a theme
/// menu control is not found, a warning is logged.</remarks>
/// <param name="e">The event data associated with the Loaded event.</param>
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);

    // Subscribe to theme variant changes using the event (compiles in this Avalonia version)
    ActualThemeVariantChanged += OnThemeVariantChanged;
    IDisposable dThemeVariant = Disposable.Create(UnsubscribeThemeVariantChanged);
    AddOrDisposeSubscription(dThemeVariant);

    // Apply saved editor theme (ThemeService.Initialize() loaded it but couldn't apply without editor)
    _themeService.ApplyEditorTheme(Editor, _themeService.CurrentEditorTheme);
    _logger.LogInformation("Applied saved editor theme: {EditorTheme}", _themeService.CurrentEditorTheme);

    // Initialize theme menus
    if (ApplicationThemeMenu is not null)
    {
        PopulateApplicationThemeMenu(ApplicationThemeMenu);
    }
    else
    {
        _logger.LogWarning("ApplicationThemeMenu control not found");
    }

    if (EditorThemeMenu is not null)
    {
        PopulateEditorThemeMenu(EditorThemeMenu);
    }
    else
    {
        _logger.LogWarning("EditorThemeMenu control not found");
    }
}

/// <summary>
/// Handles the Unloaded event by performing cleanup of theme menu items and related resources.
/// This is where we unsubscribe from event handlers and dispose to avoid leaks.
/// </summary>
/// <remarks>This method clears application and editor theme menu item collections when the control is
/// unloaded. Command bindings are automatically cleaned up by Avalonia. Override this method to implement
/// additional cleanup logic if necessary.</remarks>
/// <param name="e">The event data associated with the Unloaded event.</param>
protected override void OnUnloaded(RoutedEventArgs e)
{
    base.OnUnloaded(e);

    // Ensure cleanup runs on the UI thread to avoid races with subscription additions.
    // If we're not on the UI thread, post the cleanup and return immediately.
    if (!Dispatcher.UIThread.CheckAccess())
    {
        Dispatcher.UIThread.Post(PerformUnloadCleanup, DispatcherPriority.Normal);
        return;
    }

    PerformUnloadCleanup();
}

#endregion Window overrides

#region Named handlers (method groups) for editor-side events

/// <summary>
/// Handles the event that occurs when the text in the editor control changes.
/// </summary>
/// <remarks>This handler synchronizes the editor's text with the underlying view model, using debouncing
/// to minimize unnecessary updates. If text change suppression is active, the event is ignored.</remarks>
/// <param name="sender">The source of the event, typically the editor control whose text was modified.</param>
/// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
private void OnEditorTextChanged(object? sender, EventArgs e)
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
}

/// <summary>
/// Handles the event that occurs when the editor's selection changes.
/// </summary>
/// <param name="sender">The source of the event, typically the editor control whose selection has changed.</param>
/// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
private void OnEditorSelectionChanged(object? sender, EventArgs e)
{
    if (_suppressEditorStateSync)
    {
        return;
    }

    ScheduleEditorStateSyncIfNeeded();
}

/// <summary>
/// Handles the event that occurs when the caret position in the editor changes.
/// </summary>
/// <param name="sender">The source of the event, typically the editor control whose caret position has changed.</param>
/// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
{
    if (_suppressEditorStateSync)
    {
        return;
    }

    ScheduleEditorStateSyncIfNeeded();
}

    #endregion Named handlers (method groups) for editor-side events
}
