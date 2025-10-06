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
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Dialogs;
using MermaidPad.Models;
using MermaidPad.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MermaidPad.ViewModels;
/// <summary>
/// Main window state container with commands and (optional) live preview.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly MermaidRenderer _renderer;
    private readonly SettingsService _settingsService;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly ExportService _exportService;

    /// <summary>
    /// Gets or sets the current diagram text.
    /// </summary>
    [ObservableProperty]
    public partial string DiagramText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last error message, if any.
    /// </summary>
    [ObservableProperty]
    public partial string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the version of the bundled Mermaid.js.
    /// </summary>
    [ObservableProperty]
    public partial string BundledMermaidVersion { get; set; }

    /// <summary>
    /// Gets or sets the latest Mermaid.js version available.
    /// </summary>
    [ObservableProperty]
    public partial string? LatestMermaidVersion { get; set; }

    /// <summary>
    /// Gets or sets the current installed version of MermaidPad.
    /// </summary>
    [ObservableProperty]
    public partial string? CurrentMermaidPadVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether live preview is enabled.
    /// </summary>
    [ObservableProperty]
    public partial bool LivePreviewEnabled { get; set; }

    /// <summary>
    /// Gets or sets the selection start index in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int EditorSelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the selection length in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int EditorSelectionLength { get; set; }

    /// <summary>
    /// Gets or sets the caret offset in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int EditorCaretOffset { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="services">The service provider for dependency injection.</param>
    public MainViewModel(IServiceProvider services)
    {
        _renderer = services.GetRequiredService<MermaidRenderer>();
        _settingsService = services.GetRequiredService<SettingsService>();
        _updateService = services.GetRequiredService<MermaidUpdateService>();
        _editorDebouncer = services.GetRequiredService<IDebounceDispatcher>();
        _exportService = services.GetRequiredService<ExportService>();

        InitializeCurrentMermaidPadVersion();

        // Initialize properties from settings
        DiagramText = _settingsService.Settings.LastDiagramText ?? SampleText;
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
        LivePreviewEnabled = _settingsService.Settings.LivePreviewEnabled;
        EditorSelectionStart = _settingsService.Settings.EditorSelectionStart;
        EditorSelectionLength = _settingsService.Settings.EditorSelectionLength;
        EditorCaretOffset = _settingsService.Settings.EditorCaretOffset;
    }

    /// <summary>
    /// Renders the current diagram text in the preview.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanRender))]
    private async Task RenderAsync()
    {
        LastError = null;
        await _renderer.RenderAsync(DiagramText);
    }

    /// <summary>
    /// Determines whether the render command can execute.
    /// </summary>
    /// <returns><c>true</c> if the diagram text is not empty; otherwise, <c>false</c>.</returns>
    private bool CanRender() => !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Clears the diagram text and resets editor selection and caret.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        DiagramText = string.Empty;
        EditorSelectionStart = 0;
        EditorSelectionLength = 0;
        EditorCaretOffset = 0;
        LastError = null;
        await _renderer.RenderAsync(string.Empty);
    }

    /// <summary>
    /// Determines whether the clear command can execute.
    /// </summary>
    /// <returns><c>true</c> if the diagram text is not empty; otherwise, <c>false</c>.</returns>
    private bool CanClear() => !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Exports the current diagram to SVG or PNG format.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        try
        {
            Window? window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (window is null)
            {
                LastError = "Unable to access main window for export dialog";
                return;
            }

            // Show export dialog
            ExportDialog exportDialog = new ExportDialog();
            ExportOptions? exportOptions = await exportDialog.ShowDialog<ExportOptions?>(window);

            if (exportOptions is null)
            {
                return; // User cancelled
            }

            await _exportService.ExportDiagramAsync(window, exportOptions);
        }
        catch (Exception ex)
        {
            LastError = $"Export failed: {ex.Message}";
            Debug.WriteLine($"Export error: {ex}");
        }
    }

    /// <summary>
    /// Determines whether the export command can execute.
    /// </summary>
    /// <returns><c>true</c> if there is a diagram to export; otherwise, <c>false</c>.</returns>
    private bool CanExport() => !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Handles changes to the diagram text and triggers rendering if live preview is enabled.
    /// </summary>
    /// <remarks>
    /// If live preview is enabled, this method de-bounces the rendering operation to occur 500
    /// milliseconds after the last change. It also reevaluates the execution status of the Render and Clear
    /// commands.
    /// </remarks>
    /// <param name="value">The updated diagram text, which is not used directly in this method.</param>
    partial void OnDiagramTextChanged(string value)
    {
        if (LivePreviewEnabled)
        {
            _editorDebouncer.Debounce("render", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
            {
                try
                {
                    _renderer.RenderAsync(DiagramText).SafeFireAndForget(onException: static e => Debug.WriteLine(e));
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

    /// <summary>
    /// Handles changes to the live preview enabled state.
    /// </summary>
    /// <param name="value">The new value indicating whether live preview is enabled.</param>
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

    /// <summary>
    /// Checks for Mermaid.js updates and updates the version properties.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckForMermaidUpdatesAsync()
    {
        await _updateService.CheckAndUpdateAsync();
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
    }

    private void InitializeCurrentMermaidPadVersion()
    {
        try
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version is not null)
            {
                // Display 3 version fields as Major.Minor.Build (e.g., 1.2.3)
                CurrentMermaidPadVersion = version.ToString(3);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get current MermaidPad version: {ex}");
            CurrentMermaidPadVersion = "Unknown";
        }
    }

    /// <summary>
    /// Persists the current state to the settings service.
    /// </summary>
    public void Persist()
    {
        _settingsService.Settings.LastDiagramText = DiagramText;
        _settingsService.Settings.LivePreviewEnabled = LivePreviewEnabled;
        _settingsService.Settings.BundledMermaidVersion = BundledMermaidVersion;
        _settingsService.Settings.LatestCheckedMermaidVersion = LatestMermaidVersion;
        _settingsService.Settings.EditorSelectionStart = EditorSelectionStart;
        _settingsService.Settings.EditorSelectionLength = EditorSelectionLength;
        _settingsService.Settings.EditorCaretOffset = EditorCaretOffset;
        _settingsService.Save();
    }

    /// <summary>
    /// Gets sample Mermaid diagram text.
    /// </summary>
    private static string SampleText => """
graph TD
  A[Start] --> B{Decision}
  B -->|Yes| C[Render Diagram]
  B -->|No| D[Edit Text]
  C --> E[Done]
  D --> B
""";

    // Future stubs:
    // [ObservableProperty] private bool autoUpdateEnabled; //TODO - add implementation
    //TODO Methods for export commands, telemetry, syntax highlighting toggles, etc.
}
