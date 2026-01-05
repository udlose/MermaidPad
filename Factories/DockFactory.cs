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
internal sealed class DockFactory : Factory
{
    private readonly ILogger<DockFactory> _logger;
    private readonly IViewModelFactory _viewModelFactory;
    private readonly ObjectPool<HashSet<IDockable>> _hashSetObjectPool;

    private const string RootDockId = "RootDock";
    private const string MainProportionalDockId = "MainProportionalDock";
    private const string EditorToolDockId = "EditorToolDock";
    private const string DiagramToolDockId = "DiagramToolDock";

    private RootDock? _rootDock;
    private ProportionalDock? _mainLayout;
    private ToolDock? _editorToolDock;
    private ToolDock? _diagramToolDock;

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
    public override IRootDock CreateLayout()
    {
        return CreateDefaultLayout();
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

        // Create the tool ViewModels using the ViewModelFactory pattern
        MermaidEditorViewModel editorViewModel = _viewModelFactory.Create<MermaidEditorViewModel>();
        DiagramViewModel diagramViewModel = _viewModelFactory.Create<DiagramViewModel>();

        // Wrap in dock-aware tool ViewModels
        EditorTool = _viewModelFactory.Create<MermaidEditorToolViewModel>(editorViewModel);
        DiagramTool = _viewModelFactory.Create<DiagramToolViewModel>(diagramViewModel);

        EditorTool.Factory = this;
        DiagramTool.Factory = this;

        // Create tool docks with default 35/65 proportions
        //TODO - DaveBlack: change the proportions once I add the new panel for AI
        _editorToolDock = new ToolDock
        {
            Factory = this,
            Id = EditorToolDockId,
            Title = "Editor",
            Proportion = 0.35,
            ActiveDockable = EditorTool,
            VisibleDockables = CreateList<IDockable>(EditorTool),
            CanClose = false,
            CanPin = true,
            CanFloat = true
        };

        _diagramToolDock = new ToolDock
        {
            Factory = this,
            Id = DiagramToolDockId,
            Title = "Diagram",
            Proportion = 0.65,
            ActiveDockable = DiagramTool,
            VisibleDockables = CreateList<IDockable>(DiagramTool),
            CanClose = false,
            CanPin = true,
            CanFloat = true
        };

        // Create a proportional dock to hold both tool docks (horizontal layout)
        _mainLayout = new ProportionalDock
        {
            Factory = this,
            Id = MainProportionalDockId,
            Title = "Main",
            Orientation = Orientation.Horizontal,
            Proportion = double.NaN,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>(
                _editorToolDock,
                new ProportionalDockSplitter
                {
                    Id = "MainSplitter",
                    Title = "Splitter"
                },
                _diagramToolDock
            )
        };

        // Create the root dock
        _rootDock = new RootDock
        {
            Factory = this,
            Id = RootDockId,
            Title = "Root",
            IsCollapsable = false,
            ActiveDockable = _mainLayout,
            DefaultDockable = _mainLayout,
            VisibleDockables = CreateList<IDockable>(_mainLayout)
        };

        _logger.LogInformation("Default dock layout created with {EditorProportion}%/{DiagramProportion}% {Orientation} split", _editorToolDock.Proportion * 100, _diagramToolDock.Proportion * 100, _mainLayout.Orientation);

        return _rootDock;
    }

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

        // https://github.com/wieslawsoltes/Dock/blob/master/docs/dock-advanced.md?plain=1#L21-L36 says: to configure locators here BUT this method is called AFTER deserialization
        //TODO - DaveBlack: review whether locators need to be re-configured here
        ConfigureLocators();

        // Call base initialization before searching for IDockables or tool ViewModels - this triggers InitDockable for all dockables in the layout,
        // which uses the locators configured in the constructor
        base.InitLayout(layout);

        //TODO - DaveBlack: optimize this code to avoid iteration over the layout multiple times
        _rootDock ??= FindRoot(layout) as RootDock ?? throw new InvalidOperationException($"{nameof(_rootDock)} not found in layout during initialization.");
        _mainLayout ??= FindDockable(_rootDock, static dockable => dockable.Id == MainProportionalDockId) as ProportionalDock ??
                        throw new InvalidOperationException($"{nameof(_mainLayout)} not found in layout during initialization.");

