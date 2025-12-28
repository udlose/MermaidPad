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
/// Represents a dockable tool panel that wraps the <see cref="MermaidEditorViewModel"/>.
/// This tool provides the Mermaid source code editor within the docking layout.
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="Tool"/> from Dock.Model.Mvvm to integrate with the
/// Avalonia Dock library while delegating all editor functionality to the wrapped
/// <see cref="MermaidEditorViewModel"/>.
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
internal sealed class MermaidEditorToolViewModel : Tool
{
    /// <summary>
    /// The unique identifier for this tool type, used for layout serialization and restoration.
    /// </summary>
    private const string ToolId = "MermaidEditorTool";

    /// <summary>
    /// Gets the wrapped <see cref="MermaidEditorViewModel"/> that provides editor functionality.
    /// </summary>
    /// <remarks>
    /// This property is exposed for data binding in the tool's view. The view hosts a
    /// <see cref="Views.UserControls.MermaidEditorView"/> with its DataContext bound to this property.
    /// </remarks>
    internal MermaidEditorViewModel Editor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidEditorToolViewModel"/> class.
    /// </summary>
    /// <param name="editor">The <see cref="MermaidEditorViewModel"/> to wrap. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="editor"/> is null.</exception>
    public MermaidEditorToolViewModel(MermaidEditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        Editor = editor;

        // Configure Dock tool properties
        Id = ToolId;
        Title = "Mermaid Editor";

        // Prevent closing but allow other operations
        CanClose = false;
        CanPin = true;
        CanFloat = true;
    }
}
