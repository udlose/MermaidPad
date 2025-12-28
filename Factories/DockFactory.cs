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
    //TODO @Claude shouldn't this functionality be in CreateDefaultLayout? otherwise it seems we aren't reloading the saved layout anywhere
    public override IRootDock CreateLayout()
    {
        _logger.LogInformation("Creating default dock layout");

        // Create the tool ViewModels using the ViewModelFactory pattern
        // This ensures proper DI for all dependencies
        MermaidEditorViewModel editorViewModel = _viewModelFactory.Create<MermaidEditorViewModel>();
        DiagramViewModel diagramViewModel = _viewModelFactory.Create<DiagramViewModel>();

        // Wrap in dock-aware tool ViewModels
        EditorTool = _viewModelFactory.Create<MermaidEditorToolViewModel>(editorViewModel);
        DiagramTool = _viewModelFactory.Create<DiagramToolViewModel>(diagramViewModel);

        // Create tool docks to hold each tool
        //TODO - DaveBlack: change the proportions once I add the new panel for AI
        ToolDock editorToolDock = new ToolDock
        {
            Id = "EditorToolDock",
            Title = "Editor",
            Proportion = 0.5,  // 50% width
            ActiveDockable = EditorTool,
            VisibleDockables = CreateList<IDockable>(EditorTool),
            CanClose = false,
            CanPin = false,
            CanFloat = false
        };

        ToolDock diagramToolDock = new ToolDock
        {
            Id = "DiagramToolDock",
            Title = "Diagram",
            Proportion = 0.5,  // 50% width
            ActiveDockable = DiagramTool,
            VisibleDockables = CreateList<IDockable>(DiagramTool),
            CanClose = false,
            CanPin = false,
            CanFloat = false
        };

        // Create a proportional dock to hold both tool docks side by side
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

        _logger.LogInformation("Dock layout created successfully with EditorTool and DiagramTool");

        return rootDock;
    }

    /// <summary>
    /// Creates a default layout using the same configuration as <see cref="CreateLayout"/>.
    /// </summary>
    /// <returns>An <see cref="IRootDock"/> representing the default dock layout.</returns>
    //TODO @Claude why isn't this called anywhere? should it be called somewhere?
    public IRootDock CreateDefaultLayout()
    {
        return CreateLayout();
    }

    /// <summary>
    /// Initializes the dock layout after creation or deserialization.
    /// </summary>
    /// <param name="layout">The layout to initialize.</param>
    /// <remarks>
    /// This method calls the base <see cref="Factory.InitLayout(IDockable)"/> and then
    /// performs any additional initialization required for MermaidPad.
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
    /// <param name="dockable">The root dockable to search from.</param>
    /// <remarks>
    /// This method is called after layout deserialization to restore references to the
    /// tool ViewModels that were serialized. It recursively searches the layout tree.
    /// </remarks>
    private void FindToolsInLayout(IDockable? dockable)
    {
        //TODO @Claude this seems a bit fragile, is there a better way to do this?
        if (dockable is null)
        {
            return;
        }

        switch (dockable)
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
                foreach (IDockable child in dock.VisibleDockables)
                {
                    FindToolsInLayout(child);
                }
                break;
        }
    }
}
