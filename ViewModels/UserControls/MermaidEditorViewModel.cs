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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Models.Editor;
using MermaidPad.Services.Editor;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.UserControls;

/// <summary>
/// Represents the ViewModel for the MermaidEditorView UserControl, providing properties, commands, and logic
/// for text editing operations including clipboard, undo/redo, find, and commenting functionality.
/// </summary>
/// <remarks>
/// This ViewModel exposes state and command properties for data binding in the editor UserControl, including
/// clipboard actions, selection state, and text manipulation. It coordinates interactions between the user interface
/// and underlying services such as commenting strategy. All properties and commands are designed for use with MVVM
/// frameworks and are intended to be accessed by the View for UI updates and user interactions.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated via ViewModelFactory.")]
internal sealed partial class MermaidEditorViewModel : ViewModelBase
{
    private readonly CommentingStrategy _commentingStrategy;
    private readonly ILogger<MermaidEditorViewModel> _logger;

    #region Edit State and Clipboard Properties

    /// <summary>
    /// Gets or sets the current text content in the editor.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText))]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selection start index in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int SelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the selection length in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int SelectionLength { get; set; }

    /// <summary>
    /// Gets or sets the caret offset in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int CaretOffset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current content can be cut to the clipboard.
    /// </summary>
    [ObservableProperty]
    public partial bool CanCut { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is available content to copy to the clipboard.
    /// </summary>
    [ObservableProperty]
    public partial bool CanCopy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is available content in the clipboard to paste.
    /// </summary>
    [ObservableProperty]
    public partial bool CanPaste { get; set; }

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

    #endregion Edit State and Clipboard Properties

    #region Computed Properties

    /// <summary>
    /// Gets a value indicating whether the current text is not null or empty.
    /// </summary>
    public bool HasText => !string.IsNullOrEmpty(Text);

    #endregion Computed Properties

    #region Action Delegates

    /// <summary>
    /// Gets or sets the function to invoke when cutting text to the clipboard.
    /// </summary>
    /// <remarks>
    /// This function is set by MermaidEditorView to implement the actual async clipboard operation.
    /// IMPORTANT: Returns a Task to ensure the operation completes atomically before allowing other operations -
    /// otherwise, there is a risk of race conditions with clipboard state.
    /// </remarks>
    public Func<Task>? CutAction { get; internal set; }

    /// <summary>
    /// Gets or sets the function to invoke when copying text to the clipboard.
    /// </summary>
    /// <remarks>
    /// This function is set by MermaidEditorView to implement the actual async clipboard operation.
    /// IMPORTANT: Returns a Task to ensure the operation completes atomically before allowing other operations -
    /// otherwise, there is a risk of race conditions with clipboard state.
    /// </remarks>
    public Func<Task>? CopyAction { get; internal set; }

    /// <summary>
    /// Gets or sets the function to invoke when pasting text from the clipboard.
    /// </summary>
    /// <remarks>
    /// This function is set by MermaidEditorView to implement the actual async clipboard operation.
    /// IMPORTANT: Returns a Task to ensure the operation completes atomically before allowing other operations -
    /// otherwise, there is a risk of race conditions with clipboard state.
    /// </remarks>
    public Func<Task>? PasteAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when undoing the last edit.
    /// </summary>
    /// <remarks>This action is set by MermaidEditorView to implement the actual undo operation.</remarks>
    public Action? UndoAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when redoing the last undone edit.
    /// </summary>
    /// <remarks>This action is set by MermaidEditorView to implement the actual redo operation.</remarks>
    public Action? RedoAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when selecting all text.
    /// </summary>
    /// <remarks>This action is set by MermaidEditorView to implement the actual select all operation.</remarks>
    public Action? SelectAllAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when opening the find panel.
    /// </summary>
    /// <remarks>This action is set by MermaidEditorView to open the TextEditor's built-in search panel.</remarks>
    public Action? OpenFindAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when finding the next match.
    /// </summary>
    /// <remarks>This action is set by MermaidEditorView to find the next match using the TextEditor's search panel.</remarks>
    public Action? FindNextAction { get; internal set; }

    /// <summary>
    /// Gets or sets the action to invoke when finding the previous match.
    /// </summary>
    /// <remarks>This action is set by MermaidEditorView to find the previous match using the TextEditor's search panel.</remarks>
    public Action? FindPreviousAction { get; internal set; }

    /// <summary>
    /// Gets or sets the function to invoke when retrieving the current editor context for comment/uncomment operations.
    /// </summary>
    /// <remarks>
    /// This function is set by MermaidEditorView to extract the current editor state (document, selection, caret position) on-demand.
    /// This ensures fresh, accurate editor state is always used. Returns null if the editor is not in a valid state.
    /// </remarks>
    /// <returns>A new <see cref="EditorContext"/> instance or null if invalid.</returns>
    public Func<EditorContext?>? GetCurrentEditorContextFunc { get; internal set; }

    #endregion Action Delegates

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidEditorViewModel"/> class.
    /// </summary>
    /// <param name="commentingStrategy">The commenting strategy service for comment/uncomment operations.</param>
    /// <param name="logger">The logger instance for this view model.</param>
    public MermaidEditorViewModel(CommentingStrategy commentingStrategy, ILogger<MermaidEditorViewModel> logger)
    {
        _commentingStrategy = commentingStrategy ?? throw new ArgumentNullException(nameof(commentingStrategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
    /// is determined by the <see cref="CanCut"/> property.
    /// </para>
    /// </remarks>
    /// <returns>A task that represents the asynchronous cut operation.</returns>
    [RelayCommand(CanExecute = nameof(CanCut))]
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
    /// <remarks>This method performs no action if no copy action is defined. Use the <see cref="CanCopy"/>
    /// property to determine whether copying is currently possible before invoking this method.</remarks>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    [RelayCommand(CanExecute = nameof(CanCopy))]
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
    [RelayCommand(CanExecute = nameof(CanPaste))]
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

    #endregion Clipboard and Edit Commands

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
            _logger.LogWarning("{MethodName} called with undefined {Property}. Initialize this property in MermaidEditorView.WireUpEditorActions.", nameof(CommentSelection), nameof(GetCurrentEditorContextFunc));
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
            _logger.LogWarning("{MethodName} called with undefined {Property}. Initialize this property in MermaidEditorView.WireUpEditorActions.", nameof(UncommentSelection), nameof(GetCurrentEditorContextFunc));
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

    #region Property Change Handlers

    /// <summary>
    /// Handles changes to the Text property by notifying dependent commands that their CanExecute state may have changed.
    /// </summary>
    /// <param name="value">The new text value.</param>
    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Parameter is required for partial method signature.")]
    partial void OnTextChanged(string value)
    {
        // Notify commands that depend on HasText
        OpenFindCommand.NotifyCanExecuteChanged();
        FindNextCommand.NotifyCanExecuteChanged();
        FindPreviousCommand.NotifyCanExecuteChanged();
        CommentSelectionCommand.NotifyCanExecuteChanged();
        UncommentSelectionCommand.NotifyCanExecuteChanged();
    }

    #endregion Property Change Handlers
}
