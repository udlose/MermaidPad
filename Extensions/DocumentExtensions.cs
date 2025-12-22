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

namespace MermaidPad.Extensions;

/// <summary>
/// Provides extension methods for performing update operations on a TextDocument, including safely ending updates and
/// reverting changes if an operation fails.
/// </summary>
/// <remarks>These extension methods help ensure that document updates are properly finalized and that the
/// document remains in a consistent, usable state. They are intended to be used in scenarios where update operations
/// may partially succeed or fail, and require explicit handling to avoid leaving the document locked or in an
/// inconsistent state.</remarks>
internal static class TextDocumentExtensions
{
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
    /// <param name="document">The document to end the update on.</param>
    /// <param name="isSuccess">A value indicating whether the update operation completed successfully.
    /// If <see langword="false"/>, the changes are undone.</param>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="TextDocument"/> is null.</exception>
    public static void EndUpdateAndUndoIfFailed(this TextDocument document, bool isSuccess)
    {
        if (document is null)
        {
            throw new InvalidOperationException($"Unable to end update: {nameof(document)} is null.");
        }

        // Only end the update if we are actually in update mode
        if (document.IsInUpdate)
        {
            // NOTE: this is not like a transaction. If the Editor is in the process of updating,
            // EndUpdate must be called even if an error occurred.
            // There isn't a Cancel/Rollback/UndoUpdate. Without calling EndUpdate, the editor stays locked!
            document.EndUpdate();
        }

        // If it failed, immediately undo the partial changes to revert to original state
        if (!isSuccess)
        {
            // There is no need to restore caret/selection/etc. here because the undo operation will do that.
            document.UndoStack.Undo();
        }
    }
}
