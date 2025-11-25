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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.Panels;

/// <summary>
/// ViewModel for the Preview panel, handling diagram rendering and WebView management.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class PreviewViewModel : ViewModelBase
{
    private readonly MermaidRenderer _renderer;
    private readonly IDebounceDispatcher _debouncer;
    private readonly ILogger<PreviewViewModel> _logger;

    private const string DebounceRenderKey = "render";

    /// <summary>
    /// Gets or sets a value indicating whether the WebView is ready for rendering operations.
    /// </summary>
    [ObservableProperty]
    public partial bool IsWebViewReady { get; set; }

    /// <summary>
    /// Gets or sets the last error message, if any.
    /// </summary>
    [ObservableProperty]
    public partial string? LastError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether live preview is enabled.
    /// </summary>
    [ObservableProperty]
    public partial bool LivePreviewEnabled { get; set; }

    /// <summary>
    /// Gets a value indicating whether the render command can execute.
    /// </summary>
    public bool CanRender => IsWebViewReady;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewViewModel"/> class using application-level services.
    /// </summary>
    /// <remarks>
    /// <para>This constructor retrieves required services from the application's dependency injection
    /// container to configure the preview panel's view model. It is typically used when creating the preview panel at application
    /// startup.</para>
    /// <para>
    /// This constructor exists specifically to avoid the following warning:
    ///     AVLN3001: XAML resource "avares://MermaidPad/Views/Panels/PreviewView.axaml" won't be reachable via runtime loader, as no public constructor was found
    /// </para>
    /// </remarks>
    public PreviewViewModel()
        : this(
            App.Services.GetRequiredService<MermaidRenderer>(),
            App.Services.GetRequiredService<IDebounceDispatcher>(),
            App.Services.GetRequiredService<ILogger<PreviewViewModel>>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewViewModel"/> class.
    /// </summary>
    /// <param name="renderer">The Mermaid renderer service.</param>
    /// <param name="debouncer">The debounce dispatcher for optimizing render calls.</param>
    /// <param name="logger">The logger instance for this view model.</param>
    public PreviewViewModel(MermaidRenderer renderer, IDebounceDispatcher debouncer, ILogger<PreviewViewModel> logger)
    {
        _renderer = renderer;
        _debouncer = debouncer;
        _logger = logger;

        _logger.LogInformation("PreviewViewModel initialized");
    }

    /// <summary>
    /// Asynchronously renders the specified diagram text.
    /// </summary>
    /// <param name="diagramText">The diagram text to render.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanRender))]
    public async Task RenderAsync(string diagramText)
    {
        LastError = null;

        try
        {
            // NO ConfigureAwait(false) here - MermaidRenderer may need UI context
            await _renderer.RenderAsync(diagramText);
            _logger.LogDebug("Diagram rendered successfully");
        }
        catch (Exception ex)
        {
            LastError = $"Render failed: {ex.Message}";
            _logger.LogError(ex, "Failed to render diagram");
        }
    }

    /// <summary>
    /// Handles diagram text changes with debouncing for live preview.
    /// </summary>
    /// <param name="diagramText">The new diagram text.</param>
    public void OnDiagramTextChanged(string diagramText)
    {
        if (!LivePreviewEnabled || !IsWebViewReady)
        {
            return;
        }

        // Debounce render calls to optimize performance during typing
        _debouncer.Debounce(
            DebounceRenderKey,
            TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds),
            () =>
            {
                try
                {
                    // SafeFireAndForget handles its own context
                    _renderer.RenderAsync(diagramText).SafeFireAndForget(onException: ex =>
                    {
                        // Update error on UI thread
                        Dispatcher.UIThread.Post(() =>
                        {
                            LastError = $"Failed to render diagram: {ex.Message}";
                            Debug.WriteLine(ex);
                            _logger.LogError(ex, "Live preview render failed");
                        });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    _logger.LogError(ex, "Error initiating debounced render");
                }
            });
    }

    /// <summary>
    /// Handles changes to the WebView readiness state.
    /// </summary>
    /// <param name="value">The new readiness state.</param>
    partial void OnIsWebViewReadyChanged(bool value)
    {
        _logger.LogInformation("IsWebViewReady changed to: {IsWebViewReady}", value);

        // Update only the RenderCommand state when WebView ready state changes
        // We only need to know that we can Render, not Clear or Export
        RenderCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Handles changes to the live preview enabled state.
    /// </summary>
    /// <param name="value">The new value indicating whether live preview is enabled.</param>
    partial void OnLivePreviewEnabledChanged(bool value)
    {
        _logger.LogInformation("LivePreviewEnabled changed to: {LivePreviewEnabled}", value);

        if (!value)
        {
            // Cancel any pending debounced renders when live preview is disabled
            _debouncer.Cancel(DebounceRenderKey);
        }
    }
}
