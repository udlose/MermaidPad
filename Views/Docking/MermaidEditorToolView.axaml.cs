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

using Avalonia.Controls;

namespace MermaidPad.Views.Docking;

/// <summary>
/// A dockable tool view that hosts the <see cref="UserControls.MermaidEditorView"/> UserControl.
/// </summary>
/// <remarks>
/// <para>
/// This view serves as a thin wrapper around <see cref="UserControls.MermaidEditorView"/>,
/// enabling it to participate in the Avalonia Dock layout system. The view's DataContext
/// is <see cref="ViewModels.Docking.MermaidEditorToolViewModel"/>, which wraps the underlying
/// <see cref="ViewModels.UserControls.MermaidEditorViewModel"/>.
/// </para>
/// <para>
/// The actual editor functionality is entirely handled by the nested
/// <see cref="UserControls.MermaidEditorView"/> - this class only provides the docking integration.
/// </para>
/// </remarks>
public sealed partial class MermaidEditorToolView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidEditorToolView"/> class.
    /// </summary>
    public MermaidEditorToolView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the nested <see cref="UserControls.MermaidEditorView"/> instance.
    /// </summary>
    /// <remarks>
    /// This property provides access to the actual editor view for operations that
    /// need to interact with the editor directly, such as bringing focus to the editor
    /// or updating clipboard state.
    /// </remarks>
    public UserControls.MermaidEditorView EditorView => MermaidEditor;

    /// <summary>
    /// Brings focus to the nested editor control.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="UserControls.MermaidEditorView.BringFocusToEditor"/>.
    /// </remarks>
    public void BringFocusToEditor() => MermaidEditor.BringFocusToEditor();

    /// <summary>
    /// Updates the clipboard state when the containing window is activated.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="UserControls.MermaidEditorView.UpdateClipboardStateOnActivation"/>.
    /// </remarks>
    public void UpdateClipboardStateOnActivation() => MermaidEditor.UpdateClipboardStateOnActivation();

    /// <summary>
    /// Unsubscribes all event handlers from the nested editor view.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="UserControls.MermaidEditorView.UnsubscribeAllEventHandlers"/>.
    /// This should be called during cleanup to prevent memory leaks.
    /// </remarks>
    public void UnsubscribeAllEventHandlers() => MermaidEditor.UnsubscribeAllEventHandlers();
}
