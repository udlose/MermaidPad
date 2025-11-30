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
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Infrastructure;
using MermaidPad.Models;
using MermaidPad.Models.Editor;
using MermaidPad.Services;
using MermaidPad.Services.Editor;
using MermaidPad.Services.Export;
using MermaidPad.Services.Theming;
using MermaidPad.ViewModels.Dialogs;
using MermaidPad.Views.Dialogs;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using TextMateSharp.Grammars;

namespace MermaidPad.ViewModels;
/// <summary>
/// Represents the main view model for the application's main window, providing properties, commands, and logic for
/// editing, rendering, exporting, and managing Mermaid diagrams.
/// </summary>
/// <remarks>This view model exposes state and command properties for data binding in the main window, including
/// file operations, diagram rendering, clipboard actions, and export functionality. It coordinates interactions between
/// the user interface and underlying services such as file management, rendering, and settings persistence. All
/// properties and commands are designed for use with MVVM frameworks and are intended to be accessed by the view for UI
/// updates and user interactions.</remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly MermaidRenderer _renderer;
    private readonly SettingsService _settingsService;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly ExportService _exportService;
    private readonly IDialogFactory _dialogFactory;
    private readonly IFileService _fileService;
    private readonly CommentingStrategy _commentingStrategy;
    private readonly ILogger<MainWindowViewModel> _logger;

    private const string DebounceRenderKey = "render";

    /// <summary>
    /// A value tracking if there is currently a file being loaded.
    /// </summary>
    private bool _isLoadingFile;

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
    public partial string? BundledMermaidVersion { get; set; }

    /// <summary>
    /// Gets or sets the latest Mermaid.js version available.
    /// </summary>
    [ObservableProperty]
    public partial string? LatestMermaidVersion { get; set; }

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

    #region Clipboard and Edit Properties

    /// <summary>
    /// Gets or sets a value indicating whether the current content can be cut to the clipboard.
    /// </summary>
    [ObservableProperty]
    public partial bool CanCutClipboard { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is available content to copy to the clipboard.
    /// </summary>
    [ObservableProperty]
    public partial bool CanCopyClipboard { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is available content in the clipboard to paste.
    /// </summary>
    [ObservableProperty]
    public partial bool CanPasteClipboard { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether undo is available.
    /// </summary>
    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether redo is available.
    /// </summary>
    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the 'Select All' operation is available.
    /// </summary>
    [ObservableProperty]
    public partial bool CanSelectAll { get; set; }

    #endregion Clipboard and Edit Properties

    #region Clipboard and Edit Actions

    /// <summary>
    /// Gets or sets the function to invoke when cutting text to the clipboard.
    /// </summary>
    /// <remarks>
    /// This function is set by MainWindow to implement the actual async clipboard operation.
    /// IMPORTANT: Returns a Task to ensure the operation completes atomically before allowing other operations -
    /// otherwise, there is a risk of race conditions with clipboard state.
    /// </remarks>
    public Func<Task>? CutAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when copying text to the clipboard.
    /// </summary>
    /// <remarks>
    /// This function is set by MainWindow to implement the actual async clipboard operation.
    /// IMPORTANT: Returns a Task to ensure the operation completes atomically before allowing other operations -
    /// otherwise, there is a risk of race conditions with clipboard state.
    /// </remarks>
    public Func<Task>? CopyAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when pasting text from the clipboard.
    /// </summary>
    /// <remarks>
    /// This function is set by MainWindow to implement the actual async clipboard operation.
    /// IMPORTANT: Returns a Task to ensure the operation completes atomically before allowing other operations -
    /// otherwise, there is a risk of race conditions with clipboard state.
    /// </remarks>
    public Func<Task>? PasteAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when undoing the last edit.
    /// </summary>
    /// <remarks>This action is set by MainWindow to implement the actual undo operation.</remarks>
    public Action? UndoAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when redoing the last undone edit.
    /// </summary>
    /// <remarks>This action is set by MainWindow to implement the actual redo operation.</remarks>
    public Action? RedoAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when selecting all text.
    /// </summary>
    /// <remarks>This action is set by MainWindow to implement the actual select all operation.</remarks>
    public Action? SelectAllAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when opening the find panel.
    /// </summary>
    /// <remarks>This action is set by MainWindow to open the TextEditor's built-in search panel.</remarks>
    public Action? OpenFindAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when finding the next match.
    /// </summary>
    /// <remarks>This action is set by MainWindow to find the next match using the TextEditor's search panel.</remarks>
    public Action? FindNextAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when finding the previous match.
    /// </summary>
    /// <remarks>This action is set by MainWindow to find the previous match using the TextEditor's search panel.</remarks>
    public Action? FindPreviousAction { get; internal set; }

    /// <summary>
    /// Gets or sets the function to invoke when retrieving the current editor context for comment/uncomment operations.
    /// </summary>
    /// <remarks>
    /// This function is set by MainWindow to extract the current editor state (document, selection, caret position) on-demand.
    /// This ensures fresh, accurate editor state is always used. Returns null if the editor is not in a valid state.
    /// </remarks>
    /// <returns>A new <see cref="EditorContext"/> instance or null if invalid.</returns>
    public Func<EditorContext?>? GetCurrentEditorContextFunc { get; internal set; }

    #endregion Clipboard and Edit Actions

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
    /// Gets a value indicating whether the Save command can execute.
    /// </summary>
    public bool CanSave => HasText && IsDirty;

    /// <summary>
    /// Gets a value indicating whether there is text in the editor.
    /// </summary>
    public bool HasText => !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Gets a value indicating whether there are recent files.
    /// </summary>
    public bool HasRecentFiles => RecentFiles.Count > 0;

    #region Theme properties

    /// <summary>
    /// Gets or sets the current application theme applied to the user interface.
    /// </summary>
    /// <remarks>Changing this property updates the visual appearance of the application to match the selected
    /// theme. The available themes are defined by the application and may vary depending on configuration or
    /// platform.</remarks>
    [ObservableProperty]
    public partial ApplicationTheme CurrentApplicationTheme { get; set; }

    /// <summary>
    /// Gets or sets the current theme applied to the editor interface.
    /// </summary>
    /// <remarks>Changing this property updates the visual appearance of the code editor to match the selected
    /// theme. The available themes are defined by the application and may vary depending on configuration or
    /// platform.</remarks>
    [ObservableProperty]
    public partial ThemeName CurrentEditorTheme { get; set; }

    #endregion Theme properties

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger{MainWindowViewModel}"/> instance for this view model.</param>
    /// <param name="themeService">The <see cref="IThemeService"/> instance for this view model.</param>
    /// <param name="renderer">The <see cref="MermaidRenderer"/> instance for this view model.</param>
    /// <param name="settingsService">The <see cref="SettingsService"/> instance for this view model.</param>
    /// <param name="updateService">The <see cref="MermaidUpdateService"/> instance for this view model.</param>
    /// <param name="editorDebouncer">The <see cref="IDebounceDispatcher"/> instance for this view model.</param>
    /// <param name="exportService">The <see cref="ExportService"/> instance for this view model.</param>
    /// <param name="dialogFactory">The <see cref="IDialogFactory"/> instance for this view model.</param>
    /// <param name="fileService">The <see cref="IFileService"/> instance for this view model.</param>
    /// <param name="commentingStrategy">The <see cref="CommentingStrategy"/> instance for this view model.</param>
    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IThemeService themeService,
        MermaidRenderer renderer,
        SettingsService settingsService,
        MermaidUpdateService updateService,
        IDebounceDispatcher editorDebouncer,
        ExportService exportService,
        IDialogFactory dialogFactory,
        IFileService fileService,
        CommentingStrategy commentingStrategy)
    {
        _logger = logger;
        _renderer = renderer;
        _settingsService = settingsService;
        _updateService = updateService;
        _editorDebouncer = editorDebouncer;
        _exportService = exportService;
        _dialogFactory = dialogFactory;
        _fileService = fileService;
        _commentingStrategy = commentingStrategy;
        _themeService = themeService;

        InitializeThemes();
        InitializeSettings();
        UpdateRecentFiles();
        UpdateWindowTitle();
    }

    #region File Open/Save

    /// <summary>
    /// Asynchronously opens a file using the specified storage provider.
    /// </summary>
    /// <param name="storageProvider">The storage provider used to select and access the file. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous file open operation.</returns>
    /// <remarks>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. Even when code executes on the main thread, the absence of SynchronizationContext
    /// causes pickers to silently fail or hang indefinitely without showing dialogs.
    /// </para>
    /// <para>
    /// References:
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/13484
    ///   (IStorageProvider.OpenFilePickerAsync randomly not showing the dialog)
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/15775
    ///   (StorageProvider.OpenFolderPickerAsync blocks UI)
    /// - https://github.com/AvaloniaUI/Avalonia/issues/15806
    ///   (Async Main() causes picker failures - STA thread requirement)
    /// </para>
    /// <para>
    /// Solution: Wrap all picker calls in Dispatcher.UIThread.InvokeAsync() to ensure proper context.
    /// This is defensive programming against ConfigureAwait(false) in the call chain removing the context.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand]
    private Task OpenFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => OpenFileCoreAsync(storageProvider));
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
                    DiagramText = content;
                    CurrentFilePath = filePath;
                    IsDirty = false;
                    UpdateRecentFiles();

                    // Render the newly loaded content if WebView is ready
                    if (IsWebViewReady)
                    {
                        await _renderer.RenderAsync(DiagramText);
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
    /// <remarks>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. Even when code executes on the main thread, the absence of SynchronizationContext
    /// causes pickers to silently fail or hang indefinitely without showing dialogs.
    /// </para>
    /// <para>
    /// References:
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/13484
    ///   (IStorageProvider.OpenFilePickerAsync randomly not showing the dialog)
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/15775
    ///   (StorageProvider.OpenFolderPickerAsync blocks UI)
    /// - https://github.com/AvaloniaUI/Avalonia/issues/15806
    ///   (Async Main() causes picker failures - STA thread requirement)
    /// </para>
    /// <para>
    /// Solution: Wrap all picker calls in Dispatcher.UIThread.InvokeAsync() to ensure proper context.
    /// This is defensive programming against ConfigureAwait(false) in the call chain removing the context.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveFileAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => SaveFileCoreAsync(storageProvider));
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
            string? savedPath = await _fileService.SaveFileAsync(storageProvider, CurrentFilePath, DiagramText);
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
    /// <remarks>
    /// <para>
    /// CRITICAL: Avalonia's IStorageProvider file/folder pickers require execution within a valid UI
    /// SynchronizationContext. Even when code executes on the main thread, the absence of SynchronizationContext
    /// causes pickers to silently fail or hang indefinitely without showing dialogs.
    /// </para>
    /// <para>
    /// References:
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/13484
    ///   (IStorageProvider.OpenFilePickerAsync randomly not showing the dialog)
    /// - https://github.com/AvaloniaUI/Avalonia/discussions/15775
    ///   (StorageProvider.OpenFolderPickerAsync blocks UI)
    /// - https://github.com/AvaloniaUI/Avalonia/issues/15806
    ///   (Async Main() causes picker failures - STA thread requirement)
    /// </para>
    /// <para>
    /// Solution: Wrap all picker calls in Dispatcher.UIThread.InvokeAsync() to ensure proper context.
    /// This is defensive programming against ConfigureAwait(false) in the call chain removing the context.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageProvider"/> is null.</exception>
    [RelayCommand(CanExecute = nameof(HasText))]    // Can only 'Save As' if there is text to save, even if not dirty
    private Task SaveFileAsAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        return Dispatcher.UIThread.InvokeAsync(() => SaveFileAsCoreAsync(storageProvider));
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

            string? savedPath = await _fileService.SaveFileAsAsync(storageProvider, DiagramText, suggestedName);
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

        if (!IsDirty || string.IsNullOrWhiteSpace(DiagramText))
        {
            return Task.FromResult(true); // No unsaved changes, continue
        }

        return Dispatcher.UIThread.InvokeAsync(() => PromptSaveIfDirtyCoreAsync(storageProvider));
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
            confirmViewModel.ShowCancelButton = true;
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
                DiagramText = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                CurrentFilePath = filePath;
                IsDirty = false;

                // Move to top of recent files
                _fileService.AddToRecentFiles(filePath);
                UpdateRecentFiles();

                // Render the newly loaded content if WebView is ready
                if (IsWebViewReady)
                {
                    await _renderer.RenderAsync(DiagramText);
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
    //TODO - DaveBlack - add CanExecute logic here for ClearRecentFiles
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

    #region Help Menu Commands

    /// <summary>
    /// Gets the command that opens the application's log file directory (%APPDATA%\MermaidPad) in the system's file explorer.
    /// </summary>
    /// <summary>
    /// Opens the application's log file directory using the system's default file explorer.
    /// </summary>
    [RelayCommand]
    private void ViewLogs()
    {
        string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MermaidPad");

        try
        {
            // Use ShellExecute to open the directory in the default file explorer
            Process.Start(new ProcessStartInfo(logDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open log directory: {LogDirectory}", logDirectory);
            // Consider showing an error message to the user here if this command is expected to fail silently.
            // For now, we rely on logging.
        }
    }

    #endregion Help Menu Commands
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
        // Display a confirmation dialog before clearing
        try
        {
            Window? mainWindow = GetParentWindow();
            if (mainWindow is null)
            {
                return;
            }

            ConfirmationDialogViewModel confirmViewModel = _dialogFactory.CreateViewModel<ConfirmationDialogViewModel>();
            confirmViewModel.ShowCancelButton = false;
            confirmViewModel.Title = "Clear Editor?";
            confirmViewModel.Message = "Are you sure you want to clear the source code and diagram? To undo your changes, click Edit, then Undo.";
            confirmViewModel.IconData = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M11,7V13H13V7H11M11,15V17H13V15H11Z"; // Warning icon
            confirmViewModel.IconColor = Avalonia.Media.Brushes.Orange;
            ConfirmationDialog confirmDialog = new ConfirmationDialog { DataContext = confirmViewModel };
            ConfirmationResult result = await confirmDialog.ShowDialog<ConfirmationResult>(mainWindow);
            if (result == ConfirmationResult.Yes)
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    await ClearCoreAsync();
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(ClearCoreAsync);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show confirmation dialog");
        }
    }

    /// <summary>
    /// Asynchronously clears the contents of the current editor document and updates the UI to reflect the cleared
    /// state.
    /// </summary>
    /// <remarks>This method must be called on the UI thread, as it updates UI-bound properties and triggers
    /// rendering. If the editor context or document is invalid, the method logs a warning and performs no action. The
    /// operation is batched to ensure a single undo step is created. Undo functionality is preserved if the operation
    /// fails.</remarks>
    /// <returns>A task that represents the asynchronous clear operation.</returns>
    private async Task ClearCoreAsync()
    {
        // These property updates must happen on UI thread
        // Get the current editor context on-demand to ensure fresh state
        EditorContext? editorContext = GetCurrentEditorContextFunc?.Invoke();

        // Make sure the EditorContext is valid
        if (editorContext?.IsValid != true)
        {
            _logger.LogWarning("{MethodName} called with invalid editor context", nameof(ClearCoreAsync));
            return;
        }

        TextDocument? document = editorContext.Document;
        if (document is null)
        {
            _logger.LogWarning("{MethodName} called with null Document", nameof(ClearCoreAsync));
            return;
        }

        bool isSuccess = false;
        try
        {
            // Begin document update to batch changes together so that only one undo step is created
            document.BeginUpdate();

            // Important: operate only on the TextDocument to ensure undo works correctly.
            // DO NOT set DiagramText or any other editor-related VM properties directly here!
            document.Text = string.Empty;

            // NO ConfigureAwait(false) - renderer needs UI context
            await _renderer.RenderAsync(string.Empty);

            isSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear diagram");
        }
        finally
        {
            editorContext.EndUpdateAndUndoIfFailed(isSuccess);
        }
    }

    /// <summary>
    /// Determines whether the diagram can be cleared based on the current state.
    /// </summary>
    /// <returns><see langword="true"/> if the WebView is ready and the diagram text is not null, empty, or whitespace;
    /// otherwise, <see langword="false"/>.</returns>
    private bool CanClear() => IsWebViewReady && HasText;

    #region Export methods

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
            _logger.LogError(ex, "Export failed");

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

    #endregion Export methods

    #region Clipboard and Edit Commands

    /// <summary>
    /// Performs a cut operation by removing the selected content and placing it on the clipboard asynchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This async command ensures the cut operation completes atomically, preventing race conditions
    /// where the selection might change before the operation finishes.
    /// </para>
    /// <para>
    /// This command is typically used in clipboard or editing scenarios to implement cut
    /// functionality. The operation is only performed if a cut action is defined. The ability to execute this command
    /// is determined by the <see cref="CanCutClipboard"/> property or method.
    /// </para>
    /// </remarks>
    /// <returns>A task that represents the asynchronous cut operation.</returns>
    [RelayCommand(CanExecute = nameof(CanCutClipboard))]
    private async Task CutAsync()
    {
        if (CutAction is not null)
        {
            await CutAction();
        }
    }

    /// <summary>
    /// Asynchronously copies the current content to the clipboard if a copy action is available.
    /// </summary>
    /// <remarks>This method performs no action if no copy action is defined. Use the <see cref="CanCopyClipboard"/>
    /// property to determine whether copying is currently possible before invoking this method.</remarks>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    [RelayCommand(CanExecute = nameof(CanCopyClipboard))]
    private async Task CopyAsync()
    {
        if (CopyAction is not null)
        {
            await CopyAction();
        }
    }

    /// <summary>
    /// Executes the paste operation asynchronously if a paste action is available.
    /// </summary>
    /// <remarks>This method is intended to be used as a command handler for paste actions, typically in
    /// response to user interface events. The method does nothing if no paste action is defined.</remarks>
    /// <returns>A task that represents the asynchronous paste operation.</returns>
    [RelayCommand(CanExecute = nameof(CanPasteClipboard))]
    private async Task PasteAsync()
    {
        if (PasteAction is not null)
        {
            await PasteAction();
        }
    }

    /// <summary>
    /// Undoes the last edit operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoAction?.Invoke();

    /// <summary>
    /// Redoes the last undone edit operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => RedoAction?.Invoke();

    /// <summary>
    /// Selects all text in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAll() => SelectAllAction?.Invoke();

    /// <summary>
    /// Opens the find panel in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasText))]
    private void OpenFind() => OpenFindAction?.Invoke();

    /// <summary>
    /// Finds the next match in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasText))]
    private void FindNext() => FindNextAction?.Invoke();

    /// <summary>
    /// Finds the previous match in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasText))]
    private void FindPrevious() => FindPreviousAction?.Invoke();

    #region Comment/Uncomment Selection Commands

    /// <summary>
    /// Comments the currently selected text in the editor, if a selection is present and commenting is allowed.
    /// It uses the commenting strategy defined for the editor defined in <see cref="CommentingStrategy"/>.
    /// </summary>
    /// <remarks>
    /// The editor context is retrieved on-demand via <see cref="GetCurrentEditorContextFunc"/> to ensure
    /// the most current selection state is used. This command is enabled only when there is text in the editor.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasText))]
    private void CommentSelection()
    {
        // Make sure the GetCurrentEditorContextFunc is defined
        if (GetCurrentEditorContextFunc is null)
        {
            _logger.LogWarning("{MethodName} called with undefined {Property}. Initialize this property in MainWindow.WireUpEditorActions.", nameof(CommentSelection), nameof(GetCurrentEditorContextFunc));
            return;
        }

        // Get the current editor context on-demand to ensure fresh state
        EditorContext? editorContext = GetCurrentEditorContextFunc?.Invoke();

        // Make sure the EditorContext is valid
        if (editorContext?.IsValid != true)
        {
            _logger.LogWarning("{MethodName} called with invalid editor context", nameof(CommentSelection));
            return;
        }

        _commentingStrategy.CommentSelection(editorContext);
    }

    /// <summary>
    /// Removes comments from the currently selected text in the editor, if a selection is present and uncommenting is allowed.
    /// It uses the uncommenting strategy defined for the editor defined in <see cref="CommentingStrategy"/>.
    /// </summary>
    /// <remarks>
    /// The editor context is retrieved on-demand via <see cref="GetCurrentEditorContextFunc"/> to ensure
    /// the most current selection state is used. This command is enabled only when there is text in the editor.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasText))]
    private void UncommentSelection()
    {
        // Make sure the GetCurrentEditorContextFunc is defined
        if (GetCurrentEditorContextFunc is null)
        {
            _logger.LogWarning("{MethodName} called with undefined {Property}. Initialize this property in MainWindow.WireUpEditorActions.", nameof(UncommentSelection), nameof(GetCurrentEditorContextFunc));
            return;
        }

        // Get the current editor context on-demand to ensure fresh state
        EditorContext? editorContext = GetCurrentEditorContextFunc?.Invoke();

        // Make sure the EditorContext is valid
        if (editorContext?.IsValid != true)
        {
            _logger.LogWarning("{MethodName} called with invalid editor context", nameof(UncommentSelection));
            return;
        }

        _commentingStrategy.UncommentSelection(editorContext);
    }

    #endregion Comment/Uncomment Selection Commands

    #endregion Clipboard and Edit Commands

    #region Theme methods

    /// <summary>
    /// Sets the application's theme to the specified value.
    /// </summary>
    /// <remarks>Calling this method updates the application's appearance and raises a property change
    /// notification for <c>CurrentApplicationTheme</c>. This method should be used to switch between available themes
    /// at runtime.</remarks>
    /// <param name="theme">The theme to apply to the application. Must be a valid <see cref="ApplicationTheme"/> value.</param>
    [RelayCommand]
    private void SetApplicationTheme(ApplicationTheme theme)
    {
        try
        {
            _themeService.ApplyApplicationTheme(theme);
            CurrentApplicationTheme = theme;
            _logger.LogInformation("Application theme changed to: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply application theme: {Theme}", theme);
            LastError = $"Failed to apply theme: {ex.Message}";
        }
    }

    /// <summary>
    /// Sets the editor's theme to the specified value.
    /// </summary>
    /// <remarks>Raises the <c>CurrentEditorTheme</c> property change notification after applying the theme.
    /// This method is typically invoked in response to user actions that change the editor's appearance.</remarks>
    /// <param name="editor">The text editor to apply the theme to.</param>
    /// <param name="theme">The theme to apply to the editor. Must be a valid <see cref="ThemeName"/> value.</param>
    public void SetEditorTheme(TextEditor editor, ThemeName theme)
    {
        try
        {
            _themeService.ApplyEditorTheme(editor, theme);
            CurrentEditorTheme = theme;
            _logger.LogInformation("Editor theme changed to: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply editor theme: {Theme}", theme);
            LastError = $"Failed to apply editor theme: {ex.Message}";
        }
    }

    /// <summary>
    /// Retrieves the display name associated with the specified application theme.
    /// </summary>
    /// <param name="theme">The application theme for which to obtain the display name.</param>
    /// <returns>A string containing the display name of the specified theme. Returns an empty string if the theme does not have
    /// a display name.</returns>
    public string GetApplicationThemeDisplayName(ApplicationTheme theme) => _themeService.GetApplicationThemeDisplayName(theme);

    /// <summary>
    /// Retrieves the display name for the specified editor theme.
    /// </summary>
    /// <param name="theme">The theme for which to obtain the display name. Must be a valid value of <see cref="ThemeName"/>.</param>
    /// <returns>A string containing the display name of the specified editor theme.</returns>
    public string GetEditorThemeDisplayName(ThemeName theme) => _themeService.GetEditorThemeDisplayName(theme);

    /// <summary>
    /// Determines whether the specified application theme is considered a dark theme.
    /// </summary>
    /// <param name="theme">The application theme to evaluate.</param>
    /// <returns>true if the specified theme is classified as dark; otherwise, false.</returns>
    public bool IsDarkTheme(ApplicationTheme theme) => _themeService.IsDarkTheme(theme);

    /// <summary>
    /// Retrieves all application themes that are available for use.
    /// </summary>
    /// <returns>An array of <see cref="ApplicationTheme"/> objects representing the available themes. The array will be empty if
    /// no themes are available.</returns>
    public ApplicationTheme[] GetAvailableApplicationThemes() => _themeService.GetAvailableApplicationThemes();

    /// <summary>
    /// Retrieves the list of available editor themes that can be applied to the editor interface.
    /// </summary>
    /// <returns>An array of <see cref="ThemeName"/> values representing all available editor themes. The array will be empty if
    /// no themes are available.</returns>
    public ThemeName[] GetAvailableEditorThemes() => _themeService.GetAvailableEditorThemes();

    #endregion Theme methods

    #region Event handlers

    /// <summary>
    /// Handles changes to the WebView readiness state.
    /// </summary>
    /// <remarks>This method updates the state of related commands based on the WebView readiness state. When
    /// the WebView becomes ready, associated commands are enabled.</remarks>
    /// <param name="value">A boolean value indicating the new readiness state of the WebView.  <see langword="true"/> if the WebView is
    /// ready; otherwise, <see langword="false"/>.</param>
    partial void OnIsWebViewReadyChanged(bool value)
    {
        _logger.LogInformation("IsWebViewReady changed to: {IsWebViewReady}", value);

        // Update command states when WebView ready state changes
        RenderCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
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
        // Mark as dirty when text changes (ONLY if we're not loading a file)
        if (!_isLoadingFile)
        {
            IsDirty = true;
        }

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
        CommentSelectionCommand.NotifyCanExecuteChanged();
        UncommentSelectionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasText));
    }

    /// <summary>
    /// Handles changes to the live preview enabled state.
    /// </summary>
    /// <param name="value">The new value indicating whether live preview is enabled.</param>
    partial void OnLivePreviewEnabledChanged(bool value)
    {
        // If we can't render yet, just return
        if (!IsWebViewReady)
        {
            return;
        }

        if (value)
        {
            if (string.IsNullOrWhiteSpace(DiagramText))
            {
                return;
            }

            // SafeFireAndForget handles context, but the error handler updates UI
            _renderer.RenderAsync(DiagramText).SafeFireAndForget(onException: ex =>
            {
                // Even though SafeFireAndForget has a continueOnCapturedContext param, it doesn't guarantee UI thread here
                Dispatcher.UIThread.Post(() =>
                {
                    LastError = $"Failed to render diagram: {ex.Message}";
                    Debug.WriteLine(ex);
                    _logger.LogError(ex, "Live preview render failed");
                });
            });
        }
        else
        {
            _editorDebouncer.Cancel(DebounceRenderKey);
        }
    }

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
    /// Initializes the current application and editor themes by retrieving their values from the theme service.
    /// </summary>
    /// <remarks>This method should be called to synchronize theme-related properties with the latest values
    /// provided by the theme service. It does not apply or change themes, but updates the local state to reflect the
    /// current themes in use.</remarks>
    private void InitializeThemes()
    {
        CurrentApplicationTheme = _themeService.CurrentApplicationTheme;
        CurrentEditorTheme = _themeService.CurrentEditorTheme;
    }

    /// <summary>
    /// Initializes the application's settings by loading values from the current settings service.
    /// </summary>
    /// <remarks>This method updates properties such as diagram text, Mermaid versions, live preview state,
    /// editor selection, caret position, and the current file path to reflect the latest persisted settings. It should
    /// be called to synchronize the application's state with user preferences or previously saved
    /// configuration.</remarks>
    private void InitializeSettings()
    {
        // Initialize properties from settings
        DiagramText = _settingsService.Settings.LastDiagramText ?? SampleText;
        BundledMermaidVersion = _settingsService.Settings.BundledMermaidVersion;
        LatestMermaidVersion = _settingsService.Settings.LatestCheckedMermaidVersion;
        LivePreviewEnabled = _settingsService.Settings.LivePreviewEnabled;
        EditorSelectionStart = _settingsService.Settings.EditorSelectionStart;
        EditorSelectionLength = _settingsService.Settings.EditorSelectionLength;
        EditorCaretOffset = _settingsService.Settings.EditorCaretOffset;
        CurrentFilePath = _settingsService.Settings.CurrentFilePath;
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
        _settingsService.Settings.CurrentFilePath = CurrentFilePath;
        _settingsService.Save();
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