        if (_editorToolDock is null)
        {
            // It's possible that the tool was "floated" in the layout in which case it won't be found and will be recreated by the locator when needed
            _editorToolDock = FindDockable(_rootDock, static dockable => dockable.Id == EditorToolDockId) as ToolDock;
            if (_editorToolDock is null)
            {
                _logger.LogInformation("{ToolName} not found in layout during initialization. It will be recreated by the locator when needed.", nameof(_editorToolDock));
            }
        }

        if (_diagramToolDock is null)
        {
            // It's possible that the tool was "floated" in the layout in which case it won't be found and will be recreated by the locator when needed
            _diagramToolDock = FindDockable(_rootDock, static dockable => dockable.Id == DiagramToolDockId) as ToolDock;
            if (_diagramToolDock is null)
            {
                _logger.LogInformation("{ToolName} not found in layout during initialization. It will be recreated by the locator when needed.", nameof(_diagramToolDock));
            }
        }

        if (EditorTool is null)
        {
            // It's possible that the tool was "hidden" in the layout (not visible or pinned), in which case it won't be found and will be recreated by the locator when needed
            EditorTool = FindDockable(_rootDock, static dockable => dockable.Id == MermaidEditorToolViewModel.ToolId) as MermaidEditorToolViewModel;

            //TODO - DaveBlack: review whether to use FindDockable or GetDockable here
            // GetDockable<T> is a helper method defined in Dock.Model.Mvvm.Factory that uses the DockableLocator to create or retrieve the dockable by ID
            // It will create a new instance via the locator
            //EditorTool = GetDockable<MermaidEditorToolViewModel>(MermaidEditorToolViewModel.ToolId);
        }

        if (DiagramTool is null)
        {
            // It's possible that the tool was "hidden" in the layout (not visible or pinned), in which case it won't be found and will be recreated by the locator when needed
            DiagramTool = FindDockable(_rootDock, static dockable => dockable.Id == DiagramToolViewModel.ToolId) as DiagramToolViewModel;

            //TODO - DaveBlack: review whether to use FindDockable or GetDockable here
            // GetDockable<T> is a helper method defined in Dock.Model.Mvvm.Factory that uses the DockableLocator to create or retrieve the dockable by ID
            // It will create a new instance via the locator
            //DiagramTool = GetDockable<DiagramToolViewModel>(DiagramToolViewModel.ToolId);
        }

        // After layout initialization, try to find our tools if they were restored from serialization
        if (EditorTool is null || DiagramTool is null)
        {
            FindToolsInLayout(layout);
        }

        if (EditorTool is null)
        {
            _logger.LogInformation("{ToolName} not found in layout during initialization. It will be recreated by the locator when needed.", nameof(EditorTool));
        }

        if (DiagramTool is null)
        {
            _logger.LogInformation("{ToolName} not found in layout during initialization. It will be recreated by the locator when needed.", nameof(DiagramTool));
        }

