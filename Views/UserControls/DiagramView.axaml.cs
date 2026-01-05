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

using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
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

    private bool _areAllEventHandlersCleanedUp;

    /// <summary>
    /// Tracks whether THIS View instance has initialized its WebView.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This flag is used to detect dock state change scenarios. When a new DiagramView instance
    /// is created (after dock state change), this flag is false. However, the ViewModel's
    /// <see cref="DiagramViewModel.IsReady"/> might still be true from the previous View instance.
    /// </para>
    /// <para>
    /// The combination of <c>_vm.IsReady == true</c> AND <c>_hasInitializedWebView == false</c>
    /// indicates a dock state change that requires re-initialization.
    /// </para>
    /// </remarks>
    private bool _hasInitializedWebView;

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

            // Unsubscribe from previous ViewModel first
            UnsubscribeViewModelEventHandlers(oldViewModel);

            _vm = newViewModel;

            // Avoid double-initialization on first load:
            // - When the control is not yet attached, OnAttachedToVisualTree will do the binding.
            // - When the control is already attached (runtime VM swap), bind immediately.
            if (_vm is not null && this.IsAttachedToVisualTree())
            {
                try
                {
                    SetupViewModelBindings();
                }
                catch
                {
                    // Best-effort cleanup to avoid partially-wired state if SetupViewModelBindings throws
                    UnsubscribeViewModelEventHandlers(_vm);
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
            // Keep this validation in the try block to ensure base is always called
            // If this was "hard cleaned up", this control instance is not reusable (semaphore disposed, theme unsubscribed, etc.).
            if (_areAllEventHandlersCleanedUp)
            {
                _logger.LogWarning("{ViewName} attached after hard cleanup; skipping rebind.", nameof(DiagramView));
                return;
            }

            if (DataContext is not DiagramViewModel dataContextViewModel)
            {
                return;
            }

            // If the VM changed (or we cleared it during detach), unwind old wiring and adopt the new VM reference.
            if (!ReferenceEquals(_vm, dataContextViewModel))
            {
                UnsubscribeViewModelEventHandlers(_vm);
                _vm = dataContextViewModel;
            }

            // Always ensure bindings on attach; wiring is idempotent.
            SetupViewModelBindings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebinding {ViewName} on attach.", nameof(DiagramView));

            // Best-effort: avoid leaving partially wired state around.
            try
            {
                UnsubscribeViewModelEventHandlers(_vm);
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
            // Keep this validation in the try block to ensure base is always called
            if (_areAllEventHandlersCleanedUp)
            {
                return;
            }

            // Clean up ONLY ViewModel event handlers here (for MDI scenarios)
            UnsubscribeViewModelEventHandlers(_vm);
            _vm = null;
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
    /// <remarks>
    /// <para>
    /// This method detects when a new View instance needs to initialize its WebView.
    /// This includes:
    /// <list type="bullet">
    ///     <item><description>Initial load (first time the View is attached)</description></item>
    ///     <item><description>Dock state changes (float, dock, pin) where the View is recreated</description></item>
    ///     <item><description>Reset Layout where both View and ViewModel are recreated</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The key insight is that <c>_hasInitializedWebView</c> tracks whether THIS View instance
    /// has performed initialization. When false, we always need to initialize, regardless of
    /// the ViewModel's <see cref="DiagramViewModel.IsReady"/> state.
    /// </para>
    /// </remarks>
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

        // Trigger re-initialization only when this View instance hasn't initialized its WebView yet
        // AND the ViewModel is ready. This avoids premature initialization during initial load.
        //
        // Scenarios:
        //  1. Dock state change (float/dock/pin): ViewModel.IsReady=true (from previous View) → auto-reinitialize
        //  2. Initial app load: ViewModel.IsReady=false → skip, let MainWindow.OnOpenedAsync() handle it
        //  3. Reset Layout: ViewModel.IsReady=false (new ViewModel) → skip, MainWindowViewModel.ResetLayoutAsync() handles it
        //
        // The key insight is that dock state changes preserve the ViewModel (IsReady stays true),
        // while Reset Layout and initial load create NEW ViewModels (IsReady starts false).
        // For these cases, the caller must explicitly trigger initialization.
        if (_vm.IsReady && !_hasInitializedWebView)
        {
            _logger.LogInformation("Detected dock state change: ViewModel was ready but this View instance hasn't initialized. " +
                "Triggering automatic re-initialization.");
            TriggerReinitialization();
        }
    }

    /// <summary>
    /// Triggers asynchronous re-initialization of the WebView after a dock state change.
    /// </summary>
    /// <remarks>
    /// Uses fire-and-forget pattern with proper error handling via SafeFireAndForget.
    /// </remarks>
    private void TriggerReinitialization()
    {
        ReinitializeWebViewAsync()
            .SafeFireAndForget(onException: ex =>
                _logger.LogError(ex, "Failed to reinitialize WebView after dock state change"));
    }

    /// <summary>
    /// Re-initializes the WebView with the last rendered source after a dock state change.
    /// </summary>
    /// <returns>A task representing the asynchronous re-initialization operation.</returns>
    private async Task ReinitializeWebViewAsync()
    {
        if (_vm is null)
        {
            _logger.LogWarning("Cannot reinitialize: ViewModel is null");
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool isSuccess = false;

        try
        {
            _logger.LogInformation("=== WebView Re-initialization Started (dock state change) ===");

            // Re-initialize with the stored source from previous renders
            await _vm.ReinitializeWithCurrentSourceAsync(Preview);
            _hasInitializedWebView = true;

            isSuccess = true;
            _logger.LogInformation("=== WebView Re-initialization Completed Successfully ===");
        }
        catch (Exception ex)
        {
            isSuccess = false;
            _logger.LogError(ex, "WebView re-initialization failed");
            _vm.LastError = $"Failed to reinitialize diagram preview: {ex.Message}";
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogTiming("WebView re-initialization", stopwatch.Elapsed, isSuccess);
        }
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

        _logger.LogInformation("=== {ViewName} Initialization Started ===", nameof(DiagramView));
        Stopwatch stopwatch = Stopwatch.StartNew();

        bool isSuccess = false;
        try
        {
            // Initialize renderer (starts HTTP server + navigate)
            await _vm.InitializeWithRenderingAsync(Preview, mermaidSource);

            // Mark this View instance as having initialized its WebView
            _hasInitializedWebView = true;

            isSuccess = true;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogTiming($"{nameof(DiagramView)} initialization completed", stopwatch.Elapsed, isSuccess);
        }
    }

    #endregion WebView Initialization

    #region Cleanup

    /// <summary>
    /// Unsubscribes all ViewModel-related event handlers and clears action delegates.
    /// </summary>
    /// <param name="viewModel">The ViewModel instance to unsubscribe from.</param>
    private static void UnsubscribeViewModelEventHandlers(DiagramViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        // Clear action delegates
        viewModel.InitializeActionAsync = null;
    }

    /// <summary>
    /// Unsubscribes all event handlers when the control is being disposed.
    /// </summary>
    internal void UnsubscribeAllEventHandlers()
    {
        // Prevent double-unsubscribe
        if (!_areAllEventHandlersCleanedUp)
        {
            UnsubscribeViewModelEventHandlers(_vm);
            _vm = null;

            _logger.LogInformation("All {ViewName} event handlers unsubscribed successfully", nameof(DiagramView));
            _areAllEventHandlersCleanedUp = true;
        }
        else
        {
            _logger.LogWarning($"{nameof(UnsubscribeAllEventHandlers)} called multiple times; skipping subsequent call");
        }
    }

    #endregion Cleanup
}
