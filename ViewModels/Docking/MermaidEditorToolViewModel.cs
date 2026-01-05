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

using CommunityToolkit.Mvvm.ComponentModel;
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
internal sealed partial class MermaidEditorToolViewModel : Tool
{
    /// <summary>
    /// The unique identifier for this tool type, used for layout serialization and restoration.
    /// </summary>
    internal const string ToolId = "MermaidEditorTool";

    /// <summary>
    /// Gets the wrapped <see cref="MermaidEditorViewModel"/> that provides editor functionality.
    /// </summary>
    /// <remarks>
    /// This property is exposed for data binding in the tool's view. The view hosts a
    /// <see cref="Views.UserControls.MermaidEditorView"/> with its DataContext bound to this property.
    /// </remarks>
    internal MermaidEditorViewModel Editor { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the editor tool's view is currently visible in the visual tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is set by <see cref="Views.Docking.MermaidEditorToolView"/> when the view is
    /// attached to or detached from the visual tree. It tracks the actual visibility state of the
    /// editor panel, which changes when the panel is:
    /// </para>
    /// <list type="bullet">
    ///     <item><description>Docked and visible (IsEditorVisible = true)</description></item>
    ///     <item><description>Floated in a separate window (IsEditorVisible = true)</description></item>
    ///     <item><description>Pinned/auto-hide and expanded on hover (IsEditorVisible = true)</description></item>
    ///     <item><description>Pinned/auto-hide and collapsed (IsEditorVisible = false)</description></item>
    /// </list>
    /// <para>
    /// When this property changes, it updates <see cref="MermaidEditorViewModel.IsToolVisible"/>
    /// and notifies all editor commands to re-evaluate their CanExecute state, effectively
    /// disabling editor-specific menu items and toolbar buttons when the editor is hidden.
    /// </para>
    /// <para>
    /// <b>Initialization Note:</b> This property is initialized to <c>true</c> to match the
    /// initial value of <see cref="MermaidEditorViewModel.IsToolVisible"/>. This ensures
    /// consistent state during initialization before the view is attached to the visual tree.
    /// The view will update this value via <see cref="Views.Docking.MermaidEditorToolView.OnAttachedToVisualTree"/>
    /// and <see cref="Views.Docking.MermaidEditorToolView.OnDetachedFromVisualTree"/>.
    /// </para>
    /// </remarks>
    [ObservableProperty]
    public partial bool IsEditorVisible { get; set; } = true;

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

    /// <summary>
    /// Handles changes to the <see cref="IsEditorVisible"/> property by propagating the visibility
    /// state to the wrapped editor ViewModel.
    /// </summary>
    /// <param name="value">The new visibility state.</param>
    /// <remarks>
    /// The <see cref="MermaidEditorViewModel.IsToolVisible"/> property has <c>[NotifyCanExecuteChangedFor]</c>
    /// attributes for all editor commands, so setting it automatically triggers command re-evaluation.
    /// </remarks>
    partial void OnIsEditorVisibleChanged(bool value)
    {
        // Propagate visibility state to the wrapped editor ViewModel.
        // The IsToolVisible property has [NotifyCanExecuteChangedFor] attributes for all commands,
        // so this automatically triggers command CanExecute re-evaluation.
        Editor.IsToolVisible = value;
    }
}
