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
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using MermaidPad.Extensions;
using MermaidPad.Models.Editor;
using MermaidPad.Services;
using MermaidPad.Services.Editor;
using MermaidPad.Services.Highlighting;
using MermaidPad.ViewModels.UserControls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.UserControls;

/// <summary>
/// A UserControl that provides a Mermaid diagram text editor with syntax highlighting,
/// clipboard operations, intellisense, and commenting functionality.
/// </summary>
/// <remarks>
/// This control encapsulates all editor-related functionality including:
/// <list type="bullet">
///     <item><description>TextEditor control with Mermaid syntax highlighting</description></item>
///     <item><description>Clipboard operations (Cut, Copy, Paste)</description></item>
///     <item><description>Edit operations (Undo, Redo, Select All, Find)</description></item>
///     <item><description>Comment/Uncomment functionality</description></item>
///     <item><description>Intellisense/code completion</description></item>
///     <item><description>Two-way synchronization with the ViewModel</description></item>
/// </list>
/// </remarks>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
public sealed partial class MermaidEditorView : UserControl
{
    private MermaidEditorViewModel? _vm;
    private readonly SyntaxHighlightingService _syntaxHighlightingService;
    private readonly IDebounceDispatcher _editorDebouncer;
    private readonly DocumentAnalyzer _documentAnalyzer;
    private readonly ILogger<MermaidEditorView> _logger;

    private bool _areAllEventHandlersCleanedUp;
    private bool _suppressEditorTextChanged;
    private bool _suppressEditorStateSync;
    private readonly SemaphoreSlim _contextMenuSemaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidEditorView"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor resolves required services from the application's DI container,
    /// initializes the editor, and sets up event handlers. The ViewModel is expected to be
    /// set via the DataContext property.
    /// </remarks>
    public MermaidEditorView()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _editorDebouncer = sp.GetRequiredService<IDebounceDispatcher>();
        _syntaxHighlightingService = sp.GetRequiredService<SyntaxHighlightingService>();
        _documentAnalyzer = sp.GetRequiredService<DocumentAnalyzer>();
        _logger = sp.GetRequiredService<ILogger<MermaidEditorView>>();

        _logger.LogInformation("=== {ViewName} Initialization Started ===", nameof(MermaidEditorView));

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool isSuccess = false;
        try
        {
            // Initialize syntax highlighting
            InitializeSyntaxHighlighting();

            // Initialize intellisense
            InitializeIntellisense();

            // Subscribe to theme changes
            ActualThemeVariantChanged += OnThemeChanged;

            isSuccess = true;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogTiming($"{nameof(MermaidEditorView)} initialization completed", stopwatch.Elapsed, isSuccess);
        }
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
            MermaidEditorViewModel? oldViewModel = _vm;
            MermaidEditorViewModel? newViewModel = DataContext as MermaidEditorViewModel;

