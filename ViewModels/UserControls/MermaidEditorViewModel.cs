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

using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MermaidPad.Infrastructure.Messages;
using MermaidPad.Models.Editor;
using MermaidPad.Services.Editor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.UserControls;

/// <summary>
/// Represents the ViewModel for the MermaidEditorView UserControl, providing properties, commands, and logic
/// for text editing operations including clipboard, undo/redo, find, and commenting functionality.
/// </summary>
/// <remarks>
/// <para>
/// This ViewModel exposes state and command properties for data binding in the editor UserControl, including
/// clipboard actions, selection state, and text manipulation. It coordinates interactions between the user interface
/// and underlying services such as commenting strategy. All properties and commands are designed for use with MVVM
/// frameworks and are intended to be accessed by the View for UI updates and user interactions.
/// </para>
/// <para>
/// <b>Messaging:</b> This ViewModel inherits from <see cref="DocumentViewModelBase"/> which provides access to
/// both application-wide (<see cref="DocumentViewModelBase.AppMessenger"/>) and document-scoped
/// (<see cref="ObservableRecipient.Messenger"/>) messengers. Text change notifications are published via the
/// document-scoped messenger using <see cref="EditorTextChangedMessage"/>.
/// </para>
/// <para>
/// <b>Lifecycle:</b> Call <see cref="ObservableRecipient.IsActive"/> = true after construction to activate
/// message subscriptions. The <see cref="OnActivated"/> override registers for messages, and <see cref="OnDeactivated"/>
/// automatically unregisters via the base class.
/// </para>
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated via ViewModelFactory.")]
internal sealed partial class MermaidEditorViewModel : DocumentViewModelBase, IDisposable
{
    private readonly CommentingStrategy _commentingStrategy;
    private readonly ILogger<MermaidEditorViewModel> _logger;
    private bool _isDisposed;

    #region Tool Visibility

