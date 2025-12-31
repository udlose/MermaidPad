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

using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using MermaidPad.ViewModels.Docking;
using MermaidPad.ViewModels.UserControls;
using Microsoft.Extensions.Logging;
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
internal sealed class DockFactory : Factory
{
    private readonly IViewModelFactory _viewModelFactory;
    private readonly ILogger<DockFactory> _logger;

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
    /// <param name="viewModelFactory">The factory for creating ViewModel instances.</param>
    /// <param name="logger">The logger instance for this factory.</param>
    public DockFactory(IViewModelFactory viewModelFactory, ILogger<DockFactory> logger)
    {
        _viewModelFactory = viewModelFactory;
        _logger = logger;
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

        // Create tool docks with default 50/50 proportions
        //TODO - DaveBlack: change the proportions once I add the new panel for AI
        ToolDock editorToolDock = new ToolDock
        {
            Id = "EditorToolDock",
            Title = "Editor",
            Proportion = 0.5,
            ActiveDockable = EditorTool,
            VisibleDockables = CreateList<IDockable>(EditorTool),
            CanClose = false,
            CanPin = true,
            CanFloat = true
        };

        ToolDock diagramToolDock = new ToolDock
        {
            Id = "DiagramToolDock",
            Title = "Diagram",
            Proportion = 0.5,
            ActiveDockable = DiagramTool,
            VisibleDockables = CreateList<IDockable>(DiagramTool),
            CanClose = false,
            CanPin = true,
            CanFloat = true
        };

        // Create a proportional dock to hold both tool docks (horizontal layout)
        ProportionalDock mainLayout = new ProportionalDock
        {
            Id = "MainProportionalDock",
            Title = "Main",
            Orientation = Orientation.Horizontal,
            Proportion = double.NaN,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>(
                editorToolDock,
                new ProportionalDockSplitter
                {
                    Id = "MainSplitter",
                    Title = "Splitter"
                },
                diagramToolDock
            )
        };

        // Create the root dock
        RootDock rootDock = new RootDock
        {
            Id = "RootDock",
            Title = "Root",
            IsCollapsable = false,
            ActiveDockable = mainLayout,
            DefaultDockable = mainLayout,
            VisibleDockables = CreateList<IDockable>(mainLayout)
        };

        _logger.LogInformation("Default dock layout created with 50/50 horizontal split");

        return rootDock;
    }

    /// <summary>
    /// Initializes the dock layout after creation or deserialization.
    /// </summary>
    /// <param name="layout">The layout to initialize.</param>
    /// <remarks>
    /// <para>
    /// This method calls the base <see cref="Factory.InitLayout(IDockable)"/> and then
    /// searches for tool ViewModels in the layout hierarchy. This is necessary when
    /// restoring a layout from serialization, as the tool references need to be re-established.
    /// </para>
    /// </remarks>
    public override void InitLayout(IDockable layout)
    {
        _logger.LogInformation("Initializing dock layout");

        // Call base initialization
        base.InitLayout(layout);

        // After layout initialization, try to find our tools if they were restored from serialization
        if (EditorTool is null || DiagramTool is null)
        {
            FindToolsInLayout(layout);
        }

        _logger.LogInformation("Dock layout initialized");
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
    /// <b>Performance:</b> Uses iterative traversal with a stack instead of recursion
    /// to eliminate method call overhead. Early exit when both tools are found.
    /// </para>
    /// </remarks>
    private void FindToolsInLayout(IDockable? root)
    {
        if (root is null)
        {
            return;
        }

        // Use stack-based iteration instead of recursion to avoid stack frame overhead
        // Initial capacity of 8 is sufficient for typical dock layouts (usually 5-10 nodes)
        Stack<IDockable> stack = new Stack<IDockable>(capacity: 8);
        stack.Push(root);

        while (stack.Count > 0)
        {
            IDockable current = stack.Pop();
            switch (current)
            {
                case MermaidEditorToolViewModel editorTool:
                    EditorTool = editorTool;
                    _logger.LogDebug("Found EditorTool in layout");
                    break;

                case DiagramToolViewModel diagramTool:
                    DiagramTool = diagramTool;
                    _logger.LogDebug("Found DiagramTool in layout");
                    break;

                case IDock dock when dock.VisibleDockables is not null:
                    // Push children onto stack for processing
                    foreach (IDockable child in dock.VisibleDockables)
                    {
                        stack.Push(child);
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

    /// <summary>
    /// Makes a tool dockable visible and active in the layout.
    /// </summary>
    /// <param name="tool">The tool to show.</param>
    /// <remarks>
    /// This method ensures the tool is visible in the layout and sets it as the active dockable.
    /// It handles cases where the tool might be pinned (auto-hide) or in a collapsed state.
    /// </remarks>
    public void ShowTool(IDockable? tool)
    {
        if (tool?.Owner is IDock owner)
        {
            SetActiveDockable(tool);
            SetFocusedDockable(owner, tool);

            _logger.LogDebug("Showing tool: {ToolId}", tool.Id);
        }
    }
}