            // Always unwind previous wiring first.
            // NOTE: This also removes editor event handlers, which is safe even if they were never subscribed.
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
                _logger.LogWarning("{ViewName} attached after hard cleanup; skipping rebind.", nameof(MermaidEditorView));
                return;
            }

            if (DataContext is not MermaidEditorViewModel dataContextViewModel)
            {
                return;
            }

            // If the VM changed (or we cleared it during detach), unwind old wiring and adopt the new VM reference.
            if (!ReferenceEquals(_vm, dataContextViewModel))
            {
                UnsubscribeViewModelEventHandlers(_vm);
                _vm = dataContextViewModel;
            }

            // Always "ensure bindings" on attach; wiring is idempotent.
            SetupViewModelBindings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebinding {ViewName} on attach.", nameof(MermaidEditorView));

            // Best-effort: avoid leaving partially wired state around.
            try
            {
                UnsubscribeViewModelEventHandlers(_vm);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Error during {ViewName} attach cleanup.", nameof(MermaidEditorView));
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
    /// Sets up bindings and event handlers between the View and ViewModel.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the ViewModel is null when this method is called.</exception>
    private void SetupViewModelBindings()
    {
        if (_vm is null)
        {
            throw new InvalidOperationException($"{nameof(SetupViewModelBindings)} called with null ViewModel. Initialize ViewModel before calling this method.");
        }

        bool isEditorStateMatchingViewModel = false;
        int viewModelTextLength = _vm.Text.Length;
        int editorTextLength = Editor.Document.TextLength;

        if (viewModelTextLength == editorTextLength)
        {
            // NOTE: Accessing Editor.Text allocates a string (AvaloniaEdit API).
            // Only do it when lengths match to minimize allocations.
            string editorText = Editor.Text;

            if (string.Equals(editorText, _vm.Text, StringComparison.Ordinal) &&
                Editor.SelectionStart == _vm.SelectionStart &&
                Editor.SelectionLength == _vm.SelectionLength &&
                Editor.CaretOffset == _vm.CaretOffset)
            {
                isEditorStateMatchingViewModel = true;
            }
        }

        if (!isEditorStateMatchingViewModel)
        {
            // Initialize editor with ViewModel data using validation
            SetEditorStateWithValidation(
                _vm.Text,
                _vm.SelectionStart,
                _vm.SelectionLength,
                _vm.CaretOffset
            );
        }

        // Set up two-way synchronization between Editor and ViewModel (idempotent subscriptions)
        SetupEditorViewModelSync();

        // Wire up clipboard and edit actions to ViewModel (idempotent assignments)
        WireUpEditorActions();

        _logger.LogInformation("ViewModel bindings established for {ViewName}", nameof(MermaidEditorView));
    }

    /// <summary>
    /// Sets the editor text, selection, and caret position while validating bounds and preventing circular updates.
    /// </summary>
    /// <param name="text">The text to set into the editor. Must not be <see langword="null"/>.</param>
    /// <param name="selectionStart">Requested selection start index.</param>
    /// <param name="selectionLength">Requested selection length.</param>
    /// <param name="caretOffset">Requested caret offset.</param>
    private void SetEditorStateWithValidation(string text, int selectionStart, int selectionLength, int caretOffset)
    {
        _suppressEditorTextChanged = true;
        _suppressEditorStateSync = true;
        try
        {
            Editor.Text = text;

            // Ensure selection bounds are valid
            int textLength = Editor.Document.TextLength;
            int validSelectionStart = Math.Max(0, Math.Min(selectionStart, textLength));
            int validSelectionLength = Math.Max(0, Math.Min(selectionLength, textLength - validSelectionStart));
            int validCaretOffset = Math.Max(0, Math.Min(caretOffset, textLength));

            Editor.SelectionStart = validSelectionStart;
            Editor.SelectionLength = validSelectionLength;
            Editor.CaretOffset = validCaretOffset;

            // Since this is yaml/diagram text, convert tabs to spaces for correct rendering
            Editor.Options.ConvertTabsToSpaces = true;
            Editor.Options.HighlightCurrentLine = true;
            Editor.Options.IndentationSize = 2;
            Editor.TextArea.IndentationStrategy = new MermaidIndentationStrategy(_documentAnalyzer, Editor.Options);

            _logger.LogInformation("Editor state set with {CharacterCount} characters", textLength);
        }
        finally
        {
            _suppressEditorTextChanged = false;
            _suppressEditorStateSync = false;
        }
    }

    /// <summary>
    /// Wires up synchronization between the editor control and the view model.
    /// </summary>
    /// <remarks>
    /// - Subscribes to editor text/selection/caret events and updates the view model using a debounce dispatcher.
    /// - Subscribes to view model property changes and applies them to the editor.
    /// - Suppresses reciprocal updates to avoid feedback loops.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the ViewModel has not been initialized prior to calling this method.</exception>
    private void SetupEditorViewModelSync()
    {
        if (_vm is null)
        {
            throw new InvalidOperationException($"{nameof(SetupEditorViewModelSync)} called with null ViewModel. Initialize ViewModel before calling this method.");
        }

        // Editor -> ViewModel synchronization (text)
        Editor.TextChanged -= OnEditorTextChanged;
        Editor.TextChanged += OnEditorTextChanged;

        // Editor selection/caret -> ViewModel: subscribe to both, coalesce into one update
        Editor.TextArea.SelectionChanged -= OnEditorSelectionChanged;
        Editor.TextArea.SelectionChanged += OnEditorSelectionChanged;

        Editor.TextArea.Caret.PositionChanged -= OnEditorCaretPositionChanged;
        Editor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;

        // ViewModel -> Editor synchronization (idempotent)
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    #region TextEditor delegates

    /// <summary>
    /// Handles the event when the text in the editor changes, updating related command states and synchronizing the
    /// view model's text property.
    /// </summary>
    /// <remarks>This method immediately updates undo, redo, and select-all command states, then debounces
    /// synchronization of the editor's text with the view model to optimize performance and avoid excessive updates. If
    /// text changes are suppressed or the view model is unavailable, the method exits without performing
    /// updates.</remarks>
    /// <param name="sender">The source of the event, typically the text editor control whose content has changed.</param>
    /// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_suppressEditorTextChanged || _vm is null)
        {
            return;
        }

        // Update undo/redo states immediately (not debounced)
        _vm.HasUndoHistory = Editor.CanUndo;
        _vm.HasRedoHistory = Editor.CanRedo;
        _vm.HasSelectableContent = CanSelectAllInEditor;

        // Debounce to avoid excessive updates
        _editorDebouncer.DebounceOnUI("editor-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
        {
            if (_vm is null)
            {
                return;
            }

            // NOTE: Accessing .Text allocates a string. This is unavoidable with standard AvaloniaEdit API.
            string text = Editor.Text;

            // Only update the ViewModel when the text has actually changed
            if (_vm.Text != text)
            {
                _suppressEditorStateSync = true;
                try
                {
                    _vm.Text = text;
                }
                finally
                {
                    _suppressEditorStateSync = false;
                }
            }
        },
        DispatcherPriority.Background);
    }

    /// <summary>
    /// Handles changes to the editor's selection and updates related command states accordingly.
    /// </summary>
    /// <remarks>This method updates the cut and copy command availability based on the current selection in
    /// the editor. It also notifies associated commands to refresh their executable state. If editor state
    /// synchronization is suppressed or the view model is unavailable, no action is taken.</remarks>
    /// <param name="sender">The source of the event, typically the editor control whose selection has changed.</param>
    /// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
    private void OnEditorSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressEditorStateSync || _vm is null)
        {
            return;
        }

        // Update cut/copy states immediately based on selection
        bool hasSelection = Editor.SelectionLength > 0;
        _vm.HasCuttableSelection = hasSelection;
        _vm.HasCopiableSelection = hasSelection;

        ScheduleEditorStateSyncIfNeeded();
    }

    /// <summary>
    /// Handles the event triggered when the caret position in the editor changes, and schedules synchronization of the
    /// editor state if required.
    /// </summary>
    /// <param name="sender">The source of the event, typically the editor control whose caret position has changed.</param>
    /// <param name="e">An <see cref="EventArgs"/> instance containing event data.</param>
    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_suppressEditorStateSync || _vm is null)
        {
            return;
        }

        ScheduleEditorStateSyncIfNeeded();
    }

    #endregion TextEditor delegates

    /// <summary>
    /// Coalesces caret and selection updates and schedules a debounced update of the view model's editor state.
    /// </summary>
    /// <remarks>
    /// The method compares the current editor state with the view model and only schedules an update
    /// when a change is detected. Values are read again at the time the debounced action runs to coalesce
    /// multiple rapid events into a single update.
    /// </remarks>
    private void ScheduleEditorStateSyncIfNeeded()
    {
        if (_vm is null)
        {
            throw new InvalidOperationException($"{nameof(ScheduleEditorStateSyncIfNeeded)} called with null ViewModel. Initialize ViewModel before calling this method.");
        }

        int selectionStart = Editor.SelectionStart;
        int selectionLength = Editor.SelectionLength;
        int caretOffset = Editor.CaretOffset;

        bool isCursorStateUnchanged =
            selectionStart == _vm.SelectionStart &&
            selectionLength == _vm.SelectionLength &&
            caretOffset == _vm.CaretOffset;

        if (isCursorStateUnchanged)
        {
            return;
        }

        _editorDebouncer.DebounceOnUI("editor-state", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
        {
            if (_vm is null)
            {
                return;
            }

            _suppressEditorStateSync = true;
            try
            {
                int textLength = Editor.Document.TextLength;

                // Take the latest values at execution time to coalesce multiple events.
                int rawSelectionStart = Editor.SelectionStart;
                int rawSelectionLength = Editor.SelectionLength;
                int rawCaretOffset = Editor.CaretOffset;

                int validSelectionStart = Math.Max(0, Math.Min(rawSelectionStart, textLength));
                int validSelectionLength = Math.Max(0, Math.Min(rawSelectionLength, textLength - validSelectionStart));
                int validCaretOffset = Math.Max(0, Math.Min(rawCaretOffset, textLength));

                _vm.SelectionStart = validSelectionStart;
                _vm.SelectionLength = validSelectionLength;
                _vm.CaretOffset = validCaretOffset;
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        },
        DispatcherPriority.Background);
    }

    /// <summary>
    /// Handles property changes on the view model and synchronizes relevant values to the editor control.
    /// </summary>
    /// <param name="sender">The object raising the property changed event (typically the view model).</param>
    /// <param name="e">Property changed event arguments describing which property changed.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressEditorStateSync || _vm is null)
        {
            return;
        }

        if (e.PropertyName == nameof(MermaidEditorViewModel.Text))
        {
            HandleTextPropertyChanged();
            return;
        }

        if (IsSelectionOrCaretProperty(e.PropertyName))
        {
            HandleSelectionOrCaretPropertyChanged();
        }
    }

    /// <summary>
    /// Synchronizes the editor's text with the view model when the view model's <see cref="MermaidEditorViewModel.Text"/> property changes.
    /// </summary>
    /// <remarks>
    /// This method updates the editor's text to match the current value of the view model's <see cref="MermaidEditorViewModel.Text"/> property,
    /// using a debounce mechanism to avoid unnecessary updates. It prevents recursive change notifications during
    /// the update process and is intended to be invoked from the view model's <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// handler when the <see cref="MermaidEditorViewModel.Text"/> property changes, ensuring consistency from the view model to the editor.
    /// </remarks>
    private void HandleTextPropertyChanged()
    {
        if (_vm is null)
        {
            return;
        }

        // NOTE: Accessing .Text allocates a string. This is unavoidable with standard AvaloniaEdit API
        string currentText = Editor.Text;
        if (currentText == _vm.Text)
        {
            return;
        }

        _editorDebouncer.DebounceOnUI("vm-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
        {
            if (_vm is null)
            {
                return;
            }

            _suppressEditorTextChanged = true;
            _suppressEditorStateSync = true;
            try
            {
                Editor.Text = _vm.Text;
            }
            finally
            {
                _suppressEditorTextChanged = false;
                _suppressEditorStateSync = false;
            }
        },
        DispatcherPriority.Background);
    }

    /// <summary>
    /// Synchronizes the editor's selection and caret position with the current view model state, applying debouncing to
    /// avoid excessive updates.
    /// </summary>
    /// <remarks>This method ensures that the editor's selection and caret offset reflect the latest values
    /// from the view model, correcting any out-of-bounds values as needed. Updates are debounced to improve performance
    /// and prevent rapid, repeated changes from causing unnecessary UI updates.</remarks>
    private void HandleSelectionOrCaretPropertyChanged()
    {
        if (_vm is null)
        {
            return;
        }

        _editorDebouncer.DebounceOnUI("vm-selection", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
        {
            if (_vm is null)
            {
                return;
            }

            _suppressEditorStateSync = true;
            try
            {
                // Validate bounds before setting
                int textLength = Editor.Document.TextLength;
                int validSelectionStart = Math.Max(0, Math.Min(_vm.SelectionStart, textLength));
                int validSelectionLength = Math.Max(0, Math.Min(_vm.SelectionLength, textLength - validSelectionStart));
                int validCaretOffset = Math.Max(0, Math.Min(_vm.CaretOffset, textLength));

                if (Editor.SelectionStart != validSelectionStart ||
                    Editor.SelectionLength != validSelectionLength ||
                    Editor.CaretOffset != validCaretOffset)
                {
                    Editor.SelectionStart = validSelectionStart;
                    Editor.SelectionLength = validSelectionLength;
                    Editor.CaretOffset = validCaretOffset;
                }
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        },
        DispatcherPriority.Background);
    }

    /// <summary>
    /// Determines if the property name is related to selection or caret.
    /// </summary>
    /// <param name="propertyName">The property name to check.</param>
    /// <returns>True if the property is selection or caret related; otherwise, false.</returns>
    private static bool IsSelectionOrCaretProperty(string? propertyName)
    {
        return propertyName == nameof(MermaidEditorViewModel.SelectionStart)
            || propertyName == nameof(MermaidEditorViewModel.SelectionLength)
            || propertyName == nameof(MermaidEditorViewModel.CaretOffset);
    }

    #region Editor Actions Wiring

    /// <summary>
    /// Wires up the clipboard and edit action delegates in the ViewModel to their implementations.
    /// </summary>
    /// <remarks>
    /// This method connects the ViewModel's Action properties to the actual implementation methods,
    /// enabling proper MVVM separation while allowing the View to implement UI-specific operations.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the ViewModel is null when this method is called.</exception>
    private void WireUpEditorActions()
    {
        if (_vm is null)
        {
            throw new InvalidOperationException($"{nameof(WireUpEditorActions)} called with null ViewModel. Initialize ViewModel before calling this method.");
        }

        _vm.CutAction = CutToClipboardAsync;
        _vm.CopyAction = CopyToClipboardAsync;
        _vm.PasteAction = PasteFromClipboardAsync;
        _vm.UndoAction = UndoEdit;
        _vm.RedoAction = RedoEdit;
        _vm.SelectAllAction = SelectAllText;
        _vm.OpenFindAction = OpenFindPanel;
        _vm.FindNextAction = FindNextMatch;
        _vm.FindPreviousAction = FindPreviousMatch;
        _vm.GetCurrentEditorContextFunc = GetCurrentEditorContext;
    }

    #endregion Editor Actions Wiring

    #region Clipboard Operations

    /// <summary>
    /// Asynchronously cuts the selected text to the clipboard and removes it from the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs a two-phase atomic operation:
    /// 1. Copy selected text to clipboard (awaits completion)
    /// 2. Remove text from editor only if clipboard operation succeeded
    /// </para>
    /// <para>
    /// THREADING: Avalonia's Clipboard.SetTextAsync must be called on the UI thread.
    /// The operation runs entirely on UI thread to avoid cross-thread access issues.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous cut operation.</returns>
    private async Task CutToClipboardAsync()
    {
        try
        {
            // If we're not on the UI thread, dispatch just the core logic (not the whole method)
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(CutToClipboardCoreAsync, DispatcherPriority.Normal);
                return;
            }

            if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(Editor.SelectedText) || Clipboard is null)
            {
                return;
            }

            // We're on the UI thread, proceed directly
            await CutToClipboardCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cut text to clipboard");
        }
    }

    /// <summary>
    /// Core logic for cutting text to clipboard. Must be called on the UI thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses a document transaction (<see cref="TextDocument.BeginUpdate"/>) to ensure
    /// atomicity between the selection validation and text removal. This prevents race conditions
    /// where user input could modify the document between the async clipboard operation and the
    /// subsequent removal.
    /// </para>
    /// <para>
    /// The transaction approach is preferred over locks because:
    /// 1. AvaloniaEdit's document model is designed for transactional updates
    /// 2. It batches operations into a single undo step
    /// 3. It prevents re-entrancy issues from event handlers firing during the operation
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CutToClipboardCoreAsync()
    {
        // UI thread validation checks
        string selectedText = Editor.SelectedText;
        if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(selectedText) || Clipboard is null || _vm is null)
        {
            return;
        }

        // Snapshot and capture selection state before any async operations
        int selectionStart = Editor.SelectionStart;
        int selectionLength = Editor.SelectionLength;
        TextDocument document = Editor.Document;

        bool isSuccess = false;
        try
        {
            // Copy to clipboard (async operation on UI thread)
            // This is the only async part - after this, we need atomicity
            await Clipboard.SetTextAsync(selectedText);

            // Remove text atomically using document transaction
            // BeginUpdate prevents other modifications and event handlers from interfering
            document.BeginUpdate();
            try
            {
                // Re-validate within the transaction - selection could have changed during clipboard await
                // This check + remove is now atomic (no other code can run between them)
                if (Editor.SelectionStart == selectionStart &&
                    Editor.SelectionLength == selectionLength &&
                    selectionStart + selectionLength <= document.TextLength)
                {
                    document.Remove(selectionStart, selectionLength);
                    _logger.LogInformation("Cut {CharCount} characters to clipboard", selectedText.Length);

                    // We just put text on clipboard, so paste *should* be enabled.
                    _vm.HasClipboardContent = true;
                }
                else
                {
                    // Log why we didn't remove the text
                    if (Editor.SelectionStart != selectionStart || Editor.SelectionLength != selectionLength)
                    {
                        _logger.LogWarning(
                            "Selection changed during cut operation (was {Start}:{Length}, now {NewStart}:{NewLength}) - text not removed",
                            selectionStart, selectionLength, Editor.SelectionStart, Editor.SelectionLength);
                    }
                    else if (selectionStart + selectionLength > document.TextLength)
                    {
                        _logger.LogWarning(
                            "Document size changed during cut operation (was {OldLength}, now {NewLength}) - text not removed to prevent exception",
                            selectionStart + selectionLength, document.TextLength);
                    }
                }

                isSuccess = true;
            }
            catch (Exception innerEx)
            {
                isSuccess = false;
                _logger.LogError(innerEx, "Error during cut operation clipboard set");
            }
            finally
            {
                document.EndUpdateAndUndoIfFailed(isSuccess);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cut operation text removal");
        }
    }

    /// <summary>
    /// Asynchronously copies the selected text to the clipboard.
    /// </summary>
    /// <remarks>
    /// This method must be called on the UI thread as it accesses both the TextEditor and the clipboard.
    /// </remarks>
    /// <returns>A task representing the asynchronous copy operation.</returns>
    private async Task CopyToClipboardAsync()
    {
        try
        {
            // If we're not on the UI thread, dispatch just the core logic (not the whole method)
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(CopyToClipboardCoreAsync, DispatcherPriority.Normal);
                return;
            }

            // Now safe to check UI properties - we're guaranteed to be on UI thread
            if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(Editor.SelectedText) || Clipboard is null)
            {
                return;
            }

            // We're on the UI thread, proceed directly
            await CopyToClipboardCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy text to clipboard");
        }
    }

    /// <summary>
    /// Core logic for copying text to clipboard. Must be called on the UI thread.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CopyToClipboardCoreAsync()
    {
        // UI thread validation checks
        string selectedText = Editor.SelectedText;
        if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(selectedText) || Clipboard is null || _vm is null)
        {
            return;
        }

        try
        {
            await Clipboard.SetTextAsync(selectedText);
            _logger.LogInformation("Copied {CharCount} characters to clipboard", selectedText.Length);

            // We just put text on clipboard, so paste *should* be enabled.
            _vm.HasClipboardContent = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy text to clipboard");
        }
    }

    /// <summary>
    /// Asynchronously pastes text from the clipboard into the current context, if available.
    /// </summary>
    /// <remarks>This method ensures thread safety by dispatching clipboard operations to the UI thread when
    /// necessary. If the clipboard is unavailable, the method completes without performing any action.</remarks>
    /// <returns>A task that represents the asynchronous paste operation.</returns>
    private async Task PasteFromClipboardAsync()
    {
        try
        {
            // If we're not on the UI thread, dispatch just the core logic (not the whole method)
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(PasteFromClipboardCoreAsync, DispatcherPriority.Normal);
                return;
            }

            if (Clipboard is null)
            {
                return;
            }

            // We're on the UI thread, proceed directly
            await PasteFromClipboardCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to paste text from clipboard");
        }
    }

    /// <summary>
    /// Asynchronously pastes text from the clipboard at the current caret position.
    /// </summary>
    /// <remarks>
    /// This method must be called on the UI thread as it accesses both the TextEditor and the clipboard.
    /// The pasted text replaces any current selection, and the caret moves to the end of the pasted text.
    /// </remarks>
    /// <returns>A task representing the asynchronous paste operation.</returns>
    private async Task PasteFromClipboardCoreAsync()
    {
        // UI thread validation checks
        if (Clipboard is null)
        {
            return;
        }

        try
        {
            string? clipboardText = await GetTextFromClipboardAsync();
            if (string.IsNullOrEmpty(clipboardText))
            {
                return;
            }

            int insertPosition = Editor.SelectionStart;
            int removeLength = Editor.SelectionLength;

            // Replace selection with clipboard text
            Editor.Document.Replace(insertPosition, removeLength, clipboardText);

            // Move caret to end of pasted text (standard behavior)
            Editor.CaretOffset = insertPosition + clipboardText.Length;

            _logger.LogInformation("Pasted {CharCount} characters from clipboard", clipboardText.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to paste text from clipboard");
        }
    }

    /// <summary>
    /// Asynchronously retrieves the current text content from the clipboard.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the clipboard text if available;
    /// otherwise, null.</returns>
    private async Task<string?> GetTextFromClipboardAsync()
    {
        // Get clipboard on UI thread when necessary
        if (Dispatcher.UIThread.CheckAccess())
        {
            if (Clipboard is null)
            {
                return null;
            }

            return await Clipboard.TryGetTextAsync();
        }

        // Off-UI caller: get the clipboard reference on the UI thread
        IClipboard? uiClipboard = await Dispatcher.UIThread.InvokeAsync(() => Clipboard, DispatcherPriority.Background);
        if (uiClipboard is null)
        {
            return null;
        }

        return await uiClipboard.TryGetTextAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously updates the ViewModel to reflect whether clipboard text is available for pasting.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method returns early (no-op) if the ViewModel is null, which can occur during
    /// dock state transitions when the View is detached but window activation events still fire.
    /// </remarks>
    private async Task UpdateCanPasteAsync()
    {
        // During dock state transitions (float, dock, pin), the View may be detached
        // (_vm = null) but window activation events still fire. This is expected, not an error.
        if (_vm is null)
        {
            return;
        }

        string? clipboardText = null;

        try
        {
            clipboardText = await GetTextFromClipboardAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading clipboard text");
        }

        bool canPaste = !string.IsNullOrEmpty(clipboardText);

        // Marshal back to UI thread to update the ViewModel property
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
#pragma warning disable IDE0031
            if (_vm is not null)
#pragma warning restore IDE0031
            {
                _vm.HasClipboardContent = canPaste;
            }
        }, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Gets the clipboard instance from the parent window.
    /// </summary>
    private IClipboard? Clipboard => TopLevel.GetTopLevel(this)?.Clipboard;

    #endregion Clipboard Operations

    #region Edit Operations

    /// <summary>
    /// Undoes the last edit operation in the editor.
    /// </summary>
    private void UndoEdit()
    {
        try
        {
            if (Editor.CanUndo)
            {
                Editor.Undo();
                _logger.LogInformation("Undo operation performed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform undo operation");
        }
    }

    /// <summary>
    /// Redoes the last undone edit operation in the editor.
    /// </summary>
    private void RedoEdit()
    {
        try
        {
            if (Editor.CanRedo)
            {
                Editor.Redo();
                _logger.LogInformation("Redo operation performed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform redo operation");
        }
    }

    /// <summary>
    /// Selects all text in the editor.
    /// </summary>
    private void SelectAllText()
    {
        try
        {
            //TODO: there is a known issue where SelectAll does not update. See: https://github.com/AvaloniaUI/AvaloniaEdit/issues/512
            // we need to monitor that issue for a new release and remove this comment when it is fixed.
            // I am tracking it here: https://github.com/udlose/MermaidPad/issues/258
            //Editor.SelectAll();

            // As a temporary workaround, we manually create the selection.
            Selection selection = Selection.Create(Editor.TextArea, 0, Editor.Document.TextLength);
            Editor.TextArea.Selection = selection;
            Editor.CaretOffset = Editor.Document.TextLength;

            // Make sure caret is visible after selection
            Editor.TextArea.Caret.BringCaretToView();

            _logger.LogInformation("Select all operation performed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform select all operation");
        }
    }

    /// <summary>
    /// Gets a value indicating whether the Select All operation can be performed in the editor.
    /// </summary>
    private bool CanSelectAllInEditor => Editor?.Document?.TextLength > 0;

    /// <summary>
    /// Opens the find panel in the editor.
    /// </summary>
    private void OpenFindPanel()
    {
        try
        {
            const int maxSearchPatternPrefillLength = 100;

            // Pre-fill search with selected text if available
            if (Editor.SelectionLength is > 0 and < maxSearchPatternPrefillLength)
            {
                Editor.SearchPanel.SearchPattern = Editor.SelectedText;
            }

            Editor.SearchPanel.Open();
            _logger.LogInformation("Find panel opened");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open find panel");
        }
    }

    /// <summary>
    /// Finds the next match in the editor using the current search pattern.
    /// </summary>
    private void FindNextMatch()
    {
        try
        {
            Editor.SearchPanel.FindNext();
            _logger.LogInformation("Find next operation performed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform find next operation");
        }
    }

    /// <summary>
    /// Finds the previous match in the editor using the current search pattern.
    /// </summary>
    private void FindPreviousMatch()
    {
        try
        {
            Editor.SearchPanel.FindPrevious();
            _logger.LogInformation("Find previous operation performed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform find previous operation");
        }
    }

    #endregion Edit Operations

    #region Editor Context

    /// <summary>
    /// Extracts the current editor context from the TextEditor control.
    /// </summary>
    /// <returns>
    /// An <see cref="EditorContext"/> containing the current editor state if valid; otherwise, null.
    /// </returns>
    private EditorContext? GetCurrentEditorContext()
    {
        // Validate editor is loaded and has a document
        if (!Editor.IsLoaded)
        {
            _logger.LogWarning("{MethodName}: Editor is not loaded", nameof(GetCurrentEditorContext));
            return null;
        }

        TextDocument? document = Editor.Document;
        if (document is null)
        {
            _logger.LogWarning("{MethodName}: Editor document is null", nameof(GetCurrentEditorContext));
            return null;
        }

        // Extract current editor state
        int selectionStart = Editor.SelectionStart;
        int selectionLength = Editor.SelectionLength;
        int caretOffset = Editor.CaretOffset;

        // Validate selection and caret are within bounds
        if (selectionStart < 0 || selectionLength < 0 || caretOffset < 0 || (selectionStart + selectionLength) > document.TextLength)
        {
            _logger.LogWarning("{MethodName}: Invalid editor state - SelectionStart={SelectionStart}, SelectionLength={SelectionLength}, CaretOffset={CaretOffset}, TextLength={TextLength}",
                nameof(GetCurrentEditorContext), selectionStart, selectionLength, caretOffset, document.TextLength);

            return new EditorContext(document, selectionStart, selectionLength, caretOffset)
            {
                IsValid = false
            };
        }

        // Emulate what AvaloniaEdit.TextEditor.SelectedText does
        string selectedText = string.Empty;
        if (!Editor.TextArea.Selection.IsEmpty)
        {
            selectedText = Editor.TextArea.Document.GetText(Editor.TextArea.Selection.SurroundingSegment);
        }

        return new EditorContext(document, selectionStart, selectionLength, caretOffset)
        {
            SelectedText = selectedText,
            IsValid = true
        };
    }

    #endregion Editor Context

    #region Context Menu State

    /// <summary>
    /// Ensures context-menu commands (Copy, Cut, Undo, Redo, Paste) are enabled/disabled based on the
    /// current editor selection and clipboard contents when the context menu opens.
    /// </summary>
    /// <param name="sender">Source of the event.</param>
    /// <param name="e">CancelEventArgs for the opening event.</param>
    [SuppressMessage("ReSharper", "AsyncVoidEventHandlerMethod", Justification = "This is an event handler, so async void is appropriate here.")]
    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "This is an event handler, so async void is appropriate here.")]
    private async void GetContextMenuState(object? sender, CancelEventArgs e)
    {
        if (e.Cancel || _vm is null)
        {
            return;
        }

        bool acquired = false;
        try
        {
            acquired = await _contextMenuSemaphore.WaitAsync(TimeSpan.Zero);
            if (!acquired)
            {
                return;
            }

            // Capture ViewModel reference in case it changes during awaits
            MermaidEditorViewModel? editorViewModel = _vm;
            if (editorViewModel is null)
            {
                return;
            }

            bool canUndo = Editor.CanUndo;
            bool canRedo = Editor.CanRedo;
            bool canSelectAllInEditor = CanSelectAllInEditor;
            await UpdateContextMenuStateExceptPasteAsync(editorViewModel, canUndo, canRedo, canSelectAllInEditor);

            string? clipboardText = null;
            try
            {
                clipboardText = await GetTextFromClipboardAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Clipboard read failed in context menu state check");
            }

            // Capture a simple boolean for the lambda capture instead of the clipboardText variable which could be large
            bool hasClipboardText = !string.IsNullOrEmpty(clipboardText);
            await UpdateContextMenuStatePasteOnlyAsync(editorViewModel, hasClipboardText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update CanPaste in context menu");
        }
        finally
        {
            if (acquired)
            {
                try
                {
                    _contextMenuSemaphore.Release();
                }
                catch (SemaphoreFullException ex)
                {
#if DEBUG
                    Debug.Fail("Context menu semaphore released without a matching WaitAsync", ex.ToString());
                    Debugger.Break();
#else
                    _logger.LogError(ex, "Context menu semaphore release failed (already released) - indicates a logic bug");
#endif
                }
            }
        }
    }

    /// <summary>
    /// Asynchronously updates the state of context menu commands, except for the Paste command, based on the current
    /// editor state.
    /// </summary>
    /// <remarks>This method ensures that context menu state updates are performed on the UI thread. The Copy
    /// and Cut commands are enabled only if there is a selection in the editor. The Paste command is not affected by
    /// this method.</remarks>
    /// <param name="editorViewModel">The view model representing the Mermaid editor whose context menu state will be updated. Cannot be null.</param>
    /// <param name="canUndo">A value indicating whether the Undo command should be enabled in the context menu.</param>
    /// <param name="canRedo">A value indicating whether the Redo command should be enabled in the context menu.</param>
    /// <param name="canSelectAllInEditor">A value indicating whether the Select All command should be enabled in the context menu.</param>
    /// <returns>A task that represents the asynchronous operation of updating the context menu state.</returns>
    private static async Task UpdateContextMenuStateExceptPasteAsync(MermaidEditorViewModel editorViewModel, bool canUndo, bool canRedo, bool canSelectAllInEditor)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateContextMenuStateExceptPaste(editorViewModel, canUndo, canRedo, canSelectAllInEditor);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                UpdateContextMenuStateExceptPaste(editorViewModel, canUndo, canRedo, canSelectAllInEditor), DispatcherPriority.Normal);
        }

        static void UpdateContextMenuStateExceptPaste(MermaidEditorViewModel editorViewModel, bool canUndo, bool canRedo, bool canSelectAllInEditor)
        {
            bool hasSelection = editorViewModel.SelectionLength > 0;
            editorViewModel.HasCopiableSelection = hasSelection;
            editorViewModel.HasCuttableSelection = hasSelection;
            editorViewModel.HasUndoHistory = canUndo;
            editorViewModel.HasRedoHistory = canRedo;
            editorViewModel.HasSelectableContent = canSelectAllInEditor;
        }
    }

    /// <summary>
    /// Asynchronously updates the paste command state in the context menu to reflect whether clipboard text is
    /// available.
    /// </summary>
    /// <remarks>This method ensures that the paste command in the editor's context menu is enabled only when
    /// clipboard text is available. The update is performed on the UI thread to maintain thread safety.</remarks>
    /// <param name="editorViewModel">The view model representing the editor whose context menu state will be updated. Cannot be null.</param>
    /// <param name="hasClipboardText">A value indicating whether the clipboard currently contains text. If <see langword="true"/>, the paste command
    /// will be enabled; otherwise, it will be disabled.</param>
    /// <returns>A task that represents the asynchronous operation of updating the context menu state.</returns>
    private static async Task UpdateContextMenuStatePasteOnlyAsync(MermaidEditorViewModel editorViewModel, bool hasClipboardText)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            editorViewModel.HasClipboardContent = hasClipboardText;
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                editorViewModel.HasClipboardContent = hasClipboardText, DispatcherPriority.Normal);
        }
    }
    #endregion Context Menu State

    #region Syntax Highlighting

    /// <summary>
    /// Initializes syntax highlighting for the text editor.
    /// </summary>
    private void InitializeSyntaxHighlighting()
    {
        try
        {
            _syntaxHighlightingService.Initialize();
            _syntaxHighlightingService.ApplyTo(Editor);
            _logger.LogInformation("Syntax highlighting initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize syntax highlighting");
        }
    }

    /// <summary>
    /// Handles theme variant changes to update syntax highlighting theme.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        try
        {
            bool isDarkTheme = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
            _syntaxHighlightingService.UpdateThemeForVariant(isDarkTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling theme change");
        }
    }

    #endregion Syntax Highlighting

    #region Public Methods

    /// <summary>
    /// Brings focus to the editor control and adjusts visuals for caret and selection.
    /// </summary>
    public void BringFocusToEditor()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            SetFocusToEditor();
            return;
        }

        Dispatcher.UIThread.Post(SetFocusToEditor, DispatcherPriority.Input);

        void SetFocusToEditor()
        {
            if (Editor?.TextArea is not null)
            {
                _suppressEditorStateSync = true;
                try
                {
                    Editor.TextArea.Caret.CaretBrush = Brushes.Red;
                    Editor.TextArea.SelectionBrush = Brushes.SteelBlue;
                    if (!Editor.IsFocused)
                    {
                        Editor.Focus();
                    }
                    Editor.TextArea.Caret.BringCaretToView();
                }
                finally
                {
                    _suppressEditorStateSync = false;
                }
            }
        }
    }

    /// <summary>
    /// Updates the clipboard paste state when the parent window gains focus.
    /// </summary>
    /// <remarks>
    /// This method is a no-op if the ViewModel is null, which can occur during dock state
    /// transitions when the View is detached but window activation events still fire.
    /// </remarks>
    public void UpdateClipboardStateOnActivation()
    {
        // During dock state transitions (float, dock, pin), the View may be detached
        // (_vm = null) but window activation events still fire. This is expected, not an error.
        if (_vm is null)
        {
            return;
        }

        UpdateCanPasteAsync()
            .SafeFireAndForget(onException: ex =>
                _logger.LogError(ex, "Failed to update clipboard state on activation"));
    }

    #endregion Public Methods

    #region Cleanup

    /// <summary>
    /// Unsubscribes all ViewModel-related event handlers.
    /// </summary>
    /// <param name="viewModel">The ViewModel instance to unsubscribe from.</param>
    private void UnsubscribeViewModelEventHandlers(MermaidEditorViewModel? viewModel)
    {
        // Editor event handlers (safe even if never subscribed)
        Editor.TextChanged -= OnEditorTextChanged;
        Editor.TextArea.SelectionChanged -= OnEditorSelectionChanged;
        Editor.TextArea.Caret.PositionChanged -= OnEditorCaretPositionChanged;

        if (viewModel is null)
        {
            return;
        }

        // ViewModel event handlers
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        // Clear action delegates
        viewModel.CutAction = null;
        viewModel.CopyAction = null;
        viewModel.PasteAction = null;
        viewModel.UndoAction = null;
        viewModel.RedoAction = null;
        viewModel.SelectAllAction = null;
        viewModel.OpenFindAction = null;
        viewModel.FindNextAction = null;
        viewModel.FindPreviousAction = null;
        viewModel.GetCurrentEditorContextFunc = null;
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

            UnsubscribeIntellisenseEventHandlers();
            ActualThemeVariantChanged -= OnThemeChanged;

            _contextMenuSemaphore.Dispose();

            _logger.LogInformation("All {ViewName} event handlers unsubscribed successfully", nameof(MermaidEditorView));
            _areAllEventHandlersCleanedUp = true;
        }
        else
        {
            _logger.LogWarning($"{nameof(UnsubscribeAllEventHandlers)} called multiple times; skipping subsequent call");
        }
    }

    /// <summary>
    /// Sets whether word wrap is enabled in the editor.
    /// </summary>
    /// <param name="enabled"><c>true</c> to enable word wrap; otherwise, <c>false</c>.</param>
    public void SetWordWrap(bool enabled)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Editor.WordWrap = enabled;
            return;
        }

        Dispatcher.UIThread.Post(() => Editor.WordWrap = enabled, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Sets whether line numbers are shown in the editor.
    /// </summary>
    /// <param name="show"><c>true</c> to show line numbers; otherwise, <c>false</c>.</param>
    public void SetShowLineNumbers(bool show)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Editor.ShowLineNumbers = show;
            return;
        }

        Dispatcher.UIThread.Post(() => Editor.ShowLineNumbers = show, DispatcherPriority.Normal);
    }

    #endregion Cleanup
}
