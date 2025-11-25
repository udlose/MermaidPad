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
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using MermaidPad.ViewModels.Panels;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Factories;

/// <summary>
/// Factory for creating and configuring the docking layout for MermaidPad.
/// </summary>
/// <remarks>
/// This factory follows the ContextLocator pattern from the Dock library documentation.
/// The caller (MainViewModel) is responsible for setting the ContextLocator property
/// before calling InitLayout. This avoids circular dependencies and follows the
/// standard Dock library pattern where the Factory creates structure and the caller
/// provides the ViewModel mappings.
/// </remarks>
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Improves readability")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated via dependency injection")]
public sealed class DockFactory : Factory
{
    private const string RootDockId = "Root";
    private const string EditorDockId = "EditorView";
    private const string PreviewDockId = "PreviewView";
    private const string AIDockId = "AIView";

    private IRootDock? _rootDock;
    private IDockable? _editorTool;
    private IDockable? _previewTool;
    private IDockable? _aiTool;

    private readonly ILogger<DockFactory>? _logger;
    private readonly EditorViewModel _editorViewModel;
    private readonly PreviewViewModel _previewViewModel;
    private readonly AIViewModel _aiViewModel;

    ///// <summary>
    ///// Initializes a new instance of the <see cref="DockFactory"/> class.
    ///// </summary>
    ///// <param name="editorViewModel">The editor panel view model.</param>
    ///// <param name="previewViewModel">The preview panel view model.</param>
    ///// <param name="aiViewModel">The AI view model.</param>
    ///// <param name="logger">Optional logger for diagnostic output. May be null during bootstrapping.</param>
    public DockFactory(
        //EditorViewModel editorViewModel,
        //PreviewViewModel previewViewModel,
        //AIViewModel aiViewModel,
        ILogger<DockFactory>? logger = null)
    {
        //_editorViewModel = editorViewModel;
        //_previewViewModel = previewViewModel;
        //_aiViewModel = aiViewModel;
        _logger = logger;
    }


    /// <summary>
    /// Creates and configures the root dock layout for the application, including editor, preview, and AI assistant
    /// panels arranged horizontally.
    /// </summary>
    /// <remarks>
    /// The returned layout includes three tool docks—Editor, Preview, and AI Assistant—organized in
    /// a horizontal split. Contexts are set via ContextLocator in InitLayout, following the recommended
    /// pattern from the Dock library documentation.
    /// </remarks>
    /// <returns>An <see cref="IRootDock"/> instance representing the main layout with editor, preview, and AI assistant tool
    /// docks.</returns>
    public override IRootDock CreateLayout()
    {
        // Create tool docks for each panel
        // Note: Contexts will be assigned via ContextLocator in InitLayout()
        Tool editorTool = new Tool
        {
            Id = EditorDockId,
            Title = "Editor",
            CanClose = false,
            CanFloat = true,
            CanPin = true
        };

        Tool previewTool = new Tool
        {
            Id = PreviewDockId,
            Title = "Preview",
            CanClose = false,
            CanFloat = true,
            CanPin = true
        };

        Tool aiTool = new Tool
        {
            Id = AIDockId,
            Title = "AI Assistant",
            CanClose = false,
            CanFloat = true,
            CanPin = true
        };

        // Wrap each Tool in a ToolDock - this is required for proper rendering
        ToolDock editorToolDock = new ToolDock
        {
            Id = "EditorToolDock",
            Title = "EditorToolDock",
            Proportion = double.NaN,
            ActiveDockable = editorTool,
            VisibleDockables = CreateList<IDockable>(editorTool)
        };

        ToolDock previewToolDock = new ToolDock
        {
            Id = "PreviewToolDock",
            Title = "PreviewToolDock",
            Proportion = double.NaN,
            ActiveDockable = previewTool,
            VisibleDockables = CreateList<IDockable>(previewTool)
        };

        ToolDock aiToolDock = new ToolDock
        {
            Id = "AIToolDock",
            Title = "AIToolDock",
            Proportion = double.NaN,
            ActiveDockable = aiTool,
            VisibleDockables = CreateList<IDockable>(aiTool)
        };

        // Create proportional dock splitter for horizontal layout (Editor | Preview | AI)
        ProportionalDock proportionalDock = new ProportionalDock
        {
            Id = "MainProportionalDock",
            Title = "MainProportionalDock",
            Orientation = Orientation.Horizontal,
            Proportion = double.NaN,
            VisibleDockables = CreateList<IDockable>
            (
                editorToolDock,
                new ProportionalDockSplitter
                {
                    Id = "Splitter1",
                    Title = "Splitter1"
                },
                previewToolDock,
                new ProportionalDockSplitter
                {
                    Id = "Splitter2",
                    Title = "Splitter2"
                },
                aiToolDock
            ),
            ActiveDockable = editorToolDock
        };

        // Create root dock
        IRootDock rootDock = CreateRootDock();
        rootDock.Id = RootDockId;
        rootDock.Title = RootDockId;
        rootDock.IsCollapsable = false;
        rootDock.VisibleDockables = CreateList<IDockable>(proportionalDock);
        rootDock.ActiveDockable = proportionalDock;
        rootDock.DefaultDockable = proportionalDock;

        _rootDock = rootDock;
        _editorTool = editorTool;
        _previewTool = previewTool;
        _aiTool = aiTool;

        return rootDock;
    }

    /// <summary>
    /// Initializes the docking layout by configuring:
    /// <list type="bullet">
    /// <item><see cref="FactoryBase.DefaultHostWindowLocator"/></item>
    /// <item><see cref="FactoryBase.HostWindowLocator"/></item>
    /// <item><see cref="FactoryBase.DockableLocator"/></item>
    /// </list>
    /// </summary>
    /// <remarks>This method sets up default locators for host windows and dockable elements, enabling correct
    /// instantiation and retrieval of docking components. Callers should ensure that:
    /// <list type="number">
    ///     <item>The <see cref="FactoryBase.ContextLocator"/> is set by the caller before invoking this method.</item>
    ///     <item>The <see cref="FactoryBase.DefaultContextLocator"/> is set by the caller before invoking this method.</item>
    /// </list>
    /// </remarks>
    /// <param name="layout">The docking layout to initialize. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when layout is null.</exception>
    public override void InitLayout(IDockable layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        // ContextLocator maps Tool IDs to ViewModels - DataTemplates will render them as Views
        //ContextLocator = new Dictionary<string, Func<object?>>
        //{
        //    [EditorDockId] = () => _editorViewModel,
        //    [PreviewDockId] = () => _previewViewModel,
        //    [AIDockId] = () => _aiViewModel
        //};
        DefaultContextLocator = () => this;

        // Provide a DefaultHostWindowLocator implementation
        DefaultHostWindowLocator = static () => new HostWindow();

        // Set up HostWindowLocator for floating windows
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = static () => new HostWindow()
        };

        // Set up the DockableLocator for layout deserialization
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            [RootDockId] = () => _rootDock,
            [EditorDockId] = () => _editorTool,
            [PreviewDockId] = () => _previewTool,
            [AIDockId] = () => _aiTool
        };

        base.InitLayout(layout);
    }
}
