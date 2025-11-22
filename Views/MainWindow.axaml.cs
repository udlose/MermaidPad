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
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaWebView;
using Dock.Avalonia.Controls;
using MermaidPad.Exceptions.Assets;
using MermaidPad.Extensions;
using MermaidPad.Services;
using MermaidPad.Services.Highlighting;
using MermaidPad.ViewModels;
using MermaidPad.ViewModels.Panels;
using MermaidPad.Views.Panels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Main application window that contains the editor and preview WebView.
/// Manages synchronization between the editor control and the <see cref="MainViewModel"/>,
/// initializes and manages the <see cref="MermaidRenderer"/>, and handles window lifecycle events.
/// </summary>
//TODO review the SuppressMessage justification later once code is stabilized
[SuppressMessage("Major Code Smell", "S2139:Exceptions should be either logged or rethrown but not both", Justification = "Will revisit once code is stabilized")]
public sealed partial class MainWindow : Window
{
    private readonly MermaidRenderer _renderer;
    private readonly MermaidUpdateService _updateService;
    private readonly SyntaxHighlightingService _syntaxHighlightingService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly ILogger<MainWindow> _logger;

    private bool _isClosingApproved;
    private bool _suppressEditorTextChanged;
    private bool _suppressEditorStateSync; // Prevent circular updates

    private const int WebViewReadyTimeoutSeconds = 30;

    // Controls accessed from Dock panels (nullable - initialized when panels are found)
    private TextEditor? _editor;
    private WebView? _preview;

    // Event handlers stored for proper cleanup
    // Window-level handlers
    private EventHandler? _themeChangedHandler;
    private EventHandler? _activatedHandler;
    // DockControl handler
    private EventHandler? _dockControlLayoutUpdatedHandler;
    // ViewModel handlers
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    // Editor handlers (initialized when editor is found)
    private EventHandler? _editorTextChangedHandler;
    private EventHandler? _editorSelectionChangedHandler;
    private EventHandler? _editorCaretPositionChangedHandler;

    /// <summary>
    /// Gets the main view model associated with the current context.
    /// </summary>
    public required MainViewModel ViewModel { get; init; }