    /// <summary>
    /// Gets or sets a value indicating whether the parent tool (MermaidEditorToolViewModel) is currently visible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is set by <see cref="Docking.MermaidEditorToolViewModel"/> when its
    /// <see cref="Docking.MermaidEditorToolViewModel.IsEditorVisible"/> property changes.
    /// It tracks whether the editor panel is currently visible in the UI, which affects
    /// whether editor commands should be enabled.
    /// </para>
    /// <para>
    /// When the editor panel is pinned (auto-hide) and collapsed, this will be <c>false</c>,
    /// causing all editor commands to report CanExecute = false and disabling the
    /// associated menu items and toolbar buttons.
    /// </para>
    /// <para>
    /// The <c>CanExecuteXxx</c> methods incorporate this value, returning <c>false</c> when the tool
    /// is not visible regardless of the underlying editor state.
    /// </para>
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(PasteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFindCommand))]
    [NotifyCanExecuteChangedFor(nameof(FindNextCommand))]
    [NotifyCanExecuteChangedFor(nameof(FindPreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(CommentSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(UncommentSelectionCommand))]
    public partial bool IsToolVisible { get; set; } = true;

    #endregion Tool Visibility

    #region Edit State and Clipboard Properties

    /// <summary>
    /// Gets the persistent <see cref="TextDocument"/> that holds the editor content and undo history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This document instance is created once and persists across View detach/reattach cycles
    /// (e.g., when the editor panel is pinned/collapsed, floated, or during layout changes).
    /// The <see cref="Views.UserControls.MermaidEditorView"/> attaches this document to the
    /// <c>TextEditor</c> control instead of using the default document created by the control.
    /// </para>
    /// <para>
    /// This design ensures:
    /// <list type="bullet">
    ///     <item><description>Undo/redo history is preserved across view recreation</description></item>
    ///     <item><description>Text content stays synchronized</description></item>
    ///     <item><description>Selection and caret state can be restored accurately</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Thread-safety: <see cref="TextDocument"/> is not thread-safe. This instance is
    /// created and owned by the <see cref="MermaidEditorViewModel"/> and is intended to be
    /// accessed only from the UI thread. Views, services, and commands interacting with this
    /// document must marshal access to the UI thread and must not perform concurrent reads or
    /// writes from multiple threads.
    /// </para>
    /// </remarks>
    public TextDocument Document { get; } = new TextDocument();

    /// <summary>
    /// Gets or sets the current text content in the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property provides a convenient string-based accessor for the document text.
    /// It redirects to <see cref="Document"/>.<see cref="TextDocument.Text"/>.
    /// </para>
    /// <para>
    /// <b>Important:</b> Setting this property replaces the entire document text and
    /// clears the undo history. For editing operations that should be undoable,
    /// modify the <see cref="Document"/> directly.
    /// </para>
    /// <para>
    /// <b>Notification Flow:</b> When text changes (either via this setter or through direct
    /// <see cref="Document"/> manipulation), the <see cref="OnDocumentTextChanged"/> handler
    /// publishes an <see cref="EditorTextChangedMessage"/> via the document-scoped messenger.
    /// Subscribers like <see cref="MainWindowViewModel"/> receive this message for live preview,
    /// dirty tracking, and other text-dependent operations.
    /// </para>
    /// <para>
    /// CRITICAL: Accessing <see cref="TextDocument.Text"/> allocates a new string.
    /// </para>
    /// </remarks>
    public string Text
    {
        get => Document.Text;
        set
        {
            if (Document.Text != value)
            {
                Document.Text = value;
                // Document.TextChanged will also fire, which triggers OnDocumentTextChanged
                // to publish EditorTextChangedMessage for document-scoped subscribers.
            }
        }
    }

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
    /// Gets or sets a value indicating whether there is a cuttable selection in the editor.
    /// </summary>
    /// <remarks>
    /// This property is set by <see cref="Views.UserControls.MermaidEditorView"/> based on the current
    /// editor selection state. The <see cref="CanExecuteCut"/> method combines this with
    /// <see cref="IsToolVisible"/> to determine if the cut command can execute.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    public partial bool HasCuttableSelection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is a copiable selection in the editor.
    /// </summary>
    /// <remarks>
    /// This property is set by <see cref="Views.UserControls.MermaidEditorView"/> based on the current
    /// editor selection state. The <see cref="CanExecuteCopy"/> method combines this with
    /// <see cref="IsToolVisible"/> to determine if the copy command can execute.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    public partial bool HasCopiableSelection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the clipboard has content that can be pasted.
    /// </summary>
    /// <remarks>
    /// This property is set by <see cref="Views.UserControls.MermaidEditorView"/> based on the current
    /// clipboard state. The <see cref="CanExecutePaste"/> method combines this with
    /// <see cref="IsToolVisible"/> to determine if the paste command can execute.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PasteCommand))]
    public partial bool HasClipboardContent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are operations in the undo history.
    /// </summary>
    /// <remarks>
    /// This property is updated automatically by <see cref="OnUndoStackPropertyChanged"/> when the
    /// <see cref="Document"/>.<see cref="TextDocument.UndoStack"/> state changes.
    /// The <see cref="CanExecuteUndo"/> method combines this with <see cref="IsToolVisible"/>
    /// to determine if the undo command can execute.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    public partial bool HasUndoHistory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are operations in the redo history.
    /// </summary>
    /// <remarks>
    /// This property is updated automatically by <see cref="OnUndoStackPropertyChanged"/> when the
    /// <see cref="Document"/>.<see cref="TextDocument.UndoStack"/> state changes.
    /// The <see cref="CanExecuteRedo"/> method combines this with <see cref="IsToolVisible"/>
    /// to determine if the redo command can execute.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    public partial bool HasRedoHistory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is content that can be selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is updated automatically by <see cref="OnDocumentTextChanged"/> when the
    /// document text changes. The <see cref="CanExecuteSelectAll"/> method combines this with
    /// <see cref="IsToolVisible"/> to determine if the select all command can execute.
    /// </para>
    /// <para>
    /// This property is semantically equivalent to <see cref="HasText"/> (both are true when
    /// <c>Document.TextLength &gt; 0</c>). The <c>[NotifyCanExecuteChangedFor]</c> attributes
    /// consolidate all text-presence-dependent command notifications here, eliminating redundant
    /// manual <c>NotifyCanExecuteChanged()</c> calls in <see cref="OnDocumentTextChanged"/>.
    /// </para>
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFindCommand))]
    [NotifyCanExecuteChangedFor(nameof(FindNextCommand))]
    [NotifyCanExecuteChangedFor(nameof(FindPreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(CommentSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(UncommentSelectionCommand))]
    public partial bool HasSelectableContent { get; set; }

    #endregion Edit State and Clipboard Properties

    #region Computed Properties

    /// <summary>
    /// Gets a value indicating whether the current text is not null or empty.
    /// </summary>
    public bool HasText => !string.IsNullOrEmpty(Text);

    #endregion Computed Properties

    #region CanExecute Methods

    /// <summary>
    /// Determines whether the cut command can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and there is a cuttable selection; otherwise, <c>false</c>.</returns>
    private bool CanExecuteCut() => IsToolVisible && HasCuttableSelection;

    /// <summary>
    /// Determines whether the copy command can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and there is a copiable selection; otherwise, <c>false</c>.</returns>
    private bool CanExecuteCopy() => IsToolVisible && HasCopiableSelection;

    /// <summary>
    /// Determines whether the paste command can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and the clipboard has content; otherwise, <c>false</c>.</returns>
    private bool CanExecutePaste() => IsToolVisible && HasClipboardContent;

    /// <summary>
    /// Determines whether the undo command can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and there is undo history; otherwise, <c>false</c>.</returns>
    private bool CanExecuteUndo() => IsToolVisible && HasUndoHistory;

    /// <summary>
    /// Determines whether the redo command can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and there is redo history; otherwise, <c>false</c>.</returns>
    private bool CanExecuteRedo() => IsToolVisible && HasRedoHistory;

    /// <summary>
    /// Determines whether the select all command can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and there is selectable content; otherwise, <c>false</c>.</returns>
    private bool CanExecuteSelectAll() => IsToolVisible && HasSelectableContent;

    /// <summary>
    /// Determines whether find-related commands (OpenFind, FindNext, FindPrevious) can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and there is text to search within; otherwise, <c>false</c>.</returns>
    private bool CanExecuteFind() => IsToolVisible && HasText;

    /// <summary>
    /// Determines whether comment-related commands (CommentSelection, UncommentSelection) can execute.
    /// </summary>
    /// <returns><c>true</c> if the editor tool is visible and there is text to comment; otherwise, <c>false</c>.</returns>
    private bool CanExecuteComment() => IsToolVisible && HasText;

    #endregion CanExecute Methods

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
    /// <param name="appMessenger">The application-wide messenger for cross-document communication.</param>
    /// <param name="documentMessenger">The document-scoped messenger for this editor's messages.</param>
    /// <param name="commentingStrategy">The commenting strategy service for comment/uncomment operations.</param>
    /// <param name="logger">The logger instance for this view model.</param>
    /// <remarks>
    /// <para>
    /// The messengers are injected via keyed DI services:
    /// <list type="bullet">
    ///     <item><description><paramref name="appMessenger"/> - keyed by <see cref="MessengerKeys.App"/></description></item>
    ///     <item><description><paramref name="documentMessenger"/> - keyed by <see cref="MessengerKeys.Document"/></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// After construction, set <see cref="ObservableRecipient.IsActive"/> = true to activate message
    /// subscriptions. This is typically done by the owning <see cref="Docking.MermaidEditorToolViewModel"/>.
    /// </para>
    /// </remarks>
    public MermaidEditorViewModel(
        [FromKeyedServices(MessengerKeys.App)] IMessenger appMessenger,
        [FromKeyedServices(MessengerKeys.Document)] IMessenger documentMessenger,
        CommentingStrategy commentingStrategy,
        ILogger<MermaidEditorViewModel> logger)
        : base(appMessenger, documentMessenger)
    {
        _commentingStrategy = commentingStrategy;
        _logger = logger;

        // Subscribe to UndoStack property changes for automatic undo/redo state updates.
        Document.UndoStack.PropertyChanged += OnUndoStackPropertyChanged;

        // Subscribe to Document.TextChanged to publish EditorTextChangedMessage.
        // This decouples the notification from the View layer - the ViewModel handles
        // Document events directly and broadcasts changes via the document-scoped messenger.
        Document.TextChanged += OnDocumentTextChanged;
    }

    #region ObservableRecipient Overrides

    /// <summary>
    /// Called when the recipient is activated to register message handlers.
    /// </summary>
    /// <remarks>
    /// Override this method to register for messages via <see cref="ObservableRecipient.Messenger"/>.
    /// Currently, this ViewModel publishes messages but doesn't receive any.
    /// Future MDI scenarios may require receiving messages from other documents.
    /// </remarks>
    protected override void OnActivated()
    {
        base.OnActivated();

        // Currently this ViewModel only publishes EditorTextChangedMessage.
        // Add message registrations here if this ViewModel needs to receive messages.
        _logger.LogDebug("{ViewModelName} activated", nameof(MermaidEditorViewModel));
    }

    /// <summary>
    /// Called when the recipient is deactivated to unregister message handlers.
    /// </summary>
    /// <remarks>
    /// The base class automatically unregisters from <see cref="ObservableRecipient.Messenger"/>
    /// and <see cref="DocumentViewModelBase.AppMessenger"/>.
    /// </remarks>
    protected override void OnDeactivated()
    {
        _logger.LogDebug("{ViewModelName} deactivated", nameof(MermaidEditorViewModel));
        base.OnDeactivated();
    }

    #endregion ObservableRecipient Overrides

    #region Document Event Handlers

    /// <summary>
    /// Handles property changes from the <see cref="AvaloniaEdit.Document.UndoStack"/>.
    /// </summary>
    /// <remarks>
    /// Updates <see cref="HasUndoHistory"/> and <see cref="HasRedoHistory"/> when the undo stack changes.
    /// The <c>[NotifyCanExecuteChangedFor]</c> attributes on those properties automatically update command states.
    /// </remarks>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The property changed event arguments.</param>
    private void OnUndoStackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Document.UndoStack.CanUndo):
                HasUndoHistory = Document.UndoStack.CanUndo;
                break;

            case nameof(Document.UndoStack.CanRedo):
                HasRedoHistory = Document.UndoStack.CanRedo;
                break;
        }
    }

    /// <summary>
    /// Handles the <see cref="TextDocument.TextChanged"/> event from the Document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This handler publishes an <see cref="EditorTextChangedMessage"/> via the document-scoped
    /// <see cref="ObservableRecipient.Messenger"/> to notify interested parties (like <see cref="MainWindowViewModel"/>)
    /// that the document content changed.
    /// </para>
    /// <para>
    /// This approach decouples the notification mechanism from the View layer. The ViewModel handles
    /// the Document event directly and broadcasts the change via messaging, eliminating the need for
    /// the View to call any notification methods on the ViewModel.
    /// </para>
    /// <para>
    /// The handler updates <see cref="HasSelectableContent"/>, which has <c>[NotifyCanExecuteChangedFor]</c>
    /// attributes for all text-presence-dependent commands (SelectAll, Find, Comment). This consolidates
    /// command notifications and eliminates redundant manual <c>NotifyCanExecuteChanged()</c> calls.
    /// </para>
    /// </remarks>
    /// <param name="sender">The source of the event (the TextDocument).</param>
    /// <param name="e">The event arguments.</param>
    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        // Update HasSelectableContent based on document having text.
        // The [NotifyCanExecuteChangedFor] attributes on this property automatically notify:
        // SelectAllCommand, OpenFindCommand, FindNextCommand, FindPreviousCommand,
        // CommentSelectionCommand, and UncommentSelectionCommand.
        HasSelectableContent = Document.TextLength > 0;

        // Publish the message for decoupled subscribers (e.g., MainWindowViewModel for live preview, dirty tracking).
        // Uses the document-scoped Messenger (inherited from DocumentViewModelBase via ObservableRecipient).
        Messenger.Send(new EditorTextChangedMessage(new EditorTextChangeInfo(Document)));
    }

    #endregion Document Event Handlers

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
    /// is determined by the <see cref="CanExecuteCut"/> method.
    /// </para>
    /// </remarks>
    /// <returns>A task that represents the asynchronous cut operation.</returns>
    [RelayCommand(CanExecute = nameof(CanExecuteCut))]
    private async Task CutAsync()
    {
        if (CutAction is not null)
        {
            await CutAction();
        }
    }

    /// <summary>
    /// Executes the copy operation asynchronously if a copy action is defined.
    /// </summary>
    /// <remarks>This method is intended to be used as a command handler and will only perform the copy action
    /// if one has been assigned. The method is typically invoked through UI command binding using the RelayCommand
    /// attribute.</remarks>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    [RelayCommand(CanExecute = nameof(CanExecuteCopy))]
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
    [RelayCommand(CanExecute = nameof(CanExecutePaste))]
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
    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
    private void Undo() => UndoAction?.Invoke();

    /// <summary>
    /// Redoes the last undone edit operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteRedo))]
    private void Redo() => RedoAction?.Invoke();

    /// <summary>
    /// Selects all text in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteSelectAll))]
    private void SelectAll() => SelectAllAction?.Invoke();

    /// <summary>
    /// Opens the find panel in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFind))]
    private void OpenFind() => OpenFindAction?.Invoke();

    /// <summary>
    /// Finds the next match in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFind))]
    private void FindNext() => FindNextAction?.Invoke();

    /// <summary>
    /// Finds the previous match in the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFind))]
    private void FindPrevious() => FindPreviousAction?.Invoke();

    #endregion Clipboard and Edit Commands

    #region Comment/Uncomment Selection Commands

    /// <summary>
    /// Comments the currently selected text in the editor, if a selection is present and commenting is allowed.
    /// It uses the commenting strategy defined for the editor defined in <see cref="CommentingStrategy"/>.
    /// </summary>
    /// <remarks>
    /// The editor context is retrieved on-demand via <see cref="GetCurrentEditorContextFunc"/> to ensure
    /// the most current selection state is used. This command is enabled only when the editor tool is visible
    /// and there is text in the editor.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanExecuteComment))]
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
    /// the most current selection state is used. This command is enabled only when the editor tool is visible
    /// and there is text in the editor.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanExecuteComment))]
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

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="MermaidEditorViewModel"/> and unsubscribes from event handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In typical usage, the lifetime of a <see cref="MermaidEditorViewModel"/> instance is managed by its owner,
    /// such as the <see cref="Docking.MermaidEditorToolViewModel"/> or the dependency injection container, which
    /// is responsible for calling its <see cref="IDisposable.Dispose"/> implementation when the object is no longer needed.
    /// Callers that explicitly manage the lifetime of a <see cref="MermaidEditorViewModel"/> may call this method
    /// directly when they are finished with the instance.
    /// </para>
    /// <para>
    /// This method:
    /// <list type="bullet">
    ///     <item><description>Deactivates the ViewModel (unregisters from all messengers via <see cref="OnDeactivated"/>)</description></item>
    ///     <item><description>Unsubscribes from <see cref="TextDocument.TextChanged"/> event</description></item>
    ///     <item><description>Unsubscribes from <see cref="AvaloniaEdit.Document.UndoStack.PropertyChanged"/> event</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            // Deactivate to unregister from all messengers (handled by base class OnDeactivated)
            IsActive = false;

            // Unsubscribe from Document events to prevent memory leaks
            Document.TextChanged -= OnDocumentTextChanged;
            Document.UndoStack.PropertyChanged -= OnUndoStackPropertyChanged;

            _isDisposed = true;
        }
    }

    #endregion IDisposable
}
