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
using MermaidPad.ViewModels;

namespace MermaidPad.Infrastructure;

/// <summary>
/// Factory for creating and configuring the docking layout for MermaidPad.
/// </summary>
public class DockFactory : Factory
{
    private readonly MainViewModel _mainViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockFactory"/> class.
    /// </summary>
    /// <param name="mainViewModel">The main view model containing the panel view models.</param>
    public DockFactory(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    /// <summary>
    /// Creates the initial docking layout with three panels: Editor, Preview, and AI Assistant.
    /// </summary>
    /// <returns>The root dock element containing the layout.</returns>
    public override IDock CreateLayout()
    {
        // Create tool docks for each panel
        var editorTool = new Tool
        {
            Id = "Editor",
            Title = "Editor",
            CanClose = false,
            CanFloat = true,
            CanPin = true,
            Context = _mainViewModel
        };

        var previewTool = new Tool
        {
            Id = "Preview",
            Title = "Preview",
            CanClose = false,
            CanFloat = true,
            CanPin = true,
            Context = _mainViewModel
        };

        var aiTool = new Tool
        {
            Id = "AIAssistant",
            Title = "AI Assistant",
            CanClose = false,
            CanFloat = true,
            CanPin = true,
            Context = _mainViewModel.AIPanelViewModel
        };

        // Create proportional dock splitter for horizontal layout (Editor | Preview | AI)
        var proportionalDock = new ProportionalDock
        {
            Id = "MainProportionalDock",
            Title = "MainProportionalDock",
            Orientation = Orientation.Horizontal,
            Proportion = double.NaN,
            VisibleDockables = CreateList<IDockable>
            (
                new ToolDock
                {
                    Id = "EditorDock",
                    Title = "EditorDock",
                    Proportion = 0.33,
                    VisibleDockables = CreateList<IDockable>(editorTool),
                    ActiveDockable = editorTool
                },
                new ProportionalDockSplitter
                {
                    Id = "Splitter1",
                    Title = "Splitter1"
                },
                new ToolDock
                {
                    Id = "PreviewDock",
                    Title = "PreviewDock",
                    Proportion = 0.34,
                    VisibleDockables = CreateList<IDockable>(previewTool),
                    ActiveDockable = previewTool
                },
                new ProportionalDockSplitter
                {
                    Id = "Splitter2",
                    Title = "Splitter2"
                },
                new ToolDock
                {
                    Id = "AIDock",
                    Title = "AIDock",
                    Proportion = 0.33,
                    VisibleDockables = CreateList<IDockable>(aiTool),
                    ActiveDockable = aiTool
                }
            )
        };

        // Create root dock
        var rootDock = new RootDock
        {
            Id = "Root",
            Title = "Root",
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(proportionalDock),
            ActiveDockable = proportionalDock,
            DefaultDockable = proportionalDock
        };

        return rootDock;
    }

    /// <summary>
    /// Initializes the docking layout.
    /// </summary>
    /// <param name="layout">The root dock layout to initialize.</param>
    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["Editor"] = () => _mainViewModel,
            ["Preview"] = () => _mainViewModel,
            ["AIAssistant"] = () => _mainViewModel.AIPanelViewModel
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }
}
