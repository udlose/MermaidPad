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

using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace MermaidPad.Models.Editor;

/// <summary>
/// Represents the context of a <see cref="TextEditor"/>, including the associated text document and the current selection range.
/// </summary>
/// <remarks>The selection is defined by the combination of SelectionStart and SelectionLength. If SelectionLength
/// is 0, there is no active selection and the caret is at SelectionStart.</remarks>
/// <param name="Document">The text document being edited. Can be null.</param>
/// <param name="SelectionStart">The zero-based index of the first character in the current selection within the document.
/// Must be greater than or equal to 0.</param>
/// <param name="SelectionLength">The number of characters selected in the document. Must be greater than or equal to 0.</param>
/// <param name="CaretOffset">The zero-based index of the caret position within the document. Must be greater than or equal to 0.</param>
public sealed record EditorContext(
    TextDocument? Document,
    int SelectionStart,
    int SelectionLength,
    int CaretOffset)
{
    /// <summary>
    /// Gets a value indicating whether the current object is in a valid state.
    /// </summary>
    /// <remarks>This lets the converter invalidate old contexts without nulls.</remarks>
    public required bool IsValid { get; init; } = true;

    /// <summary>
    /// Gets the currently selected text.
    /// </summary>
    /// <remarks>This property is purposely not passed into the constructor because
    /// of the potential race condition; in AvaloniaEdit, these properties are not guaranteed to update atomically.
    /// So we derive this from the <see cref="Document"/>, <see cref="SelectionStart"/>, and <see cref="SelectionLength"/>.</remarks>
    public string SelectedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether there is an active selection.
    /// </summary>
    public bool HasSelection => SelectionLength > 0;

    /// <summary>
    /// Gets the starting line number of the selection (1-based).
    /// </summary>
    public int StartLine => Document?.GetLineByOffset(SelectionStart).LineNumber ?? 0;

    /// <summary>
    /// Gets the ending line number of the selection (1-based).
    /// </summary>
    public int EndLine => Document?.GetLineByOffset(SelectionStart + SelectionLength).LineNumber ?? 0;

    /// <summary>
    /// Ends the update operation on the specified document and undoes changes if the operation did not succeed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CRITICAL: This method must be called after every update operation, regardless of success or failure.
    /// Failing to call this method may leave the document in a locked state.
    /// </para>
    /// <para>
    /// The document update mechanism is not a traditional transaction - <see cref="TextDocument.EndUpdate"/>
    /// must be called even if an error occurs. There is no Cancel/Rollback/UndoUpdate method.
    /// Without calling <see cref="TextDocument.EndUpdate"/>, the editor remains locked and unusable.
    /// </para>
    /// <para>
    /// If the operation was not successful, this method performs an immediate undo operation to revert
    /// any partial changes, ensuring the document returns to its original state.
    /// </para>
    /// </remarks>
    /// <param name="isSuccess">A value indicating whether the update operation completed successfully.
    /// If <see langword="false"/>, the changes are undone.</param>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="Document"/> is null.</exception>
    public void EndUpdateAndUndoIfFailed(bool isSuccess)
    {
        if (Document is null)
        {
            throw new InvalidOperationException($"Unable to end update: {nameof(Document)} is null.");
        }

        // Only end the update if we are actually in update mode
        if (Document.IsInUpdate)
        {
            // NOTE: this is not like a transaction. If the Editor is in the process of updating,
            // EndUpdate must be called even if an error occurred.
            // There isn't a Cancel/Rollback/UndoUpdate. Without calling EndUpdate, the editor stays locked!
            Document.EndUpdate();
        }

        // If it failed, immediately undo the partial changes to revert to original state
        if (!isSuccess)
        {
            // There is no need to restore caret/selection/etc. here because the undo operation will do that.
            Document.UndoStack.Undo();
        }
    }
}
