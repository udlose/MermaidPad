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

using Dock.Model.Mvvm.Controls;
using MermaidPad.ViewModels.UserControls;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.Docking;

/// <summary>
/// Represents a dockable tool panel that wraps the <see cref="DiagramViewModel"/>.
/// This tool provides the Mermaid diagram preview within the docking layout.
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="Tool"/> from Dock.Model.Mvvm to integrate with the
/// Avalonia Dock library while delegating all diagram functionality to the wrapped
/// <see cref="DiagramViewModel"/>.
/// </para>
/// <para>
/// The tool is configured with <c>CanClose = false</c> to prevent users from closing
/// the panel, while still allowing docking, undocking, and pinning operations.
/// </para>
/// <para>
/// For MDI scenarios, each document would have its own instance of this tool,
/// created via the <see cref="Factories.IViewModelFactory"/>.
/// </para>
/// </remarks>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated via DockFactory.")]
internal sealed class DiagramToolViewModel : Tool
{
    /// <summary>
    /// The unique identifier for this tool type, used for layout serialization and restoration.
    /// </summary>
    internal const string ToolId = "DiagramTool";

    /// <summary>
    /// Gets the wrapped <see cref="DiagramViewModel"/> that provides diagram preview functionality.
    /// </summary>
    /// <remarks>
    /// This property is exposed for data binding in the tool's view. The view hosts a
    /// <see cref="Views.UserControls.DiagramView"/> with its DataContext bound to this property.
    /// </remarks>
    internal DiagramViewModel Diagram { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramToolViewModel"/> class.
    /// </summary>
    /// <param name="diagram">The <see cref="DiagramViewModel"/> to wrap. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="diagram"/> is null.</exception>
    public DiagramToolViewModel(DiagramViewModel diagram)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        Diagram = diagram;

        // Configure Dock tool properties
        Id = ToolId;
        Title = "Diagram Preview";

        // Prevent closing & floating but allow other operations
        CanClose = false;
        CanDrag = true;
        CanDrop = true;
        CanPin = true;
        CanFloat = false;
    }
}
