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

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using MermaidPad.Extensions;
using MermaidPad.ViewModels.Docking;
using MermaidPad.ViewModels.UserControls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Factories;

/// <summary>
/// Factory for creating and managing the dock layout for MermaidPad.
/// Extends <see cref="Factory"/> from Dock.Model.Mvvm to provide custom layout creation.
/// </summary>
/// <remarks>
/// <para>
/// This factory is responsible for:
/// <list type="bullet">
///     <item><description>Creating the initial dock layout with editor and diagram panels</description></item>
///     <item><description>Creating tool ViewModels using the <see cref="IViewModelFactory"/></description></item>
///     <item><description>Providing a default layout when no saved layout exists</description></item>
/// </list>
/// </para>
/// <para>
/// The factory uses composition to wrap existing ViewModels (<see cref="MermaidEditorViewModel"/>
/// and <see cref="DiagramViewModel"/>) in dock-aware tool ViewModels, preserving all existing
/// functionality while adding docking capabilities.
/// </para>
/// <para>
/// <b>MDI Migration Note:</b> For MDI scenarios, this factory would create separate tool instances
/// for each document, with each document having its own editor and diagram panels. The current
/// SDI design stores references to a single EditorTool and DiagramTool.
/// </para>
/// </remarks>
//TODO - DaveBlack: MDI Migration Note: For MDI scenarios, this factory needs to create separate tool instances for each document, with each document having its own editor and diagram panels
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated via DI container.")]
[SuppressMessage("Maintainability", "S1192: String literals should not be duplicated", Justification = "Logging message template is used")]
[SuppressMessage("Maintainability", "S3267:Loops should be simplified with LINQ expressions", Justification = "This code is performance-sensitive.")]
// ReSharper disable MergeIntoPattern
internal sealed class DockFactory : Factory
{
    private readonly ILogger<DockFactory> _logger;
    private readonly IViewModelFactory _viewModelFactory;
    private readonly ObjectPool<HashSet<IDockable>> _hashSetObjectPool;

    private const string RootDockId = "RootDock";
    private const string MainProportionalDockId = "MainProportionalDock";
    private const string EditorToolDockId = "EditorToolDock";
    private const string DiagramToolDockId = "DiagramToolDock";

    /// <summary>
    /// Gets the editor tool ViewModel, created during layout initialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is available after <see cref="CreateLayout"/> or <see cref="CreateDefaultLayout"/> is called.
    /// Returns null if the layout has not been created yet.
    /// </para>
    /// <para>
    /// <b>MDI Migration Note:</b> In an MDI design, this would be replaced by an ActiveDocument pattern
    /// where the current document exposes its own EditorTool. For SDI, this direct reference is acceptable.
    /// </para>
    /// </remarks>
    //TODO - DaveBlack: MDI Migration Note: this needs to be replaced by an ActiveDocument pattern where the current document exposes its own EditorTool
    public MermaidEditorToolViewModel? EditorTool { get; private set; }

    /// <summary>
    /// Gets the diagram tool ViewModel, created during layout initialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is available after <see cref="CreateLayout"/> or <see cref="CreateDefaultLayout"/> is called.
    /// Returns null if the layout has not been created yet.
    /// </para>
    /// <para>
    /// <b>MDI Migration Note:</b> In an MDI design, this would be replaced by an ActiveDocument pattern
    /// where the current document exposes its own DiagramTool. For SDI, this direct reference is acceptable.
    /// </para>
    /// </remarks>
    //TODO - DaveBlack: MDI Migration Note: this needs to be replaced by an ActiveDocument pattern where the current document exposes its own DiagramTool
    public DiagramToolViewModel? DiagramTool { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DockFactory"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for this factory.</param>
    /// <param name="viewModelFactory">The factory for creating ViewModel instances.</param>
    /// <param name="hashSetObjectPool">The object pool for managing <see cref="HashSet{T}"/> instances.</param>
    public DockFactory(ILogger<DockFactory> logger, IViewModelFactory viewModelFactory, ObjectPool<HashSet<IDockable>> hashSetObjectPool)
    {
        _logger = logger;
        _viewModelFactory = viewModelFactory;
        _hashSetObjectPool = hashSetObjectPool;

        // Configure locators immediately in constructor so they're available
        // before any deserialization occurs
        ConfigureLocators();
    }

    /// <summary>
    /// Configures the locators that the Dock framework uses to create and initialize
    /// dockable instances during layout creation and restoration from serialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method must be called before any layout deserialization occurs, as the serializer
    /// uses these locators to recreate dockable instances. It is called automatically in the
    /// constructor.
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///         <description>
    ///             <b>DockableLocator:</b> Maps dockable IDs to factory methods that create dockable instances.
    ///             The serializer uses this when it encounters a dockable type that cannot be instantiated
    ///             via <c>Activator.CreateInstance()</c> (e.g., types with constructor dependencies).
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             <b>ContextLocator:</b> Maps dockable IDs to factory methods that provide the <c>Context</c>
    ///             (DataContext/ViewModel) for each dockable. This separates the docking infrastructure
    ///             from the business logic ViewModels.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             <b>HostWindowLocator:</b> Maps window IDs to factory methods that create host windows
    ///             for floating dockables.
    ///         </description>
    ///     </item>
    /// </list>
    /// </remarks>
    private void ConfigureLocators()
    {
        _logger.LogDebug("Configuring Dock locators");

        // Configure DockableLocator - maps dockable IDs to factory methods.
        // The Dock serializer uses this to recreate dockables by ID when deserializing layouts.
        // This is required because our tool ViewModels have constructor dependencies and cannot
        // be instantiated via Activator.CreateInstance().
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            [MermaidEditorToolViewModel.ToolId] = () =>
            {
                _logger.LogDebug("{Locator} creating {ViewModel}", nameof(DockableLocator), nameof(MermaidEditorToolViewModel));
                MermaidEditorViewModel editorViewModel = _viewModelFactory.Create<MermaidEditorViewModel>();
                MermaidEditorToolViewModel tool = _viewModelFactory.Create<MermaidEditorToolViewModel>(editorViewModel);

                Debug.Assert(EditorTool is null, "Overwriting existing EditorTool in DockableLocator!!!");

                tool.Factory = this;
                EditorTool = tool;
                return tool;
            },
            [DiagramToolViewModel.ToolId] = () =>
            {
                _logger.LogDebug("{Locator} creating {ViewModel}", nameof(DockableLocator), nameof(DiagramToolViewModel));
                DiagramViewModel diagramViewModel = _viewModelFactory.Create<DiagramViewModel>();
                DiagramToolViewModel tool = _viewModelFactory.Create<DiagramToolViewModel>(diagramViewModel);

                Debug.Assert(DiagramTool is null, "Overwriting existing DiagramTool in DockableLocator!!!");

                tool.Factory = this;
                DiagramTool = tool;
                return tool;
            }
        };

        // Configure ContextLocator - maps dockable IDs to context (ViewModel) factory methods.
        // The Dock framework assigns dockable.Context from this locator during InitDockable
        // when the dockable's Context is null. This separates docking structure from business logic.
        // Note: Our tool ViewModels already contain their wrapped ViewModels (Editor/Diagram),
        // so we return those as the Context for data binding in the views.
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            [MermaidEditorToolViewModel.ToolId] = () =>
            {
                // EditorTool may be null if layout is being deserialized before the tool is created, or if the tool is hidden, pinned, or floated
                if (EditorTool is null)
                {
                    _logger.LogWarning("{Locator} requested context for {DockableId} but {ToolName} is null", nameof(ContextLocator), MermaidEditorToolViewModel.ToolId, nameof(EditorTool));
                }
                else
                {
                    _logger.LogDebug("{Locator} found {ToolName} for context of {DockableId}", nameof(ContextLocator), nameof(EditorTool), MermaidEditorToolViewModel.ToolId);
                }

                // Return the EditorTool itself as context - the view binds to EditorTool.Editor
                return EditorTool;
            },
            [DiagramToolViewModel.ToolId] = () =>
            {
                // DiagramTool may be null if layout is being deserialized before the tool is created, or if the tool is hidden, pinned, or floated
                if (DiagramTool is null)
                {
                    _logger.LogWarning("{Locator} requested context for {DockableId} but {ToolName} is null", nameof(ContextLocator), DiagramToolViewModel.ToolId, nameof(DiagramTool));
                }
                else
                {
                    _logger.LogDebug("{Locator} found {ToolName} for context of {DockableId}", nameof(ContextLocator), nameof(DiagramTool), DiagramToolViewModel.ToolId);
                }

                // Return the DiagramTool itself as context - the view binds to DiagramTool.Diagram
                return DiagramTool;
            }
        };

