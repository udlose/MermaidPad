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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Controls;
using MermaidPad.Extensions;
using MermaidPad.Factories;
using MermaidPad.Infrastructure;
using MermaidPad.Infrastructure.Messages;
using MermaidPad.Models;
using MermaidPad.Models.Editor;
using MermaidPad.Services;
using MermaidPad.Services.Export;
using MermaidPad.ViewModels.Dialogs;
using MermaidPad.ViewModels.Docking;
using MermaidPad.ViewModels.UserControls;
using MermaidPad.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MermaidPad.ViewModels;

/// <summary>
/// Represents the main view model for the application's main window, providing properties, commands, and logic for
/// editing, rendering, exporting, and managing Mermaid diagrams.
/// </summary>
/// <remarks>This view model exposes state and command properties for data binding in the main window, including
/// file operations, diagram rendering, clipboard actions, and export functionality. It coordinates interactions between
/// the user interface and underlying services such as file management, rendering, and settings persistence. All
/// properties and commands are designed for use with MVVM frameworks and are intended to be accessed by the view for UI
/// updates and user interactions.</remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
internal sealed partial class MainWindowViewModel :
    ViewModelBase,
    IRecipient<EditorTextChangedMessage>,
    IRecipient<DiagramViewReadyMessage>,
    IRecipient<FileDroppedMessage>,
    IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly ExportService _exportService;
    private readonly IDialogFactory _dialogFactory;
    private readonly IFileService _fileService;
    private readonly DockLayoutService _dockLayoutService;
    private readonly IMessenger _documentMessenger;
    private readonly ILogger<MainWindowViewModel> _logger;

    private bool _isDisposed;
    private const string DebounceRenderKey = "render";

    /// <summary>
    /// A value indicating whether to suppress tracking of unsaved changes.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the <see cref="Receive(EditorTextChangedMessage)"/> handler will not set
    /// <see cref="HasUnsavedChanges"/> to <c>true</c>. This prevents the modified file flag from being set during
    /// file load operations, new file creation, and file close operations where the editor content
    /// changes but those changes should not be considered "unsaved changes".
    /// </remarks>
    private bool _suppressHasUnsavedChangesTracking;

    /// <summary>
    /// Tracks if we've already warned the user about WebView being unready during live preview.
    /// Prevents log/error spam on every keystroke.
    /// </summary>
    private bool _hasWarnedAboutUnreadyWebView;

    /// <summary>
    /// Tracks the diagram initialization state using a three-state flag for thread-safe coordination.
    /// Uses <c>int</c> to enable atomic operations via <see cref="Interlocked"/> methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// State values:
    /// <list type="bullet">
    ///     <item><description><c>0</c> (Idle): No initialization needed or in progress</description></item>
    ///     <item><description><c>1</c> (Pending): Waiting for <see cref="DiagramViewReadyMessage"/> to trigger initialization</description></item>
    ///     <item><description><c>2</c> (Initializing): <see cref="InitializeDiagramAsync"/> is currently running</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// State transitions:
    /// <list type="bullet">
    ///     <item><description>Idle -> Pending: <see cref="SetPendingDiagramInitialization"/> called</description></item>
    ///     <item><description>Pending -> Initializing: <see cref="Receive(DiagramViewReadyMessage)"/> wins the atomic compare-exchange</description></item>
    ///     <item><description>Initializing -> Idle: <see cref="InitializeDiagramAsync"/> completes (success or failure)</description></item>
    ///     <item><description>Pending -> Idle: Initialization triggered directly (not via message)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The three-state design prevents a race condition where <see cref="SetPendingDiagramInitialization"/>
    /// could be called during a slow initialization, causing duplicate initialization when another
    /// <see cref="DiagramViewReadyMessage"/> arrives.
    /// </para>
    /// <para>
    /// Thread-safety: this field may be read and written from multiple threads (for example, the UI
    /// thread and background workers) and is mutated exclusively via
    /// <see cref="System.Threading.Interlocked"/> operations such as
    /// <see cref="System.Threading.Interlocked.CompareExchange(ref int, int, int)"/>. These atomic
    /// operations provide the necessary synchronization without using explicit locks.
    /// </para>
    /// <para>
    /// Although the state is conceptually an enumeration of three values, the backing field is an
    /// <see langword="int"/> because <see cref="System.Threading.Interlocked"/> operates on primitive
    /// integral types, not directly on enum types. The <c>InitState*</c> constants below document the
    /// mapping between the numeric values and their logical states.
    /// </para>
    /// </remarks>
    private int _diagramInitializationState;

    /// <summary>No initialization needed or in progress.</summary>
    private const int InitStateIdle = 0;

    /// <summary>Waiting for <see cref="DiagramViewReadyMessage"/> to trigger initialization.</summary>
    private const int InitStatePending = 1;

    /// <summary><see cref="InitializeDiagramAsync"/> is currently running.</summary>
    private const int InitStateInitializing = 2;

    #region Dock Layout

    /// <summary>
    /// The dock factory used to create and manage the dock layout.
    /// </summary>
    /// <remarks>
    /// This field provides access to the underlying tool ViewModels through
    /// <see cref="DockFactory.EditorTool"/> and <see cref="DockFactory.DiagramTool"/>.
    /// </remarks>
    [SuppressMessage("Style", "IDE0032:Use auto property", Justification = "Field is readonly and initialized via constructor injection.")]
    private readonly DockFactory _dockFactory;

    /// <summary>
    /// Gets or sets the root dock layout for the application.
    /// </summary>
    /// <remarks>
    /// This property is bound to the DockControl in MainWindow.axaml.
    /// The layout is created by <see cref="DockFactory"/> and can be saved/restored
    /// via <see cref="DockLayoutService"/>.
    /// </remarks>
    [ObservableProperty]
    public partial IRootDock? Layout { get; set; }

    #endregion Dock Layout

    #region Editor ViewModel

    /// <summary>
    /// Gets the editor view model that manages text editing, clipboard, and related operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This view model is accessed through the dock layout's EditorTool.
    /// For menu command binding (e.g., Edit menu items bind to Editor.CutCommand, Editor.CopyCommand, etc.).
    /// </para>
    /// <para>
    /// <b>MDI Migration Note:</b> In an MDI design, this property would be replaced by an ActiveDocument
    /// pattern where the current document exposes its own Editor. For SDI, this direct reference through
    /// the DockFactory is acceptable and provides convenient XAML binding.
    /// </para>
    /// </remarks>
    //TODO - DaveBlack: MDI Migration - Replace with ActiveDocument pattern
    public MermaidEditorViewModel Editor => _dockFactory.EditorTool?.Editor
        ?? throw new InvalidOperationException($"{nameof(_dockFactory.EditorTool)} is not initialized. Ensure the dock layout is created first.");

    /// <summary>
    /// Gets a value indicating whether there is text in the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property delegates to <see cref="Editor"/>.HasText to maintain a single source of truth.
    /// It is kept for convenience in command CanExecute logic.
    /// </para>
    /// <para>
    /// <b>MDI Migration Note:</b> In an MDI design, this would delegate to ActiveDocument?.Editor.HasText.
    /// </para>
    /// </remarks>
    //TODO - DaveBlack: MDI Migration - Replace with ActiveDocument pattern
    public bool EditorHasText => Editor.HasText;

    /// <summary>
    /// Gets a value indicating whether the editor contains any non-whitespace text.
    /// </summary>
    /// <remarks>
    /// This property delegates to <see cref="Editor"/>.HasNonWhitespaceText to maintain a single source of truth.
    /// It is kept for convenience in command CanExecute logic.
    /// </remarks>
    public bool EditorHasNonWhitespaceText => Editor.HasNonWhitespaceText;

    /// <summary>
    /// Gets a value indicating whether the editor tool is currently visible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property delegates to <see cref="MermaidEditorToolViewModel.IsEditorVisible"/> to track
    /// whether the editor panel is currently visible in the UI. When the editor is pinned (auto-hide)
    /// and collapsed, this returns <c>false</c>.
    /// </para>
    /// <para>
    /// This property is used by the <see cref="CanExecuteClear"/> method to disable the Clear button
    /// when the editor is not visible.
    /// </para>
    /// </remarks>
    public bool IsEditorVisible => _dockFactory.EditorTool?.IsEditorVisible ?? false;

    #endregion Editor ViewModel

    #region Diagram ViewModel

    /// <summary>
    /// Gets the diagram view model that manages WebView rendering and related operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This view model is accessed through the dock layout's DiagramTool.
    /// It encapsulates all WebView-related state and operations.
    /// </para>
    /// <para>
    /// <b>MDI Migration Note:</b> In an MDI design, this property would be replaced by an ActiveDocument
    /// pattern where the current document exposes its own Diagram. For SDI, this direct reference through
    /// the DockFactory is acceptable and provides convenient XAML binding.
    /// </para>
    /// </remarks>
    public DiagramViewModel Diagram => _dockFactory.DiagramTool?.Diagram
        ?? throw new InvalidOperationException($"{nameof(_dockFactory.DiagramTool)} is not initialized. Ensure the dock layout is created first.");

    #endregion Diagram ViewModel

    /// <summary>
    /// Gets or sets the version of the bundled Mermaid.js.
    /// </summary>
    [ObservableProperty]
    public partial string BundledMermaidVersion { get; set; }

    /// <summary>
    /// Gets or sets the latest Mermaid.js version available.
    /// </summary>
    [ObservableProperty]
    public partial string? LatestMermaidVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether live preview is enabled.
    /// </summary>
    [ObservableProperty]
    public partial bool LivePreviewEnabled { get; set; }

    /// <summary>
    /// Gets or sets the current file path being edited.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFileLocationCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseFileCommand))]
    public partial string? CurrentFilePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current document has unsaved changes.
    /// </summary>
    /// <remarks>
    /// When this property changes, the <see cref="SaveFileCommand"/> is automatically notified
    /// to re-evaluate its CanExecute state via the <see cref="NotifyCanExecuteChangedForAttribute"/>.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    public partial bool HasUnsavedChanges { get; set; }

    /// <summary>
    /// Gets the window title including file name and dirty indicator.
    /// </summary>
    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "MermaidPad - The Cross-Platform Mermaid Chart Editor";

    /// <summary>
    /// Gets the status text showing current file info.
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "No file open";

    /// <summary>
    /// Gets a value indicating whether there is an open file to reference.
    /// </summary>
    public bool HasOpenFile => !string.IsNullOrEmpty(CurrentFilePath);

    /// <summary>
    /// Gets or sets the list of recent files for the menu.
    /// </summary>
    /// <remarks>
    /// Each item contains both the file path and a reference to the open command,
    /// enabling proper command binding in the menu item DataTemplate.
    /// </remarks>
    [ObservableProperty]
    public partial ObservableCollection<RecentFileItem> RecentFiles { get; set; } = new ObservableCollection<RecentFileItem>();

    #region CanExecute Methods

    /// <summary>
    /// Determines whether the save file command can execute.
    /// </summary>
    /// <returns><see langword="true"/> if the editor has text and the document has unsaved changes; otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteSave() => EditorHasText && HasUnsavedChanges;

    /// <summary>
    /// Determines whether the save file as command can execute.
    /// </summary>
    /// <returns><see langword="true"/> if the editor has text to save; otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteSaveAs() => EditorHasText;

    /// <summary>
    /// Determines whether the diagram can be rendered based on the current state.
    /// </summary>
    /// <returns><see langword="true"/> if the WebView is ready and the diagram text is not null, empty, or whitespace;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteRender() => Diagram.IsReady && EditorHasNonWhitespaceText;

    /// <summary>
    /// Determines whether the diagram can be cleared based on the current state.
    /// </summary>
    /// <remarks>
    /// This command doesn't take whitespace-only text into account because clearing the editor of whitespace is a valid operation.
    /// </remarks>
    /// <returns><see langword="true"/> if the WebView is ready, the editor is visible, and the diagram text is not null or empty;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteClear() => Diagram.IsReady && IsEditorVisible && EditorHasText;

    /// <summary>
    /// Determines whether the export operation can be performed.
    /// </summary>
    /// <returns><see langword="true"/> if the web view is ready and the diagram text is not null, empty, or whitespace;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteExport() => Diagram.IsReady && EditorHasNonWhitespaceText;

    /// <summary>
    /// Determines whether the open file location command (from filename click on StatusBar) can execute.
    /// </summary>
    /// <returns><see langword="true"/> if a file is currently open; otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteOpenFileLocation() => HasOpenFile;

    /// <summary>
    /// Determines whether the close file command can execute.
    /// </summary>
    /// <returns><see langword="true"/> if there is an open file or the editor has text; otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteCloseFile() => HasOpenFile || EditorHasText;

    /// <summary>
    /// Determines whether the clear recent files command can execute.
    /// </summary>
    /// <returns><see langword="true"/> if there are recent files to clear; otherwise, <see langword="false"/>.</returns>
    private bool CanExecuteClearRecentFiles() => HasRecentFiles;

    #endregion CanExecute Methods

    /// <summary>
    /// Gets a value indicating whether there are recent files.
    /// </summary>
    public bool HasRecentFiles => RecentFiles.Count > 0;

    /// <summary>
    /// Gets the factory used to create and manage dockable UI components.
    /// </summary>
    [SuppressMessage("ReSharper", "ConvertToAutoPropertyWhenPossible", Justification = "Field is readonly and initialized via constructor injection.")]
    public DockFactory DockFactory => _dockFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="dockFactory">The factory for creating the dock layout.</param>
    /// <param name="dockLayoutService">The service for saving and loading dock layout.</param>
    /// <param name="documentMessenger">The document-scoped messenger for receiving text change notifications.</param>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="logger">The logger instance for this view model.</param>
    /// <remarks>
    /// <para>
    /// The <paramref name="dockFactory"/> creates the dock layout with <see cref="MermaidEditorToolViewModel"/>
    /// and <see cref="DiagramToolViewModel"/> instances that wrap the underlying editor and diagram ViewModels.
    /// This ensures each window/document has its own independent editor and diagram state.
    /// </para>
    /// <para>
    /// The <paramref name="documentMessenger"/> is keyed by <see cref="MessengerKeys.Document"/> and is used
    /// to receive <see cref="EditorTextChangedMessage"/> notifications from the editor. This prepares the
    /// architecture for MDI migration where each document has its own messenger instance.
    /// </para>
    /// </remarks>
    public MainWindowViewModel(
        DockFactory dockFactory,
        DockLayoutService dockLayoutService,
        [FromKeyedServices(MessengerKeys.Document)] IMessenger documentMessenger,
        IServiceProvider services,
        ILogger<MainWindowViewModel> logger)
    {
        _dockFactory = dockFactory;
        _dockLayoutService = dockLayoutService;
        _documentMessenger = documentMessenger;
        _logger = logger;

        _settingsService = services.GetRequiredService<SettingsService>();
        _updateService = services.GetRequiredService<MermaidUpdateService>();
        _editorDebouncer = services.GetRequiredService<IDebounceDispatcher>();
        _exportService = services.GetRequiredService<ExportService>();
        _dialogFactory = services.GetRequiredService<IDialogFactory>();
        _fileService = services.GetRequiredService<IFileService>();

        // Create the dock layout - this creates the Editor and Diagram tool ViewModels
        InitializeDockLayout();

        RegisterEventHandlers();

        // Register for EditorTextChangedMessage via the document-scoped messenger using IRecipient<T> pattern.
        // RegisterAll automatically registers handlers for all IRecipient<TMessage> interfaces implemented by this class.
        // This decouples MainWindowViewModel from knowing how text change notifications work
        // and prepares for MDI migration where each document has its own messenger.
        _documentMessenger.RegisterAll(this);

        // Initialize properties from settings
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
        LivePreviewEnabled = _settingsService.Settings.LivePreviewEnabled;
        CurrentFilePath = _settingsService.Settings.CurrentFilePath;

        UpdateRecentFiles();
        UpdateWindowTitle();
    }

    /// <summary>
    /// Initializes the dock layout by loading a previously saved layout if available; otherwise, creates and
    /// initializes a default layout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method attempts to restore the user's last dock layout, including any associated state
    /// such as tool visibility or pinned status. If no valid saved layout is found, a default layout is created and
    /// initialized. This ensures that the dock control is always in a valid state after initialization.
    /// </para>
    /// <para>
    /// This method sets up the docking layout by passing the provided root dock model to the dock
    /// factory and updates the current layout reference. Ensure that the root dock model is properly configured before
    /// calling this method.
    /// </para>
    /// <para>
    ///     Per https://github.com/wieslawsoltes/Dock/blob/master/docs/dock-faq.md#focus-management, you should:
    ///
    ///     1. Assign the layout to DockControl.Layout BEFORE calling InitLayout()
    ///     2. Do not overwrite ActiveDockable or DefaultDockable after loading
    ///
    /// </para>
    /// </remarks>
    private void InitializeDockLayout()
    {
        // Try to load saved layout, fall back to default if not found or invalid
        IRootDock? savedLayout = _dockLayoutService.Load();
        if (savedLayout is not null)
        {
            // First, restore any additional state (e.g., tool visibility, pinned state) from the saved layout
            _dockLayoutService.RestoreState(savedLayout);

            // Assign the layout to DockControl.Layout before calling InitLayout
            Layout = savedLayout;

            // Now we can initialize the layout
            _dockFactory.InitLayout(savedLayout);

            _logger.LogInformation("Dock layout restored from saved file");
        }
        else
        {
            CreateAndInitializeDefaultDockLayout();
        }

        Debug.Assert(_dockFactory.ContextLocator is not null);
        Debug.Assert(_dockFactory.DockableLocator is not null);
    }

    /// <summary>
    /// Initializes the dock layout to its default configuration and captures its initial state for future restoration.
    /// </summary>
    /// <remarks>
    /// <para>This method creates the default dock layout, applies any necessary initialization, and
    /// records the initial state. It is typically called during application startup or when resetting the layout to
    /// defaults.
    /// </para>
    /// <para>
    ///     Per https://github.com/wieslawsoltes/Dock/blob/master/docs/dock-faq.md#focus-management, you should:
    ///
    ///     1. Assign the layout to DockControl.Layout BEFORE calling InitLayout()
    ///     2. Do not overwrite ActiveDockable or DefaultDockable after loading
    ///
    /// </para>
    /// </remarks>
    private void CreateAndInitializeDefaultDockLayout()
    {
        // Create default layout
        IRootDock defaultLayout = _dockFactory.CreateDefaultLayout();

        // Assign the layout to DockControl.Layout before calling InitLayout
        Layout = defaultLayout;

        // Now we can initialize the layout
        _dockFactory.InitLayout(defaultLayout);

        // Capture initial state for future restoration
        _dockLayoutService.CaptureState(defaultLayout);

        _logger.LogInformation("Dock layout initialized with defaults");
    }

    #region Event Handlers

    /// <summary>
    /// Receives the <see cref="EditorTextChangedMessage"/> published when editor text changes.
    /// Implements <see cref="IRecipient{TMessage}.Receive"/> for the standard messaging pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The message parameter is intentionally discarded because this handler uses debounced
    /// rendering that always operates on the latest editor text. When the debounce timer fires,
    /// we want to render the current <see cref="MermaidEditorViewModel.Text"/>, not the text
    /// that was present when the original change occurred.
    /// </para>
    /// <para>
    /// While this technically constitutes a "race condition" (the text may change between
    /// message publish and render), this is the desired behavior for live preview - we always
    /// want to render the most recent content, not stale intermediate states.
    /// </para>
    /// </remarks>
    /// <param name="message">The message is not used directly; see remarks for rationale.</param>
    public void Receive(EditorTextChangedMessage message)
    {
        _ = message; // Explicitly discard unused parameter to satisfy static analysis and document intent

        // Forward HasText, HasNonWhitespaceText property changes and notify SaveFileCommand
        OnPropertyChanged(nameof(EditorHasText));
        OnPropertyChanged(nameof(EditorHasNonWhitespaceText));
        SaveFileCommand.NotifyCanExecuteChanged();
        SaveFileAsCommand.NotifyCanExecuteChanged();

        // Mark as dirty when text changes (ONLY if we're not suppressing dirty tracking)
        if (!_suppressHasUnsavedChangesTracking)
        {
            HasUnsavedChanges = true;
        }

        // Trigger debounced render if live preview is enabled
        if (LivePreviewEnabled)
        {
            // Defensive: Check if WebView is ready before attempting to render
            if (!Diagram.IsReady)
            {
                // Only warn once to avoid log/error spam on every keystroke
                if (!_hasWarnedAboutUnreadyWebView)
                {
                    _logger.LogWarning("Live preview disabled - WebView is not ready");
                    Diagram.LastError = "Live preview is initializing. Please wait for the preview to load, or click Render to manually update the diagram.";
                    _hasWarnedAboutUnreadyWebView = true;
                }
                return;
            }

            _editorDebouncer.Debounce(DebounceRenderKey, TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
            {
                try
                {
                    // Capture the current Editor instance to avoid race conditions where it may become null.
                    MermaidEditorViewModel? editor = Editor;

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    if (editor is null)
                    {
                        return;
                    }

                    // Intentionally use editor.Text (latest value) rather than captured message data (message.Value.Text).
                    // For debounced rendering, we always want the most current content to avoid
                    // rendering stale intermediate states during rapid typing.
                    Diagram.RenderAsync(editor.Text)
                        .SafeFireAndForget(
                            onException: ex => _logger.LogError(ex, "Error while rendering diagram text."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while rendering diagram text.");

                    // If we hit an exception here, rethrow on the UI thread to avoid silent failures
                    throw;
                }
            });
        }

        // Update command states - these are UI operations
        RenderCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Receives the <see cref="DiagramViewReadyMessage"/> published when the DiagramView has attached
    /// to the visual tree and wired up its <see cref="DiagramViewModel.InitializeActionAsync"/> delegate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This message solves the timing issue where <see cref="Views.MainWindow.OnOpenedAsync"/> or
    /// <see cref="ResetLayoutAsync"/> attempts to initialize the diagram before the View has attached.
    /// This happens when the diagram panel starts pinned or in a floating window.
    /// </para>
    /// <para>
    /// The message contains a <see cref="DiagramViewReadyInfo.RequiresInitialization"/> flag:
    /// <list type="bullet">
    ///     <item><description><c>true</c>: ViewModel's <see cref="DiagramViewModel.IsReady"/> was false (initial load or Reset Layout) -> we should initialize</description></item>
    ///     <item><description><c>false</c>: ViewModel's <see cref="DiagramViewModel.IsReady"/> was true (dock state change) -> View handles re-initialization automatically</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="message">The message containing information about the DiagramView's ready state.</param>
    public void Receive(DiagramViewReadyMessage message)
    {
        int currentState = Volatile.Read(ref _diagramInitializationState);

        _logger.LogDebug("Received {MessageName} with {PropertyName}={Value}, {StateField}={StateValue}",
            nameof(DiagramViewReadyMessage), nameof(DiagramViewReadyInfo.RequiresInitialization), message.Value.RequiresInitialization,
            nameof(_diagramInitializationState), currentState);

        // Only initialize if:
        //   1. The message indicates initialization is needed (ViewModel.IsReady was false when View attached)
        //   2. State is Pending (not Idle or already Initializing)
        //
        // Use Interlocked.CompareExchange for atomic transition: Pending -> Initializing
        // This prevents duplicate initialization if:
        //   - Multiple DiagramViewReadyMessage messages arrive rapidly
        //   - SetPendingDiagramInitialization() is called during a slow initialization
        if (message.Value.RequiresInitialization &&
            Interlocked.CompareExchange(ref _diagramInitializationState, InitStateInitializing, InitStatePending) == InitStatePending)
        {
            _logger.LogInformation("DiagramView is ready and initialization was pending. Triggering diagram initialization...");

            // State transitions back to Idle when initialization completes (success or failure) via InitializeDiagramWithStateTrackingAsync().
            // NOTE: Because initialization is queued through Dispatcher.UIThread, there is a rare but real case where the dispatcher task
            // faults/cancels before the delegate executes (e.g., app shutdown). In that case, InitializeDiagramWithStateTrackingAsync() never
            // runs, so we also observe the dispatcher Task completion and reset the state to Idle if it is still InitStateInitializing.
            Task initializationDispatchTask = Dispatcher.UIThread.InvokeAsync(InitializeDiagramWithStateTrackingAsync);

            // Observe dispatcher queue failures explicitly.
            // This continuation is not about the initialization logic itself; it is only a safety net to prevent the state machine from
            // getting stuck in InitStateInitializing if the dispatcher task never executes the delegate.
            Task dispatchCompletionObserverTask = initializationDispatchTask.ContinueWith(HandleDiagramInitializationDispatchCompletion,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            dispatchCompletionObserverTask.SafeFireAndForget(onException: ex =>
                _logger.LogError(ex, "Diagram initialization dispatch completion observer task failed"));

            initializationDispatchTask.SafeFireAndForget(onException: ex =>
            {
                _logger.LogError(ex, "Failed to initialize diagram after {MessageType}", nameof(DiagramViewReadyMessage));

                // Provide a message to the user as well, but ensure this handler itself never throws.
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        Diagram.LastError = $"Failed to initialize diagram: {ex.Message}";
                    }
                    catch (Exception postException)
                    {
                        _logger.LogError(postException, "Failed to update diagram error UI after initialization failure");
                    }
                });
            });
        }
    }

    /// <summary>
    /// Receives the <see cref="FileDroppedMessage"/> published when a Mermaid file (.mmd) is dropped
    /// onto the <see cref="Views.UserControls.MermaidEditorView"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This message enables drag-and-drop file opening when files are dropped specifically onto the
    /// TextEditor control. Files dropped on the WebView control are intercepted by the browser's
    /// native drag/drop handling and cannot be reliably captured by Avalonia.
    /// </para>
    /// <para>
    /// The handler delegates to <see cref="OpenRecentFileCommand"/> which handles:
    /// <list type="bullet">
    ///     <item><description>Unsaved changes prompting</description></item>
    ///     <item><description>File existence validation</description></item>
    ///     <item><description>File size validation</description></item>
    ///     <item><description>Loading and rendering</description></item>
    ///     <item><description>Recent files list update</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="message">The message containing the file path of the dropped .mmd file.</param>
    public void Receive(FileDroppedMessage message)
    {
        string filePath = message.Value;
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogWarning("Received {MessageName} with null or empty file path", nameof(FileDroppedMessage));
            return;
        }

        _logger.LogInformation("Received {MessageName}: {FilePath}", nameof(FileDroppedMessage), filePath);

        // Delegate to OpenRecentFileCommand which handles all the file opening logic
        // including unsaved changes prompting, validation, loading, and rendering
        // Ensure the messenger handler returns immediately by posting to the UI thread (fire & forget)
        Dispatcher.UIThread.Post(() =>
        {
            if (OpenRecentFileCommand.CanExecute(filePath))
            {
                OpenRecentFileCommand.ExecuteAsync(filePath)
                    .SafeFireAndForget(onException: ex =>
                        _logger.LogError(ex, "Failed to open dropped file: {FilePath}", filePath));
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Handles completion of the diagram initialization dispatch task, resetting the initialization state if the task
    /// was canceled or faulted.
    /// </summary>
    /// <remarks>This method should be called when the diagram initialization dispatch task completes. If the
    /// task did not complete successfully, the initialization state is reset to allow for future initialization
    /// attempts. A warning is logged if the state is reset.</remarks>
    /// <param name="task">The task representing the diagram initialization dispatch operation. If the task is canceled or faulted, the
    /// initialization state will be reset.</param>
    [SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Clarity of intent.")]
    private void HandleDiagramInitializationDispatchCompletion(Task task)
    {
        try
        {
            if (!task.IsCanceled && !task.IsFaulted)
            {
                return;
            }

            int observedState = Interlocked.CompareExchange(ref _diagramInitializationState, InitStateIdle, InitStateInitializing);
            if (observedState == InitStateInitializing)
            {
                _logger.LogWarning("Diagram initialization dispatch did not run (TaskStatus={TaskStatus}); state reset to {State}",
                    task.Status, nameof(InitStateIdle));
            }
        }
        catch (Exception ex)
        {
            // This method is a state-machine safety net; it must never throw.
            _logger.LogCritical(ex, "Diagram initialization dispatch completion handler failed; state may be inconsistent");

            // Provide a message to the user as well, but ensure this handler itself never throws.
            try
            {
                Dispatcher.UIThread.Post(() => Diagram.LastError = $"Internal error during diagram initialization: {ex.Message}");
            }
            catch (Exception uiEx)
            {
                _logger.LogError(uiEx, "Failed to dispatch diagram initialization error message to UI thread.");
            }
        }
    }

    /// <summary>
    /// Initializes the diagram and manages state transitions to ensure accurate tracking of the initialization process.
    /// </summary>
    /// <remarks>This method resets the diagram initialization state to idle upon completion, unless the state
    /// has already changed to a different value. This helps prevent overwriting newer state transitions that may have
    /// occurred during initialization.</remarks>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    private async Task InitializeDiagramWithStateTrackingAsync()
    {
        try
        {
            await InitializeDiagramAsync();
        }
        finally
        {
            // Attempt to transition back to Idle when done, regardless of success/failure.
            // Only reset if the state is still Initializing to avoid overwriting a newer Pending state.
            int observedState = Interlocked.CompareExchange(ref _diagramInitializationState, InitStateIdle, InitStateInitializing);
            if (observedState == InitStateInitializing)
            {
                _logger.LogDebug("Diagram initialization complete, state reset to {State}", nameof(InitStateIdle));
            }
            else
            {
                _logger.LogWarning("Diagram initialization complete; state not reset because current state changed to {StateValue}", observedState);
            }
        }
    }

    /// <summary>
    /// Handles property changes from the Diagram ViewModel.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The property changed event arguments.</param>
    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Diagram.IsReady):
                // Update command states when WebView ready state changes
                _logger.LogInformation("Diagram.IsReady changed to: {IsReady}", Diagram.IsReady);
                RenderCommand.NotifyCanExecuteChanged();
                ClearCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();

                // Reset warning flag when WebView becomes ready
                // This ensures the flag is cleared even if the user isn't typing
                if (Diagram.IsReady && _hasWarnedAboutUnreadyWebView)
                {
                    _logger.LogInformation("Live preview re-enabled - WebView is now ready");
                    Diagram.LastError = null;
                    _hasWarnedAboutUnreadyWebView = false;
                }
                break;
        }
    }

    /// <summary>
    /// Handles property changes from the EditorTool ViewModel.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The property changed event arguments.</param>
    private void OnEditorToolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MermaidEditorToolViewModel.IsEditorVisible):
                // Update command states when editor visibility changes
                _logger.LogInformation("EditorTool.IsEditorVisible changed to: {IsVisible}", IsEditorVisible);

                OnPropertyChanged(nameof(IsEditorVisible));
                ClearCommand.NotifyCanExecuteChanged();
                break;
        }
    }

    /// <summary>
    /// Handles changes to the live preview enabled state.
    /// </summary>
    /// <param name="value">The new value indicating whether live preview is enabled.</param>
    partial void OnLivePreviewEnabledChanged(bool value)
    {
        // If we can't render yet, just return
        if (!Diagram.IsReady)
        {
            return;
        }

        if (value)
        {
            // Avoid allocating Editor.Text just to determine whether we have renderable content
            if (!EditorHasNonWhitespaceText)
            {
                return;
            }

            // NOTE: Accessing Editor.Text allocates a string (AvaloniaEdit API).
            // Capture once for the actual render
            string editorText = Editor.Text;

            // SafeFireAndForget handles context, but the error handler updates UI
            Diagram.RenderAsync(editorText).SafeFireAndForget(onException: ex =>
            {
                // Even though SafeFireAndForget has a continueOnCapturedContext param, it doesn't guarantee UI thread here
                Dispatcher.UIThread.Post(() =>
                {
                    Diagram.LastError = $"Failed to render diagram: {ex.Message}";
                    Debug.WriteLine(ex);
                    _logger.LogError(ex, "Live preview render failed");
                });
            }, continueOnCapturedContext: true);    // Use captured UI context so any continuations that update Diagram state run on the UI thread
        }
        else
        {
            _editorDebouncer.Cancel(DebounceRenderKey);
        }
    }

    /// <summary>
    /// Handles changes to the current file path by updating application settings and related UI elements.
    /// </summary>
    /// <remarks>This method updates the application's settings and refreshes the window title and status text
    /// to reflect the new file path.</remarks>
    /// <param name="value">The new file path to set as the current file. Can be null to indicate no file is selected.</param>
    partial void OnCurrentFilePathChanged(string? value)
    {
        _settingsService.Settings.CurrentFilePath = value;
        _settingsService.Save();
        UpdateWindowTitle();
        UpdateStatusText();
        OnPropertyChanged(nameof(HasOpenFile));
    }

    /// <summary>
    /// Handles changes to the dirty state of the object when the value of the <see cref="HasUnsavedChanges"/> property changes.
    /// </summary>
    /// <remarks>This method is invoked automatically when the <see cref="HasUnsavedChanges"/> property changes. Override this partial
    /// method to perform custom actions in response to changes in the dirty state, such as updating UI elements or
    /// enabling save functionality.</remarks>
    /// <param name="value">A value indicating whether the object is now considered dirty. <see langword="true"/> if the object has unsaved
    /// changes; otherwise, <see langword="false"/>.</param>
    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Parameter is required for partial method signature.")]
    partial void OnHasUnsavedChangesChanged(bool value)
    {
        UpdateWindowTitle();
        // Note: SaveFileCommand.NotifyCanExecuteChanged() is automatically called via
        // [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))] on the HasUnsavedChanges property.
    }

    #endregion Event Handlers

    #region Diagram Initialization

    /// <summary>
    /// Initializes the diagram view asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// This method delegates to the <see cref="DiagramViewModel.InitializeAsync"/> method,
    /// which in turn invokes the InitializeAction set by the DiagramView.
    /// </remarks>
    public Task InitializeDiagramAsync()
    {
        string mermaidSource = Editor.Text;
        return Diagram.InitializeAsync(mermaidSource);
    }

    /// <summary>
    /// Sets the pending diagram initialization flag to indicate that diagram initialization
    /// should occur when the <see cref="DiagramViewReadyMessage"/> is received.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method when the DiagramView is not yet attached (e.g., panel is pinned/floating)
    /// and initialization cannot proceed immediately. When the DiagramView attaches and sends
    /// <see cref="DiagramViewReadyMessage"/> with <see cref="DiagramViewReadyInfo.RequiresInitialization"/>=true,
    /// the <see cref="Receive(DiagramViewReadyMessage)"/> handler will trigger initialization.
    /// </para>
    /// <para>
    /// This method only transitions to Pending if the current state is Idle. If initialization
    /// is already in progress (Initializing state), this call is ignored to prevent the race
    /// condition where a new pending request could cause duplicate initialization.
    /// </para>
    /// <para>
    /// This method is called from:
    /// <list type="bullet">
    ///     <item><description><see cref="Views.MainWindow.OnOpenedAsync"/>: when diagram panel starts pinned/floating</description></item>
    ///     <item><description><see cref="ResetLayoutAsync"/>: when diagram panel is not visible after layout reset</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void SetPendingDiagramInitialization()
    {
        // Only transition to Pending if currently Idle.
        // If already Initializing, don't overwrite - the current initialization will complete
        // and transition back to Idle, at which point a new SetPending call would be needed.
        int observedState = Interlocked.CompareExchange(ref _diagramInitializationState, InitStatePending, InitStateIdle);
        if (observedState == InitStateIdle)
        {
            _logger.LogDebug("Set {FlagName} to {State} (Pending)", nameof(_diagramInitializationState), InitStatePending);
        }
        else
        {
            _logger.LogDebug("Ignored {MethodName} call - current state is {CurrentState} (expected {ExpectedState})",
                nameof(SetPendingDiagramInitialization), observedState, InitStateIdle);
        }
    }

    #endregion Diagram Initialization

    #region File Open/Save
    /// <summary>
    /// Creates a new empty file, prompting to save if there are unsaved changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This command clears the current document and creates a fresh editing state.
    /// If there are unsaved changes, the user is prompted to save before proceeding.
    /// </para>
    /// <para>
    /// Unlike <see cref="CloseFileCommand"/>, this command is always available because
    /// creating a new file is valid even when no file is currently open.
    /// </para>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. This command wraps the core logic in Dispatcher.UIThread.InvokeAsync()
    /// to ensure proper context.
    /// </para>
    /// </remarks>
    /// <param name="storageProvider">The storage provider used to save the file if the user chooses to save changes.</param>
    /// <returns>A task that represents the asynchronous new file operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand]
    private Task NewFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => NewFileCoreAsync(storageProvider));
    }

    /// <summary>
    /// Performs the core logic to create a new file, including prompting for unsaved changes
    /// and resetting the editor and diagram state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="number">
    ///     <item><description>Prompts the user to save if there are unsaved changes</description></item>
    ///     <item><description>Creates a new empty TextDocument (which clears the undo stack)</description></item>
    ///     <item><description>Clears the diagram preview</description></item>
    ///     <item><description>Resets file-related state (CurrentFilePath, IsDirty)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="storageProvider">The storage provider used if the user chooses to save changes.</param>
    /// <returns>A task that represents the asynchronous new file operation.</returns>
    private async Task NewFileCoreAsync(IStorageProvider storageProvider)
    {
        // Check for unsaved changes and prompt user
        if (HasUnsavedChanges && EditorHasNonWhitespaceText)
        {
            bool canProceed = await PromptSaveIfDirtyAsync(storageProvider);
            if (!canProceed)
            {
                return; // User cancelled
            }
        }

        // Reset file state
        CurrentFilePath = null;
        HasUnsavedChanges = false;

        // Suppress dirty tracking while resetting the document
        // ResetDocument triggers EditorTextChangedMessage which would otherwise set IsDirty = true
        _suppressHasUnsavedChangesTracking = true;
        try
        {
            // Create a new empty document - this automatically clears the undo stack
            Editor.ResetDocument(string.Empty);

            // Clear the diagram preview if WebView is ready
            if (Diagram.IsReady)
            {
                await Diagram.RenderAsync(string.Empty);
            }
        }
        finally
        {
            _suppressHasUnsavedChangesTracking = false;
        }

        // Notify property changes for binding updates
        OnPropertyChanged(nameof(EditorHasText));
        OnPropertyChanged(nameof(EditorHasNonWhitespaceText));

        // Update command states
        NewFileCommand.NotifyCanExecuteChanged();
        CloseFileCommand.NotifyCanExecuteChanged();
        SaveFileCommand.NotifyCanExecuteChanged();
        SaveFileAsCommand.NotifyCanExecuteChanged();
        RenderCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Asynchronously opens a file using the specified storage provider.
    /// </summary>
    /// <param name="storageProvider">The storage provider used to select and access the file. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous file open operation.</returns>
    /// <remarks>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. Even when code executes on the main thread, the absence of SynchronizationContext
    /// causes pickers to silently fail or hang indefinitely without showing dialogs.
    /// </para>
    /// <para>
    /// References:
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/13484
    ///   (IStorageProvider.OpenFilePickerAsync randomly not showing the dialog)
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/15775
    ///   (StorageProvider.OpenFolderPickerAsync blocks UI)
    /// - https://github.com/AvaloniaUI/Avalonia/issues/15806
    ///   (Async Main() causes picker failures - STA thread requirement)
    /// </para>
    /// <para>
    /// Solution: Wrap all picker calls in Dispatcher.UIThread.InvokeAsync() to ensure proper context.
    /// This is defensive programming against ConfigureAwait(false) in the call chain removing the context.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand]
    private Task OpenFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => OpenFileCoreAsync(storageProvider));
    }

    /// <summary>
    /// Opens a file using the specified storage provider, prompting the user to save unsaved changes if necessary, and
    /// loads the file content into the current diagram.
    /// </summary>
    /// <remarks>If there are unsaved changes, the user is prompted to save before proceeding. The method
    /// updates the current file path, diagram content, and recent files list upon successful file load. If the WebView
    /// is ready, the loaded content is rendered immediately. Any errors encountered during the operation are logged and
    /// displayed to the user.</remarks>
    /// <param name="storageProvider">The storage provider used to access and open the file. Must not be null.</param>
    /// <returns>A task that represents the asynchronous operation of opening the file.</returns>
    private async Task OpenFileCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            // Check for unsaved changes
            if (HasUnsavedChanges)
            {
                bool canProceed = await PromptSaveIfDirtyAsync(storageProvider);
                if (!canProceed)
                {
                    return; // User cancelled
                }
            }

            (string? filePath, string? content) = await _fileService.OpenFileAsync(storageProvider);
            if (filePath is not null && content is not null)
            {
                _suppressHasUnsavedChangesTracking = true;
                try
                {
                    Editor.Text = content;
                    CurrentFilePath = filePath;
                    HasUnsavedChanges = false;
                    UpdateRecentFiles();

                    // Render the newly loaded content if WebView is ready
                    if (Diagram.IsReady)
                    {
                        // Use the loaded content directly to avoid redundant Editor.Text access
                        await Diagram.RenderAsync(content);
                    }

                    _logger.LogInformation("Opened file: {FilePath}", filePath);
                }
                finally
                {
                    _suppressHasUnsavedChangesTracking = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file");
            await ShowErrorMessageAsync("Failed to open file. " + ex.Message);
        }
    }

    /// <summary>
    /// Saves the current file asynchronously using the specified storage provider.
    /// </summary>
    /// <param name="storageProvider">The storage provider used to select the destination and perform the file save operation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <remarks>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. Even when code executes on the main thread, the absence of SynchronizationContext
    /// causes pickers to silently fail or hang indefinitely without showing dialogs.
    /// </para>
    /// <para>
    /// References:
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/13484
    ///   (IStorageProvider.OpenFilePickerAsync randomly not showing the dialog)
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/15775
    ///   (StorageProvider.OpenFolderPickerAsync blocks UI)
    /// - https://github.com/AvaloniaUI/Avalonia/issues/15806
    ///   (Async Main() causes picker failures - STA thread requirement)
    /// </para>
    /// <para>
    /// Solution: Wrap all picker calls in Dispatcher.UIThread.InvokeAsync() to ensure proper context.
    /// This is defensive programming against ConfigureAwait(false) in the call chain removing the context.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand(CanExecute = nameof(CanExecuteSave))]
    private Task SaveFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => SaveFileCoreAsync(storageProvider));
    }

    /// <summary>
    /// Asynchronously saves the current diagram to a file using the specified storage provider.
    /// </summary>
    /// <remarks>If the save operation succeeds, the current file path is updated and the diagram is marked as
    /// not dirty. If the operation fails, an error message is displayed and the error is logged.</remarks>
    /// <param name="storageProvider">The storage provider used to save the file. Must not be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    private async Task SaveFileCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            string? savedPath = await _fileService.SaveFileAsync(storageProvider, CurrentFilePath, Editor.Text);
            if (savedPath is not null)
            {
                CurrentFilePath = savedPath;
                HasUnsavedChanges = false;
                UpdateRecentFiles();
                _logger.LogInformation("Saved file: {SavedPath}", savedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file");
            await ShowErrorMessageAsync("Failed to save file. " + ex.Message);
        }
    }

    /// <summary>
    /// Initiates a file save operation using the specified storage provider, allowing the user to choose the file
    /// location and name.
    /// </summary>
    /// <param name="storageProvider">The storage provider used to present the file save dialog and handle file system access. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation. The task completes when the file has been saved or the
    /// operation is canceled.</returns>
    /// <remarks>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. Even when code executes on the main thread, the absence of SynchronizationContext
    /// causes pickers to silently fail or hang indefinitely without showing dialogs.
    /// </para>
    /// <para>
    /// References:
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/13484
    ///   (IStorageProvider.OpenFilePickerAsync randomly not showing the dialog)
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/15775
    ///   (StorageProvider.OpenFolderPickerAsync blocks UI)
    /// - https://github.com/AvaloniaUI/Avalonia/issues/15806
    ///   (Async Main() causes picker failures - STA thread requirement)
    /// </para>
    /// <para>
    /// Solution: Wrap all picker calls in Dispatcher.UIThread.InvokeAsync() to ensure proper context.
    /// This is defensive programming against ConfigureAwait(false) in the call chain removing the context.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand(CanExecute = nameof(CanExecuteSaveAs))]
    private Task SaveFileAsAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => SaveFileAsCoreAsync(storageProvider));
    }

    /// <summary>
    /// Saves the current diagram text to a new file using the specified storage provider.
    /// </summary>
    /// <remarks>If the save operation is successful, the current file path is updated and the dirty state is
    /// cleared. If an error occurs during saving, an error message is displayed to the user and the failure is
    /// logged.</remarks>
    /// <param name="storageProvider">The storage provider used to select the destination and save the file. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    private async Task SaveFileAsCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            string? suggestedName = !string.IsNullOrEmpty(CurrentFilePath)
                ? Path.GetFileName(CurrentFilePath)
                : null;

            string? savedPath = await _fileService.SaveFileAsAsync(storageProvider, Editor.Text, suggestedName);
            if (savedPath is not null)
            {
                CurrentFilePath = savedPath;
                HasUnsavedChanges = false;
                UpdateRecentFiles();
                _logger.LogInformation("Saved file as: {SavedPath}", savedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file as");
            await ShowErrorMessageAsync("Failed to save file. " + ex.Message);
        }
    }

    /// <summary>
    /// Closes the currently open file, prompting to save if there are unsaved changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This command clears the editor content and diagram preview, resets the current file path,
    /// and creates a fresh document state. If there are unsaved changes, the user is prompted
    /// to save before closing.
    /// </para>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. This command wraps the core logic in Dispatcher.UIThread.InvokeAsync()
    /// to ensure proper context, matching the pattern used by OpenFileCommand and SaveFileCommand.
    /// </para>
    /// </remarks>
    /// <param name="storageProvider">The storage provider used to save the file if the user chooses to save changes.</param>
    /// <returns>A task that represents the asynchronous close operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand(CanExecute = nameof(CanExecuteCloseFile))]
    private Task CloseFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => CloseFileCoreAsync(storageProvider));
    }

    /// <summary>
    /// Performs the core logic to close the current file, including prompting for unsaved changes
    /// and resetting the editor and diagram state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="number">
    ///     <item><description>Prompts the user to save if there are unsaved changes</description></item>
    ///     <item><description>Creates a new empty TextDocument (which clears the undo stack)</description></item>
    ///     <item><description>Clears the diagram preview</description></item>
    ///     <item><description>Resets file-related state (CurrentFilePath, IsDirty)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The undo stack is automatically cleared when a new TextDocument is created via
    /// <see cref="MermaidEditorViewModel.ResetDocument"/>, which is the expected behavior when closing a file.
    /// </para>
    /// </remarks>
    /// <param name="storageProvider">The storage provider used if the user chooses to save changes.</param>
    /// <returns>A task that represents the asynchronous close operation.</returns>
    private async Task CloseFileCoreAsync(IStorageProvider storageProvider)
    {
        // Check for unsaved changes and prompt user
        if (HasUnsavedChanges && EditorHasNonWhitespaceText)
        {
            bool canProceed = await PromptSaveIfDirtyAsync(storageProvider);
            if (!canProceed)
            {
                return; // User cancelled
            }
        }

        // Reset file state BEFORE clearing editor to ensure proper state during notifications
        CurrentFilePath = null;
        HasUnsavedChanges = false;

        // Suppress dirty tracking while resetting the document
        // ResetDocument triggers EditorTextChangedMessage which would otherwise set IsDirty = true
        _suppressHasUnsavedChangesTracking = true;
        try
        {
            // Create a new empty document - this automatically clears the undo stack
            // Use ResetDocument which handles event subscription/unsubscription
            Editor.ResetDocument(string.Empty);

            // Clear the diagram preview if WebView is ready
            if (Diagram.IsReady)
            {
                // NO ConfigureAwait(false) - Diagram.RenderAsync needs UI context
                await Diagram.RenderAsync(string.Empty);
            }
        }
        finally
        {
            _suppressHasUnsavedChangesTracking = false;
        }

        // Notify property changes for binding updates
        OnPropertyChanged(nameof(EditorHasText));
        OnPropertyChanged(nameof(EditorHasNonWhitespaceText));

        // Update command states
        CloseFileCommand.NotifyCanExecuteChanged();
        SaveFileCommand.NotifyCanExecuteChanged();
        SaveFileAsCommand.NotifyCanExecuteChanged();
        RenderCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Prompts the user to save unsaved changes to the current diagram, if any, before continuing the operation.
    /// </summary>
    /// <remarks>If there are no unsaved changes or the diagram is empty, the method returns immediately and
    /// continues the operation. If the user chooses to save, the diagram is saved using the specified storage provider.
    /// If the user cancels, the operation is halted. In case of an error displaying the dialog, the method returns <see
    /// langword="true"/> to avoid blocking the user.</remarks>
    /// <param name="storageProvider">The storage provider used to save the diagram file if the user chooses to save changes.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the operation
    /// should continue; otherwise, <see langword="false"/> if the user cancels.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    public Task<bool> PromptSaveIfDirtyAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        if (!HasUnsavedChanges || !EditorHasNonWhitespaceText)
        {
            return Task.FromResult(true); // No unsaved changes, continue
        }

        return Dispatcher.UIThread.InvokeAsync(() => PromptSaveIfDirtyCoreAsync(storageProvider));
    }

    /// <summary>
    /// Displays a confirmation dialog prompting the user to save unsaved changes, and saves the file if the user
    /// chooses to do so.
    /// </summary>
    /// <remarks>If the main application window is unavailable or an error occurs while displaying the dialog,
    /// the method returns true to allow the operation to continue. The dialog presents options to save, discard, or
    /// cancel, and saving is performed using the provided storage provider.</remarks>
    /// <param name="storageProvider">The storage provider used to save the file if the user confirms the save operation. Cannot be null.</param>
    /// <returns>true if the user chooses to save or discard changes, or if the dialog cannot be shown; false if the user cancels
    /// the operation.</returns>
    private async Task<bool> PromptSaveIfDirtyCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            Window? mainWindow = GetParentWindow();
            if (mainWindow is null)
            {
                return true;
            }

            ConfirmationDialogViewModel confirmViewModel = _dialogFactory.CreateViewModel<ConfirmationDialogViewModel>();
            confirmViewModel.ShowCancelButton = true;
            confirmViewModel.Title = "Unsaved Changes";

            string fileName = !string.IsNullOrEmpty(CurrentFilePath)
                ? Path.GetFileName(CurrentFilePath)
                : "Untitled";
            confirmViewModel.Message = $"Do you want to save changes to {fileName}?";
            confirmViewModel.IconData = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M11,7V13H13V7H11M11,15V17H13V15H11Z"; // Warning icon
            confirmViewModel.IconColor = Avalonia.Media.Brushes.Orange;

            ConfirmationDialog confirmDialog = new ConfirmationDialog { DataContext = confirmViewModel };
            ConfirmationResult result = await confirmDialog.ShowDialog<ConfirmationResult>(mainWindow);
            switch (result)
            {
                case ConfirmationResult.Yes:
                    // Save the file
                    await SaveFileCoreAsync(storageProvider);
                    return true;

                case ConfirmationResult.No:
                    // Don't save, continue
                    return true;

                case ConfirmationResult.Cancel:
                default:
                    // Cancel the operation
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show save confirmation dialog");
            return true; // Continue on error to avoid blocking the user
        }
    }

    /// <summary>
    /// Opens the specified recent file asynchronously and loads its contents into the editor, handling unsaved changes
    /// and file validation as needed.
    /// </summary>
    /// <remarks>If there are unsaved changes, the method prompts the user to save before proceeding. If the
    /// file does not exist or exceeds the allowed size, an error message is displayed and the file is removed from the
    /// recent files list. The method updates the recent files list and renders the loaded content if
    /// applicable.</remarks>
    /// <param name="filePath">The full path of the file to open. Cannot be null or empty. The file must exist and not exceed the maximum
    /// allowed size.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the file has been loaded or if the
    /// operation is cancelled due to validation or user action.</returns>
    [RelayCommand]
    private async Task OpenRecentFileAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            // Check for unsaved changes
            if (HasUnsavedChanges)
            {
                Window? mainWindow = GetParentWindow();
                if (mainWindow?.StorageProvider is null)
                {
                    return;
                }

                bool canProceed = await PromptSaveIfDirtyAsync(mainWindow.StorageProvider);
                if (!canProceed)
                {
                    return;
                }
            }

            // Runs all file-system checks on a background thread to avoid blocking UI
            // These checks may involve underlying I/O operations in the OS which can be slow for network drives, etc.
            (bool exists, bool? isSizeValid) = await Task.Run(() =>
            {
                bool fileExists = File.Exists(filePath);
                if (!fileExists)
                {
                    return (exists: false, isSizeValid: null);
                }

                bool? isValid = _fileService.ValidateFileSize(filePath);
                return (exists: true, isSizeValid: isValid);
            }).ConfigureAwait(false);

            if (!exists)
            {
                await ShowErrorMessageAsync($"File not found: {filePath}");

                // Remove from recent files
                _settingsService.Settings.RecentFiles.Remove(filePath);
                _settingsService.Save();
                UpdateRecentFiles();
                return;
            }

            if (isSizeValid == false)
            {
                // ReSharper disable once InconsistentNaming
                const double maxSizeMB = FileService.MaxFileSizeBytes / FileService.OneMBInBytes;
                await ShowErrorMessageAsync($"File size exceeds the maximum allowed size of {maxSizeMB:0.#}MB.");
                return;
            }

            // Read and load the file
            _suppressHasUnsavedChangesTracking = true;
            try
            {
                string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8)
                    .ConfigureAwait(false);

                // Explicitly cast the lambda to Func<Task> to avoid ambiguous overload resolution
                await Dispatcher.UIThread.InvokeAsync((Func<Task>)(async () =>
                {
                    Editor.Text = content;
                    CurrentFilePath = filePath;
                    HasUnsavedChanges = false;

                    // Move to top of recent files
                    _fileService.AddToRecentFiles(filePath);
                    UpdateRecentFiles();

                    // Render the newly loaded content if WebView is ready
                    if (Diagram.IsReady)
                    {
                        // Use the loaded content directly to avoid redundant Editor.Text access
                        await Diagram.RenderAsync(content);
                    }

                    _logger.LogInformation("Opened recent file: {FilePath}", filePath);
                }));
            }
            finally
            {
                _suppressHasUnsavedChangesTracking = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open recent file: {FilePath}", filePath);
            await ShowErrorMessageAsync($"Failed to open file: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the list of recently accessed files from the application's history.
    /// </summary>
    /// <remarks>This command removes all entries from the recent files list and updates any associated user
    /// interface elements to reflect the change. Use this method to reset the recent files history, for example, when
    /// privacy is a concern or to start a new session.</remarks>
    [RelayCommand(CanExecute = nameof(CanExecuteClearRecentFiles))]
    private void ClearRecentFiles()
    {
        _fileService.ClearRecentFiles();
        UpdateRecentFiles();
    }

    /// <summary>
    /// Updates the window title to reflect the current file name and unsaved changes status.
    /// </summary>
    /// <remarks>The window title is set to include the name of the current file, or "Untitled" if no file is
    /// open. An asterisk is appended if there are unsaved changes.</remarks>
    private void UpdateWindowTitle()
    {
        string fileName = !string.IsNullOrEmpty(CurrentFilePath)
            ? Path.GetFileName(CurrentFilePath)
            : "Untitled";

        string dirtyIndicator = HasUnsavedChanges ? " *" : "";
        WindowTitle = $"MermaidPad - {fileName}{dirtyIndicator}";
    }

    #region View Menu Commands

    /// <summary>
    /// Resets the dock layout to its default configuration.
    /// </summary>
    /// <remarks>
    /// This command deletes any saved layout file and recreates the default layout
    /// with the editor and diagram panels in their original positions and proportions.
    /// </remarks>
    [RelayCommand]
    private async Task ResetLayoutAsync()
    {
        try
        {
            // Show confirmation dialog
            Window? mainWindow = GetParentWindow();
            if (mainWindow is null)
            {
                return;
            }

            ConfirmationDialogViewModel confirmViewModel = _dialogFactory.CreateViewModel<ConfirmationDialogViewModel>();
            confirmViewModel.ShowCancelButton = false;
            confirmViewModel.Title = "Reset Layout?";
            confirmViewModel.Message = "Are you sure you want to reset the layout to default? This will restore the original panel positions and sizes.";
            confirmViewModel.IconData = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M11,7V13H13V7H11M11,15V17H13V15H11Z"; // Warning icon
            confirmViewModel.IconColor = Avalonia.Media.Brushes.Orange;

            ConfirmationDialog confirmDialog = new ConfirmationDialog { DataContext = confirmViewModel };
            ConfirmationResult result = await confirmDialog.ShowDialog<ConfirmationResult>(mainWindow);

            if (result != ConfirmationResult.Yes)
            {
                return;
            }

            // Delete the saved layout state - synchronous operation (using async here is overkill)
            if (!_dockLayoutService.DeleteSavedLayout())
            {
                await ShowErrorMessageAsync("Failed to delete saved layout file. The layout may not reset correctly.");
            }

            // Close existing layout if possible
            if (Layout is not null)
            {
                if (Layout.Close.CanExecute(null))
                {
                    Layout.Close.Execute(null);
                }

                // For some reason closing the Layout via 'Layout.Close.Execute()'
                // doesn't close floating windows. So we have to explicitly close them here as well.
                // Close any floating windows before resetting the layout
                _dockFactory.CloseAllFloatingWindows(Layout);
            }

            // Preserve current editor state before resetting
            string currentText = Editor.Text;
            int selectionStart = Editor.SelectionStart;
            int selectionLength = Editor.SelectionLength;
            int caretOffset = Editor.CaretOffset;

            // Reset the layout to default
            _dockLayoutService.ResetLayoutState();

            // Create and initialize a new default layout
            // This creates NEW ViewModels for Editor and Diagram
            CreateAndInitializeDefaultDockLayout();

            // Restore editor state to the NEW editor ViewModel
            Editor.Text = currentText;
            Editor.SelectionStart = selectionStart;
            Editor.SelectionLength = selectionLength;
            Editor.CaretOffset = caretOffset;

            // Subscribe to new tool ViewModels (unsubscribes from old ones first)
            RegisterEventHandlers();

            // Notify property changes for binding updates
            OnPropertyChanged(nameof(Editor));
            OnPropertyChanged(nameof(Diagram));
            OnPropertyChanged(nameof(EditorHasText));
            OnPropertyChanged(nameof(EditorHasNonWhitespaceText));
            OnPropertyChanged(nameof(IsEditorVisible));

            // Reset the warning flag since we have a new Diagram ViewModel
            _hasWarnedAboutUnreadyWebView = false;

            // Explicitly initialize the diagram after Reset Layout.
            // DiagramView.SetupViewModelBindings() only triggers re-initialization when IsReady=true
            // (dock state changes), but Reset Layout creates a NEW ViewModel with IsReady=false.
            // This mirrors the initialization done in MainWindow.OnOpenedAsync().
            if (Diagram.InitializeActionAsync is not null)
            {
                _logger.LogInformation("Initializing diagram after layout reset...");
                await InitializeDiagramAsync();
            }
            else
            {
                // DiagramView hasn't attached yet (new layout with pinned/floating diagram panel).
                // Set the pending flag so Receive(DiagramViewReadyMessage) will trigger initialization
                // when the View becomes ready.
                SetPendingDiagramInitialization();

                _logger.LogInformation(
                    "DiagramView not yet attached after layout reset - panel may be pinned/floating. " +
                    "Initialization will be triggered when {MessageName} is received.",
                    nameof(DiagramViewReadyMessage));
            }

            _logger.LogInformation("Dock layout reset to default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset dock layout");

            await ShowErrorMessageAsync($"Failed to reset layout: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers event handlers for property change notifications on editor and diagram components to ensure command
    /// states are updated appropriately.
    /// </summary>
    /// <remarks>This method should be called to initialize or refresh event subscriptions for property
    /// changes. It removes any existing handlers before adding new ones to prevent duplicate subscriptions. Calling
    /// this method multiple times is safe and will not result in multiple event handler registrations.</remarks>
    private void RegisterEventHandlers()
    {
        // Ensure we don't end up with duplicate subscriptions by removing handlers before adding them.
        // Unsubscribing a handler that isn't currently subscribed is safe (no-op in C#).

        // Subscribe to Diagram.PropertyChanged to update command states when IsReady changes
        Diagram.PropertyChanged -= OnDiagramPropertyChanged;
        Diagram.PropertyChanged += OnDiagramPropertyChanged;

        // Subscribe to EditorTool.PropertyChanged to update ClearCommand when IsEditorVisible changes
        _dockFactory.EditorTool!.PropertyChanged -= OnEditorToolPropertyChanged;
        _dockFactory.EditorTool!.PropertyChanged += OnEditorToolPropertyChanged;
    }

    /// <summary>
    /// Shows and activates the editor panel.
    /// </summary>
    /// <remarks>
    /// This command ensures the editor panel is visible and focused, useful when
    /// the panel has been pinned (auto-hide) or otherwise hidden.
    /// </remarks>
    [RelayCommand]
    private void ShowEditorPanel()
    {
        if (_dockFactory.EditorTool is not null)
        {
            _dockFactory.ShowTool(_dockFactory.EditorTool);
            _logger.LogDebug("Editor panel shown and activated");
        }
    }

    /// <summary>
    /// Shows and activates the diagram panel.
    /// </summary>
    /// <remarks>
    /// This command ensures the diagram panel is visible and focused, useful when
    /// the panel has been pinned (auto-hide) or otherwise hidden.
    /// </remarks>
    [RelayCommand]
    private void ShowDiagramPanel()
    {
        if (_dockFactory.DiagramTool is not null)
        {
            _dockFactory.ShowTool(_dockFactory.DiagramTool);
            _logger.LogDebug("Diagram panel shown and activated");
        }
    }

#if DEBUG
    /// <summary>
    /// Logs diagnostic information about the current dock layout state for debugging drop target issues.
    /// Only available in DEBUG builds.
    /// </summary>
    /// <remarks>
    /// Call this from the View Menu or via a keyboard shortcut (e.g., Ctrl+Shift+D) to dump
    /// the dock hierarchy state to the log file. Useful when both panels are floated and
    /// re-docking isn't working.
    /// </remarks>
    [RelayCommand]
    private void DumpDockDiagnostics()
    {
        _dockFactory.LogDropTargetDiagnostics(Layout);
    }
#endif

    #endregion View Menu Commands

    #region Help Menu Commands

    /// <summary>
    /// Gets the command that opens the application's log file directory (%APPDATA%\MermaidPad) in the system's file explorer.
    /// </summary>
    /// <summary>
    /// Opens the application's log file directory using the system's default file explorer.
    /// </summary>
    [RelayCommand]
    private void ViewLogs()
    {
        //TODO refactor this path construction into a common utility method
        string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MermaidPad");

        try
        {
            // Use ShellExecute to open the directory in the default file explorer
            using Process? process = Process.Start(new ProcessStartInfo(logDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open log directory: {LogDirectory}", logDirectory);

            ShowErrorMessageAsync("Failed to open log directory. " + ex.Message)
                .SafeFireAndForget(onException: logEx => _logger.LogError(logEx, "Failed to show error message dialog."));
        }
    }

    #endregion Help Menu Commands

    /// <summary>
    /// Updates the status text to reflect the currently open file or indicate that no file is open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a file is open, the status text displays the file name. Otherwise, it shows a default
    /// message indicating that no file is open.
    /// </para>
    /// <para>
    /// We do not call <see cref="ObservableObject.OnPropertyChanged(string?)"/>
    /// for <see cref="HasOpenFile"/> here because it is called in <see cref="OnCurrentFilePathChanged(string?)"/>.
    /// </para>
    /// </remarks>
    private void UpdateStatusText()
    {
        StatusText = !string.IsNullOrEmpty(CurrentFilePath) ? $"{Path.GetFileName(CurrentFilePath)}" : "No file open";
    }

    /// <summary>
    /// Refreshes the list of recent files by retrieving the latest entries from the file service.
    /// </summary>
    /// <remarks>
    /// Creates <see cref="RecentFileItem"/> instances that wrap both the file path and the
    /// <see cref="OpenRecentFileCommand"/>, enabling proper command binding in the menu template.
    /// Raises property change notifications for <see cref="HasRecentFiles"/> and notifies
    /// <see cref="ClearRecentFilesCommand"/> to update its enabled state.
    /// </remarks>
    private void UpdateRecentFiles()
    {
        RecentFiles.Clear();
        foreach (string filePath in _fileService.GetRecentFiles())
        {
            RecentFiles.Add(new RecentFileItem(filePath, OpenRecentFileCommand));
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        ClearRecentFilesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Displays an error message dialog to the user asynchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method ensures thread safety by executing all UI operations on the UI thread.
    /// If already on the UI thread, the operations execute directly. If called from a background
    /// thread, the operations are marshaled to the UI thread via <see cref="Dispatcher.InvokeAsync(Func{Task})"/>.
    /// </para>
    /// <para>
    /// If the main application window is not available, the dialog will not be shown. The dialog
    /// uses a standard error icon and is intended for user-facing error notifications.
    /// </para>
    /// </remarks>
    /// <param name="message">The error message text to display in the dialog. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation of showing the error message dialog.</returns>
    private Task ShowErrorMessageAsync(string message)
    {
        // Ensure we're on the UI thread since this method accesses UI elements
        if (Dispatcher.UIThread.CheckAccess())
        {
            return ShowErrorMessageCoreAsync(message, _logger, _dialogFactory);
        }

        // Enforce the correct overload resolution for InvokeAsync to avoid ambiguity by casting to Func<Task>
        return Dispatcher.UIThread.InvokeAsync(
            (Func<Task>)(() => ShowErrorMessageCoreAsync(message, _logger, _dialogFactory)));

        static async Task ShowErrorMessageCoreAsync(string msg, ILogger logger, IDialogFactory dialogFactory)
        {
            try
            {
                Window? mainWindow = GetParentWindow();
                if (mainWindow is null)
                {
                    return;
                }

                MessageDialogViewModel messageViewModel = dialogFactory.CreateViewModel<MessageDialogViewModel>();
                messageViewModel.Title = "Error";
                messageViewModel.Message = msg;
                messageViewModel.IconData = "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16"; // Error icon
                messageViewModel.IconColor = Avalonia.Media.Brushes.Red;

                MessageDialog messageDialog = new MessageDialog
                {
                    DataContext = messageViewModel
                };

                await messageDialog.ShowDialog(mainWindow);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to show error message");
            }
        }
    }

    #endregion File Open/Save

    #region Relay Commands

    /// <summary>
    /// Asynchronously renders the diagram text using the configured renderer.
    /// </summary>
    /// <remarks>This method clears any previous errors before rendering. The rendering process may require
    /// access to the UI context, so it does not use <see cref="Task.ConfigureAwait(bool)"/>. Ensure that the <see
    /// cref="CanExecuteRender"/> method returns <see langword="true"/> before invoking this command.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanExecuteRender))]
    private async Task RenderAsync()
    {
        Diagram.LastError = null;

        // NO ConfigureAwait(false) here - Diagram.RenderAsync needs UI context
        await Diagram.RenderAsync(Editor.Text);
    }

    /// <summary>
    /// Clears the diagram text, resets the editor selection and caret position, and removes the last error.
    /// </summary>
    /// <remarks>This method updates several UI-related properties and invokes the renderer to clear the
    /// diagram.  It must be executed on the UI thread to ensure proper synchronization with the user
    /// interface.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanExecuteClear))]
    private async Task ClearAsync()
    {
        // Display a confirmation dialog before clearing
        try
        {
            Window? mainWindow = GetParentWindow();
            if (mainWindow is null)
            {
                return;
            }

            ConfirmationDialogViewModel confirmViewModel = _dialogFactory.CreateViewModel<ConfirmationDialogViewModel>();
            confirmViewModel.ShowCancelButton = false;
            confirmViewModel.Title = "Clear Editor?";
            confirmViewModel.Message = "Are you sure you want to clear the source code and diagram? To undo your changes, click Edit, then Undo.";
            confirmViewModel.IconData = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M11,7V13H13V7H11M11,15V17H13V15H11Z"; // Warning icon
            confirmViewModel.IconColor = Avalonia.Media.Brushes.Orange;
            ConfirmationDialog confirmDialog = new ConfirmationDialog { DataContext = confirmViewModel };
            ConfirmationResult result = await confirmDialog.ShowDialog<ConfirmationResult>(mainWindow);
            if (result == ConfirmationResult.Yes)
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    await ClearCoreAsync();
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(ClearCoreAsync);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show confirmation dialog");
        }
    }

    /// <summary>
    /// Asynchronously clears the contents of the current editor document and updates the UI to reflect the cleared
    /// state.
    /// </summary>
    /// <remarks>This method must be called on the UI thread, as it updates UI-bound properties and triggers
    /// rendering. If the editor context or document is invalid, the method logs a warning and performs no action. The
    /// operation is batched to ensure a single undo step is created. Undo functionality is preserved if the operation
    /// fails.</remarks>
    /// <returns>A task that represents the asynchronous clear operation.</returns>
    private async Task ClearCoreAsync()
    {
        // These property updates must happen on UI thread
        // Get the current editor context on-demand to ensure fresh state
        EditorContext? editorContext = Editor.GetCurrentEditorContextFunc?.Invoke();

        // Make sure the EditorContext is valid
        if (editorContext?.IsValid != true)
        {
            _logger.LogWarning("{MethodName} called with invalid editor context", nameof(ClearCoreAsync));
            return;
        }

        // Snapshot and capture the current state to avoid races if the editor changes during async operation
        TextDocument? document = editorContext.Document;
        if (document is null)
        {
            _logger.LogWarning("{MethodName} called with null Document", nameof(ClearCoreAsync));
            return;
        }

        bool isSuccess = false;
        try
        {
            // Begin document update to batch changes together so that only one undo step is created
            document.BeginUpdate();

            // Important: operate only on the TextDocument to ensure undo works correctly.
            // DO NOT set DiagramText or any other editor-related VM properties directly here!
            // The Receive(EditorTextChangedMessage message) method will take care of updating everything else.
            document.Text = string.Empty;

            // NO ConfigureAwait(false) - renderer needs UI context
            await Diagram.RenderAsync(string.Empty);

            isSuccess = true;
        }
        catch (Exception ex)
        {
            isSuccess = false;
            _logger.LogError(ex, "Failed to clear diagram");
        }
        finally
        {
            document.EndUpdateAndUndoIfFailed(isSuccess);
        }
    }

    /// <summary>
    /// Initiates the export process by displaying an export dialog to the user and performing the export operation
    /// based on the selected options.
    /// </summary>
    /// <remarks>
    /// <para>This method displays a dialog to the user for configuring export options. If the user
    /// confirms the dialog, the export operation is performed asynchronously with progress feedback. If the user
    /// cancels the dialog, the method exits without performing the export. The method ensures that all UI
    /// interactions, such as displaying dialogs, are executed on the UI thread.
    /// </para>
    /// <para>
    /// Any errors encountered during the export process are logged and reflected in the <c>LastError</c> property,
    /// which can be used to display error messages in the UI.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanExecuteExport))]
    private async Task ExportAsync()
    {
        try
        {
            Window? window = GetParentWindow();
            if (window is null)
            {
                Diagram.LastError = "Unable to access main window for export dialog";
                return;
            }

            // Create the export dialog and its view model using DI
            ExportDialogViewModel exportViewModel = _dialogFactory.CreateViewModel<ExportDialogViewModel>();

            // Create the dialog with the storage provider
            ExportDialog exportDialog = new ExportDialog
            {
                DataContext = exportViewModel
            };

            // NO ConfigureAwait(false) - ShowDialog must run on UI thread
            ExportOptions? exportOptions = await exportDialog.ShowDialog<ExportOptions?>(window);

            // Check if user cancelled
            if (exportOptions is null)
            {
                return; // User cancelled
            }

            // NO ConfigureAwait(false) - may show UI dialogs
            await ExportWithProgressAsync(window, exportOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");

            // Setting LastError updates UI, must be on UI thread
            Diagram.LastError = $"Export failed: {ex.Message}";
            Debug.WriteLine($"Export error: {ex}");
        }
    }

    /// <summary>
    /// Exports data to a specified file format, optionally displaying a progress dialog during the operation.
    /// </summary>
    /// <remarks>If the <see cref="ExportOptions.ShowProgress"/> property is set to <see langword="true"/> and
    /// the export format is PNG, a progress dialog is displayed to provide feedback during the export process.  The
    /// dialog allows cancellation if <see cref="ExportOptions.AllowCancellation"/> is enabled.  For other formats or
    /// when progress is not shown, the export operation runs without displaying a dialog.</remarks>
    /// <param name="window">The parent window for displaying the progress dialog, if applicable.</param>
    /// <param name="options">The export options specifying the file format, file path, and additional settings such as progress visibility
    /// and cancellation support.</param>
    /// <returns>A task representing the asynchronous export operation.</returns>
    /// <exception cref="NotSupportedException">Thrown if the specified export format is not supported.</exception>
    private async Task ExportWithProgressAsync(Window window, ExportOptions options)
    {
        try
        {
            if (options is { ShowProgress: true, Format: ExportFormat.PNG })
            {
                // Create progress dialog using DI
                ProgressDialogViewModel progressViewModel = _dialogFactory.CreateViewModel<ProgressDialogViewModel>();
                progressViewModel.Title = "Exporting PNG";
                progressViewModel.StatusMessage = "Preparing export...";

                ProgressDialog progressDialog = new ProgressDialog
                {
                    DataContext = progressViewModel
                };

                // Set up cancellation
                using CancellationTokenSource cts = new CancellationTokenSource();
                if (options.AllowCancellation)
                {
                    progressViewModel.SetCancellationTokenSource(cts);
                }

                // Create event handler that can be unsubscribed to prevent memory leaks
                void ProgressHandler(object? _, PropertyChangedEventArgs args)
                {
                    // Watch for two conditions:
                    // 1. Export completes (IsComplete becomes true)
                    // 2. User clicks Close button (CloseRequested becomes true)
                    bool shouldClose = (args.PropertyName == nameof(ProgressDialogViewModel.IsComplete) && progressViewModel.IsComplete) ||
                                       (args.PropertyName == nameof(ProgressDialogViewModel.CloseRequested) && progressViewModel.CloseRequested);

                    if (!shouldClose)
                    {
                        return;
                    }

                    // Unsubscribe to prevent memory leaks
                    progressViewModel.PropertyChanged -= ProgressHandler;

                    // Capture dialog reference locally to prevent closure memory leak
                    ProgressDialog localDialog = progressDialog;

                    // Close dialog on UI thread - fire and forget
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (localDialog.IsVisible)
                            {
                                localDialog.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to close progress dialog: {ex}");
                        }
                    });
                }

                // Subscribe to property changes
                progressViewModel.PropertyChanged += ProgressHandler;

                // Start the progress dialog and track it for cleanup
                Task dialogTask = progressDialog.ShowDialog(window);

                // Small delay to ensure dialog is rendered before starting export
                // This prevents race conditions where export completes before dialog is visible
                await Task.Delay(100, cts.Token);

                try
                {
                    // Start export - ExportPngAsync manages its own threading
                    // It runs on UI thread for WebView access, then background for PNG conversion
                    await _exportService.ExportPngAsync(
                        options.FilePath,
                        options.PngOptions,
                        progressViewModel,
                        cts.Token);

                    // If export succeeded, wait for user to click Close button
                    // The dialog will close when IsComplete is set and user clicks Close
                }
                catch (OperationCanceledException)
                {
                    // User cancelled the export - unsubscribe and close dialog
                    progressViewModel.PropertyChanged -= ProgressHandler;

                    // Capture dialog reference locally
                    ProgressDialog localDialog = progressDialog;
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (localDialog.IsVisible)
                            {
                                localDialog.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to close progress dialog after cancellation: {ex}");
                        }
                    });
                    throw; // Re-throw to be caught by outer catch
                }
                catch (Exception outerEx)
                {
                    _logger.LogError(outerEx, "Export failed during PNG export with progress");

                    // Export failed - unsubscribe and close dialog
                    progressViewModel.PropertyChanged -= ProgressHandler;

                    // Capture dialog reference locally
                    ProgressDialog localDialog = progressDialog;
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (localDialog.IsVisible)
                            {
                                localDialog.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error awaiting progress dialog task");
                            Debug.WriteLine($"Failed to close progress dialog after error: {ex}");
                        }
                    });
                    throw; // Re-throw to be caught by outer catch
                }
                finally
                {
                    // Ensure we await the dialog task for proper cleanup
                    try
                    {
                        await dialogTask;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error awaiting progress dialog task during cleanup");
                        Debug.WriteLine($"Dialog task completed with error: {ex}");
                    }
                }
            }
            else
            {
                switch (options.Format)
                {
                    case ExportFormat.PNG:
                        // Export PNG without progress dialog
                        await _exportService.ExportPngAsync(options.FilePath, options.PngOptions);
                        break;

                    case ExportFormat.SVG:
                        // Export SVG (no progress needed)
                        await _exportService.ExportSvgAsync(options.FilePath, options.SvgOptions);
                        break;

                    default:
                        throw new NotSupportedException($"Export format {options.Format} is not supported");
                }
            }

            // NO ConfigureAwait(false) - will show UI dialog
            await ShowSuccessMessageAsync(window, $"Export completed successfully to:{Environment.NewLine}{options.FilePath}");
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Export cancelled by user");
            // Don't show error for user cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");

            // Setting LastError updates UI, must be on UI thread
            string errorMessage = $"Export failed: {ex.Message}";
            await ShowErrorAsync(errorMessage);
        }
    }

    /// <summary>
    /// Opens the directory containing the current file in the system's file explorer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method handles both regular file paths and root directory paths (e.g., "C:\file.txt" or "C:\").
    /// For root directory files, it opens the root directory itself.
    /// </para>
    /// <para>
    /// The operation is cross-platform compatible and uses the system's default file explorer.
    /// If the directory does not exist or is invalid, an error dialog is displayed to the user.
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanExecuteOpenFileLocation))]
    private async Task OpenFileLocationAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            return;
        }

        // Get the directory path, handling both regular paths and root directories
        string? directory = Path.GetDirectoryName(CurrentFilePath);

        // For root directory files (e.g., "C:\file.txt"), GetDirectoryName returns "C:\"
        // For root directory itself (e.g., "C:\"), GetDirectoryName returns empty string
        // In the latter case, use GetPathRoot to get "C:\"
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.GetPathRoot(CurrentFilePath);
        }

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            await ShowErrorMessageAsync($"Cannot open file location - directory does not exist or is invalid: {CurrentFilePath}");
            return;
        }

        try
        {
            // Use ShellExecute to open the directory of the current file in the default file explorer
            using Process? process = Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file location: {Directory}", directory);
            await ShowErrorMessageAsync($"Failed to open file location: {ex.Message}");
        }
    }

    #endregion Relay Commands

    /// <summary>
    /// Displays an error message to the user asynchronously and updates the diagram's last error state.
    /// </summary>
    /// <remarks>This method ensures that the error message is shown on the UI thread. It also sets the
    /// diagram's last error to the specified message before displaying it.</remarks>
    /// <param name="message">The error message to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ShowErrorAsync(string message)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Diagram.LastError = message;
            await ShowErrorMessageAsync(message);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Diagram.LastError = message;
            return ShowErrorMessageAsync(message);
        });
    }

    /// <summary>
    /// Displays a success message dialog with the specified message and a checkmark icon.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method ensures thread safety by executing all UI operations on the UI thread.
    /// If already on the UI thread, the operations execute directly. If called from a background
    /// thread, the operations are marshaled to the UI thread via <see cref="Dispatcher.InvokeAsync(Func{Task})"/>.
    /// </para>
    /// <para>
    /// The dialog includes a title, a success message, and a green checkmark icon.
    /// </para>
    /// </remarks>
    /// <param name="window">The parent window that owns the dialog. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="message">The message to display in the dialog. This parameter cannot be <see langword="null"/> or empty.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task ShowSuccessMessageAsync(Window window, string message)
    {
        // Ensure we're on the UI thread since this method accesses UI elements
        if (Dispatcher.UIThread.CheckAccess())
        {
            return ShowSuccessMessageCoreAsync(window, message, _logger, _dialogFactory);
        }

        // Enforce the correct overload resolution for InvokeAsync to avoid ambiguity by casting to Func<Task>
        return Dispatcher.UIThread.InvokeAsync(
            (Func<Task>)(() => ShowSuccessMessageCoreAsync(window, message, _logger, _dialogFactory)));

        static async Task ShowSuccessMessageCoreAsync(Window window, string message, ILogger logger, IDialogFactory dialogFactory)
        {
            try
            {
                MessageDialogViewModel messageViewModel = dialogFactory.CreateViewModel<MessageDialogViewModel>();
                messageViewModel.Title = "Export Complete";
                messageViewModel.Message = message;
                messageViewModel.IconData = "M9 12l2 2 4-4"; // Checkmark icon path
                messageViewModel.IconColor = Avalonia.Media.Brushes.Green;

                MessageDialog messageDialog = new MessageDialog
                {
                    DataContext = messageViewModel
                };

                // NO ConfigureAwait(false) - ShowDialog needs UI thread
                await messageDialog.ShowDialog(window);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to show success message");
            }
        }
    }

    /// <summary>
    /// Checks for updates to the Mermaid library and updates the application state with the latest version information.
    /// </summary>
    /// <remarks>This method performs a network call to check for updates asynchronously. The application
    /// state is updated with the  bundled and latest checked Mermaid versions, ensuring that property updates occur on
    /// the UI thread.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckForMermaidUpdatesAsync()
    {
        // This CAN use ConfigureAwait(false) for the network call
        await _updateService.CheckAndUpdateAsync()
            .ConfigureAwait(false);

        // Marshal property updates back to UI thread since ObservableProperty triggers INotifyPropertyChanged
        // Use Post for fire-and-forget. These properties values are not needed immediately - so no need for InvokeAsync
        Dispatcher.UIThread.Post(() =>
        {
            BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
            LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
        });
    }

    /// <summary>
    /// Persists the current application settings and dock layout to storage synchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method updates the settings service with the current state of the application,
    /// including diagram text, live preview settings, Mermaid version information, editor selection details,
    /// and dock layout configuration. After updating the settings, the method saves them to ensure they
    /// are persisted across application sessions.
    /// </para>
    /// </remarks>
    public void Persist()
    {
        // Persist ViewModel-specific settings to AppSettings
        Editor.Persist(_settingsService.Settings);

        _settingsService.Settings.LivePreviewEnabled = LivePreviewEnabled;
        _settingsService.Settings.BundledMermaidVersion = BundledMermaidVersion;
        _settingsService.Settings.LatestCheckedMermaidVersion = LatestMermaidVersion;
        _settingsService.Settings.CurrentFilePath = CurrentFilePath;
        _settingsService.Save();

        // Save dock layout state before any floating windows are closed
        if (Layout is not null)
        {
            bool saved = _dockLayoutService.Save(Layout);
            if (!saved)
            {
                _logger.LogWarning("Failed to save dock layout state during {MethodName} method call", nameof(Persist));
            }
        }
    }

    /// <summary>
    /// Performs cleanup operations to prepare the application layout for shutdown, ensuring that all windows and
    /// resources are properly closed.
    /// </summary>
    /// <remarks>This method attempts to close the main layout using the associated close command if
    /// available. If the close command cannot be executed, it closes any floating windows as a fallback via
    /// <see cref="DockFactory.CloseAllFloatingWindows"/>. This helps prevent resource leaks and ensures a
    /// consistent shutdown state. It is recommended to call this method before application exit to avoid leaving
    /// open windows or incomplete layout states.</remarks>
    public void PrepareForShutdown()
    {
        try
        {
            if (Layout is not null)
            {
                if (Layout.Close.CanExecute(null))
                {
                    Layout.Close.Execute(null);
                }
                else
                {
                    _logger.LogWarning("Layout.Close command cannot be executed during {MethodName}. Calling {MethodName2}", nameof(PrepareForShutdown), nameof(_dockFactory.CloseAllFloatingWindows));

                    // Close any floating windows before resetting the layout
                    _dockFactory.CloseAllFloatingWindows(Layout);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {MethodName}", nameof(PrepareForShutdown));
        }
    }

    /// <summary>
    /// Releases resources used by the object and unsubscribes from property change notifications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Do NOT call this method directly. The <see cref="MainWindowViewModel"/> is owned by the dependency
    /// injection container and its <see cref="IDisposable"/> implementation will be called by the container
    /// when the object is no longer needed. This method does not dispose of dependencies managed by the
    /// dependency injection container.
    /// </para>
    /// <para>
    /// This method:
    /// <list type="bullet">
    ///     <item><description>Unregisters all message handlers from the document-scoped messenger</description></item>
    ///     <item><description>Unsubscribes from Editor, Diagram, and EditorTool PropertyChanged events</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            // Unregister all message handlers from the document-scoped messenger.
            _documentMessenger.UnregisterAll(this);

            // Unsubscribe from PropertyChanged events
            if (_dockFactory.EditorTool is not null)
            {
                _dockFactory.EditorTool.PropertyChanged -= OnEditorToolPropertyChanged;
            }

            if (_dockFactory.DiagramTool is not null)
            {
                Diagram.PropertyChanged -= OnDiagramPropertyChanged;
            }

            _isDisposed = true;
        }
    }
}
