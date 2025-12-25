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
using MermaidPad.Exceptions.Assets;
using MermaidPad.Extensions;
using MermaidPad.ViewModels.UserControls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MermaidPad.Views.UserControls;

/// <summary>
/// A UserControl that provides a WebView for rendering Mermaid diagrams with pan/zoom capabilities.
/// </summary>
/// <remarks>
/// This control encapsulates all WebView-related functionality including:
/// <list type="bullet">
///     <item><description>WebView initialization and lifecycle management</description></item>
///     <item><description>MermaidRenderer coordination</description></item>
///     <item><description>Error display overlay</description></item>
///     <item><description>Proper event handler cleanup</description></item>
/// </list>
/// </remarks>
public sealed partial class DiagramView : UserControl
{
    private DiagramViewModel? _vm;
    private readonly ILogger<DiagramView> _logger;

    private bool _areViewModelEventHandlersCleanedUp;
    private bool _areAllEventHandlersCleanedUp;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramView"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor resolves required services from the application's DI container.
    /// The ViewModel is expected to be set via the DataContext property.
    /// </remarks>
    public DiagramView()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _logger = sp.GetRequiredService<ILogger<DiagramView>>();
    }

    #region Overrides

    /// <summary>
    /// Handles changes to the data context by updating event subscriptions and bindings to the associated view model.
    /// </summary>
    /// <remarks>This method ensures that event handlers and bindings are correctly updated when the data
    /// context changes, preventing memory leaks and ensuring the view reflects the current view model. It is typically
    /// called by the framework when the data context of the control changes.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> object that contains the event data.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        try
        {
            DiagramViewModel? oldViewModel = _vm;
            DiagramViewModel? newViewModel = DataContext as DiagramViewModel;
            if (oldViewModel is not null)
            {
                // Ensure UnsubscribeViewModelEventHandlers() operates on the old VM
                _vm = oldViewModel;

                // Unsubscribe from previous ViewModel first
                UnsubscribeViewModelEventHandlers();
            }

            _vm = newViewModel;

            if (_vm is not null)
            {
                try
                {
                    SetupViewModelBindings();
                }
                catch
                {
                    // Best-effort cleanup to avoid partially-wired state if SetupViewModelBindings throws
                    UnsubscribeViewModelEventHandlers();
                    _vm = null;
                    throw;
                }
            }
        }
        finally
        {
            // Call base method last
            base.OnDataContextChanged(e);
        }
    }

    /// <summary>
    /// Handles logic that occurs when the control is attached to the visual tree.
    /// </summary>
    /// <remarks>This method restores necessary bindings and event handlers when the control is reattached to
    /// the visual tree, provided the control has not been fully cleaned up. If the control was previously detached and
    /// partially cleaned up, this method ensures that the view model bindings are re-established. If the control has
    /// undergone a full cleanup, no re-binding occurs.</remarks>
    /// <param name="e">The event data associated with the visual tree attachment event.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        try
        {
            if (_areAllEventHandlersCleanedUp)
            {
                _logger.LogWarning("{ViewName} attached after hard cleanup; skipping rebind.", nameof(DiagramView));
                return;
            }

            if (DataContext is not DiagramViewModel dataContextViewModel)
            {
                return;
            }

            if (!ReferenceEquals(_vm, dataContextViewModel))
            {
                if (_vm is not null)
                {
                    UnsubscribeViewModelEventHandlers();
                }

                _vm = dataContextViewModel;
            }

            if (_areViewModelEventHandlersCleanedUp)
            {
                SetupViewModelBindings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebinding {ViewName} on attach.", nameof(DiagramView));

            try
            {
                UnsubscribeViewModelEventHandlers();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Error during {ViewName} attach cleanup.", nameof(DiagramView));
            }

            _vm = null;
            throw;
        }
        finally
        {
            base.OnAttachedToVisualTree(e);
        }
    }

    /// <summary>
    /// Called when the control is detached from the visual tree.
    /// </summary>
    /// <remarks>Override this method to perform cleanup or release resources when the control is removed from
    /// the visual tree. This method is called after the control is no longer part of the visual tree
    /// hierarchy.</remarks>
    /// <param name="e">The event data associated with the detachment from the visual tree.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        try
        {
            if (!_areAllEventHandlersCleanedUp && _vm is not null)
            {
                // Clean up ONLY ViewModel event handlers here (for MDI scenarios)
                UnsubscribeViewModelEventHandlers();
                _vm = null;
            }
        }
        finally
        {
            base.OnDetachedFromVisualTree(e);
        }
    }

    #endregion Overrides

    /// <summary>
    /// Sets up bindings and action delegates between the View and ViewModel.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the ViewModel is null when this method is called.</exception>
    private void SetupViewModelBindings()
    {
        if (_vm is null)
        {
            throw new InvalidOperationException($"{nameof(SetupViewModelBindings)} called with null ViewModel. Initialize ViewModel before calling this method.");
        }

        // Wire up action delegates
        _vm.InitializeActionAsync = null; // Clear any existing delegate
        _vm.InitializeActionAsync = InitializeWebViewAsync;

        _areViewModelEventHandlersCleanedUp = false;
    }

    #region WebView Initialization

    /// <summary>
    /// Initializes the WebView and prepares it to render the specified Mermaid diagram source asynchronously.
    /// </summary>
    /// <remarks>This method starts the WebView rendering process and logs initialization timing information.
    /// If the associated ViewModel is null, the method logs a warning and does not perform initialization.</remarks>
    /// <param name="mermaidSource">The Mermaid source code to render. If the source is null, empty,
    /// or consists only of whitespace, the output in the WebView will be cleared instead.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    /// <exception cref="OperationCanceledException">Propagated if initialization is canceled.</exception>
    /// <exception cref="AssetIntegrityException">Propagated for asset integrity errors.</exception>
    /// <exception cref="MissingAssetException">Propagated when required assets are missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the ViewModel is null when this method is called.</exception>
    private async Task InitializeWebViewAsync(string mermaidSource)
    {
        if (_vm is null)
        {
            throw new InvalidOperationException($"{nameof(InitializeWebViewAsync)} called with null ViewModel. Initialize ViewModel before calling this method.");
        }

        _logger.LogInformation("=== WebView Initialization Started ===");
        Stopwatch stopwatch = Stopwatch.StartNew();

        bool success = false;
        try
        {
            // Initialize renderer (starts HTTP server + navigate)
            await _vm.InitializeWithRenderingAsync(Preview, mermaidSource);

            success = true;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogTiming("WebView initialization", stopwatch.Elapsed, success);
        }
    }

    #endregion WebView Initialization

    #region Cleanup

    /// <summary>
    /// Unsubscribes all ViewModel-related event handlers and clears action delegates.
    /// </summary>
    private void UnsubscribeViewModelEventHandlers()
    {
        // Prevent double-unsubscribe
        if (_areViewModelEventHandlersCleanedUp)
        {
            return;
        }

#pragma warning disable IDE0031
        if (_vm is not null)
#pragma warning restore IDE0031
        {
            // Clear action delegates
            _vm.InitializeActionAsync = null;
        }

        _areViewModelEventHandlersCleanedUp = true;
    }

    /// <summary>
    /// Unsubscribes all event handlers when the control is being disposed.
    /// </summary>
    internal void UnsubscribeAllEventHandlers()
    {
        // Prevent double-unsubscribe
        if (!_areAllEventHandlersCleanedUp)
        {
            UnsubscribeViewModelEventHandlers();

            _logger.LogInformation("All DiagramView event handlers unsubscribed successfully");
            _areAllEventHandlersCleanedUp = true;
        }
        else
        {
            _logger.LogWarning($"{nameof(UnsubscribeAllEventHandlers)} called multiple times; skipping subsequent call");
        }
    }

    #endregion Cleanup
}
