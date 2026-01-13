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

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MermaidPad.ViewModels.Docking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MermaidPad.Views.Docking;

/// <summary>
/// A dockable tool view that hosts the <see cref="UserControls.MermaidEditorView"/> UserControl.
/// </summary>
/// <remarks>
/// <para>
/// This view serves as a thin wrapper around <see cref="UserControls.MermaidEditorView"/>,
/// enabling it to participate in the Avalonia Dock layout system. The view's DataContext
/// is <see cref="MermaidEditorToolViewModel"/>, which wraps the underlying
/// <see cref="ViewModels.UserControls.MermaidEditorViewModel"/>.
/// </para>
/// <para>
/// The actual editor functionality is entirely handled by the nested
/// <see cref="UserControls.MermaidEditorView"/> - this class only provides the docking integration
/// and tracks visual tree attachment state to enable/disable editor commands appropriately.
/// </para>
/// </remarks>
public sealed partial class MermaidEditorToolView : UserControl
{
    private ILogger<MermaidEditorToolView>? _logger;

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

    #region Overrides

    /// <summary>
    /// Called when the DataContext property changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This override handles DataContext changes that may occur when the dock system
    /// reassigns or clears the DataContext during layout operations (e.g., when a panel
    /// is floated, docked, or the layout is restored from serialization).
    /// </para>
    /// <para>
    /// When the DataContext changes:
    /// <list type="bullet">
    ///     <item><description>If set to a valid <see cref="MermaidEditorToolViewModel"/>, syncs the visibility state</description></item>
    ///     <item><description>If cleared (null), logs a warning for diagnostic purposes</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="e">The event arguments containing old and new DataContext values.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        // Call base implementation first
        base.OnDataContextChanged(e);

        // Lazy-initialize logger to avoid DI access during construction
        _logger ??= App.Services.GetService<ILogger<MermaidEditorToolView>>();

        if (DataContext is MermaidEditorToolViewModel toolViewModel)
        {
            // Sync the visibility state with the current visual tree attachment state
            // This handles the case where DataContext is set after the view is already in the visual tree
            bool isInVisualTree = this.IsAttachedToVisualTree();
            toolViewModel.IsEditorVisible = isInVisualTree;

            _logger?.LogDebug("{ViewName} DataContext set to {ViewModel}, IsEditorVisible={IsVisible}",
                nameof(MermaidEditorToolView), nameof(MermaidEditorToolViewModel), isInVisualTree);
        }
        else if (DataContext is null)
        {
            _logger?.LogWarning("{ViewName} DataContext was set to null - this may indicate a dock layout issue, unless the app is shutting down", nameof(MermaidEditorToolView));
        }
        else
        {
            _logger?.LogWarning("{ViewName} DataContext was set to unexpected type: {Type}", nameof(MermaidEditorToolView), DataContext.GetType().Name);
        }
    }

    /// <summary>
    /// Called when the control is attached to the visual tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method sets <see cref="MermaidEditorToolViewModel.IsEditorVisible"/> to <c>true</c>,
    /// which in turn enables all editor-specific commands (Cut, Copy, Paste, Undo, Redo, Find, etc.).
    /// </para>
    /// <para>
    /// This fires in the following scenarios:
    /// </para>
    /// <list type="bullet">
    ///     <item><description>Editor panel is docked and visible</description></item>
    ///     <item><description>Editor panel is floated into a separate window</description></item>
    ///     <item><description>Editor panel is pinned (auto-hide) and user hovers to expand it</description></item>
    /// </list>
    /// </remarks>
    /// <param name="e">The event arguments containing attachment information.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Always call base first to ensure proper attachment
        base.OnAttachedToVisualTree(e);

        try
        {
            if (DataContext is MermaidEditorToolViewModel toolVm)
            {
                toolVm.IsEditorVisible = true;
            }

            _logger?.LogDebug("{ViewName} attached to visual tree.", nameof(MermaidEditorToolView));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error rebinding {ViewName} on attach.", nameof(MermaidEditorToolView));

            // Best-effort: avoid leaving partially wired state around.
            try
            {
                UnsubscribeAllEventHandlers();
            }
            catch (Exception cleanupEx)
            {
                _logger?.LogError(cleanupEx, "Error during {ViewName} attach cleanup.", nameof(MermaidEditorToolView));
            }

            throw;
        }
    }

    /// <summary>
    /// Called when the control is detached from the visual tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method sets <see cref="MermaidEditorToolViewModel.IsEditorVisible"/> to <c>false</c>,
    /// which in turn disables all editor-specific commands (Cut, Copy, Paste, Undo, Redo, Find, etc.).
    /// This ensures menu items and toolbar buttons are grayed out when the editor is not visible.
    /// </para>
    /// <para>
    /// This fires in the following scenarios:
    /// </para>
    /// <list type="bullet">
    ///     <item><description>Editor panel is pinned (auto-hide) and collapses</description></item>
    ///     <item><description>Editor panel's floated window is closed</description></item>
    ///     <item><description>Application is shutting down</description></item>
    /// </list>
    /// </remarks>
    /// <param name="e">The event arguments containing detachment information.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        try
        {
            if (DataContext is MermaidEditorToolViewModel toolVm)
            {
                toolVm.IsEditorVisible = false;
            }

            _logger?.LogDebug("{ViewName} detached from visual tree.", nameof(MermaidEditorToolView));
        }
        finally
        {
            // Always call base last to ensure proper detachment
            base.OnDetachedFromVisualTree(e);
        }
    }

    #endregion Overrides
}