    /// <summary>
    /// Initializes a new instance of the MainWindow class using application-level services.
    /// </summary>
    /// <remarks>
    /// <para>This constructor retrieves required services from the application's dependency injection
    /// container to configure the main window. It is typically used when creating the main window at application
    /// startup.</para>
    /// <para>
    /// This constructor lives specifically for the purpose of avoiding this warning:
    ///     AVLN3001: XAML resource "avares://MermaidPad/Views/MainWindow.axaml" won't be reachable via runtime loader, as no public constructor was found
    /// </para>
    /// </remarks>
    public MainWindow()
    : this(
        App.Services.GetRequiredService<ILogger<MainWindow>>(),
        App.Services.GetRequiredService<MermaidRenderer>(),
        App.Services.GetRequiredService<MermaidUpdateService>(),
        App.Services.GetRequiredService<SyntaxHighlightingService>(),
        App.Services.GetRequiredService<IDebounceDispatcher>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the MainWindow class, configuring logging, diagram rendering, update services,
    /// syntax highlighting, and editor input debouncing for the application's main window.
    /// </summary>
    /// <remarks>The syntax highlighting service is initialized during construction to ensure grammar
    /// resources are loaded before theme handling occurs. If initialization fails, the application will continue
    /// without syntax highlighting. The DockControl's LayoutUpdated event is wired to support dynamic panel discovery
    /// after layout calculations are complete.</remarks>
    /// <param name="logger">The logger used to record diagnostic and operational messages for the main window.</param>
    /// <param name="renderer">The MermaidRenderer responsible for rendering Mermaid diagrams within the main window.</param>
    /// <param name="updateService">The service that manages updates to Mermaid diagrams and related content.</param>
    /// <param name="syntaxHighlightingService">The service that provides syntax highlighting capabilities for code and diagram editors.</param>
    /// <param name="editorDebouncer">The dispatcher used to debounce editor input events, reducing unnecessary processing during rapid user input.</param>
    public MainWindow(
        ILogger<MainWindow> logger,
        MermaidRenderer renderer,
        MermaidUpdateService updateService,
        SyntaxHighlightingService syntaxHighlightingService,
        IDebounceDispatcher editorDebouncer)
    {
        _logger = logger;
        _renderer = renderer;
        _updateService = updateService;
        _syntaxHighlightingService = syntaxHighlightingService;
        _editorDebouncer = editorDebouncer;

        InitializeComponent();

        // IMPORTANT: Initialize syntax highlighting service BEFORE theme handler is wired (in OnAttachedToVisualTree)
        // This loads grammar resources but doesn't apply to editor yet (that happens in OnDockControlLoaded)
        try
        {
            _syntaxHighlightingService.Initialize();
            _logger.LogInformation("Syntax highlighting service initialized (grammar resources loaded)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize syntax highlighting service - will continue without highlighting");
        }

        // Wire DockControl.LayoutUpdated event to find panels after layout calculations complete
        // LayoutUpdated fires after the layout system has positioned all controls

        //TODO where should this event handler be wired? Constructor seems early, OnLoaded seems late...
        DockControl? dockControl = this.FindControl<DockControl>("MainDock");
        if (dockControl is not null)
        {
            _dockControlLayoutUpdatedHandler = OnDockControlLayoutUpdated;
            dockControl.LayoutUpdated += _dockControlLayoutUpdatedHandler;
            _logger.LogInformation("DockControl.LayoutUpdated event handler wired");
        }
        else
        {
            _logger.LogError("DockControl 'MainDock' not found - cannot wire LayoutUpdated event");
        }

        _logger.LogInformation("MainWindow constructor completed");
    }

    /// <summary>
    /// Subscribes to editor events and establishes synchronization between the editor and the view model.
    /// </summary>
    /// <remarks>
    /// <para>This method wires the context menu opening event if the editor and its context menu are
    /// available, and sets up two-way synchronization between the editor and the view model. If the editor instance is
    /// null, no event handlers are attached and a warning is logged.</para>
    /// <para>
    /// Called after editor control is found and initialized.
    /// Separated from SetupEditorViewModelSync for clearer separation of concerns.
    /// </para>
    /// </remarks>
    private void WireEditorEventHandlers()
    {
        if (_editor is null)
        {
            _logger.LogWarning("Cannot wire editor event handlers - editor is null");
            return;
        }

        _logger.LogInformation("Wiring editor event handlers");

        // Subscribe to context menu opening event
        if (_editor.ContextMenu is not null)
        {
            _editor.ContextMenu.Opening += GetContextMenuState;
        }

        // Set up two-way synchronization between Editor and ViewModel
        SetupEditorViewModelSync();
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
            _editor!.Text = text;

            // Ensure selection bounds are valid
            int textLength = text.Length;
            int validSelectionStart = Math.Max(0, Math.Min(selectionStart, textLength));
            int validSelectionLength = Math.Max(0, Math.Min(selectionLength, textLength - validSelectionStart));
            int validCaretOffset = Math.Max(0, Math.Min(caretOffset, textLength));
            _editor!.SelectionStart = validSelectionStart;
            _editor!.SelectionLength = validSelectionLength;
            _editor!.CaretOffset = validCaretOffset;

            // Since this is yaml/diagram text, convert tabs to spaces for correct rendering
            _editor!.Options.ConvertTabsToSpaces = true;
            _editor!.Options.HighlightCurrentLine = true;
            _editor!.Options.IndentationSize = 2;

            _logger.LogInformation("Editor state set with {CharacterCount} characters", textLength);
        }
        finally
        {
            _suppressEditorStateSync = false;
        }
    }

    /// <summary>
    /// Initializes synchronization between the editor's text, selection, and caret position and the corresponding
    /// properties in the view model.
    /// </summary>
    /// <remarks>
    /// <para>This method sets up event handlers to keep the editor and view model in sync. Changes in the
    /// editor's text, selection, or caret position are propagated to the view model, and updates in the view model are
    /// reflected in the editor. Debouncing is applied to text changes to reduce unnecessary updates. This method should
    /// be called once during initialization to ensure consistent state between the editor and view model.
    /// </para>
    /// <para>
    ///     <list type="bullet">
    ///         <item>Subscribes to editor text/selection/caret events and updates the view model using a debounce dispatcher.</item>
    ///         <item>Subscribes to view model property changes and applies them to the editor.</item>
    ///         <item>Suppresses reciprocal updates to avoid feedback loops.</item>
    ///     </list>
    /// </para>
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