        LogDockMetadata();
        _logger.LogInformation("Dock layout initialized");
    }

    [SuppressMessage("Maintainability", "S6664: The code block contains too many logging calls", Justification = "Detailed logging for debugging purposes.")]
    private void LogDockMetadata()
    {
        // look thru each collection in the Factory:
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
        foreach (var win in HostWindows)
        {
            _logger.LogDebug("HostWindow: Id: {WindowId}, Owner: {OwnerId}, Title: {WindowTitle}, Topmost: {WindowTopmost}, HostWindowState is present: {HostWindowState}",
                win.Window?.Id, win.Window?.Owner?.Id, win.Window?.Title, win.Window?.Topmost, win.HostWindowState is not null);
        }

        _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(PinnedDockableControls), PinnedDockableControls.Count);
        foreach (var pinnedDockableControl in PinnedDockableControls)
        {
            _logger.LogDebug("PinnedDockableControl: key: {KeyId}, keyTitle: {KeyTitle}, value: {Name}",
                pinnedDockableControl.Key.Id, pinnedDockableControl.Key.Title, pinnedDockableControl.Value.GetType().Name);
        }

        _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(VisibleDockableControls), VisibleDockableControls.Count);
        foreach (var visibleDockableControl in VisibleDockableControls)
        {
            _logger.LogDebug("VisibleDockableControl: key: {KeyId}, keyTitle: {KeyTitle}, value: {Name}",
                visibleDockableControl.Key.Id, visibleDockableControl.Key.Title, visibleDockableControl.Value.GetType().Name);
        }

        RootDock? rootDockX = _rootDock;
        if (rootDockX is not null)
        {
            LogDockable(rootDockX.ActiveDockable, nameof(rootDockX.ActiveDockable));
            LogDockable(rootDockX.DefaultDockable, nameof(rootDockX.DefaultDockable));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDockX.BottomPinnedDockables), rootDockX.BottomPinnedDockables?.Count);
            rootDockX.BottomPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDockX.BottomPinnedDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDockX.TopPinnedDockables), rootDockX.TopPinnedDockables?.Count);
            rootDockX.TopPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDockX.TopPinnedDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDockX.RightPinnedDockables), rootDockX.RightPinnedDockables?.Count);
            rootDockX.RightPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDockX.RightPinnedDockables)));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDockX.LeftPinnedDockables), rootDockX.LeftPinnedDockables?.Count);
            rootDockX.LeftPinnedDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDockX.LeftPinnedDockables)));

            LogDockable(rootDockX.FocusedDockable, nameof(rootDockX.FocusedDockable));

            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDockX.VisibleDockables), rootDockX.VisibleDockables?.Count);
            rootDockX.VisibleDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDockX.VisibleDockables)));
            _logger.LogDebug("Logging {Collection} collections: count: {Count}", nameof(rootDockX.HiddenDockables), rootDockX.HiddenDockables?.Count);
            rootDockX.HiddenDockables?.ToList().ForEach(d => LogDockable(d, nameof(rootDockX.HiddenDockables)));

            _logger.LogDebug("");
            _logger.LogDebug("");
            _logger.LogDebug("");

            void LogDockable(IDockable? dockable, string property) =>
                _logger.LogDebug("DOCK: {Property}: Id: {DockableId}, Title: {DockableTitle}, Type: {Name}", property, dockable?.Id ?? "<null>", dockable?.Title ?? "<null>", dockable?.GetType().Name ?? "<null>");
        }
        else
        {
            _logger.LogWarning("RootDock is null in LogDockMetadata");
        }
    }

    /// <summary>
    /// Searches the dock layout hierarchy to find and cache references to the tool ViewModels.
    /// </summary>
    /// <param name="root">The root dockable to search from.</param>
    /// <remarks>
    /// <para>
    /// This method is called after layout initialization to ensure we have references to the
    /// tool ViewModels. It uses an iterative depth-first search with a stack to avoid recursion overhead.
    /// </para>
    /// <para>
    /// The search includes:
    /// <list type="bullet">
    ///     <item><description><c>VisibleDockables</c> - Normal docked panels</description></item>
    ///     <item><description><c>LeftPinnedDockables</c>, <c>RightPinnedDockables</c>, etc. - Auto-hide/pinned panels</description></item>
    ///     <item><description><c>FocusedDockable</c> - Currently focused dockable (maybe in a floating window)</description></item>
    ///     <item><description><c>FocusedDockable.Owner</c> chain - To find floating window containers</description></item>
    ///     <item><description><c>Window.Layout</c> - Floating window layouts</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Performance:</b> Uses iterative traversal with a stack instead of recursion
    /// to eliminate method call overhead. Early exit when both tools are found.
    /// Uses a HashSet for cycle detection to handle circular references in the dock hierarchy.
    /// </para>
    /// </remarks>
    private void FindToolsInLayout(IDockable? root)
    {
        if (root is null)
        {
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Use stack-based iteration instead of recursion to avoid stack frame overhead
        // Initial capacity of 16 to account for pinned dockables collections and floating windows
        Stack<IDockable> stack = new Stack<IDockable>(capacity: 16);

        // Track visited nodes to prevent infinite loops from circular references
        // The dock hierarchy can have cycles (e.g., Window.Layout referencing back to the same RootDock)
        HashSet<IDockable>? visited = null;
        try
        {
            visited = _hashSetObjectPool.Get();

            stack.Push(root);
            while (stack.Count > 0)
            {
                IDockable current = stack.Pop();

                // Skip if already visited (cycle detection)
                if (!visited.Add(current))
                {
                    continue;
                }

                // Log what we're processing for debugging
                _logger.LogDebug("{Method} processing: {Type} (Id: {Id})", nameof(FindToolsInLayout), current.GetType().Name, current.Id ?? "<null>");

                switch (current)
                {
                    case MermaidEditorToolViewModel editorTool:
                        EditorTool = editorTool;
                        _logger.LogDebug("Found ToolName: {ToolName} in layout with ViewModel: {ViewModel}",
                            nameof(EditorTool), nameof(MermaidEditorToolViewModel));
                        break;

                    case DiagramToolViewModel diagramTool:
                        DiagramTool = diagramTool;
                        _logger.LogDebug("Found ToolName: {ToolName} in layout with ViewModel: {ViewModel}",
                            nameof(DiagramTool), nameof(DiagramToolViewModel));
                        break;

                    case IRootDock rootDock:
                        // Search pinned dockables collections (auto-hide panels)
                        // These are populated when a tool is pinned to the edge of the window
                        PushDockablesToStack(stack, rootDock.LeftPinnedDockables);
                        PushDockablesToStack(stack, rootDock.RightPinnedDockables);
                        PushDockablesToStack(stack, rootDock.TopPinnedDockables);
                        PushDockablesToStack(stack, rootDock.BottomPinnedDockables);

                        // Search hidden dockables
                        PushDockablesToStack(stack, rootDock.HiddenDockables);

                        // Search visible dockables
                        PushDockablesToStack(stack, rootDock.VisibleDockables);

                        // Search DefaultDockable - may contain floating window content
                        if (rootDock.DefaultDockable is not null && !visited.Contains(rootDock.DefaultDockable))
                        {
                            _logger.LogDebug("Pushing DefaultDockable: {Type} (Id: {Id})",
                                rootDock.DefaultDockable.GetType().Name,
                                rootDock.DefaultDockable.Id ?? "<null>");
                            stack.Push(rootDock.DefaultDockable);
                        }

                        // Search ActiveDockable - may contain floating window content
                        if (rootDock.ActiveDockable is not null && !visited.Contains(rootDock.ActiveDockable))
                        {
                            _logger.LogDebug("Pushing ActiveDockable: {Type} (Id: {Id})",
                                rootDock.ActiveDockable.GetType().Name,
                                rootDock.ActiveDockable.Id ?? "<null>");
                            stack.Push(rootDock.ActiveDockable);
                        }

                        // Search FocusedDockable - may reference a tool in a floating window
                        // IMPORTANT: When a tool is floated, FocusedDockable points to the tool,
                        // and the tool's Owner chain leads to the floating window's RootDock
                        if (rootDock.FocusedDockable is not null)
                        {
                            _logger.LogDebug("Pushing FocusedDockable: {Type} (Id: {Id})",
                                rootDock.FocusedDockable.GetType().Name,
                                rootDock.FocusedDockable.Id ?? "<null>");
                            stack.Push(rootDock.FocusedDockable);

                            // Also traverse the Owner chain of FocusedDockable to find floating windows
                            // The tool's Owner -> ToolDock -> RootDock -> Window structure
                            IDockable? ownerCurrent = rootDock.FocusedDockable.Owner;
                            while (ownerCurrent is not null)
                            {
                                if (!visited.Contains(ownerCurrent))
                                {
                                    _logger.LogDebug("Pushing FocusedDockable.Owner chain: {Type} (Id: {Id})",
                                        ownerCurrent.GetType().Name,
                                        ownerCurrent.Id ?? "<null>");
                                    stack.Push(ownerCurrent);
                                }
                                ownerCurrent = ownerCurrent.Owner;
                            }
                        }
                        else
                        {
                            _logger.LogDebug("FocusedDockable is null for RootDock: {Id}", rootDock.Id ?? "<null>");
                        }

                        // Search the Window property - contains floating window layout
                        // The Window has a Layout property which is another RootDock
                        if (rootDock.Window?.Layout is not null)
                        {
                            _logger.LogDebug("Pushing Window.Layout: {Type} (Id: {Id})",
                                rootDock.Window.Layout.GetType().Name,
                                rootDock.Window.Layout.Id ?? "<null>");
                            stack.Push(rootDock.Window.Layout);
                        }
                        break;

                    case IDock dock:
                        // Push visible children onto stack for processing
                        PushDockablesToStack(stack, dock.VisibleDockables);

                        // Search ActiveDockable - may contain tool in floating window
                        if (dock.ActiveDockable is not null && !visited.Contains(dock.ActiveDockable))
                        {
                            _logger.LogDebug("Pushing IDock.ActiveDockable: {Type} (Id: {Id})",
                                dock.ActiveDockable.GetType().Name,
                                dock.ActiveDockable.Id ?? "<null>");
                            stack.Push(dock.ActiveDockable);
                        }
                        break;
                }

                // Early exit if both tools found - no need to continue traversal
                if (EditorTool is not null && DiagramTool is not null)
                {
                    return;
                }
            }
        }
        finally
        {
            if (visited is not null)
            {
                _hashSetObjectPool.Return(visited);
            }

            stopwatch.Stop();
            _logger.LogTiming(nameof(FindToolsInLayout), stopwatch.Elapsed, success: true);
        }

        // Log warning if tools weren't found
        if (EditorTool is null)
        {
            _logger.LogWarning("{Tool} was not found during layout traversal", nameof(EditorTool));
        }
        if (DiagramTool is null)
        {
            _logger.LogWarning("{Tool} was not found during layout traversal", nameof(DiagramTool));
        }
    }

    /// <summary>
    /// Pushes all dockables from a collection onto the search stack.
    /// </summary>
    /// <param name="stack">The stack to push dockables onto.</param>
    /// <param name="dockables">The collection of dockables to push. Can be null.</param>
    private static void PushDockablesToStack(Stack<IDockable> stack, IList<IDockable>? dockables)
    {
        if (dockables is null)
        {
            return;
        }

        foreach (IDockable dockable in dockables)
        {
            stack.Push(dockable);
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
        for (var i = HostWindows.Count - 1; i >= 0; i--)
        {
            var hostWindow = HostWindows[i];
            try
            {
                _logger.LogDebug("Closing floating window: {WindowId}", hostWindow.Window?.Id ?? "<null>");
                if (hostWindow.Window is not null)
                {
                    RemoveWindow(hostWindow.Window); // if hostWindow.Window.Owner is IRootDock rootDock, Exit() is called inside RemoveWindow
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close floating window: {WindowId}", hostWindow.Window?.Id ?? "<null>");
            }
        }

        stopwatch.Stop();
        _logger.LogTiming(nameof(CloseAllFloatingWindows), stopwatch.Elapsed, success: true);
        _logger.LogDebug("Finished closing floating windows");
    }

    //TODO - DaveBlack: review whether we need this method - it seems unused
    ///// <summary>
    ///// Recursively collects all floating windows from the dock hierarchy.
    ///// </summary>
    ///// <param name="dockable">The current dockable to search from.</param>
    ///// <param name="windows">The list to add found windows to.</param>
    ///// <param name="visited">Set of visited dockables for cycle detection.</param>
    //private void CollectFloatingWindows(IDockable? dockable, List<IDockWindow> windows, HashSet<IDockable> visited)
    //{
    //    if (dockable is null || !visited.Add(dockable))
    //    {
    //        return;
    //    }

    //    // Check if this is a root dock with a floating window
    //    if (dockable is IRootDock rootDock)
    //    {
    //        // The Window property holds the floating window for this root dock
    //        if (rootDock.Window?.Host is not null)
    //        {
    //            windows.Add(rootDock.Window);

    //            // Also search within the window's layout for nested floating windows
    //            if (rootDock.Window.Layout is not null)
    //            {
    //                CollectFloatingWindows(rootDock.Window.Layout, windows, visited);
    //            }
    //        }

    //        //TODO - @Claude, if the method is collecting "floating" windows, why is it also searching pinned dockables???
    //        // Search pinned dockables for any floating windows
    //        CollectFloatingWindowsFromList(rootDock.LeftPinnedDockables, windows, visited);
    //        CollectFloatingWindowsFromList(rootDock.RightPinnedDockables, windows, visited);
    //        CollectFloatingWindowsFromList(rootDock.TopPinnedDockables, windows, visited);
    //        CollectFloatingWindowsFromList(rootDock.BottomPinnedDockables, windows, visited);
    //    }

    //    // Search visible dockables for any dock type
    //    if (dockable is IDock dock)
    //    {
    //        CollectFloatingWindowsFromList(dock.VisibleDockables, windows, visited);
    //    }
    //}

    ///// <summary>
    ///// Recursively collects all floating dock windows from the specified list of dockable elements and adds them to the
    ///// provided collection.
    ///// </summary>
    ///// <param name="dockables">The list of dockable elements to search for floating windows. Can be null, in which case no action is taken.</param>
    ///// <param name="windows">The collection to which found floating dock windows are added. Must not be null.</param>
    ///// <param name="visited">A set used to track dockable elements that have already been processed to prevent duplicate processing. Must not
    ///// be null.</param>
    //private void CollectFloatingWindowsFromList(IList<IDockable>? dockables, List<IDockWindow> windows, HashSet<IDockable> visited)
    //{
    //    if (dockables is null)
    //    {
    //        return;
    //    }

    //    foreach (IDockable child in dockables)
    //    {
    //        CollectFloatingWindows(child, windows, visited);
    //    }
    //}

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

        _logger.LogDebug("Showing tool: {ToolId}", tool.Id);

        Stopwatch stopwatch = Stopwatch.StartNew();

        //TODO - DaveBlack: review all the commented code below and remove what is not needed anymore
        //AddDockable(owner, tool);
        //               FloatDockable(tool);
        //                InsertDockable(owner, tool, 0);

        // if it was hidden, let's peek it
        if (IsDockablePinned(tool))
        {
            //   PreviewPinnedDockable(tool);
            //           RestoreDockable(tool);




            UnpinDockable(tool);    // makes it visible if it was pinned (auto-hide)
        }


        //_rootDock.ActiveDockable = tool;
        //if (_rootDock.ShowWindows.CanExecute(null))
        //{
        //    _rootDock.ShowWindows.Execute(null);
        //}




        //MoveDockable(owner, tool.Owner, tool.OriginalOwner);
        //            PinDockable(tool);
        //RestoreDockable(tool);  //  doesn't work
        //SwapDockable(owner, tool.Owner, tool.OriginalOwner);

        //PreviewPinnedDockable(tool);
        //if (!IsDockablePinned(tool))
        //{
        //    PinDockable(tool);      // makes it visible if it wasn't pinned (auto-hide)
        //}



        SetActiveDockable(tool);
        InitActiveDockable(tool, owner);        // this call also sets the tool as the focused dockable
        SetFocusedDockable(owner, tool);

        // If the tool is in a floating window, bring that window to front (topmost)
        // Walk up the ownership chain to find the IRootDock that owns this tool's container
        IDockable? current = owner;
        while (current is not null)
        {
            //TODO - @Claude: determine how to activate a tabbed/hidden panel and programmatically unhide, pin, and dock it if needed

            if (current is IRootDock rootDock && rootDock.Window?.Host is not null)
            {
                // Found the floating window - set it to the top of the Z-order
                rootDock.Window.Topmost = true;
                break;
            }

            current = current.Owner;
        }

        stopwatch.Stop();
        _logger.LogTiming(nameof(ShowTool), stopwatch.Elapsed, success: true);
    }

    public override void OnWindowActivated(IDockWindow? window)
    {
        base.OnWindowActivated(window);
        _logger.LogInformation("Window activated: {WindowId}", window?.Id ?? "<null>");
    }

    public override void OnWindowDeactivated(IDockWindow? window)
    {
        base.OnWindowDeactivated(window);
        _logger.LogInformation("Window deactivated: {WindowId}", window?.Id ?? "<null>");
    }

    public override void OnWindowClosed(IDockWindow? window)
    {
        base.OnWindowClosed(window);
        _logger.LogInformation("Window closed: {WindowId}", window?.Id ?? "<null>");
    }

    public override void OnWindowOpened(IDockWindow? window)
    {
        base.OnWindowOpened(window);
        _logger.LogInformation("Window opened: {WindowId}", window?.Id ?? "<null>");
    }

    public override void OnWindowRemoved(IDockWindow? window)
    {
        base.OnWindowRemoved(window);
        _logger.LogInformation("Window removed: {WindowId}", window?.Id ?? "<null>");
    }
}