        // Configure DefaultHostWindowLocator - provides host windows for floating dockables.
        // This is required for floating windows to work correctly.
        DefaultHostWindowLocator = () =>
        {
            _logger.LogDebug("{Locator} creating {Host}", nameof(DefaultHostWindowLocator), nameof(HostWindow));
            return new HostWindow();
        };

        // Configure HostWindowLocator - provides host windows for floating dockables.
        // This is required for floating windows to work correctly.
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () =>
            {
                _logger.LogDebug("{Locator} creating {Host}", nameof(HostWindowLocator), nameof(HostWindow));
                return new HostWindow();
            }
        };

        _logger.LogDebug("Dock locators configured");
    }

    #region Overrides

    /// <summary>
    /// Creates the default dock layout with side-by-side editor and diagram panels.
    /// </summary>
    /// <returns>
    /// An <see cref="IRootDock"/> representing the root of the dock layout hierarchy.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is called by the Dock framework. It always creates a fresh default layout
    /// with new tool ViewModels. For restoring saved layouts, use the <see cref="Services.DockLayoutService"/>.
    /// </para>
    /// <para>
    /// The default layout consists of:
    /// <list type="bullet">
    ///     <item><description>A <see cref="RootDock"/> as the root container</description></item>
    ///     <item><description>A <see cref="ProportionalDock"/> with horizontal orientation</description></item>
    ///     <item><description>Two <see cref="ToolDock"/> containers, one for each tool</description></item>
    ///     <item><description>A <see cref="ProportionalDockSplitter"/> between the tools</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The initial proportions are set to 50/50 (equal width for both panels).
    /// </para>
    /// </remarks>
    public override IRootDock CreateLayout() => CreateDefaultLayout();

    /// <summary>
    /// Initializes the dock layout after creation or deserialization.
    /// </summary>
    /// <param name="layout">The layout to initialize.</param>
    /// <remarks>
    /// <para>
    /// This method calls the base <see cref="Factory.InitLayout(IDockable)"/> which triggers
    /// <c>InitDockable</c> for all dockables in the layout. The locators (DockableLocator,
    /// ContextLocator, HostWindowLocator) are configured in the constructor via
    /// <see cref="ConfigureLocators"/> so they are available before any deserialization occurs.
    /// </para>
    /// <para>
    /// After base initialization, this method searches for tool ViewModels in the layout hierarchy
    /// to cache references in <see cref="EditorTool"/> and <see cref="DiagramTool"/>.
    /// </para>
    /// </remarks>
    public override void InitLayout(IDockable layout)
    {
        _logger.LogInformation("Initializing dock layout");

        // DEBUG: Dump hierarchy BEFORE InitLayout to see what changed
        //_logger.LogDebug("=== BEFORE base.InitLayout() + FindToolsInLayout - Initial hierarchy ===");
        //DumpHierarchy(layout, depth: 0);
        //_logger.LogDebug("=== END hierarchy dump ===");
        //DumpDockMetadata(rootDock);

        // Capture references to existing tools BEFORE base.InitLayout() may replace them.
        // During deserialization, the DockableLocator lambdas may create new tool instances,
        // which would overwrite EditorTool/DiagramTool. We need to dispose the old ones afterward.
        MermaidEditorToolViewModel? previousEditorTool = EditorTool;
        _ = DiagramTool; // Capture for future IDisposable implementation (currently unused)

        // https://github.com/wieslawsoltes/Dock/blob/master/docs/dock-advanced.md?plain=1#L21-L36 says: to configure locators here BUT this method is called AFTER deserialization
        //TODO - DaveBlack: review whether locators need to be re-configured here
        ConfigureLocators();

        // Call base initialization - this triggers InitDockable for all dockables in the layout,
        // which uses the locators configured above. New tools may be created via DockableLocator.
        base.InitLayout(layout);

        IRootDock? rootDock = layout as IRootDock;
        if (rootDock is null)
        {
            rootDock = FindRoot(layout);
            if (rootDock is null)
            {
                _logger.LogError("RootDock not found in layout during initialization.");
                throw new InvalidOperationException("RootDock not found in layout during initialization.");
            }
        }

        // After layout initialization, try to find our tools if they were restored from serialization
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (EditorTool is null || DiagramTool is null)
        {
            // We can't use the Find with the following Find method signature:
            //      Find(Func<IDockable, bool> predicate)
            //
            // because it iterates on the DockControl and the DockControl has not yet been
            // assigned to the Factory at this point. That doesn't happen until the DockControl's
            // OnAttachedToVisualTree is called, which is well after InitLayout completes.
            List<IDockable> dockables = FindAllDockables(rootDock, HostWindows).ToList();
            EditorTool ??= dockables.Find(static dockable => dockable.Id == MermaidEditorToolViewModel.ToolId) as MermaidEditorToolViewModel;
            DiagramTool ??= dockables.Find(static dockable => dockable.Id == DiagramToolViewModel.ToolId) as DiagramToolViewModel;
        }
        stopwatch.Stop();
        _logger.LogTiming(nameof(FindAllDockables), stopwatch.Elapsed, true);

        // Dispose stale tools AFTER base initialization completes and new tools are assigned.
        // This prevents race conditions where base.InitLayout() might access null properties.
        // Only dispose if the tool was actually replaced (reference changed).
        if (previousEditorTool is not null && !ReferenceEquals(previousEditorTool, EditorTool))
        {
            _logger.LogDebug("Disposing stale {ToolName} after layout reinitialization", nameof(EditorTool));
            previousEditorTool.Dispose();
        }

        // DiagramTool doesn't implement IDisposable yet, but ready for future
        // if (previousDiagramTool is IDisposable disposableDiagram && !ReferenceEquals(previousDiagramTool, DiagramTool))
        // {
        //     _logger.LogDebug("Disposing stale {ToolName} after layout reinitialization", nameof(DiagramTool));
        //     disposableDiagram.Dispose();
        // }

        if (EditorTool is null)
        {
            _logger.LogInformation("{ToolName} not found in layout during initialization. It will be recreated by the locator when needed.", nameof(EditorTool));
        }

        if (DiagramTool is null)
        {
            _logger.LogInformation("{ToolName} not found in layout during initialization. It will be recreated by the locator when needed.", nameof(DiagramTool));
        }

        // DEBUG: Dump hierarchy AFTER InitLayout to see what changed
        //_logger.LogDebug("=== AFTER base.InitLayout() + FindToolsInLayout - Final hierarchy ===");
        //DumpHierarchy(layout, depth: 0);
        //_logger.LogDebug("=== END hierarchy dump ===");
        //DumpDockMetadata(rootDock);

        _logger.LogInformation("Dock layout initialized");
    }

    /// <summary>
    /// Creates a fresh default layout with new tool ViewModels and default proportions.
    /// </summary>
    /// <returns>An <see cref="IRootDock"/> representing the default dock layout.</returns>
    /// <remarks>
    /// <para>
    /// This method always creates new tool ViewModels and a fresh layout structure
    /// with default 50/50 proportions. It is called by <see cref="CreateLayout"/>
    /// and can also be called directly to reset the layout to defaults.
    /// </para>
    /// <para>
    /// After calling this method, the <see cref="EditorTool"/> and <see cref="DiagramTool"/>
    /// properties will contain the newly created tool ViewModels.
    /// </para>
    /// </remarks>
    public IRootDock CreateDefaultLayout()
    {
        _logger.LogInformation("Creating default dock layout");

        // Dispose existing tools before creating new ones to prevent memory leaks.
        // This is important because the tools may have event subscriptions (e.g., UndoStack.PropertyChanged)
        // that need to be cleaned up.
        DisposeExistingToolViewModels();

        // Create the tool ViewModels using the ViewModelFactory pattern
        MermaidEditorViewModel editorViewModel = _viewModelFactory.Create<MermaidEditorViewModel>();
        DiagramViewModel diagramViewModel = _viewModelFactory.Create<DiagramViewModel>();

        // Wrap in dock-aware tool ViewModels
        EditorTool = _viewModelFactory.Create<MermaidEditorToolViewModel>(editorViewModel);
        DiagramTool = _viewModelFactory.Create<DiagramToolViewModel>(diagramViewModel);

        EditorTool.Factory = this;
        DiagramTool.Factory = this;

        // IMPORTANT: Use factory methods (CreateToolDock, CreateProportionalDock, CreateRootDock) instead of
        // direct instantiation (new ToolDock, new ProportionalDock, new RootDock). The factory methods set up
        // internal relationships (e.g., Window.Layout) that the Dock framework relies on during drag-drop
        // operations and serialization. Direct instantiation causes Window.Layout to get out of sync with
        // RootDock.VisibleDockables when dockables are moved, leading to panels disappearing after app restart.
        // NOTE: There can only be 1 ActiveDockable per ToolDock, so we set it for each tool dock below (except RootDock).
        // Create tool docks with default 35/65 proportions
        //TODO - DaveBlack: change the proportions once I add the new panel for AI
        IToolDock editorToolDock = CreateToolDock();
        editorToolDock.Factory = this;
        editorToolDock.Id = EditorToolDockId;
        editorToolDock.Title = "Editor";
        editorToolDock.Proportion = 0.35;
        editorToolDock.VisibleDockables = CreateList<IDockable>(EditorTool);
        editorToolDock.ActiveDockable = EditorTool;
        editorToolDock.CanClose = false;
        editorToolDock.CanPin = true;
        editorToolDock.CanFloat = false;

        IToolDock diagramToolDock = CreateToolDock();
        diagramToolDock.Factory = this;
        diagramToolDock.Id = DiagramToolDockId;
        diagramToolDock.Title = "Diagram";
        diagramToolDock.Proportion = 0.65;
        diagramToolDock.VisibleDockables = CreateList<IDockable>(DiagramTool);
        diagramToolDock.ActiveDockable = DiagramTool;
        diagramToolDock.CanClose = false;
        diagramToolDock.CanPin = true;
        diagramToolDock.CanFloat = false;

        // Create a proportional dock to hold both tool docks (horizontal layout)
        IProportionalDockSplitter splitter = CreateProportionalDockSplitter();
        splitter.Id = "MainSplitter";
        splitter.Title = "Splitter";
        splitter.Factory = this;
        splitter.CanClose = false;
        splitter.CanDrag = false;
        splitter.IsCollapsable = false;
        splitter.CanResize = true;
        splitter.CanFloat = false;
        splitter.ResizePreview = false;  // when false, show live preview while resizing (versus a drag indicator)

        IProportionalDock mainLayout = CreateProportionalDock();
        mainLayout.Factory = this;
        mainLayout.Id = MainProportionalDockId;
        mainLayout.Title = "Main";
        mainLayout.CanCloseLastDockable = false;
        mainLayout.CanClose = false;
        mainLayout.CanDrag = false;
        mainLayout.CanDrop = false;
        mainLayout.CanFloat = false;
        mainLayout.IsCollapsable = false;       // only when this is false can we re-dock floated tool docks when BOTH are floated
        mainLayout.Orientation = Orientation.Horizontal;
        mainLayout.VisibleDockables = CreateList<IDockable>(
            editorToolDock,
            splitter,
            diagramToolDock
        );
        mainLayout.EnableGlobalDocking = true; // allow dragging dockables between tool docks (Dock enables this by default)
        //mainLayout.KeepPinnedDockableVisible = true; // ensure pinned dockables remain visible when other dockables are added/removed

        // https://github.com/wieslawsoltes/Dock/blob/master/docfx/articles/dock-manager-guide.md#dock-target-visibility

        // Create the root dock using the factory method
        // CreateRootDock() is a base class method that properly initializes internal relationships
        // CreateRootDock() also creates non-null, empty collections for all PinnedDockables (Left, Right, Top, Bottom)
        IRootDock rootDock = CreateRootDock();
        rootDock.Factory = this;
        rootDock.Id = RootDockId;
        rootDock.Title = "Root";
        rootDock.CanCloseLastDockable = false;
        rootDock.CanClose = false;
        rootDock.CanDrag = false;
        rootDock.CanDrop = false;
        rootDock.CanFloat = false;
        rootDock.IsCollapsable = false;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        // WARNING: DO NOT set RootDock's DefaultDockable to anything.
        // Doing so breaks initial display and the drag-drop of dockables between ToolDocks

        //TODO - DaveBlack: create something to set the active, focused dockable on app startup for default layout settings
        // we can't do it here because the Owner window isn't assigned to the IDockable yet. We need to figure out
        // where the best place is to do this after the layout is fully initialized and the DockControl is attached to the visual tree.
        // We have to make sure it only happens once, not every time the layout is restored.
        //      1. Set the active and focused dockables to the editor by default
        //      2. Floating windows should NOT appear behind the main window on startup
        //SetActiveDockable(EditorTool);
        //SetFocusedDockable(someRootDock, EditorTool);

        _logger.LogInformation("Default dock layout created with {EditorProportion}%/{DiagramProportion}% {Orientation} split", editorToolDock.Proportion * 100, diagramToolDock.Proportion * 100, mainLayout.Orientation);

        return rootDock;
    }

    public override void OnDockableActivated(IDockable? dockable)
    {
        _logger.LogDebug("Dockable activated: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableActivated(dockable);
    }

    public override void OnDockableClosed(IDockable? dockable)
    {
        _logger.LogDebug("Dockable closed: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableClosed(dockable);
    }

    public override void OnDockableDeactivated(IDockable? dockable)
    {
        _logger.LogDebug("Dockable deactivated: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableDeactivated(dockable);
    }

    public override void OnDockableDocked(IDockable? dockable, DockOperation operation)
    {
        _logger.LogDebug("Dockable docked: {DockableId}, {Title}, Operation: {Operation}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>", operation);
        base.OnDockableDocked(dockable, operation);
    }

    public override void OnDockableHidden(IDockable? dockable)
    {
        _logger.LogDebug("Dockable hidden: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableHidden(dockable);
    }

    public override void OnDockableInit(IDockable? dockable)
    {
        _logger.LogDebug("Dockable initialized: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableInit(dockable);
    }

    public override void OnDockableMoved(IDockable? dockable)
    {
        _logger.LogDebug("Dockable moved: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableMoved(dockable);
    }

    public override void OnDockablePinned(IDockable? dockable)
    {
        _logger.LogDebug("Dockable pinned: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockablePinned(dockable);
    }

    public override void OnDockableRemoved(IDockable? dockable)
    {
        _logger.LogDebug("Dockable removed: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableRemoved(dockable);
    }

    public override void OnDockableRestored(IDockable? dockable)
    {
        _logger.LogDebug("Dockable restored: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableRestored(dockable);
    }

    public override void OnDockableSwapped(IDockable? dockable)
    {
        _logger.LogDebug("Dockable swapped: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableSwapped(dockable);
    }

    public override void OnDockableUndocked(IDockable? dockable, DockOperation operation)
    {
        _logger.LogDebug("Dockable undocked: {DockableId}, {Title}, Operation: {Operation}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>", operation);
        base.OnDockableUndocked(dockable, operation);
    }

    public override void OnDockableUnpinned(IDockable? dockable)
    {
        _logger.LogDebug("Dockable unpinned: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableUnpinned(dockable);
    }

    public override void OnDockableAdded(IDockable? dockable)
    {
        _logger.LogDebug("Dockable added: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnDockableAdded(dockable);
    }

    public override void OnActiveDockableChanged(IDockable? dockable)
    {
        _logger.LogDebug("Active dockable changed: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnActiveDockableChanged(dockable);
    }

    public override void OnFocusedDockableChanged(IDockable? dockable)
    {
        _logger.LogDebug("Focused dockable changed: {DockableId}, {Title}", dockable?.Id ?? "<null>", dockable?.Title ?? "<null>");
        base.OnFocusedDockableChanged(dockable);
    }

    /// <summary>
    /// Handles the activation event for a dock window.
    /// </summary>
    /// <param name="window">The dock window that has been activated. Can be null if no window is currently active.</param>
    public override void OnWindowActivated(IDockWindow? window)
    {
        base.OnWindowActivated(window);
        _logger.LogDebug("Window activated: {WindowId}", window?.Id ?? "<null>");
    }

    /// <summary>
    /// Handles the event that occurs when a dock window is deactivated.
    /// </summary>
    /// <param name="window">The dock window that was deactivated. Can be null if the window is not specified.</param>
    public override void OnWindowDeactivated(IDockWindow? window)
    {
        base.OnWindowDeactivated(window);
        _logger.LogDebug("Window deactivated: {WindowId}", window?.Id ?? "<null>");
    }

    /// <summary>
    /// Handles the event when a dock window is closed.
    /// </summary>
    /// <param name="window">The dock window instance that was closed. Can be null if the window reference is unavailable.</param>
    public override void OnWindowClosed(IDockWindow? window)
    {
        base.OnWindowClosed(window);
        _logger.LogDebug("Window closed: {WindowId}", window?.Id ?? "<null>");
    }

    /// <summary>
    /// Handles the event that occurs when a dock window is opened.
    /// </summary>
    /// <param name="window">The window that was opened. Can be null if the window reference is unavailable.</param>
    public override void OnWindowOpened(IDockWindow? window)
    {
        base.OnWindowOpened(window);
        _logger.LogDebug("Window opened: {WindowId}", window?.Id ?? "<null>");
    }

    /// <summary>
    /// Handles the removal of a dock window from the layout.
    /// </summary>
    /// <remarks>This method is called when a dock window is removed from the layout. It can be overridden to
    /// perform custom logic when windows are removed.</remarks>
    /// <param name="window">The window that was removed. Can be null if the window reference is unavailable.</param>
    public override void OnWindowRemoved(IDockWindow? window)
    {
        base.OnWindowRemoved(window);
        _logger.LogDebug("Window removed: {WindowId}", window?.Id ?? "<null>");
    }

    #endregion Overrides

    /// <summary>
    /// Enumerates all dockable elements reachable from the specified root dock and any floating windows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs a depth-first traversal of the dock hierarchy, including dockables that
    /// may only be accessible through floating windows. Each dockable is returned only once, even if it is referenced
    /// multiple times in the hierarchy. Cyclic references in the dock structure are handled safely.
    /// </para>
    /// <para>
    /// This method is called after layout initialization to ensure we have references to the tool ViewModels.
    /// </para>
    /// <para>
    /// The search includes:
    /// <list type="bullet">
    ///     <item><description><c>VisibleDockables</c> - Normal docked panels</description></item>
    ///     <item><description><c>LeftPinnedDockables</c>, <c>RightPinnedDockables</c>, <c>BottomPinnedDockables</c>, <c>TopPinnedDockables</c>
    ///     - Auto-hide/pinned panels</description></item>
    ///     <item><description><c>ActiveDockable</c> - Currently active dockable (may be in a floating window)</description></item>
    ///     <item><description><c>DefaultDockable</c> - Default dockable (may be in a floating window)</description></item>
    ///     <item><description><c>FocusedDockable</c> - Currently focused dockable (maybe in a floating window)</description></item>
    ///     <item><description><c>FocusedDockable.Owner</c> chain - To find floating window containers</description></item>
    ///     <item><description><c>Window.Layout</c> - Floating window layouts</description></item>
    ///     <item><description><c>Windows.Layout</c> - All floating windows layouts</description></item>
    ///     <item><description><c>HostWindows</c> collection - All floating windows managed by the factory</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Floating Window Support:</b> When tools are floated (moved to separate windows), they exist
    /// in the <see cref="Factory.HostWindows"/> collection rather than in the main layout hierarchy.
    /// This method explicitly searches all floating windows to find tools that were floated before
    /// the layout was saved.
    /// </para>
    /// <para>
    /// <b>Performance:</b> It uses an iterative depth-first search with a stack to avoid recursion overhead.
    /// Uses a HashSet for cycle detection to handle circular references in the dock hierarchy.
    /// </para>
    /// </remarks>
    /// <param name="root">The <see cref="IRootDock" /> dock from which to begin the traversal.
    /// If null, no dockables are returned.</param>
    /// <param name="hostWindows">A collection of host windows whose layouts may contain additional
    /// floating dockables to include in the  enumeration.</param>
    /// <returns>An enumerable collection of all unique dockable elements found in the dock hierarchy
    /// and floating windows. The collection is empty if no dockables are found.
    /// The enumeration order is not guaranteed.</returns>
    private static IEnumerable<IDockable> FindAllDockables(IRootDock? root, IList<IHostWindow> hostWindows)
    {
        if (root is null)
        {
            yield break;
        }

        // NOTE: Always seed the traversal via AddDockable (not stack.Push directly).
        // AddDockable is responsible for both pushing and marking items as visited,
        // so everything that ever enters the stack has already been checked against
        // the visited set. This keeps the DFS invariant simple: no item is ever
        // pushed twice, including the root, and the main loop never has to re-check
        // for duplicates when popping.

        // Use stack-based iteration instead of recursion to avoid stack frame overhead
        // Initial capacity of 16 to account for pinned dockables collections and floating windows
        Stack<IDockable> stack = new Stack<IDockable>(capacity: 16);

        // Track visited nodes to prevent infinite loops from circular references
        // The dock hierarchy can have cycles (e.g., Window.Layout referencing back to the same RootDock)
        // Use ReferenceEquality to ensure we are tracking specific instances, handling cycles correctly
        HashSet<IDockable> visited = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);

        // Seed the traversal with the root dock
        AddDockable(root, stack, visited);

        // Search all floating windows in the HostWindows collection.
        // When tools are floated, they exist in separate HostWindow containers that may not be
        // reachable from the main layout hierarchy. The HostWindows collection is populated
        // during deserialization when floating windows are restored.
        for (int i = 0; i < hostWindows.Count; i++)
        {
            IHostWindow hostWindow = hostWindows[i];
            if (hostWindow.Window?.Layout is not null)
            {
                AddDockable(hostWindow.Window.Layout, stack, visited);
            }
        }

        while (stack.Count > 0)
        {
            IDockable current = stack.Pop();

            // We already checked 'visited' before pushing, so we can yield immediately
            yield return current;

            switch (current)
            {
                // Check for IRootDock first, since it is also an IDock
                case IRootDock rootDock:
                    AddDockable(rootDock.ActiveDockable, stack, visited, includeOwnerChain: true);
                    AddDockable(rootDock.DefaultDockable, stack, visited, includeOwnerChain: true);
                    AddDockable(rootDock.FocusedDockable, stack, visited, includeOwnerChain: true);

                    AddDockables(rootDock.HiddenDockables, stack, visited);
                    AddDockables(rootDock.LeftPinnedDockables, stack, visited);
                    AddDockables(rootDock.RightPinnedDockables, stack, visited);
                    AddDockables(rootDock.TopPinnedDockables, stack, visited);
                    AddDockables(rootDock.BottomPinnedDockables, stack, visited);
                    AddDockables(rootDock.VisibleDockables, stack, visited);

                    if (rootDock.Window?.Layout is IDockable winLayout)
                    {
                        AddDockable(winLayout, stack, visited);
                    }

                    if (rootDock.Windows is not null)
                    {
                        for (int i = 0; i < rootDock.Windows.Count; i++)
                        {
                            IRootDock? dockWindowRootDock = rootDock.Windows[i].Layout;
                            AddDockable(dockWindowRootDock, stack, visited);
                        }
                    }

                    break;

                case IDock dock:
                    // NOTE: IRootDock is also IDock. The switch ensures root docks are handled in the IRootDock case,
                    // avoiding double-processing VisibleDockables.
                    AddDockable(dock.ActiveDockable, stack, visited, includeOwnerChain: true);
                    AddDockable(dock.DefaultDockable, stack, visited, includeOwnerChain: true);
                    AddDockable(dock.FocusedDockable, stack, visited, includeOwnerChain: true);

                    AddDockables(dock.VisibleDockables, stack, visited);
                    break;
            }
        }
    }

    /// <summary>
    /// Adds the specified dockable and, optionally, its owner chain to the stack if they have not already been visited.
    /// </summary>
    /// <remarks>If includeOwnerChain is set to true, the method traverses the Owner property chain of the
    /// dockable, adding each unvisited owner to the stack. This is useful for scenarios where dockable items may be
    /// nested or related through ownership, such as in floating window structures.</remarks>
    /// <param name="dockable">The dockable item to add to the stack. If null, no action is taken.</param>
    /// <param name="stackOfDockables">The stack to which unvisited dockable items are pushed.</param>
    /// <param name="visited">A set tracking dockable items that have already been processed.
    /// Items are only added to the stack if they are not present in this set.</param>
    /// <param name="includeOwnerChain">true to also add the owner chain of the dockable to the stack; otherwise, false.</param>
    private static void AddDockable(IDockable? dockable, Stack<IDockable> stackOfDockables,
        HashSet<IDockable> visited, bool includeOwnerChain = false)
    {
        if (dockable is null)
        {
            return;
        }

        // Only push if NOT already visited
        if (visited.Add(dockable))
        {
            stackOfDockables.Push(dockable);
        }

        if (!includeOwnerChain)
        {
            return;
        }

        // The Dock graph can require following the Owner chain to reach floating-window structures.
        // We walk the chain regardless of whether 'dockable' was new, because we might have reached
        // 'dockable' previously without the owner chain (e.g. via VisibleDockables).
        IDockable? ownerCurrent = dockable.Owner;
        while (ownerCurrent is not null)
        {
            if (visited.Add(ownerCurrent))
            {
                stackOfDockables.Push(ownerCurrent);
            }

            ownerCurrent = ownerCurrent.Owner;
        }
    }

    /// <summary>
    /// Adds dockable items from the specified collection to the stack if they have not already been visited.
    /// </summary>
    /// <param name="dockables">The collection of dockable items to process. If null, no items are added.</param>
    /// <param name="stackOfDockables">The stack to which unvisited dockable items are pushed.</param>
    /// <param name="visited">A set containing dockable items that have already been processed.
    /// Items are added to this set as they are pushed onto the stack.</param>
    private static void AddDockables(IList<IDockable>? dockables, Stack<IDockable> stackOfDockables, HashSet<IDockable> visited)
    {
        if (dockables is null)
        {
            return;
        }

        for (int i = 0; i < dockables.Count; i++)
        {
            IDockable child = dockables[i];
            if (visited.Add(child))
            {
                stackOfDockables.Push(child);
            }
        }
    }

    /// <summary>
    /// Closes all floating windows associated with the specified layout.
    /// </summary>
    /// <param name="layout">The root dock layout to search for floating windows.</param>
    /// <remarks>
    /// <para>
    /// This method walks the dock hierarchy and closes all <see cref="IHostWindow"/> instances
    /// that are hosting floating dockables. It uses the <see cref="Factory.RemoveWindow"/> method
    /// from the base class to properly clean up each window.
    /// </para>
    /// <para>
    /// This should be called before resetting the layout to ensure floating windows don't
    /// remain open after the layout is replaced.
    /// </para>
    /// </remarks>
    public void CloseAllFloatingWindows(IRootDock? layout)
    {
        if (layout is null)
        {
            return;
        }

        _logger.LogDebug("Closing all floating windows");
        Stopwatch stopwatch = Stopwatch.StartNew();

        //TODO - DaveBlack: i think there is already a method somewhere that does this - check Dock.Model.Mvvm.Factory
        // Iterate backwards to safely remove windows while iterating
        for (int i = HostWindows.Count - 1; i >= 0; i--)
        {
            IHostWindow hostWindow = HostWindows[i];
            try
            {
                _logger.LogDebug("Closing floating window: {WindowId}", hostWindow.Window?.Id ?? "<null>");
                if (hostWindow.Window is not null)
                {
                    // If hostWindow.Window.Owner is IRootDock rootDock, Exit() is called inside RemoveWindow
                    // so there is no need to call rootDock.Exit() here
                    RemoveWindow(hostWindow.Window);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close floating window: {WindowId}", hostWindow.Window?.Id ?? "<null>");
            }
        }

        // now try to close any remaining windows that might be attached to the layout
        if (layout.Windows is not null)
        {
            // Iterate backwards to safely remove windows while iterating
            for (int i = layout.Windows.Count - 1; i >= 0; i--)
            {
                IDockWindow dockWindow = layout.Windows[i];
                try
                {
                    _logger.LogDebug("Closing floating window from layout: {WindowId}", dockWindow.Id);
                    if (dockWindow.Host is not null)
                    {
                        // If dockWindow.Owner is IRootDock rootDock, Exit() is called inside RemoveWindow
                        // so there is no need to call rootDock.Exit() here
                        RemoveWindow(dockWindow);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to close floating window from layout: {WindowId}", dockWindow.Id);
                }
            }
        }

        stopwatch.Stop();
        _logger.LogTiming(nameof(CloseAllFloatingWindows), stopwatch.Elapsed, success: true);
        _logger.LogDebug("Finished closing floating windows");
    }

    /// <summary>
    /// Makes a tool dockable visible and active in the layout.
    /// </summary>
    /// <param name="tool">The tool to show.</param>
    /// <remarks>
    /// This method ensures the tool is visible in the layout and sets it as the active dockable.
    /// It handles cases where the tool might be pinned (auto-hide), in a collapsed state,
    /// or in a floating window.
    /// </remarks>
    public void ShowTool(IDockable? tool)
    {
        if (tool?.Owner is not IDock owner)
        {
            if (tool is null)
            {
                _logger.LogWarning("Cannot show tool: tool is null");
            }
            else
            {
                _logger.LogWarning("Cannot show tool: tool has no owner dock (ToolId: {ToolId})", tool.Id);
            }
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            // NOTE: checking PinnedDockableControls is not reliable. There appear to be times when a tool is pinned,
            // but it isn't contained in PinnedDockableControls. The correct way is to call IsDockablePinned(tool)
            if (IsDockablePinned(tool))
            {
                //TODO - DaveBlack: should we unpin or peek?
                //PreviewPinnedDockable(tool);
                UnpinDockable(tool);    // makes it visible if it was pinned (auto-hide)
            }
            // if the IDockable is hidden, restore it
            else if (IsDockableHidden(tool))
            {
                RestoreDockable(tool);  // makes it visible if it was hidden
            }
            else
            {
                //TODO - DaveBlack: find a way to do this without using Topmost it seems to mess things up eventually
                //// Find all floating windows that might contain the tool. If the tool is in a floating window,
                //// bring that window to front. Otherwise, set Topmost on the main window.
                //// First reset all Topmost states to false
                //foreach (IHostWindow hostWindow in HostWindows)
                //{
                //    IDockWindow? dockWindow = hostWindow.Window;
                //    if (dockWindow is not null)
                //    {
                //        // reset Topmost state
                //        dockWindow.Topmost = false;
                //    }
                //}

                // first check for floating windows
                for (int i = 0; i < HostWindows.Count; i++)
                {
                    IHostWindow hostWindow = HostWindows[i];
                    IDockWindow? dockWindow = hostWindow.Window;
                    if (dockWindow is not null)
                    {
                        // Check if the tool's owner chain leads to this window
                        IDockable? current = tool;
                        while (current is not null)
                        {
                            if (current is IRootDock rootDock && rootDock.Window == dockWindow)
                            {
                                _logger.LogDebug("Bringing floating window to front for tool: {ToolId}", tool.Id);

                                // Found the floating window that contains the tool
                                BringToolToFront(rootDock, tool, dockWindow);
                                return;
                            }

                            current = current.Owner;
                        }
                    }
                }

                // NOTE: To activate a pinned/hidden panel programmatically:
                // - If pinned (auto-hide): use UnpinDockable(tool) to restore it to docked state
                // - If hidden: use RestoreDockable(tool) or re-add via AddDockable()
                // The framework doesn't expose a single "show" operation because these states are mutually exclusive.

                BringToolToFront(owner, tool);
            }
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogTiming(nameof(ShowTool), stopwatch.Elapsed, success: true);
        }
    }

    /// <summary>
    /// Determines whether the specified dockable element is currently hidden within its root dock.
    /// </summary>
    /// <param name="dockable">The dockable element to check for hidden status. Cannot be null.</param>
    /// <returns>true if the specified dockable is hidden in its root dock; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="dockable"/> is null.</exception>
    internal bool IsDockableHidden(IDockable dockable)
    {
        ArgumentNullException.ThrowIfNull(dockable);

        IRootDock? rootDock = FindRoot(dockable);
        if (rootDock is null)
        {
            return false;
        }

        return rootDock.HiddenDockables?.Contains(dockable) == true;
    }

    /// <summary>
    /// Brings the specified tool window to the front within its docking context, making it the active and focused
    /// dockable element.
    /// </summary>
    /// <remarks>This method is typically used to ensure that a tool window is visible and receives input
    /// focus within a docking layout. If the tool is hosted in a floating window, the window may be brought to the top
    /// of the z-order to ensure visibility.</remarks>
    /// <param name="owner">The dock that owns the tool. This parameter determines the context in which the tool is activated and focused.</param>
    /// <param name="tool">The dockable tool to bring to the front. Cannot be null.</param>
    /// <param name="dockWindow">The optional dock window containing the tool. If null, the method attempts to determine the appropriate window
    /// from the owner chain.</param>
    [SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    private void BringToolToFront(IDock owner, IDockable tool, IDockWindow? dockWindow = null)
    {
        SetActiveDockable(tool);
        SetFocusedDockable(owner, tool);
        //if (dockWindow is null)
        //{
        //    // find the dockWindow from the owner chain
        //    IDockable? current = tool;
        //    while (current is not null)
        //    {
        //        if (current is IRootDock rootDock && rootDock.Window is not null)
        //        {
        //            dockWindow = rootDock.Window;
        //            break;
        //        }
        //        current = current.Owner;
        //    }
        //}

        //TODO - DaveBlack: find a way to do this without using Topmost it seems to mess things up eventually
        // bring the floating window to the top of the z-order
        //dockWindow?.Topmost = true;

        _logger.LogDebug("Showing tool: {ToolId}", tool.Id);
    }

    /// <summary>
    /// Disposes existing tool ViewModels to prevent memory leaks.
    /// </summary>
    /// <remarks>
    /// This method should be called before creating new tools (e.g., during layout reset)
    /// to ensure proper cleanup of event subscriptions like <c>UndoStack.PropertyChanged</c>.
    /// </remarks>
    private void DisposeExistingToolViewModels()
    {
        if (EditorTool is IDisposable disposableEditorTool)
        {
            _logger.LogDebug("Disposing existing {ToolName}", nameof(EditorTool));
            disposableEditorTool.Dispose();
            EditorTool = null;
        }

        // TODO - DiagramTool doesn't implement IDisposable currently, but check for future-proofing
        //if (DiagramTool is IDisposable disposableDiagramTool)
        //{
        //    _logger.LogDebug("Disposing existing {ToolName}", nameof(DiagramTool));
        //    disposableDiagramTool.Dispose();
        //    DiagramTool = null;
        //}
    }

    /// <summary>
    /// Outputs a debug log of the hierarchy for the specified dockable element and its visible children.
    /// </summary>
    /// <remarks>This method is only included in builds where the DEBUG symbol is defined. It recursively logs
    /// the type, ID, title, and owner information for each dockable element, including visible children and, for root
    /// docks, the associated window layout if present. Intended for diagnostic and debugging purposes.</remarks>
    /// <param name="dockable">The root <see cref="IDockable"/> element from which to begin dumping the hierarchy. If null, no output is
    /// produced.</param>
    /// <param name="depth">The indentation level to use for the initial element. Must be zero or greater.</param>
    [Conditional("DOCK_DEBUG")]
    private void DumpHierarchy(IDockable? dockable, int depth)
    {
        while (true)
        {
            if (dockable is null)
            {
                return;
            }

            string indent = new string(' ', depth * 2);
            string ownerInfo = dockable.Owner is not null ? $"Owner.Id={dockable.Owner.Id}" : "Owner=null";
            _logger.LogDebug("{Indent}[{Type}] Id='{Id}', Title='{Title}', {OwnerInfo}", indent, dockable.GetType().Name, dockable.Id, dockable.Title, ownerInfo);

            // For docks with visible children, recurse
            if (dockable is IDock { VisibleDockables.Count: > 0 } dock)
            {
                foreach (IDockable child in dock.VisibleDockables)
                {
                    DumpHierarchy(child, depth + 1);
                }
            }

            // For root docks, also dump the Window.Layout if present
            if (dockable is IRootDock { Window.Layout: not null } rootDock && rootDock.Window.Layout != dockable)
            {
                _logger.LogDebug("{Indent}  [Window.Layout]:", indent);
                dockable = rootDock.Window.Layout;
                depth += 2;
                continue;
            }

            break;
        }
    }

    [Conditional("DOCK_DEBUG")]
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem", Justification = "Method is just a pass-thru to Serilog")]
    [SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Method is just a pass-thru to Serilog")]
    private void LogDebugDetail(string? message, params object?[] args) => _logger.LogDebug(message, args);

    /// <summary>
    /// Logs detailed metadata about the current state of dockable collections and related UI elements for diagnostic
    /// purposes.
    /// </summary>
    /// <remarks>This method outputs debug-level log entries describing the contents and properties of various
    /// dockable collections, including visible, pinned, hidden, and active dockables, as well as host windows. It is
    /// intended to assist with troubleshooting and understanding the current layout and state of the docking system.
    /// Logging is performed at a granular level and may generate a large volume of output. This method does not modify
    /// any state.</remarks>
    [Conditional("DOCK_DEBUG")]
    [SuppressMessage("Maintainability", "S6664: The code block contains too many logging calls", Justification = "Detailed logging for debugging purposes.")]
    private void DumpDockMetadata(IRootDock? rootDock)
    {
        // look thru each collection in the Factory:
        // - HostWindows
        // - VisibleDockables
        // - LeftPinnedDockables
        // - RightPinnedDockables
        // - TopPinnedDockables
        // - BottomPinnedDockables
        // - HiddenDockables
        // - DefaultDockable
        // - ActiveDockable
        // - FocusedDockable
        // - Window.Layout
        _logger.LogDebug("");
        _logger.LogDebug("");
        _logger.LogDebug("");

        _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(HostWindows), HostWindows.Count);
        foreach (IHostWindow win in HostWindows)
        {
            _logger.LogDebug("HostWindow: Id: {WindowId}, Owner: {OwnerId}, Title: {WindowTitle}, Topmost: {WindowTopmost}, HostWindowState is present: {HostWindowState}",
                win.Window?.Id, win.Window?.Owner?.Id, win.Window?.Title, win.Window?.Topmost, win.HostWindowState is not null);
        }

        _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(PinnedDockableControls), PinnedDockableControls.Count);
        foreach (KeyValuePair<IDockable, IDockableControl> pinnedDockableControl in PinnedDockableControls)
        {
            _logger.LogDebug("PinnedDockableControl: key: {KeyId}, keyTitle: {KeyTitle}, value: {Name}",
                pinnedDockableControl.Key.Id, pinnedDockableControl.Key.Title, pinnedDockableControl.Value.GetType().Name);
        }

        _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(VisibleDockableControls), VisibleDockableControls.Count);
        foreach (KeyValuePair<IDockable, IDockableControl> visibleDockableControl in VisibleDockableControls)
        {
            _logger.LogDebug("VisibleDockableControl: key: {KeyId}, keyTitle: {KeyTitle}, value: {Name}",
                visibleDockableControl.Key.Id, visibleDockableControl.Key.Title, visibleDockableControl.Value.GetType().Name);
        }

        if (rootDock is not null)
        {
            LogDockable(rootDock.ActiveDockable, nameof(rootDock.ActiveDockable));
            LogDockable(rootDock.DefaultDockable, nameof(rootDock.DefaultDockable));
            LogDockable(rootDock.FocusedDockable, nameof(rootDock.FocusedDockable));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDock.BottomPinnedDockables), rootDock.BottomPinnedDockables?.Count);
            rootDock.BottomPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDock.BottomPinnedDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDock.TopPinnedDockables), rootDock.TopPinnedDockables?.Count);
            rootDock.TopPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDock.TopPinnedDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDock.RightPinnedDockables), rootDock.RightPinnedDockables?.Count);
            rootDock.RightPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDock.RightPinnedDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDock.LeftPinnedDockables), rootDock.LeftPinnedDockables?.Count);
            rootDock.LeftPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDock.LeftPinnedDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDock.VisibleDockables), rootDock.VisibleDockables?.Count);
            rootDock.VisibleDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDock.VisibleDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDock.HiddenDockables), rootDock.HiddenDockables?.Count);
            rootDock.HiddenDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDock.HiddenDockables)));

            _logger.LogDebug("");
            _logger.LogDebug("");
            _logger.LogDebug("");

            void LogDockable(IDockable? dockable, string property) =>
                _logger.LogDebug("DOCK: {Property}: Id: {DockableId}, Title: {DockableTitle}, Type: {Name}", property, dockable?.Id ?? "<null>", dockable?.Title ?? "<null>", dockable?.GetType().Name ?? "<null>");
        }
    }

    /// <summary>
    /// Logs diagnostic information about the current dock layout state for debugging drop target issues.
    /// Call this when panels are floated to understand why re-docking may not be working.
    /// </summary>
    /// <param name="rootDock">The root dock to diagnose. If null, only DockControls are logged.</param>
    /// <remarks>
    /// This method helps diagnose why floating panels cannot re-dock by showing:
    /// <list type="bullet">
    ///     <item><description>Number of DockControl instances (increases with each floating window)</description></item>
    ///     <item><description>VisibleDockables hierarchy to identify "empty" containers</description></item>
    ///     <item><description>Drop-related properties (CanDrop, IsCollapsable, etc.)</description></item>
    /// </list>
    /// </remarks>
    [Conditional("DEBUG")]
    [SuppressMessage("Maintainability", "S6664: The code block contains too many logging calls", Justification = "Detailed logging for debugging purposes.")]
    public void LogDropTargetDiagnostics(IRootDock? rootDock)
    {
        _logger.LogInformation("=== DROP TARGET DIAGNOSTICS ===");
        _logger.LogInformation("DockControls.Count: {Count}", DockControls.Count);

        for (int i = 0; i < DockControls.Count; i++)
        {
            IDockControl dc = DockControls[i];
            IRootDock? dcLayout = dc.Layout as IRootDock;
            _logger.LogInformation("DockControl[{Index}]: Layout.Id={LayoutId}, Layout.CanDrop={CanDrop}, VisibleDockables.Count={Count}",
                i,
                dc.Layout?.Id ?? "<null>",
                dcLayout?.CanDrop,
                dcLayout?.VisibleDockables?.Count ?? -1);
        }

        if (rootDock is null)
        {
            _logger.LogInformation("RootDock is null");
            _logger.LogInformation("=== END DIAGNOSTICS ===");
            return;
        }

        _logger.LogInformation("Main RootDock.Id: {Id}", rootDock.Id);
        _logger.LogInformation("Main RootDock.CanDrop: {CanDrop}", rootDock.CanDrop);
        _logger.LogInformation("Main RootDock.IsCollapsable: {IsCollapsable}", rootDock.IsCollapsable);
        _logger.LogInformation("Main RootDock.VisibleDockables.Count: {Count}", rootDock.VisibleDockables?.Count ?? 0);

        if (rootDock.VisibleDockables is not null)
        {
            for (int i = 0; i < rootDock.VisibleDockables.Count; i++)
            {
                IDockable dockable = rootDock.VisibleDockables[i];
                LogDockableHierarchy(dockable, depth: 1);
            }
        }

        _logger.LogInformation("HostWindows.Count: {Count}", HostWindows.Count);
        for (int i = 0; i < HostWindows.Count; i++)
        {
            IHostWindow hostWindow = HostWindows[i];
            _logger.LogInformation("  HostWindow: Layout.Id={LayoutId}", hostWindow.Window?.Id ?? "<null>");
        }

        _logger.LogInformation("=== END DIAGNOSTICS ===");

        void LogDockableHierarchy(IDockable dockable, int depth)
        {
            string indent = new string(' ', depth * 2);
            IDock? dock = dockable as IDock;

            _logger.LogInformation("{Indent}- {Type}: Id={Id}, CanDrop={CanDrop}, VisibleDockables.Count={Count}",
                indent,
                dockable.GetType().Name,
                dockable.Id,
                dock?.CanDrop,
                dock?.VisibleDockables?.Count ?? -1);

            if (dock?.VisibleDockables is not null)
            {
                for (int i = 0; i < dock.VisibleDockables.Count; i++)
                {
                    IDockable child = dock.VisibleDockables[i];
                    LogDockableHierarchy(child, depth + 1);
                }
            }
        }
    }
}
