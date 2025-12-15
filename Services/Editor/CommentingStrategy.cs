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
using MermaidPad.Models.Editor;
using Microsoft.Extensions.Logging;

namespace MermaidPad.Services.Editor;

/// <summary>
/// Provides functionality to comment and uncomment selected lines in an editor by inserting or removing line comment
/// tokens.
/// </summary>
/// <remarks>This class is intended for use within editor extensions or tools that require batch commenting or
/// uncommenting of code or text selections. All operations are performed as single undoable actions to maintain a
/// consistent editing experience. Diagnostic and operational messages are logged using the provided logger.</remarks>
internal sealed class CommentingStrategy
{
    private readonly ILogger<CommentingStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the CommentingStrategy class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger to use for recording diagnostic and operational messages
    /// related to the CommentingStrategy.</param>
    public CommentingStrategy(ILogger<CommentingStrategy> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Comments out all non-blank lines within the current selection in the editor by inserting a comment token at the
    /// start of each line.
    /// </summary>
    /// <remarks>Blank lines or lines containing only whitespace are not commented. The operation is performed
    /// as a single undoable action in the editor. If an error occurs, the changes are rolled back.</remarks>
    /// <param name="editorContext">The editor context containing the document and selection information. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="editorContext"/> is null.</exception>
    public void CommentSelection(EditorContext editorContext)
    {
        ArgumentNullException.ThrowIfNull(editorContext);
        TextDocument? document = editorContext.Document;
        if (document is null)
        {
            _logger.LogWarning("{MethodName} called with null document", nameof(CommentSelection));
            return;
        }

        int selectionStart = editorContext.SelectionStart;
        int selectionLength = editorContext.SelectionLength;
        bool success = false;
        try
        {
            //TODO - DaveBlack: add support for determining line location relative in diagram syntax (use "%%") or frontmatter (use "#")
            const string commentToken = "%%";
            int startLine = document.GetLineByOffset(selectionStart).LineNumber;
            int endLine = document.GetLineByOffset(selectionStart + selectionLength).LineNumber;

            // Begin document update to batch changes together so that only one undo step is created
            document.BeginUpdate();

            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                DocumentLine line = document.GetLineByNumber(lineNumber);

                // Skip blank lines (TotalLength includes line ending so use Length)
                // Blank lines are defined as those that are empty or contain only whitespace
                // Check if line is not just whitespace. Avoid calling GetText on the line to reduce allocations.
                if (line.Length > 0 && TextUtilities.GetLeadingWhitespace(document, line).Length != line.Length)
                {
                    document.Insert(line.Offset, commentToken);
                }
            }

            success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to comment selection");
        }
        finally
        {
            EndUpdateAndUndoIfFailed(document, success);
        }
    }

    /// <summary>
    /// Removes line comment tokens from the currently selected lines in the editor, effectively uncommenting them.
    /// </summary>
    /// <remarks>Only lines that begin with the comment token are affected. The operation is performed as a
    /// single undoable action. If an error occurs during the process, the changes are reverted.</remarks>
    public void UncommentSelection(EditorContext editorContext)
    {
        ArgumentNullException.ThrowIfNull(editorContext);
        TextDocument? document = editorContext.Document;
        if (document is null)
        {
            _logger.LogWarning("{MethodName} called with null document", nameof(UncommentSelection));
            return;
        }

        int selectionStart = editorContext.SelectionStart;
        int selectionLength = editorContext.SelectionLength;
        bool success = false;
        try
        {
            //TODO - DaveBlack: add support for determining line location relative in diagram syntax (use "%%") or frontmatter (use "#")
            const string commentToken = "%%";
            int startLine = document.GetLineByOffset(selectionStart).LineNumber;
            int endLine = document.GetLineByOffset(selectionStart + selectionLength).LineNumber;

            // Begin document update to batch changes together so that only one undo step is created
            document.BeginUpdate();

            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                DocumentLine line = document.GetLineByNumber(lineNumber);
                ReadOnlySpan<char> lineSpan = document.GetText(line).AsSpan();
                if (lineSpan.TrimStart().StartsWith(commentToken))
                {
                    int commentIndex = lineSpan.IndexOf(commentToken, StringComparison.Ordinal);
                    document.Remove(line.Offset + commentIndex, commentToken.Length);
                }
            }

            success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uncomment selection");
        }
        finally
        {
            EndUpdateAndUndoIfFailed(document, success);
        }
    }

    /// <summary>
    /// Ends the update operation on the specified document and undoes changes if the operation did not succeed.
    /// </summary>
    /// <remarks>This method must be called after every update operation, regardless of success or failure.
    /// Failing to call this method may leave the document in a locked state. If the operation was not successful, any
    /// partial changes are immediately reverted.</remarks>
    /// <param name="document">The text document on which to end the update and, if necessary, perform an
    /// undo operation. Cannot be null.</param>
    /// <param name="success">A value indicating whether the update operation completed successfully.
    /// If <see langword="false"/>, the changes
    /// are undone.</param>
    private static void EndUpdateAndUndoIfFailed(TextDocument document, bool success)
    {
        // NOTE: this is not like a transaction - it must be called even if an error occurs.
        // There isn't a Cancel/Rollback/UndoUpdate. Without calling EndUpdate, the editor stays locked!
        document.EndUpdate();

        // If it failed, immediately undo the partial changes to revert to original state
        if (!success)
        {
            document.UndoStack.Undo();
        }
    }
}
