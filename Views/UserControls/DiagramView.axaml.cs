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

        _logger.LogInformation("=== DiagramView Initialization Started ===");

        // Subscribe to DataContext changes to wire up the ViewModel
        DataContextChanged += OnDataContextChanged;

        _logger.LogInformation("=== DiagramView Initialization Completed ===");
    }

    /// <summary>
    /// Handles changes to the DataContext, setting up or tearing down ViewModel bindings.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event arguments.</param>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel
        if (_vm is not null)
        {
            UnsubscribeViewModelEventHandlers();
        }

        // Set up new ViewModel
        if (DataContext is DiagramViewModel vm)
        {
            _vm = vm;
            SetupViewModelBindings();
        }
        else
        {
            _vm = null;
        }
    }

    /// <summary>
    /// Sets up bindings and action delegates between the View and ViewModel.
    /// </summary>
    private void SetupViewModelBindings()
    {
        if (_vm is null)
        {
            return;
        }

        // Wire up action delegates
        _vm.InitializeActionAsync = InitializeWebViewAsync;

        _areViewModelEventHandlersCleanedUp = false;

        _logger.LogInformation("ViewModel bindings established for DiagramView");
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
    private async Task InitializeWebViewAsync(string mermaidSource)
    {
        if (_vm is null)
        {
            _logger.LogWarning($"{nameof(InitializeWebViewAsync)} called with null ViewModel");
            return;
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
        if (!_areViewModelEventHandlersCleanedUp)
        {
            if (_vm?.InitializeActionAsync is not null)
            {
                // Clear action delegates
                _vm.InitializeActionAsync = null;
            }

            _areViewModelEventHandlersCleanedUp = true;
        }
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

            DataContextChanged -= OnDataContextChanged;

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
