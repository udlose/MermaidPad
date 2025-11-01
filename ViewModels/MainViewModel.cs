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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Infrastructure;
using MermaidPad.Services;
using MermaidPad.Services.Export;
using MermaidPad.ViewModels.Dialogs;
using MermaidPad.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MermaidPad.ViewModels;

/// <summary>
/// Main window state container with commands and (optional) live preview.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly MermaidRenderer _renderer;
    private readonly SettingsService _settingsService;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly ExportService _exportService;
    private readonly IDialogFactory _dialogFactory;

    private const string DebounceRenderKey = "render";

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
    /// Gets or sets a value indicating whether the WebView is ready for rendering operations.
    /// </summary>
    [ObservableProperty]
    public partial bool IsWebViewReady { get; set; }

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
        _dialogFactory = services.GetRequiredService<IDialogFactory>();

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
    /// Handles changes to the WebView readiness state.
    /// </summary>
    /// <remarks>This method updates the state of related commands based on the WebView readiness state. When
    /// the WebView becomes ready, associated commands are enabled.</remarks>
    /// <param name="value">A boolean value indicating the new readiness state of the WebView.  <see langword="true"/> if the WebView is
    /// ready; otherwise, <see langword="false"/>.</param>
    partial void OnIsWebViewReadyChanged(bool value)
    {
        SimpleLogger.Log($"IsWebViewReady changed to: {value}");

        // Update command states when WebView ready state changes
        RenderCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Asynchronously renders the diagram text using the configured renderer.
    /// </summary>
    /// <remarks>This method clears any previous errors before rendering. The rendering process may require
    /// access to the UI context, so it does not use <see cref="Task.ConfigureAwait(bool)"/>. Ensure that the <see
    /// cref="CanRender"/> method returns <see langword="true"/> before invoking this command.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanRender))]
    private async Task RenderAsync()
    {
        LastError = null;

        // NO ConfigureAwait(false) here - MermaidRenderer may need UI context
        await _renderer.RenderAsync(DiagramText);
    }

    /// <summary>
    /// Determines whether the diagram can be rendered based on the current state.
    /// </summary>
    /// <returns><see langword="true"/> if the WebView is ready and the diagram text is not null or whitespace; otherwise, <see
    /// langword="false"/>.</returns>
    private bool CanRender() => IsWebViewReady && !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Clears the diagram text, resets the editor selection and caret position, and removes the last error.
    /// </summary>
    /// <remarks>This method updates several UI-related properties and invokes the renderer to clear the
    /// diagram.  It must be executed on the UI thread to ensure proper synchronization with the user
    /// interface.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        // These property updates must happen on UI thread
        DiagramText = string.Empty;
        EditorSelectionStart = 0;
        EditorSelectionLength = 0;
        EditorCaretOffset = 0;
        LastError = null;

        // NO ConfigureAwait(false) - renderer needs UI context
        await _renderer.RenderAsync(string.Empty);
    }

    /// <summary>
    /// Determines whether the diagram can be cleared based on the current state.
    /// </summary>
    /// <returns><see langword="true"/> if the WebView is ready and the diagram text is not null, empty, or whitespace;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanClear() => IsWebViewReady && !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Initiates the export process by displaying an export dialog to the user and performing the export operation
    /// based on the selected options.
    /// </summary>
    /// <remarks>This method displays a dialog to the user for configuring export options. If the user
    /// confirms the dialog, the export operation is performed asynchronously with progress feedback. If the user
    /// cancels the dialog, the method exits without performing the export. <para> The method ensures that all UI
    /// interactions, such as displaying dialogs, are executed on the UI thread. </para> <para> Any errors encountered
    /// during the export process are logged and reflected in the <c>LastError</c> property, which can be used to
    /// display error messages in the UI. </para></remarks>
    /// <returns></returns>
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

            // Create the export dialog and its view model using DI
            ExportDialogViewModel exportViewModel = _dialogFactory.CreateViewModel<ExportDialogViewModel>();

            // Create the dialog with the storage provider
            ExportDialog exportDialog = new ExportDialog
            {
                DataContext = exportViewModel
            };

            // NO ConfigureAwait(false) - ShowDialog must run on UI thread
            await exportDialog.ShowDialog(window);

            // Check if user cancelled
            if (exportViewModel.DialogResult != true)
            {
                return; // User cancelled
            }

            // Get export options from the view model
            ExportOptions exportOptions = exportViewModel.GetExportOptions();

            // NO ConfigureAwait(false) - may show UI dialogs
            await ExportWithProgressAsync(window, exportOptions);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Export failed", ex);

            // Setting LastError updates UI, must be on UI thread
            LastError = $"Export failed: {ex.Message}";
            Debug.WriteLine($"Export error: {ex}");
        }
    }

    /// <summary>
    /// Determines whether the export operation can be performed.
    /// </summary>
    /// <returns><see langword="true"/> if the web view is ready and the diagram text is not null, empty, or whitespace;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanExport() => IsWebViewReady && !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Exports data to a specified file format, optionally displaying a progress dialog during the operation.
    /// </summary>
    /// <remarks>If the <see cref="ExportOptions.ShowProgress"/> property is set to <see langword="true"/> and
    /// the export format is PNG, a progress dialog is displayed to provide feedback during the export process.  The
    /// dialog allows cancellation if <see cref="ExportOptions.AllowCancellation"/> is enabled.  For other formats or
    /// when progress is not shown, the export operation runs without displaying a dialog.</remarks>
    /// <param name="window">The parent window for displaying the progress dialog, if applicable.</param>
    /// <param name="options">The export options specifying the file format, file path, and additional settings such as progress visibility
    /// and cancellation support.</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">Thrown if the specified export format is not supported.</exception>
    private async Task ExportWithProgressAsync(Window window, ExportOptions options)
    {
        try
        {
            if (options is { ShowProgress: true, Format: ExportFormat.PNG })
            {
                // Create progress dialog using DI
                ProgressDialogViewModel progressViewModel = _dialogFactory.CreateViewModel<ProgressDialogViewModel>();
                progressViewModel.Title = "Exporting PNG";
                progressViewModel.StatusMessage = "Preparing export...";

                ProgressDialog progressDialog = new ProgressDialog
                {
                    DataContext = progressViewModel
                };

                // Set up cancellation
                using CancellationTokenSource cts = new CancellationTokenSource();
                if (options.AllowCancellation)
                {
                    progressViewModel.SetCancellationTokenSource(cts);
                }

                // Create event handler that can be unsubscribed to prevent memory leaks
                void ProgressHandler(object? _, PropertyChangedEventArgs args)
                {
                    // Watch for two conditions:
                    // 1. Export completes (IsComplete becomes true)
                    // 2. User clicks Close button (CloseRequested becomes true)
                    bool shouldClose = (args.PropertyName == nameof(ProgressDialogViewModel.IsComplete) && progressViewModel.IsComplete) ||
                                       (args.PropertyName == nameof(ProgressDialogViewModel.CloseRequested) && progressViewModel.CloseRequested);

                    if (!shouldClose)
                    {
                        return;
                    }

                    // Unsubscribe to prevent memory leaks
                    progressViewModel.PropertyChanged -= ProgressHandler;

                    // Capture dialog reference locally to prevent closure memory leak
                    ProgressDialog localDialog = progressDialog;

                    // Close dialog on UI thread - fire and forget
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (localDialog.IsVisible)
                            {
                                localDialog.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to close progress dialog: {ex}");
                        }
                    });
                }

                // Subscribe to property changes
                progressViewModel.PropertyChanged += ProgressHandler;

                // Start the progress dialog and track it for cleanup
                Task dialogTask = progressDialog.ShowDialog(window);

                // Small delay to ensure dialog is rendered before starting export
                // This prevents race conditions where export completes before dialog is visible
                await Task.Delay(100, cts.Token);

                try
                {
                    // Start export - ExportPngAsync manages its own threading
                    // It runs on UI thread for WebView access, then background for PNG conversion
                    await _exportService.ExportPngAsync(
                        options.FilePath,
                        options.PngOptions,
                        progressViewModel,
                        cts.Token);

                    // If export succeeded, wait for user to click Close button
                    // The dialog will close when IsComplete is set and user clicks Close
                }
                catch (OperationCanceledException)
                {
                    // User cancelled the export - unsubscribe and close dialog
                    progressViewModel.PropertyChanged -= ProgressHandler;

                    // Capture dialog reference locally
                    ProgressDialog localDialog = progressDialog;
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (localDialog.IsVisible)
                            {
                                localDialog.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to close progress dialog after cancellation: {ex}");
                        }
                    });
                    throw; // Re-throw to be caught by outer catch
                }
                catch (Exception outerEx)
                {
                    SimpleLogger.LogError("Export failed", outerEx);

                    // Export failed - unsubscribe and close dialog
                    progressViewModel.PropertyChanged -= ProgressHandler;

                    // Capture dialog reference locally
                    ProgressDialog localDialog = progressDialog;
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (localDialog.IsVisible)
                            {
                                localDialog.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            SimpleLogger.LogError("Error awaiting progress dialog task", ex);
                            Debug.WriteLine($"Failed to close progress dialog after error: {ex}");
                        }
                    });
                    throw; // Re-throw to be caught by outer catch
                }
                finally
                {
                    // Ensure we await the dialog task for proper cleanup
                    try
                    {
                        await dialogTask;
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError("Error awaiting progress dialog task", ex);
                        Debug.WriteLine($"Dialog task completed with error: {ex}");
                    }
                }
            }
            else
            {
                switch (options.Format)
                {
                    case ExportFormat.PNG:
                        // Export PNG without progress dialog
                        await _exportService.ExportPngAsync(options.FilePath, options.PngOptions);
                        break;

                    case ExportFormat.SVG:
                        // Export SVG (no progress needed)
                        await _exportService.ExportSvgAsync(options.FilePath);
                        break;

                    default:
                        throw new NotSupportedException($"Export format {options.Format} is not supported");
                }
            }

            // NO ConfigureAwait(false) - will show UI dialog
            await ShowSuccessMessageAsync(window, $"Export completed successfully to:{Environment.NewLine}{options.FilePath}");
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Export cancelled by user");
            // Don't show error for user cancellation
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Export failed", ex);

            // Setting LastError updates UI, must be on UI thread
            LastError = $"Export failed: {ex.Message}";
            Debug.WriteLine($"Export error: {ex}");
        }
    }

    /// <summary>
    /// Displays a success message dialog with the specified message and a checkmark icon.
    /// </summary>
    /// <remarks>The dialog includes a title, a success message, and a green checkmark icon.  This method must
    /// be called on the UI thread as it interacts with the user interface.</remarks>
    /// <param name="window">The parent window that owns the dialog. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="message">The message to display in the dialog. This parameter cannot be <see langword="null"/> or empty.</param>
    /// <returns></returns>
    private async Task ShowSuccessMessageAsync(Window window, string message)
    {
        try
        {
            // Create message dialog using DI
            MessageDialogViewModel messageViewModel = _dialogFactory.CreateViewModel<MessageDialogViewModel>();
            messageViewModel.Title = "Export Complete";
            messageViewModel.Message = message;
            messageViewModel.IconData = "M9 12l2 2 4-4"; // Checkmark icon path
            messageViewModel.IconColor = Avalonia.Media.Brushes.Green;

            MessageDialog messageDialog = new MessageDialog
            {
                DataContext = messageViewModel
            };

            // NO ConfigureAwait(false) - ShowDialog needs UI thread
            await messageDialog.ShowDialog(window);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to show success message", ex);
            Debug.WriteLine($"Failed to show success message: {ex}");
        }
    }

    /// <summary>
    /// Handles changes to the diagram text and triggers appropriate updates, such as rendering the diagram and updating
    /// command states.
    /// </summary>
    /// <remarks>If live preview is enabled, this method debounces rendering operations to optimize
    /// performance.  It also ensures that related commands update their execution states to reflect the current
    /// context.</remarks>
    /// <param name="value">The new value of the diagram text.</param>
    partial void OnDiagramTextChanged(string value)
    {
        if (LivePreviewEnabled)
        {
            _editorDebouncer.Debounce(DebounceRenderKey, TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
            {
                try
                {
                    // SafeFireAndForget handles its own context
                    _renderer.RenderAsync(DiagramText).SafeFireAndForget(onException: static e => Debug.WriteLine(e));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            });
        }

        // Update command states - these are UI operations
        RenderCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
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

            // SafeFireAndForget handles context, but the error handler updates UI
            _renderer.RenderAsync(DiagramText).SafeFireAndForget(onException: ex =>
            {
                // This runs on UI thread due to SafeFireAndForget
                LastError = $"Failed to render diagram: {ex.Message}";
                Debug.WriteLine(ex);
            });
        }
        else
        {
            _editorDebouncer.Cancel(DebounceRenderKey);
        }
    }

    /// <summary>
    /// Checks for updates to the Mermaid library and updates the application state with the latest version information.
    /// </summary>
    /// <remarks>This method performs a network call to check for updates asynchronously. The application
    /// state is updated with the  bundled and latest checked Mermaid versions, ensuring that property updates occur on
    /// the UI thread.</remarks>
    /// <returns></returns>
    public async Task CheckForMermaidUpdatesAsync()
    {
        // This CAN use ConfigureAwait(false) for the network call
        await _updateService.CheckAndUpdateAsync()
            .ConfigureAwait(false);

        // But property updates should happen on UI thread
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
    }

    /// <summary>
    /// Initializes the current version of the MermaidPad application by retrieving the version information from the
    /// executing assembly.
    /// </summary>
    /// <remarks>This method attempts to retrieve the version number of the executing assembly and formats it
    /// as a string in the format "Major.Minor.Build". If the version cannot be determined, the <see
    /// cref="CurrentMermaidPadVersion"/> property is set to "Unknown".</remarks>
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
    /// Persists the current application settings to storage.
    /// </summary>
    /// <remarks>This method updates the settings service with the current state of the application,
    /// including diagram text, live preview settings, Mermaid version information, and editor  selection details. After
    /// updating the settings, the method saves them to ensure they  are persisted across application
    /// sessions.</remarks>
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