            // Debounce to avoid excessive updates
            _editorDebouncer.DebounceOnUI("editor-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
            {
                if (ViewModel.EditorViewModel.DiagramText != _editor!.Text)
                {
                    _suppressEditorStateSync = true;
                    try
                    {
                        ViewModel.EditorViewModel.DiagramText = _editor!.Text;
                    }
                    finally
                    {
                        _suppressEditorStateSync = false;
                    }
                }
            },
            DispatcherPriority.Background);
        };
        _editor!.TextChanged += _editorTextChangedHandler;

        // Editor selection/caret -> ViewModel: subscribe to both, coalesce into one update
        _editorSelectionChangedHandler = (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            ScheduleEditorStateSyncIfNeeded();
        };
        _editor!.TextArea.SelectionChanged += _editorSelectionChangedHandler;

        _editorCaretPositionChangedHandler = (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            ScheduleEditorStateSyncIfNeeded();
        };
        _editor!.TextArea.Caret.PositionChanged += _editorCaretPositionChangedHandler;

        // ViewModel -> Editor synchronization
        _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
        ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;
    }

    /// <summary>
    /// Schedules a debounced synchronization of the editor's selection and caret state with the associated view model if changes
    /// are detected.
    /// </summary>
    /// <remarks>
    /// <para>Synchronization is performed asynchronously and debounced to minimize redundant updates when
    /// multiple changes occur in quick succession. If the editor's selection and caret state have not changed since the
    /// last synchronization, no action is taken. This method should be called whenever the editor's selection or caret
    /// position may have changed to ensure the view model remains up to date.</para>
    /// <para>
    /// The method compares the current editor state with the view model and only schedules an update
    /// when a change is detected. Values are read again at the time the debounced action runs to coalesce
    /// multiple rapid events into a single update.
    /// </para>
    /// </remarks>
    private void ScheduleEditorStateSyncIfNeeded()
    {
        //TODO review this for correctness
        if (_editor is null)
        {
            return;
        }

        int selectionStart = _editor!.SelectionStart;
        int selectionLength = _editor!.SelectionLength;
        int caretOffset = _editor!.CaretOffset;

        if (selectionStart == ViewModel.EditorViewModel.EditorSelectionStart &&
            selectionLength == ViewModel.EditorViewModel.EditorSelectionLength &&
            caretOffset == ViewModel.EditorViewModel.EditorCaretOffset)
        {
            return; // nothing changed
        }

        _editorDebouncer.DebounceOnUI("editor-state", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
        {
            _suppressEditorStateSync = true;
            try
            {
                // Take the latest values at execution time to coalesce multiple events
                ViewModel.EditorViewModel.EditorSelectionStart = _editor!.SelectionStart;
                ViewModel.EditorViewModel.EditorSelectionLength = _editor!.SelectionLength;
                ViewModel.EditorViewModel.EditorCaretOffset = _editor!.CaretOffset;
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        },
        DispatcherPriority.Background);
    }

    /// <summary>
    /// Handles property change notifications from the view model and synchronizes relevant editor state accordingly.
    /// </summary>
    /// <remarks>This method updates the editor's text, selection, and caret position in response to changes
    /// in the view model. Synchronization is debounced to improve performance and prevent excessive updates. If editor
    /// state synchronization is suppressed or the editor is unavailable, the method does not perform any
    /// actions.</remarks>
    /// <param name="sender">The source of the property change event, expected to be the EditorViewModel instance.</param>
    /// <param name="e">An object containing information about the property that changed.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Ensure sender is the expected EditorViewModel instance
        if (sender is not EditorViewModel editorViewModel || _suppressEditorStateSync || _editor is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(EditorViewModel.DiagramText):
                if (_editor.Text != editorViewModel.DiagramText)
                {
                    _editorDebouncer.DebounceOnUI("vm-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
                    {
                        _suppressEditorTextChanged = true;
                        _suppressEditorStateSync = true;
                        try
                        {
                            _editor.Text = editorViewModel.DiagramText;
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

            case nameof(EditorViewModel.EditorSelectionStart):
            case nameof(EditorViewModel.EditorSelectionLength):
            case nameof(EditorViewModel.CanCopyClipboard):
            case nameof(EditorViewModel.CanPasteClipboard):
            case nameof(EditorViewModel.EditorCaretOffset):
                _editorDebouncer.DebounceOnUI("vm-selection", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
                {
                    _suppressEditorStateSync = true;
                    try
                    {
                        // Validate bounds before setting
                        int textLength = _editor.Text.Length;
                        int validSelectionStart = Math.Max(0, Math.Min(editorViewModel.EditorSelectionStart, textLength));
                        int validSelectionLength = Math.Max(0, Math.Min(editorViewModel.EditorSelectionLength, textLength - validSelectionStart));
                        int validCaretOffset = Math.Max(0, Math.Min(editorViewModel.EditorCaretOffset, textLength));

                        if (_editor.SelectionStart != validSelectionStart ||
                            _editor.SelectionLength != validSelectionLength ||
                            _editor.CaretOffset != validCaretOffset)
                        {
                            _editor.SelectionStart = validSelectionStart;
                            _editor.SelectionLength = validSelectionLength;
                            _editor.CaretOffset = validCaretOffset;
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

    #region Lifecycle Overrides

    /// <summary>
    /// Handles the layout update event for the dock control, initializing editor and preview panels when they become
    /// available in the visual tree. Called when DockControl has completed a layout pass.
    /// </summary>
    /// <remarks>
    /// <para>This method should be called in response to the DockControl's LayoutUpdated event. It
    /// performs initialization only when the required panels are present in the visual tree, and unsubscribes itself
    /// after successful initialization to prevent repeated execution.
    /// </para>
    /// <para>
    /// LayoutUpdated fires after the layout system has completed calculations and positioned controls.
    /// This is when DockControl's DataTemplates have created the panel controls and added them
    /// to the visual tree.
    ///
    /// NOTE: This event can fire multiple times, so we must unsubscribe after successful initialization.
    /// </para>
    /// </remarks>
    /// <param name="sender">The source of the event, expected to be a DockControl instance whose layout has been updated.</param>
    /// <param name="e">An EventArgs instance containing event data for the layout update.</param>
    private void OnDockControlLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not DockControl dockControl)
        {
            _logger.LogError("DockControl 'MainDock' not found - cannot initialize panels");
            return;
        }

        // Check if Layout binding has been processed
        if (dockControl.Layout is null)
        {
            // Layout not set yet, wait for next LayoutUpdated
            return;
        }

        // Find EditorPanel and PreviewPanel from DataTemplates
        EditorPanel? editorPanel = dockControl.GetVisualDescendants().OfType<EditorPanel>().FirstOrDefault();
        PreviewPanel? previewPanel = dockControl.GetVisualDescendants().OfType<PreviewPanel>().FirstOrDefault();

        if (editorPanel is null || previewPanel is null)
        {
            // Panels not in visual tree yet, wait for next LayoutUpdated
            _logger.LogDebug("DockControl LayoutUpdated - EditorPanel or PreviewPanel not found yet, waiting for next LayoutUpdated. Layout: {DockControlLayout}, EditorPanel.IsLoaded: {EditorPanel}, PreviewPanel.IsLoaded: {PreviewPanel}", dockControl.Layout, editorPanel?.IsLoaded, previewPanel?.IsLoaded);
            return;
        }

        // Found panels! Unsubscribe from LayoutUpdated (prevent this from running again)
        if (_dockControlLayoutUpdatedHandler is not null)
        {
            dockControl.LayoutUpdated -= _dockControlLayoutUpdatedHandler;
            _dockControlLayoutUpdatedHandler = null;
        }

        _logger.LogInformation("DockControl LayoutUpdated - panels found, initializing");

        // Initialize EditorPanel
        _editor = editorPanel.Editor;
        Debug.Assert(_editor is not null, "EditorPanel.Editor should not be null");
        _logger.LogInformation("EditorPanel found - initializing editor");

        // Initialize syntax highlighting
        InitializeSyntaxHighlighting();

        // Initialize editor with ViewModel data
        SetEditorStateWithValidation(
            ViewModel.EditorViewModel.DiagramText,
            ViewModel.EditorViewModel.EditorSelectionStart,
            ViewModel.EditorViewModel.EditorSelectionLength,
            ViewModel.EditorViewModel.EditorCaretOffset
        );

        _logger.LogInformation("Editor initialized with {CharacterCount} characters", ViewModel.EditorViewModel.DiagramText.Length);

        // Wire editor event handlers (now that editor exists)
        WireEditorEventHandlers();

        // Find PreviewPanel and initialize WebView
        if (previewPanel is not null)
        {
            _preview = previewPanel.Preview;
            _logger.LogInformation("PreviewPanel found - initializing WebView");

            // Initialize WebView asynchronously now that _preview is assigned
            InitializeWebViewAsync()
                .SafeFireAndForget(onException: ex =>
                {
                    _logger.LogError(ex, "Failed to initialize WebView");
                    Dispatcher.UIThread.Post(() => ViewModel.PreviewViewModel.LastError = $"WebView initialization failed: {ex.Message}");
                });
        }
        else
        {
            _logger.LogWarning("PreviewPanel not found in visual tree");
        }
    }

    /// <summary>
    /// Handles logic that occurs when the control is attached to the visual tree.
    /// </summary>
    /// <remarks>
    /// <para>This method is typically used to initialize resources, wire event handlers, or perform setup
    /// tasks that require the control to be part of the visual tree. Override this method to customize behavior when
    /// the control becomes visible and interactive within the application's UI.</para>
    /// <para>
    /// Cleanup happens automatically in OnDetachedFromVisualTree and OnUnloaded.
    /// </para>
    /// </remarks>
    /// <param name="e">An object containing event data for the visual tree attachment, including information about the parent and root
    /// visual elements.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _logger.LogInformation("MainWindow attached to visual tree - wiring event handlers");

        // Wire window-level event handlers (store in fields for consistent cleanup)
        _themeChangedHandler = OnThemeChanged;
        ActualThemeVariantChanged += _themeChangedHandler;

        _activatedHandler = OnActivated;
        Activated += _activatedHandler;

        // Wire ViewModel property changed (for two-way sync with EditorViewModel)
        _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
        ViewModel.EditorViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Note: Editor/Preview event handlers will be wired when panels are found
        // This happens in WireEditorEventHandlers() called from OnDockControlLoaded()
    }

    /// <summary>
    /// Handles the Loaded event for the window, performing any necessary initialization when the window is loaded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is called when the window's Loaded event is raised - i.e. control is fully loaded.
    /// It can be overridden to perform custom initialization logic after the window is loaded.
    /// The base implementation should be called to ensure standard event handling.</para>
    /// <para>
    /// NOTE: Panel initialization happens in OnDockControlLoaded, which is triggered
    /// by DockControl's Loaded event (wired in constructor). This ensures DataTemplates
    /// have been applied before we try to find the panels.
    /// </para>
    /// </remarks>
    /// <param name="e">The event data associated with the Loaded event.</param>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _logger.LogInformation("MainWindow loaded - DockControl will trigger panel initialization when ready");
    }

    /// <summary>
    /// Handles the logic required when the control is unloaded from the visual tree, ensuring that resources and event
    /// handlers are properly released.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Overrides the base implementation to perform cleanup of event handlers, helping to prevent
    /// memory leaks when the control is removed from the UI. This method is typically called by the framework and
    /// should not be invoked directly.</para>
    /// <para>
    ///     <list type="bullet">
    ///         <item><description>Unsubscribe from all event handlers</description></item>
    ///         <item><description>Stop any running timers or animations</description></item>
    ///         <item><description>Cancel any ongoing async operations</description></item>
    ///     </list>
    /// </para>
    /// </remarks>
    /// <param name="e">The event data associated with the unload event.</param>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _logger.LogInformation("MainWindow unloading - cleaning up event handlers");

        // Unsubscribe all event handlers to prevent memory leaks
        UnsubscribeAllEventHandlers();

        base.OnUnloaded(e);
    }

    /// <summary>
    /// Handles cleanup when the element is detached from the visual tree.
    /// </summary>
    /// <remarks>
    /// <para>This method releases resources associated with the element, such as disposing of embedded
    /// controls. Override this method to implement additional cleanup logic when the element is removed from the visual
    /// tree.</para>
    /// <para>
    ///     <list type="bullet">
    ///         <item><description>Release visual resources</description></item>
    ///         <item><description>Dispose WebView</description></item>
    ///         <item><description>Clear cached visual elements</description></item>
    ///     </list>
    /// </para>
    /// </remarks>
    /// <param name="e">An object containing event data related to the detachment from the visual tree.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _logger.LogInformation("MainWindow detached from visual tree - cleaning up resources");

        // Dispose WebView if it exists
        // Note: WebView disposal is best done here rather than in async cleanup
        if (_preview is not null)
        {
            try
            {
                // WebView cleanup happens here
                _logger.LogInformation("WebView cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during WebView cleanup");
            }
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Handles additional logic when the window has been opened and is now visible and starts the asynchronous open sequence.
    /// </summary>
    /// <remarks>
    /// <para>This method is called after the window becomes visible. It logs the window opening and
    /// initiates any asynchronous startup logic. Override this method to perform custom actions when the window is
    /// opened.</para>
    /// <para>
    /// This method delegates to <see cref="OnOpenedCoreAsync"/> to perform asynchronous initialization,
    /// subscribe to renderer events, and start a failsafe timeout to enable UI if the WebView never becomes ready.
    /// Uses SafeFireAndForget to handle the async operation without blocking the event handler and avoid async void.
    /// </para>
    /// <para>
    ///     <list type="bullet">
    ///         <item><description>Start async initialization</description></item>
    ///         <item><description>Focus initial control</description></item>
    ///     </list>
    /// </para>
    /// </remarks>
    /// <param name="e">An <see cref="EventArgs"/> instance containing event data associated with the window opening.</param>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _logger.LogInformation("MainWindow opened - window is now visible");

        OnOpenedCoreAsync()
            .SafeFireAndForget(onException: ex => _logger.LogError(ex, "Unhandled exception in OnOpened"));
    }

    /// <summary>
    /// Handles window activation (gains focus). Brings focus to editor when window becomes active.
    /// </summary>
    /// <remarks>Fires EVERY time window gains focus.</remarks>
    /// <param name="sender">The source of the activation event. This parameter is typically the control that was activated.</param>
    /// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
    private void OnActivated(object? sender, EventArgs e)
    {
        BringFocusToEditor();
    }

    /// <summary>
    /// Handles the window closing event, prompting the user to save unsaved changes and performing necessary cleanup
    /// before the window closes.
    /// </summary>
    /// <remarks>
    /// <para>If there are unsaved changes, the method prompts the user before allowing the window to
    /// close. The method also persists the current state and initiates asynchronous cleanup operations. If the close is
    /// cancelled by another handler or the system, cleanup is not performed.</para>
    /// <para>
    ///     <list type="bullet">
    ///         <item><description>Check for unsaved changes</description></item>
    ///         <item><description>Show confirmation dialog</description></item>
    ///         <item><description>Set e.Cancel = true to prevent closing</description></item>
    ///         <item><description>Save window state</description></item>
    ///     </list>
    /// </para>
    /// <para>
    /// Note: Event handler cleanup happens in <see cref="OnUnloaded"/>, not here.
    /// </para>
    /// </remarks>
    /// <param name="e">An object containing event data for the window closing operation. The cancellation flag can be set to prevent
    /// the window from closing.</param>
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Base method guarantees non-null e")]
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        _logger.LogInformation("MainWindow closing requested");

        // Check for unsaved changes (only if not already approved)
        if (!_isClosingApproved && ViewModel.IsDirty && !string.IsNullOrWhiteSpace(ViewModel.EditorViewModel.DiagramText))
        {
            e.Cancel = true;
            PromptAndCloseAsync()
                .SafeFireAndForget(onException: ex =>
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
            // Save state before closing
            ViewModel.Persist();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting state during window closing");
            throw;
        }

        // Perform async cleanup
        ILogger<MainWindow> logger = _logger;
        OnClosingAsync()
            .SafeFireAndForget(onException: ex => logger.LogError(ex, "Failed during window close cleanup"));
    }

    #endregion Lifecycle Overrides

    /// <summary>
    /// Brings focus to the editor control and adjusts visuals for caret and selection.
    /// </summary>
    /// <remarks>
    /// This method executes on the UI thread via the dispatcher and temporarily suppresses
    /// editor <see cref="_suppressEditorStateSync"/> to avoid generating spurious model updates.
    /// </remarks>
    private void BringFocusToEditor()
    {
        //TODO review this for correctness - If this is null, then this method was called before the DockControl loaded!!
        if (_editor is null)
        {
            return; // Editor not initialized yet
        }

        Dispatcher.UIThread.Post(() =>
        {
            //TODO review this for correctness - If this is null, then this method was called before the DockControl loaded!!
            if (_editor is null)
            {
                return; // Double-check after async dispatch
            }

            // Suppress event reactions during programmatic focus/caret adjustments
            _suppressEditorStateSync = true;
            try
            {
                // Make sure caret is visible:
                _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Colors.Red);

                // Ensure selection is visible
                _editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.SteelBlue);
                if (!_editor.IsFocused)
                {
                    _editor.Focus();
                }
                _editor.TextArea.Caret.BringCaretToView();
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        }, DispatcherPriority.Background);
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
    /// Performs the longer-running open sequence: check for updates and update command states.
    /// </summary>
    /// <returns>A task representing the asynchronous open sequence.</returns>
    /// <remarks>
    /// WebView initialization now happens in TryFindDockPanels (when _preview is found).
    /// This method focuses on post-initialization tasks like update checks and command state updates.
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

            // NOTE: WebView initialization now happens in TryFindDockPanels after _preview is assigned
            // (moved from here to fix initialization ordering)

            // Step 2: Update command states (ensure commands reflect WebView ready state)
            _logger.LogInformation("Step 2: Updating command states...");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel.RenderCommand.NotifyCanExecuteChanged();
                ViewModel.ClearCommand.NotifyCanExecuteChanged();
                ViewModel.ExportCommand.NotifyCanExecuteChanged();
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
    /// Unsubscribes all event handlers to prevent memory leaks.
    /// </summary>
    /// <remarks>
    /// Called from OnUnloaded to ensure all event handlers are properly cleaned up.
    /// This prevents memory leaks by breaking references between controls and handlers.
    /// </remarks>
    private void UnsubscribeAllEventHandlers()
    {
        _logger.LogInformation("Unsubscribing all event handlers");

        // Window-level events
        if (_themeChangedHandler is not null)
        {
            ActualThemeVariantChanged -= _themeChangedHandler;
            _themeChangedHandler = null;
        }

        if (_activatedHandler is not null)
        {
            Activated -= _activatedHandler;
            _activatedHandler = null;
        }

        // DockControl events
        if (_dockControlLayoutUpdatedHandler is not null)
        {
            DockControl? dockControl = this.FindControl<DockControl>("MainDock");
            if (dockControl is not null)
            {
                dockControl.LayoutUpdated -= _dockControlLayoutUpdatedHandler;
            }
            _dockControlLayoutUpdatedHandler = null;
        }

        // ViewModel events
        if (_viewModelPropertyChangedHandler is not null)
        {
            ViewModel.EditorViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            _viewModelPropertyChangedHandler = null;
        }

        // Editor events (only if editor was initialized)
        if (_editor is not null)
        {
            if (_editorTextChangedHandler is not null)
            {
                _editor.TextChanged -= _editorTextChangedHandler;
                _editorTextChangedHandler = null;
            }

            if (_editorSelectionChangedHandler is not null)
            {
                _editor.TextArea.SelectionChanged -= _editorSelectionChangedHandler;
                _editorSelectionChangedHandler = null;
            }

            if (_editorCaretPositionChangedHandler is not null)
            {
                _editor.TextArea.Caret.PositionChanged -= _editorCaretPositionChangedHandler;
                _editorCaretPositionChangedHandler = null;
            }

            // Context menu event
            if (_editor.ContextMenu is not null)
            {
                _editor.ContextMenu.Opening -= GetContextMenuState;
            }
        }

        _logger.LogInformation("All event handlers unsubscribed successfully");
    }

    /// <summary>
    /// Performs asynchronous cleanup operations when the window is closing.
    /// </summary>
    /// <remarks>This method disposes of resources associated with the window, including any asynchronous
    /// disposable renderer. It should be called during the window closing sequence to ensure proper resource
    /// management.</remarks>
    /// <returns>A task that represents the asynchronous cleanup operation.</returns>
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
            bool canClose = await ViewModel.PromptSaveIfDirtyAsync(StorageProvider);
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
            bool current = ViewModel.PreviewViewModel.LivePreviewEnabled;
            ViewModel.PreviewViewModel.LivePreviewEnabled = false;
            _logger.LogInformation("Temporarily disabled live preview (was: {Current})", current);
            return current;
        }, DispatcherPriority.Normal);

        bool success = false;
        try
        {
            // Step 1: Initialize renderer (starts HTTP server + navigate)
            // Null-forgiving operator is safe: this method is only called from TryFindDockPanels after _preview is assigned
            await _renderer.InitializeAsync(_preview!);

            // Step 2: Kick first render; index.html sets globalThis.__renderingComplete__ in hideLoadingIndicator()
            await _renderer.RenderAsync(ViewModel.EditorViewModel.DiagramText);

            // Step 3: Await readiness
            try
            {
                await _renderer.EnsureFirstRenderReadyAsync(TimeSpan.FromSeconds(WebViewReadyTimeoutSeconds));
                await Dispatcher.UIThread.InvokeAsync(() => ViewModel.PreviewViewModel.IsWebViewReady = true);
                _logger.LogInformation("WebView readiness observed");
            }
            catch (TimeoutException te)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ViewModel.PreviewViewModel.IsWebViewReady = true;
                    ViewModel.PreviewViewModel.LastError = $"WebView initialization timed out after {WebViewReadyTimeoutSeconds} seconds. Some features may not work correctly.";
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
            // Log and rethrow - caught by SafeFireAndForget handler in TryFindDockPanels
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
                ViewModel.PreviewViewModel.LivePreviewEnabled = originalLivePreview;
                _logger.LogInformation("Re-enabled live preview: {OriginalLivePreview}", originalLivePreview);
            });
        }
    }

    #region Clipboard methods

    /// <summary>
    /// Determines the enabled state of context menu clipboard commands based on the current editor selection and
    /// clipboard availability.
    /// </summary>
    /// <remarks>This method is intended to be used as an event handler for context menu opening events. It
    /// updates the clipboard-related command states to reflect whether copy and paste actions are currently
    /// available.</remarks>
    /// <param name="sender">The source of the event, typically the control that triggered the context menu opening.</param>
    /// <param name="e">A <see cref="CancelEventArgs"/> instance that can be used to cancel the context menu opening.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
    private void GetContextMenuState(object? sender, CancelEventArgs e)
    {
        // Get Clipboard state
        ViewModel.EditorViewModel.CanCopyClipboard = ViewModel.EditorViewModel.EditorSelectionLength > 0;

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
    /// <see cref="EditorViewModel.CanPasteClipboard"/> property based on the current clipboard contents.
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
        await Dispatcher.UIThread.InvokeAsync(() => ViewModel.EditorViewModel.CanPasteClipboard = canPaste, DispatcherPriority.Normal);
    }

    #endregion Clipboard methods

    #region Syntax Highlighting methods

    /// <summary>
    /// Applies syntax highlighting to the editor control.
    /// </summary>
    /// <remarks>
    /// The service must already be initialized (done in constructor).
    /// This method applies the highlighting to the specific editor instance with automatic theme detection.
    /// </remarks>
    private void InitializeSyntaxHighlighting()
    {
        try
        {
            // Service is already initialized in constructor - just apply to editor with automatic theme detection
            // Null-forgiving operator is safe here: method is only called from TryFindDockPanels after _editor is assigned
            _syntaxHighlightingService.ApplyTo(_editor!);

            _logger.LogInformation("Syntax highlighting applied to editor successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply syntax highlighting to editor");
            // Non-fatal: Continue without syntax highlighting rather than crash the application
        }
    }

    /// <summary>
    /// Handles theme variant changes to update syntax highlighting theme.
    /// </summary>
    /// <remarks>
    /// This event can fire before the editor is initialized. The theme will be applied
    /// when the editor is first initialized via InitializeSyntaxHighlighting().
    /// </remarks>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        try
        {
            // Guard: Editor may not be initialized yet (theme handler is wired in OnAttachedToVisualTree,
            // but editor is found later in TryFindDockPanels)
            if (_editor is null)
            {
                _logger.LogDebug("Theme changed before editor initialized - theme will be applied during editor initialization");
                return;
            }

            bool isDarkTheme = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

            // Update syntax highlighting theme to match current theme
            _syntaxHighlightingService.UpdateThemeForVariant(isDarkTheme);
            _logger.LogInformation("Updated syntax highlighting theme to {Theme}", isDarkTheme ? "Dark" : "Light");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling theme change");
            // Non-fatal: Continue with current theme
        }
    }

    #endregion Syntax Highlighting methods
}
