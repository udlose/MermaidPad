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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using MermaidPad.Infrastructure;
using MermaidPad.Services;
using MermaidPad.Services.AI;
using MermaidPad.Services.Export;
using MermaidPad.ViewModels.Dialogs;
using MermaidPad.ViewModels.Panels;
using MermaidPad.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

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
    private readonly SettingsService _settingsService;
    private readonly UISettingsService _uiSettingsService;
    private readonly MermaidUpdateService _updateService;
    private readonly ExportService _exportService;
    private readonly IDialogFactory _dialogFactory;
    private readonly IFileService _fileService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly AIServiceFactory _aiServiceFactory;
    private readonly DockFactory _dockFactory;

    /// <summary>
    /// A value tracking if there is currently a file being loaded.
    /// </summary>
    private bool _isLoadingFile;

    /// <summary>
    /// Gets the editor view model.
    /// </summary>
    public EditorViewModel EditorViewModel { get; private set; } = null!;

    /// <summary>
    /// Gets the preview view model.
    /// </summary>
    public PreviewViewModel PreviewViewModel { get; private set; } = null!;

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
    /// Gets or sets the current file path being edited.
    /// </summary>
    [ObservableProperty]
    public partial string? CurrentFilePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current document has unsaved changes.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    /// <summary>
    /// Gets the window title including file name and dirty indicator.
    /// </summary>
    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "MermaidPad - The Cross-Platform Mermaid Chart Editor";

    /// <summary>
    /// Gets the status text showing current file info.
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "No file open";

    /// <summary>
    /// Gets the list of recent files for the menu.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<string> RecentFiles { get; set; } = [];

    /// <summary>
    /// Gets the AI panel view model.
    /// </summary>
    public AIPanelViewModel AIPanelViewModel { get; private set; } = null!;

    /// <summary>
    /// Gets or sets the docking layout.
    /// </summary>
    [ObservableProperty]
    public partial IDock? Layout { get; set; }

    /// <summary>
    /// Gets a value indicating whether the Save command can execute.
    /// </summary>
    public bool CanSave => EditorViewModel.HasText && IsDirty;

    /// <summary>
    /// Gets a value indicating whether there is text in the editor.
    /// </summary>
    public bool HasText => EditorViewModel.HasText;

    /// <summary>
    /// Gets a value indicating whether there are recent files.
    /// </summary>
    public bool HasRecentFiles => RecentFiles.Count > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="logger">The logger instance for this view model.</param>
    public MainViewModel(
        IServiceProvider services,
        ILogger<MainViewModel> logger)
    {
        // TODO inject these services via constructor, not service locator

        _settingsService = services.GetRequiredService<SettingsService>();
        _uiSettingsService = services.GetRequiredService<UISettingsService>();
        _updateService = services.GetRequiredService<MermaidUpdateService>();
        _exportService = services.GetRequiredService<ExportService>();
        _dialogFactory = services.GetRequiredService<IDialogFactory>();
        _fileService = services.GetRequiredService<IFileService>();
        _logger = logger;
        _aiServiceFactory = services.GetRequiredService<AIServiceFactory>();

        // Get DockFactory from DI (registered via AddDock)
        _dockFactory = (DockFactory)services.GetRequiredService<IFactory>();

        // Create child ViewModels
        EditorViewModel = services.GetRequiredService<EditorViewModel>();
        PreviewViewModel = services.GetRequiredService<PreviewViewModel>();

        // Wire up communication: Editor -> Preview
        EditorViewModel.DiagramTextChanged += (_, diagramText) =>
        {
            PreviewViewModel.OnDiagramTextChanged(diagramText);

            // Mark as dirty when text changes (ONLY if we're not loading a file)
            if (!_isLoadingFile)
            {
                IsDirty = true;
            }
        };

        InitializeCurrentMermaidPadVersion();

        // Initialize properties from settings
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
        CurrentFilePath = _settingsService.Settings.CurrentFilePath;

        // Initialize EditorViewModel from settings
        string diagramText = _settingsService.Settings.LastDiagramText ?? SampleText;
        EditorViewModel.SetEditorState(
            diagramText,
            _settingsService.Settings.EditorSelectionStart,
            _settingsService.Settings.EditorSelectionLength,
            _settingsService.Settings.EditorCaretOffset,
            suppressEvents: true); // Suppress events during initialization

        // Initialize PreviewViewModel from settings
        PreviewViewModel.LivePreviewEnabled = _settingsService.Settings.LivePreviewEnabled;

        // Initialize AI panel
        IAIService aiService = _aiServiceFactory.CreateService(_settingsService.Settings.AI);
        AIPanelViewModel = new AIPanelViewModel(aiService);
        AIPanelViewModel.DiagramGenerated += OnDiagramGenerated;

        // Initialize docking layout
        // Set up ContextLocator to map panel IDs to ViewModels
        _dockFactory.ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["Editor"] = () => EditorViewModel,
            ["Preview"] = () => PreviewViewModel,
            ["AIAssistant"] = () => AIPanelViewModel
        };

        // Try to load saved layout from UI settings, or create default if none exists
        if (!string.IsNullOrWhiteSpace(_uiSettingsService.Settings.DockLayout))
        {
            IDock? deserializedLayout = _dockFactory.DeserializeLayout(_uiSettingsService.Settings.DockLayout);
            if (deserializedLayout is not null)
            {
                Layout = deserializedLayout;
                _dockFactory.InitLayout(Layout);
                _logger.LogInformation("Loaded saved dock layout from UI settings");
            }
            else
            {
                // Deserialization failed, create default layout
                Layout = _dockFactory.CreateLayout();
                _dockFactory.InitLayout(Layout);
                _logger.LogWarning("Failed to load saved dock layout, using default");
            }
        }
        else
        {
            // No saved layout, create default
            Layout = _dockFactory.CreateLayout();
            _dockFactory.InitLayout(Layout);
            _logger.LogInformation("No saved dock layout found in UI settings, using default");
        }

        UpdateRecentFiles();
        UpdateWindowTitle();
    }

    #region File Open/Save

    /// <summary>
    /// Asynchronously opens a file using the specified storage provider.
    /// </summary>
    /// <param name="storageProvider">The storage provider used to select and access the file. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous file open operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand]
    private Task OpenFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return OpenFileCoreAsync(storageProvider);
    }

    /// <summary>
    /// Opens a file using the specified storage provider, prompting the user to save unsaved changes if necessary, and
    /// loads the file content into the current diagram.
    /// </summary>
    /// <remarks>If there are unsaved changes, the user is prompted to save before proceeding. The method
    /// updates the current file path, diagram content, and recent files list upon successful file load. If the WebView
    /// is ready, the loaded content is rendered immediately. Any errors encountered during the operation are logged and
    /// displayed to the user.</remarks>
    /// <param name="storageProvider">The storage provider used to access and open the file. Must not be null.</param>
    /// <returns>A task that represents the asynchronous operation of opening the file.</returns>
    private async Task OpenFileCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            // Check for unsaved changes
            if (IsDirty)
            {
                bool canProceed = await PromptSaveIfDirtyAsync(storageProvider);
                if (!canProceed)
                {
                    return; // User cancelled
                }
            }

            (string? filePath, string? content) = await _fileService.OpenFileAsync(storageProvider);
            if (filePath is not null && content is not null)
            {
                _isLoadingFile = true;
                try
                {
                    EditorViewModel.SetEditorState(content, 0, 0, 0, suppressEvents: true);
                    CurrentFilePath = filePath;
                    IsDirty = false;
                    UpdateRecentFiles();

                    // Render the newly loaded content if WebView is ready
                    if (PreviewViewModel.IsWebViewReady)
                    {
                        await PreviewViewModel.RenderAsync(content);
                    }

                    _logger.LogInformation("Opened file: {FilePath}", filePath);
                }
                finally
                {
                    _isLoadingFile = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file");
            await ShowErrorMessageAsync("Failed to open file. " + ex.Message);
        }
    }

    /// <summary>
    /// Saves the current file asynchronously using the specified storage provider.
    /// </summary>
    /// <param name="storageProvider">The storage provider used to select the destination and perform the file save operation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand]
    private Task SaveFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return SaveFileCoreAsync(storageProvider);
    }

    /// <summary>
    /// Asynchronously saves the current diagram to a file using the specified storage provider.
    /// </summary>
    /// <remarks>If the save operation succeeds, the current file path is updated and the diagram is marked as
    /// not dirty. If the operation fails, an error message is displayed and the error is logged.</remarks>
    /// <param name="storageProvider">The storage provider used to save the file. Must not be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    private async Task SaveFileCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            string? savedPath = await _fileService.SaveFileAsync(storageProvider, CurrentFilePath, EditorViewModel.DiagramText);
            if (savedPath is not null)
            {
                CurrentFilePath = savedPath;
                IsDirty = false;
                UpdateRecentFiles();
                _logger.LogInformation("Saved file: {SavedPath}", savedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file");
            await ShowErrorMessageAsync("Failed to save file. " + ex.Message);
        }
    }

    /// <summary>
    /// Initiates a file save operation using the specified storage provider, allowing the user to choose the file
    /// location and name.
    /// </summary>
    /// <param name="storageProvider">The storage provider used to present the file save dialog and handle file system access. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation. The task completes when the file has been saved or the
    /// operation is canceled.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand]
    private Task SaveFileAsAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return SaveFileAsCoreAsync(storageProvider);
    }

    /// <summary>
    /// Saves the current diagram text to a new file using the specified storage provider.
    /// </summary>
    /// <remarks>If the save operation is successful, the current file path is updated and the dirty state is
    /// cleared. If an error occurs during saving, an error message is displayed to the user and the failure is
    /// logged.</remarks>
    /// <param name="storageProvider">The storage provider used to select the destination and save the file. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    private async Task SaveFileAsCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            string? suggestedName = !string.IsNullOrEmpty(CurrentFilePath)
                ? Path.GetFileName(CurrentFilePath)
                : null;

            string? savedPath = await _fileService.SaveFileAsAsync(storageProvider, EditorViewModel.DiagramText, suggestedName);
            if (savedPath is not null)
            {
                CurrentFilePath = savedPath;
                IsDirty = false;
                UpdateRecentFiles();
                _logger.LogInformation("Saved file as: {SavedPath}", savedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file as");
            await ShowErrorMessageAsync("Failed to save file. " + ex.Message);
        }
    }

    /// <summary>
    /// Prompts the user to save unsaved changes to the current diagram, if any, before continuing the operation.
    /// </summary>
    /// <remarks>If there are no unsaved changes or the diagram is empty, the method returns immediately and
    /// continues the operation. If the user chooses to save, the diagram is saved using the specified storage provider.
    /// If the user cancels, the operation is halted. In case of an error displaying the dialog, the method returns <see
    /// langword="true"/> to avoid blocking the user.</remarks>
    /// <param name="storageProvider">The storage provider used to save the diagram file if the user chooses to save changes.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the operation
    /// should continue; otherwise, <see langword="false"/> if the user cancels.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    public Task<bool> PromptSaveIfDirtyAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        if (!IsDirty || string.IsNullOrWhiteSpace(EditorViewModel.DiagramText))
        {
            return Task.FromResult(true); // No unsaved changes, continue
        }

        return PromptSaveIfDirtyCoreAsync(storageProvider);
    }

    /// <summary>
    /// Displays a confirmation dialog prompting the user to save unsaved changes, and saves the file if the user
    /// chooses to do so.
    /// </summary>
    /// <remarks>If the main application window is unavailable or an error occurs while displaying the dialog,
    /// the method returns true to allow the operation to continue. The dialog presents options to save, discard, or
    /// cancel, and saving is performed using the provided storage provider.</remarks>
    /// <param name="storageProvider">The storage provider used to save the file if the user confirms the save operation. Cannot be null.</param>
    /// <returns>true if the user chooses to save or discard changes, or if the dialog cannot be shown; false if the user cancels
    /// the operation.</returns>
    private async Task<bool> PromptSaveIfDirtyCoreAsync(IStorageProvider storageProvider)
    {
        try
        {
            Window? mainWindow = GetParentWindow();
            if (mainWindow is null)
            {
                return true;
            }

            ConfirmationDialogViewModel confirmViewModel = _dialogFactory.CreateViewModel<ConfirmationDialogViewModel>();
            confirmViewModel.Title = "Unsaved Changes";

            string fileName = !string.IsNullOrEmpty(CurrentFilePath)
                ? Path.GetFileName(CurrentFilePath)
                : "Untitled";

            confirmViewModel.Message = $"Do you want to save changes to {fileName}?";
            confirmViewModel.IconData = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M11,7V13H13V7H11M11,15V17H13V15H11Z"; // Warning icon
            confirmViewModel.IconColor = Avalonia.Media.Brushes.Orange;

            ConfirmationDialog confirmDialog = new ConfirmationDialog { DataContext = confirmViewModel };
            ConfirmationResult result = await confirmDialog.ShowDialog<ConfirmationResult>(mainWindow);
            switch (result)
            {
                case ConfirmationResult.Yes:
                    // Save the file
                    await SaveFileCoreAsync(storageProvider);
                    return true;

                case ConfirmationResult.No:
                    // Don't save, continue
                    return true;

                case ConfirmationResult.Cancel:
                default:
                    // Cancel the operation
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show save confirmation dialog");
            return true; // Continue on error to avoid blocking the user
        }
    }

    /// <summary>
    /// Opens the specified recent file asynchronously and loads its contents into the editor, handling unsaved changes
    /// and file validation as needed.
    /// </summary>
    /// <remarks>If there are unsaved changes, the method prompts the user to save before proceeding. If the
    /// file does not exist or exceeds the allowed size, an error message is displayed and the file is removed from the
    /// recent files list. The method updates the recent files list and renders the loaded content if
    /// applicable.</remarks>
    /// <param name="filePath">The full path of the file to open. Cannot be null or empty. The file must exist and not exceed the maximum
    /// allowed size.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the file has been loaded or if the
    /// operation is cancelled due to validation or user action.</returns>
    [RelayCommand]
    private async Task OpenRecentFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            // Check for unsaved changes
            if (IsDirty)
            {
                Window? mainWindow = GetParentWindow();
                if (mainWindow?.StorageProvider is null)
                {
                    return;
                }

                bool canProceed = await PromptSaveIfDirtyAsync(mainWindow.StorageProvider);
                if (!canProceed)
                {
                    return;
                }
            }

            if (!File.Exists(filePath))
            {
                await ShowErrorMessageAsync($"File not found: {filePath}");

                // Remove from recent files
                _settingsService.Settings.RecentFiles.Remove(filePath);
                _settingsService.Save();
                UpdateRecentFiles();
                return;
            }

            if (!_fileService.ValidateFileSize(filePath))
            {
                // ReSharper disable once InconsistentNaming
                const double maxSizeMB = FileService.MaxFileSizeBytes / FileService.OneMBInBytes;
                await ShowErrorMessageAsync($"File size exceeds the maximum allowed size of {maxSizeMB:0.#}MB.");
                return;
            }

            // Read and load the file
            _isLoadingFile = true;
            try
            {
                string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                EditorViewModel.SetEditorState(content, 0, 0, 0, suppressEvents: true);
                CurrentFilePath = filePath;
                IsDirty = false;

                // Move to top of recent files
                _fileService.AddToRecentFiles(filePath);
                UpdateRecentFiles();

                // Render the newly loaded content if WebView is ready
                if (PreviewViewModel.IsWebViewReady)
                {
                    await PreviewViewModel.RenderAsync(content);
                }

                _logger.LogInformation("Opened recent file: {FilePath}", filePath);
            }
            finally
            {
                _isLoadingFile = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open recent file: {FilePath}", filePath);
            await ShowErrorMessageAsync($"Failed to open file: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the list of recently accessed files from the application's history.
    /// </summary>
    /// <remarks>This command removes all entries from the recent files list and updates any associated user
    /// interface elements to reflect the change. Use this method to reset the recent files history, for example, when
    /// privacy is a concern or to start a new session.</remarks>
    [RelayCommand]
    private void ClearRecentFiles()
    {
        _fileService.ClearRecentFiles();
        UpdateRecentFiles();
        _logger.LogInformation("Recent files cleared");
    }

    /// <summary>
    /// Updates the window title to reflect the current file name and unsaved changes status.
    /// </summary>
    /// <remarks>The window title is set to include the name of the current file, or "Untitled" if no file is
    /// open. An asterisk is appended if there are unsaved changes.</remarks>
    private void UpdateWindowTitle()
    {
        string fileName = !string.IsNullOrEmpty(CurrentFilePath)
            ? Path.GetFileName(CurrentFilePath)
            : "Untitled";

        string dirtyIndicator = IsDirty ? " *" : "";
        WindowTitle = $"MermaidPad - {fileName}{dirtyIndicator}";
    }

    /// <summary>
    /// Updates the status text to reflect the currently open file or indicate that no file is open.
    /// </summary>
    /// <remarks>If a file is open, the status text displays the file name. Otherwise, it shows a default
    /// message indicating that no file is open.</remarks>
    private void UpdateStatusText()
    {
        StatusText = !string.IsNullOrEmpty(CurrentFilePath) ? $"File: {Path.GetFileName(CurrentFilePath)}" : "No file open";
    }

    /// <summary>
    /// Refreshes the list of recent files by retrieving the latest entries from the file service.
    /// </summary>
    /// <remarks>Raises a property change notification for <c>HasRecentFiles</c> after updating the list. This
    /// method should be called when the recent files may have changed, such as after opening or closing
    /// files.</remarks>
    private void UpdateRecentFiles()
    {
        RecentFiles.Clear();
        foreach (string filePath in _fileService.GetRecentFiles())
        {
            RecentFiles.Add(filePath);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
    }

    /// <summary>
    /// Displays an error message dialog to the user asynchronously.
    /// </summary>
    /// <remarks>If the main application window is not available, the dialog will not be shown. The dialog
    /// uses a standard error icon and is intended for user-facing error notifications.</remarks>
    /// <param name="message">The error message text to display in the dialog. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation of showing the error message dialog.</returns>
    private async Task ShowErrorMessageAsync(string message)
    {
        try
        {
            Window? mainWindow = GetParentWindow();
            if (mainWindow is null)
            {
                return;
            }

            MessageDialogViewModel messageViewModel = _dialogFactory.CreateViewModel<MessageDialogViewModel>();
            messageViewModel.Title = "Error";
            messageViewModel.Message = message;
            messageViewModel.IconData = "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16"; // Error icon
            messageViewModel.IconColor = Avalonia.Media.Brushes.Red;

            MessageDialog messageDialog = new MessageDialog
            {
                DataContext = messageViewModel
            };

            await messageDialog.ShowDialog(mainWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show error message");
        }
    }

    #endregion File Open/Save

    #region AI Features and Panel Docking

    /// <summary>
    /// Opens the settings dialog for AI configuration.
    /// </summary>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        try
        {
            Window? mainWindow = GetParentWindow();
            if (mainWindow is null)
            {
                _logger.LogWarning("Cannot open settings: main window not found");
                return;
            }

            SettingsDialogViewModel settingsViewModel = _dialogFactory.CreateViewModel<SettingsDialogViewModel>();
            SettingsDialog settingsDialog = new SettingsDialog(settingsViewModel);
            bool? result = await settingsDialog.ShowDialog<bool?>(mainWindow);

            if (result == true)
            {
                // Settings were saved, update and persist settings, then recreate AI service
                _settingsService.Settings.AI = settingsViewModel.GetUpdatedSettings();
                _settingsService.Save();

                IAIService aiService = _aiServiceFactory.CreateService(_settingsService.Settings.AI);
                AIPanelViewModel.UpdateAIService(aiService);
                _logger.LogInformation("Settings saved and AI service updated");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open settings dialog");
            await ShowErrorMessageAsync("Failed to open settings. " + ex.Message);
        }
    }

    /// <summary>
    /// Handles the DiagramGenerated event from the AI panel.
    /// </summary>
    private void OnDiagramGenerated(object? sender, string generatedDiagram)
    {
        try
        {
            // Insert or replace the diagram text
            Dispatcher.UIThread.Post(() =>
            {
                EditorViewModel.DiagramText = generatedDiagram;
                _logger.LogInformation("Diagram generated by AI and inserted into editor");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle generated diagram");
        }
    }

    #endregion AI Features and Panel Docking

    /// <summary>
    /// Asynchronously renders the diagram text using the configured renderer.
    /// </summary>
    /// <remarks>This method delegates to the PreviewViewModel to handle rendering. The rendering process may require
    /// access to the UI context, so it does not use <see cref="Task.ConfigureAwait(bool)"/>. Ensure that the <see
    /// cref="CanRender"/> method returns <see langword="true"/> before invoking this command.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanRender))]
    private async Task RenderAsync()
    {
        await PreviewViewModel.RenderAsync(EditorViewModel.DiagramText);
    }

    /// <summary>
    /// Determines whether the diagram can be rendered based on the current state.
    /// </summary>
    /// <returns><see langword="true"/> if the WebView is ready and the diagram text is not null or whitespace; otherwise, <see
    /// langword="false"/>.</returns>
    private bool CanRender() => PreviewViewModel.IsWebViewReady && EditorViewModel.HasText;

    /// <summary>
    /// Clears the diagram text, resets the editor selection and caret position, and removes the last error.
    /// </summary>
    /// <remarks>This method delegates to the EditorViewModel and PreviewViewModel to handle clearing
    /// the diagram.  It must be executed on the UI thread to ensure proper synchronization with the user
    /// interface.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        // Clear editor state
        EditorViewModel.Clear();

        // Clear preview and render empty diagram
        PreviewViewModel.LastError = null;
        await PreviewViewModel.RenderAsync(string.Empty);
    }

    /// <summary>
    /// Determines whether the diagram can be cleared based on the current state.
    /// </summary>
    /// <returns><see langword="true"/> if the WebView is ready and the diagram text is not null, empty, or whitespace;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanClear() => PreviewViewModel.IsWebViewReady && EditorViewModel.HasText;

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
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        try
        {
            Window? window = GetParentWindow();
            if (window is null)
            {
                PreviewViewModel.LastError = "Unable to access main window for export dialog";
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
            _logger.LogError(ex, "Export failed");

            // Setting LastError updates UI, must be on UI thread
            PreviewViewModel.LastError = $"Export failed: {ex.Message}";
            Debug.WriteLine($"Export error: {ex}");
        }
    }

    /// <summary>
    /// Determines whether the export operation can be performed.
    /// </summary>
    /// <returns><see langword="true"/> if the web view is ready and the diagram text is not null, empty, or whitespace;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanExport() => PreviewViewModel.IsWebViewReady && EditorViewModel.HasText;

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
    /// <returns>A task representing the asynchronous export operation.</returns>
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
                    _logger.LogError(outerEx, "Export failed during PNG export with progress");

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
                            _logger.LogError(ex, "Error awaiting progress dialog task");
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
                        _logger.LogError(ex, "Error awaiting progress dialog task during cleanup");
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
            _logger.LogError(ex, "Export failed");

            // Setting LastError updates UI, must be on UI thread
            PreviewViewModel.LastError = $"Export failed: {ex.Message}";
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
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ShowSuccessMessageAsync(Window window, string message)
    {
        try
        {
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
            _logger.LogError(ex, "Failed to show success message");
            Debug.WriteLine($"Failed to show success message: {ex}");
        }
    }

    #region Event handlers

    /// <summary>
    /// Handles changes to the current file path by updating application settings and related UI elements.
    /// </summary>
    /// <remarks>This method updates the application's settings and refreshes the window title and status text
    /// to reflect the new file path.</remarks>
    /// <param name="value">The new file path to set as the current file. Can be null to indicate no file is selected.</param>
    partial void OnCurrentFilePathChanged(string? value)
    {
        _settingsService.Settings.CurrentFilePath = value;
        _settingsService.Save();
        UpdateWindowTitle();
        UpdateStatusText();
    }

    /// <summary>
    /// Handles changes to the dirty state of the object when the value of the IsDirty property changes.
    /// </summary>
    /// <remarks>This method is invoked automatically when the IsDirty property changes. Override this partial
    /// method to perform custom actions in response to changes in the dirty state, such as updating UI elements or
    /// enabling save functionality.</remarks>
    /// <param name="value">A value indicating whether the object is now considered dirty. <see langword="true"/> if the object has unsaved
    /// changes; otherwise, <see langword="false"/>.</param>
    partial void OnIsDirtyChanged(bool value)
    {
        UpdateWindowTitle();
        OnPropertyChanged(nameof(CanSave));
    }

    #endregion Event handlers

    /// <summary>
    /// Checks for updates to the Mermaid library and updates the application state with the latest version information.
    /// </summary>
    /// <remarks>This method performs a network call to check for updates asynchronously. The application
    /// state is updated with the  bundled and latest checked Mermaid versions, ensuring that property updates occur on
    /// the UI thread.</remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckForMermaidUpdatesAsync()
    {
        // This CAN use ConfigureAwait(false) for the network call
        await _updateService.CheckAndUpdateAsync()
            .ConfigureAwait(false);

        // Marshal property updates back to UI thread since ObservableProperty triggers INotifyPropertyChanged
        // Use Post for fire-and-forget. These properties values are not needed immediately - so no need for InvokeAsync
        Dispatcher.UIThread.Post(() =>
        {
            BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
            LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
        });
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
    /// including diagram text, live preview settings, Mermaid version information, and editor selection details.
    /// It also persists UI-specific settings like dock layout to a separate UI settings file.
    /// After updating the settings, both settings files are saved to ensure they are persisted across application
    /// sessions.</remarks>
    public void Persist()
    {
        // Save application settings
        _settingsService.Settings.LastDiagramText = EditorViewModel.DiagramText;
        _settingsService.Settings.LivePreviewEnabled = PreviewViewModel.LivePreviewEnabled;
        _settingsService.Settings.BundledMermaidVersion = BundledMermaidVersion;
        _settingsService.Settings.LatestCheckedMermaidVersion = LatestMermaidVersion;
        _settingsService.Settings.EditorSelectionStart = EditorViewModel.EditorSelectionStart;
        _settingsService.Settings.EditorSelectionLength = EditorViewModel.EditorSelectionLength;
        _settingsService.Settings.EditorCaretOffset = EditorViewModel.EditorCaretOffset;
        _settingsService.Settings.CurrentFilePath = CurrentFilePath;
        _settingsService.Save();

        // Save UI settings (dock layout)
        if (Layout is not null)
        {
            string? serializedLayout = _dockFactory.SerializeLayout(Layout);
            if (serializedLayout is not null)
            {
                _uiSettingsService.Settings.DockLayout = serializedLayout;
                _logger.LogInformation("Dock layout saved to UI settings");
            }
            else
            {
                _logger.LogWarning("Failed to serialize dock layout");
            }
        }
        _uiSettingsService.Save();
    }

    /// <summary>
    /// Gets sample Mermaid diagram text.
    /// </summary>
    /// <returns>A string containing the sample Mermaid diagram text.</returns>
    private static string SampleText => """
graph TD
  A[Start] --> B{Decision}
  B -->|Yes| C[Render Diagram]
  B -->|No| D[Edit Text]
  C --> E[Done]
  D --> B
""";
}
