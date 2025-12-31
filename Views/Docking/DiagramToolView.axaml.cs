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
using MermaidPad.ViewModels.Docking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MermaidPad.Views.Docking;

/// <summary>
/// A dockable tool view that hosts the <see cref="UserControls.DiagramView"/> UserControl.
/// </summary>
/// <remarks>
/// <para>
/// This view serves as a thin wrapper around <see cref="UserControls.DiagramView"/>,
/// enabling it to participate in the Avalonia Dock layout system. The view's DataContext
/// is <see cref="DiagramToolViewModel"/>, which wraps the underlying
/// <see cref="ViewModels.UserControls.DiagramViewModel"/>.
/// </para>
/// <para>
/// The actual diagram preview functionality is entirely handled by the nested
/// <see cref="UserControls.DiagramView"/> - this class only provides the docking integration.
/// </para>
/// </remarks>
public sealed partial class DiagramToolView : UserControl
{
    private ILogger<DiagramToolView>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramToolView"/> class.
    /// </summary>
    public DiagramToolView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the nested <see cref="UserControls.DiagramView"/> instance.
    /// </summary>
    /// <remarks>
    /// This property provides access to the actual diagram view for operations that
    /// need to interact with the diagram directly.
    /// </remarks>
    public UserControls.DiagramView DiagramView => DiagramPreview;

    /// <summary>
    /// Unsubscribes all event handlers from the nested diagram view.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="UserControls.DiagramView.UnsubscribeAllEventHandlers"/>.
    /// This should be called during cleanup to prevent memory leaks.
    /// </remarks>
    public void UnsubscribeAllEventHandlers() => DiagramPreview.UnsubscribeAllEventHandlers();

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
    ///     <item><description>If set to a valid <see cref="DiagramToolViewModel"/>, logs for diagnostics</description></item>
    ///     <item><description>If cleared (null), logs a warning for diagnostic purposes</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="e">The event arguments containing old and new DataContext values.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Lazy-initialize logger to avoid DI access during construction
        _logger ??= App.Services.GetService<ILogger<DiagramToolView>>();

        if (DataContext is DiagramToolViewModel _)
        {
            _logger?.LogDebug("{ViewName} DataContext set to {ViewModel}", nameof(DiagramToolView), nameof(DiagramToolViewModel));
        }
        else if (DataContext is null)
        {
            _logger?.LogWarning("{ViewName} DataContext was set to null - this may indicate a dock layout issue", nameof(DiagramToolView));
        }
        else
        {
            _logger?.LogWarning("{ViewName} DataContext was set to unexpected type: {Type}",
                nameof(DiagramToolView), DataContext.GetType().Name);
        }
    }

    #endregion Overrides
}
