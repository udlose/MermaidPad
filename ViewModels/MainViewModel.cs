using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace MermaidPad.ViewModels;
/// <summary>
/// Main window state container with commands and (optional) live preview.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly MermaidRenderer _renderer;
    private readonly SettingsService _settingsService;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;

    [ObservableProperty]
    public partial string DiagramText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? LastError { get; set; }

    [ObservableProperty]
    public partial string BundledMermaidVersion { get; set; } = "11.9.0";

    [ObservableProperty]
    public partial string? LatestMermaidVersion { get; set; }

    [ObservableProperty]
    public partial bool LivePreviewEnabled { get; set; } = true;

    public MainViewModel(IServiceProvider services)
    {
        _renderer = services.GetRequiredService<MermaidRenderer>();
        _settingsService = services.GetRequiredService<SettingsService>();
        _updateService = services.GetRequiredService<MermaidUpdateService>();
        _editorDebouncer = services.GetRequiredService<IDebounceDispatcher>();

        // Initialize properties from settings
        DiagramText = _settingsService.Settings.LastDiagramText ?? SampleText();
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
        LivePreviewEnabled = _settingsService.Settings.LivePreviewEnabled;
    }

    [RelayCommand(CanExecute = nameof(CanRender))]
    private async Task RenderAsync()
    {
        LastError = null;
        await _renderer.RenderAsync(DiagramText);
    }

    private bool CanRender() => !string.IsNullOrWhiteSpace(DiagramText);

    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        DiagramText = string.Empty;
        LastError = null;
        await _renderer.RenderAsync(string.Empty);
    }

    private bool CanClear() => !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Handles changes to the diagram text and triggers rendering if live preview is enabled.
    /// </summary>
    /// <remarks>If live preview is enabled, this method de-bounces the rendering operation to occur 500
    /// milliseconds after the last change. It also reevaluates the execution status of the Render and Clear
    /// commands.</remarks>
    /// <param name="value">The updated diagram text, which is not used directly in this method.</param>
    partial void OnDiagramTextChanged(string value)
    {
        if (LivePreviewEnabled)
        {
            _editorDebouncer.Debounce("render", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultDebounceMilliseconds), () =>
            {
                try
                {
                    _renderer.RenderAsync(DiagramText).SafeFireAndForget(onException: e => Debug.WriteLine(e));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            });
        }

        // RenderCommand / ClearCommand CanExecute reevaluation:
        RenderCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    partial void OnLivePreviewEnabledChanged(bool value)
    {
        if (value)
        {
            if (string.IsNullOrWhiteSpace(DiagramText))
            {
                return;
            }
            _renderer.RenderAsync(DiagramText).SafeFireAndForget(onException: ex =>
            {
                LastError = $"Failed to render diagram: {ex.Message}";
                Debug.WriteLine(ex);
            });
        }
        else
        {
            _editorDebouncer.Cancel("render");
        }
    }

    public async Task CheckForMermaidUpdatesAsync()
    {
        await _updateService.CheckAndUpdateAsync();
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
    }

    public void Persist()
    {
        _settingsService.Settings.LastDiagramText = DiagramText;
        _settingsService.Settings.LivePreviewEnabled = LivePreviewEnabled;
        _settingsService.Settings.BundledMermaidVersion = BundledMermaidVersion;
        _settingsService.Settings.LatestCheckedMermaidVersion = LatestMermaidVersion;
        _settingsService.Save();
    }

    private static string SampleText() => """
graph TD
  A[Start] --> B{Decision}
  B -->|Yes| C[Render Diagram]
  B -->|No| D[Edit Text]
  C --> E[Done]
  D --> B
""";

    // Future stubs:
    // [ObservableProperty] private bool autoUpdateEnabled; //TODO - add implementation
    // TODO Methods for export commands, telemetry, syntax highlighting toggles, etc.
}
