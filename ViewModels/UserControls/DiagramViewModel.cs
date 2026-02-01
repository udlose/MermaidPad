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

using Avalonia.Threading;
using AvaloniaWebView;
using CommunityToolkit.Mvvm.ComponentModel;
using MermaidPad.Exceptions.Assets;
using MermaidPad.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace MermaidPad.ViewModels.UserControls;

/// <summary>
/// Represents the ViewModel for the DiagramView UserControl, providing properties and actions
/// for WebView initialization, rendering, and state management.
/// </summary>
/// <remarks>
/// This ViewModel exposes state properties for data binding in the diagram preview UserControl, including
/// readiness state and error information. It coordinates interactions between the user interface
/// and the MermaidRenderer service. All properties and commands are designed for use with MVVM
/// frameworks and are intended to be accessed by the View for UI updates and user interactions.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated via ViewModelFactory.")]
internal sealed partial class DiagramViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<DiagramViewModel> _logger;
    private readonly MermaidRenderer _mermaidRenderer;

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "Intentionally not disposing _renderGate to avoid ObjectDisposedException in any in-flight/pending WaitAsync callers during shutdown")]
    private readonly SemaphoreSlim _renderGate = new SemaphoreSlim(1, 1);
    private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();
    private volatile bool _isDisposed;

    /// <summary>
    /// Monotonic, atomic render id used for last-write-wins
    /// </summary>
    private long _renderSequence;

    private const int WebViewReadyTimeoutSeconds = 30;

    /// <summary>
    /// Stores the last rendered Mermaid source for re-initialization after dock state changes.
    /// </summary>
    /// <remarks>
    /// When a dock state change (float, dock, pin) destroys and recreates the View,
    /// this stored source allows automatic re-rendering without requiring access to
    /// the editor text from the MainWindowViewModel.
    /// </remarks>
    private string _lastRenderedSource = string.Empty;

    #region State Properties

    /// <summary>
    /// Gets or sets a value indicating whether the WebView is ready for rendering operations.
    /// </summary>
    [ObservableProperty]
    public partial bool IsReady { get; set; }

    /// <summary>
    /// Gets or sets the last error message, if any.
    /// </summary>
    [ObservableProperty]
    public partial string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the width of the currently rendered diagram canvas in pixels.
    /// </summary>
    /// <remarks>
    /// This value is extracted from the SVG viewBox or bounding box after each render.
    /// A null value indicates no diagram is currently rendered.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCanvasSize))]
    public partial double? CanvasWidth { get; set; }

    /// <summary>
    /// Gets or sets the height of the currently rendered diagram canvas in pixels.
    /// </summary>
    /// <remarks>
    /// This value is extracted from the SVG viewBox or bounding box after each render.
    /// A null value indicates no diagram is currently rendered.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCanvasSize))]
    public partial double? CanvasHeight { get; set; }

    /// <summary>
    /// Gets a value indicating whether valid canvas dimensions are available.
    /// </summary>
    public bool HasCanvasSize => CanvasWidth.HasValue && CanvasHeight.HasValue &&
                                  CanvasWidth.Value > 0 && CanvasHeight.Value > 0;

    #endregion State Properties

    #region Action Delegates

    /// <summary>
    /// Gets or sets the function to invoke when initializing the WebView.
    /// </summary>
    /// <remarks>
    /// This function is set by DiagramView to implement the actual WebView initialization.
    /// The function should initialize the MermaidRenderer with the WebView control and
    /// perform the initial render of the current diagram text.
    /// </remarks>
    public Func<string, Task>? InitializeActionAsync { get; internal set; }

    #endregion Action Delegates

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramViewModel"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger{DiagramViewModel}"/> instance for this view model.</param>
    /// <param name="mermaidRenderer">The <see cref="MermaidRenderer"/> service for diagram rendering.</param>
    public DiagramViewModel(ILogger<DiagramViewModel> logger, MermaidRenderer mermaidRenderer)
    {
        _logger = logger;
        _mermaidRenderer = mermaidRenderer;
    }

    /// <summary>
    /// Initializes the WebView asynchronously by invoking the initialization action set by the View.
    /// </summary>
    /// <param name="mermaidSource">The Mermaid source code to render. If the source is null, empty,
    /// or consists only of whitespace, the output in the WebView will be cleared instead.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if InitializeAction has not been set.</exception>
    public Task InitializeAsync(string mermaidSource)
    {
        // Do not check mermaidSource; null, empty and whitespace are valid values because that clears the diagram
        if (InitializeActionAsync is null)
        {
            const string error = $"{nameof(InitializeActionAsync)} was not correctly set by DiagramView; fix the implementation so this delegate is initialized correctly. The WebView initialization action is not available.";
            _logger.LogCritical(error);
            throw new InvalidOperationException(error);
        }

        return InitializeActionAsync(mermaidSource);
    }

    /// <summary>
    /// Prepares the ViewModel for re-initialization after the View has been detached and re-attached
    /// (e.g., after a dock state change such as float, dock, or pin).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method resets the ready state so commands properly reflect that the WebView needs
    /// initialization. It should be called when a new View instance attaches to this ViewModel
    /// but the ViewModel was previously in a ready state from a prior View instance.
    /// </para>
    /// <para>
    /// This happens during dock operations where the View is destroyed and recreated but the
    /// ViewModel persists (since it's held by the DockFactory).
    /// </para>
    /// <para>
    /// <b>MDI Migration Note:</b> For MDI, each document's DiagramViewModel would have its own
    /// lifecycle. This method remains useful for dock state changes within a single document.
    /// </para>
    /// </remarks>
    internal async Task PrepareForReinitializationAsync()
    {
        //TODO - DaveBlack: MDI Migration Note: for MDI, each document's DiagramViewModel would have its own lifecycle. This method remains useful for dock state changes within a single document.
        _logger.LogInformation("Preparing {ModelName} for re-initialization (dock state change detected)", nameof(DiagramViewModel));

        if (Dispatcher.UIThread.CheckAccess())
        {
            PreparePropertiesForReinitialization();
            return;
        }

        // We need to ensure ordering here between the property reset and any subsequent re-initialization logic
        // that runs after this method completes. Callers rely on PrepareForReinitializationAsync having fully
        // cleared UI-bound state (IsReady, LastError, CanvasWidth, CanvasHeight) before they continue.
        // By awaiting the InvokeAsync task (instead of fire-and-forget via Dispatcher.UIThread.Post),
        // we guarantee that the dispatcher work executes on the UI thread and finishes before this method returns to its caller.
        await Dispatcher.UIThread
            .InvokeAsync(PreparePropertiesForReinitialization)
            .GetTask()
            .ConfigureAwait(false);

        void PreparePropertiesForReinitialization()
        {
            IsReady = false;
            LastError = null;

            // Clear canvas size since WebView is being re-initialized
            CanvasWidth = null;
            CanvasHeight = null;
        }
    }

    /// <summary>
    /// Re-initializes the WebView with the last rendered source after a dock state change.
    /// </summary>
    /// <param name="preview">The new WebView instance to initialize.</param>
    /// <returns>A task representing the asynchronous re-initialization operation.</returns>
    /// <remarks>
    /// <para>
    /// This method is called by DiagramView when it detects that it's a new View instance
    /// attached to an existing, previously-initialized ViewModel. This happens during dock
    /// operations (float, dock, pin) where the View is destroyed and recreated.
    /// </para>
    /// <para>
    /// The method uses the stored <see cref="_lastRenderedSource"/> to re-render the diagram
    /// automatically, providing seamless UX during dock state changes.
    /// </para>
    /// </remarks>
    internal async Task ReinitializeWithCurrentSourceAsync(WebView preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        _logger.LogInformation("Re-initializing WebView with stored source ({SourceLength} chars)", _lastRenderedSource.Length);

        await PrepareForReinitializationAsync();
        await InitializeWithRenderingAsync(preview, _lastRenderedSource);
    }

    /// <summary>
    /// Renders a Mermaid diagram asynchronously by invoking the render action set by the View.
    /// </summary>
    /// <param name="mermaidSource">The Mermaid source code to render. If the source is null, empty,
    /// or consists only of whitespace, the output in the WebView will be cleared instead.</param>
    /// <returns>A task representing the asynchronous render operation.</returns>
    /// <remarks>
    /// This method stores the source for potential re-initialization after dock state changes.
    /// </remarks>
    public async Task RenderAsync(string mermaidSource)
    {
        // Do not check mermaidSource; null, empty and whitespace are valid values because that clears the diagram

        // Store source for potential re-initialization after dock state changes
        _lastRenderedSource = mermaidSource;

        try
        {
            long requestId = Interlocked.Increment(ref _renderSequence);
            bool isRenderSuccess = await RenderCoreAsync(requestId, mermaidSource);
            if (isRenderSuccess)
            {
                // Update canvas size after initial render
                await UpdateCanvasSizeAsync()
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            // Shutdown cancellation; ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {Method}", nameof(RenderAsync));

            await Dispatcher.UIThread.InvokeAsync(() =>
                    LastError = $"Failed to render diagram ({ex.GetType().Name}): {ex.Message}")
                .GetTask()
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs the core asynchronous rendering operation for a Mermaid diagram associated with the specified request.
    /// </summary>
    /// <remarks>Rendering requests are serialized to prevent overlapping operations. If a newer render
    /// request arrives while a previous one is pending, only the latest request is processed.</remarks>
    /// <param name="requestId">The unique identifier for the render request. Only the most recent request is processed; earlier requests may be
    /// skipped if superseded.</param>
    /// <param name="mermaidSource">The Mermaid diagram source code to render. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous rendering operation, returning true if the render was successful,
    /// false otherwise.</returns>
    private async Task<bool> RenderCoreAsync(long requestId, string mermaidSource)
    {
        bool isRenderSuccess = false;
        bool isAcquired = false;
        try
        {
            // Serialize WebView access so LivePreview cannot overlap renders
            await _renderGate.WaitAsync(_shutdownCts.Token)
                .ConfigureAwait(false);

            isAcquired = true;

            // If a newer render request arrived while this one was queued, skip it
            long latestRequestId = Interlocked.Read(ref _renderSequence);
            if (requestId != latestRequestId)
            {
                return false;
            }

            // MermaidRenderer internally marshals to the UI thread when needed
            await _mermaidRenderer.RenderAsync(mermaidSource)
                .ConfigureAwait(false);

            isRenderSuccess = true;
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            // Shutdown cancellation; ignore
        }
        finally
        {
            if (isAcquired)
            {
                _renderGate.Release();
            }
        }

        return isRenderSuccess;
    }

    /// <summary>
    /// Initializes the Mermaid renderer with the specified WebView and prepares it for rendering the provided Mermaid
    /// diagram source asynchronously.
    /// </summary>
    /// <remarks>If the WebView does not become ready within the configured timeout, initialization continues
    /// with a warning and some features may not function correctly. Asset-related exceptions are propagated to the
    /// caller for higher-level handling. This method must be awaited to ensure the renderer is fully initialized before
    /// issuing rendering commands.</remarks>
    /// <param name="preview">The WebView instance to be initialized for rendering Mermaid diagrams. Cannot be null.</param>
    /// <param name="mermaidSource">The Mermaid source code to render. If the source is null, empty,
    /// or consists only of whitespace, the output in the WebView will be cleared instead.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="preview"/> is null.</exception>
    public Task InitializeWithRenderingAsync(WebView preview, string mermaidSource)
    {
        ArgumentNullException.ThrowIfNull(preview);
        // Do not check mermaidSource; null, empty and whitespace are valid values because that clears the diagram

        return InitializeWithRenderingCoreAsync(preview, mermaidSource);
    }

    /// <summary>
    /// Initializes the Mermaid rendering environment using the specified WebView and Mermaid source code
    /// asynchronously.
    /// </summary>
    /// <remarks>If the WebView does not become ready within the configured timeout, initialization continues
    /// with a warning and some features may not function correctly. Asset-related exceptions are propagated to the
    /// caller for higher-level handling. The method logs initialization progress and errors for diagnostic
    /// purposes.</remarks>
    /// <param name="preview">The WebView instance to be initialized for rendering Mermaid diagrams. Must not be null.</param>
    /// <param name="mermaidSource">The Mermaid source code to render. If the source is null, empty,
    /// or consists only of whitespace, the output in the WebView will be cleared instead.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    private async Task InitializeWithRenderingCoreAsync(WebView preview, string mermaidSource)
    {
        try
        {
            // Store source for potential re-initialization after dock state changes
            _lastRenderedSource = mermaidSource;

            // Initialize MermaidRenderer with WebView
            await _mermaidRenderer.InitializeAsync(preview);

            // Kick first render; index.html sets globalThis.__renderingComplete__ in hideLoadingIndicator()
            await _mermaidRenderer.RenderAsync(mermaidSource);

            // Await readiness
            try
            {
                await _mermaidRenderer.EnsureFirstRenderReadyAsync(TimeSpan.FromSeconds(WebViewReadyTimeoutSeconds))
                    .ConfigureAwait(false);

                await Dispatcher.UIThread.InvokeAsync(() => IsReady = true)
                    .GetTask()
                    .ConfigureAwait(false);

                // Update canvas size after successful initialization
                await UpdateCanvasSizeAsync()
                    .ConfigureAwait(false);

                _logger.LogInformation("WebView readiness observed");
            }
            catch (TimeoutException tex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsReady = true;
                    LastError = $"WebView initialization timed out after {WebViewReadyTimeoutSeconds} seconds. Some features may not work correctly.";
                });
                _logger.LogWarning(tex, "WebView readiness timed out after {TimeoutSeconds}s; enabling with warning", WebViewReadyTimeoutSeconds);
            }

            _logger.LogInformation("=== WebView Initialization Completed Successfully ===");
        }
        catch (OperationCanceledException ocex)
        {
            // Treat cancellations distinctly; still propagate
            _logger.LogInformation(ocex, "WebView initialization was canceled.");
            throw;
        }
        catch (Exception ex) when (ex is AssetIntegrityException or MissingAssetException)
        {
            // Let asset-related exceptions bubble up for higher-level handling
            throw;
        }
        catch (Exception ex)
        {
            // Log and rethrow so caller can handle the failure
            _logger.LogError(ex, "Error during {Method}", nameof(InitializeWithRenderingAsync));
            throw;
        }
    }

    /// <summary>
    /// Updates the canvas size properties by querying the rendered SVG dimensions from the WebView.
    /// </summary>
    /// <remarks>
    /// This method executes JavaScript in the WebView to extract the current SVG dimensions
    /// from the viewBox or bounding box. It should be called after each successful render
    /// to keep the displayed dimensions in sync with the actual diagram.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task UpdateCanvasSizeAsync()
    {
        try
        {
            const string script = "getDiagramDimensions()";
            string? result = await _mermaidRenderer.ExecuteScriptAsync(script)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result))
            {
                await ClearCanvasSizeAsync()
                    .ConfigureAwait(false);
                return;
            }

            // Parse the JSON result - WebView returns JSON-encoded string
            // Result format: "{\"width\":1024,\"height\":768}" or with quotes: "\"{\\\"width\\\":...}\""
            string jsonString = result.Trim();

            // NOTE:
            // The underlying WebView / ExecuteScriptAsync implementation JSON-serializes the return value
            // from JavaScript. When the script itself returns a JSON string (e.g. JSON.stringify(...)),
            // the .NET side receives a JSON *string* that contains JSON text (double-encoded).
            // In that case, we first deserialize the outer JSON string to retrieve the inner JSON object text.
            // If the WebView's serialization behavior changes, this unwrapping logic may need to be revisited
            if (jsonString.StartsWith('"') && jsonString.EndsWith('"'))
            {
                jsonString = JsonSerializer.Deserialize<string>(jsonString) ?? string.Empty;
            }

            // Parse using System.Text.Json
            using JsonDocument doc = JsonDocument.Parse(jsonString);
            JsonElement root = doc.RootElement;

            double? width = null;
            double? height = null;

            if (root.TryGetProperty("width", out JsonElement widthElement) &&
                widthElement.ValueKind == JsonValueKind.Number)
            {
                width = widthElement.GetDouble();
            }

            if (root.TryGetProperty("height", out JsonElement heightElement) &&
                heightElement.ValueKind == JsonValueKind.Number)
            {
                height = heightElement.GetDouble();
            }

            // Update on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CanvasWidth = width;
                CanvasHeight = height;
            })
            .GetTask()
            .ConfigureAwait(false);
        }
        catch (JsonException jex)
        {
            _logger.LogWarning(jex, "Failed to parse canvas dimensions JSON");
            await ClearCanvasSizeAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update canvas size");
            await ClearCanvasSizeAsync()
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clears the canvas size properties on the UI thread.
    /// </summary>
    private async Task ClearCanvasSizeAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CanvasWidth = null;
            CanvasHeight = null;
        })
        .GetTask()
        .ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates the shutdown process by signaling cancellation to any ongoing operations.
    /// </summary>
    /// <remarks>This method is intended to be called before disposing of the instance to ensure that all
    /// background operations are notified to stop. Calling this method multiple times has no effect after the first
    /// invocation.</remarks>
    internal void PrepareForShutdown()
    {
        if (_isDisposed)
        {
            return;
        }

        _shutdownCts.Cancel();
    }

    #region IDisposable

    /// <summary>
    /// Releases resources used by the object and unsubscribes from property change notifications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Do NOT call this method directly. The <see cref="DiagramViewModel"/> is owned by the dependency
    /// injection container and its <see cref="IDisposable"/> implementation will be called by the container
    /// when the object is no longer needed. This method does not dispose of dependencies managed by the
    /// dependency injection container.
    /// </para>
    /// <para>
    /// This method:
    /// <list type="bullet">
    ///     <item><description>Unregisters all message handlers from the document-scoped messenger</description></item>
    ///     <item><description>Unsubscribes from Editor, Diagram, and EditorTool PropertyChanged events</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            // To avoid race conditions, cancel any ongoing operations first so that
            // once _isDisposed = true, all tokens are already canceled
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();

            // NOTE: Intentionally not disposing _renderGate to avoid ObjectDisposedException
            // in any in-flight/pending WaitAsync callers during shutdown

            _isDisposed = true;
        }
    }

    #endregion IDisposable
}
