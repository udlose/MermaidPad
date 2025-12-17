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
using MermaidPad.Models.Constants;
using MermaidPad.Models.Editor;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Services.Editor;

/// <summary>
/// Provides functionality to comment and uncomment selected lines in a Mermaid editor by inserting or removing
/// context-appropriate line comment tokens.
/// </summary>
/// <remarks>
/// <para>
/// This strategy is context-aware and uses different comment tokens based on the document structure:
/// <list type="bullet">
/// <item><description>YAML frontmatter content: Uses <c>#</c> comment token</description></item>
/// <item><description>Mermaid diagram content: Uses <c>%%</c> comment token</description></item>
/// <item><description>Frontmatter delimiters (<c>---</c>): Never commented (always skipped)</description></item>
/// <item><description>Blank lines: Always skipped (no comment added)</description></item>
/// </list>
/// </para>
/// <para>
/// Comment layering behavior:
/// <list type="bullet">
/// <item><description>Commenting: Adds one layer at a time, even to already-commented lines</description></item>
/// <item><description>Uncommenting: Removes one layer at a time, handling multiple comment layers gracefully</description></item>
/// </list>
/// </para>
/// <para>
/// All operations are performed as single undoable actions to maintain a consistent editing experience.
/// If an error occurs during commenting or uncommenting, changes are automatically rolled back.
/// </para>
/// </remarks>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance-sensitive code should avoid reflection which pattern matching relies on.")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance-sensitive code should avoid reflection which pattern matching relies on.")]
internal sealed class CommentingStrategy
{
    private readonly DocumentAnalyzer _documentAnalyzer;
    private readonly ILogger<CommentingStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentingStrategy"/> class with the specified dependencies.
    /// </summary>
    /// <param name="documentAnalyzer">The document analyzer for determining line context (frontmatter vs. diagram). Cannot be null.</param>
    /// <param name="logger">The logger to use for recording diagnostic and operational messages. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="documentAnalyzer"/> or <paramref name="logger"/> is null.</exception>
    public CommentingStrategy(DocumentAnalyzer documentAnalyzer, ILogger<CommentingStrategy> logger)
    {
        ArgumentNullException.ThrowIfNull(documentAnalyzer);
        ArgumentNullException.ThrowIfNull(logger);

        _documentAnalyzer = documentAnalyzer;
        _logger = logger;
    }

    /// <summary>
    /// Comments out all non-blank, non-delimiter lines within the current selection by inserting context-appropriate
    /// comment tokens at the start of each line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Commenting rules:
    /// <list type="bullet">
    /// <item><description>Frontmatter content lines: Insert <c>#</c> at start of line</description></item>
    /// <item><description>Diagram content lines: Insert <c>%%</c> at start of line</description></item>
    /// <item><description>Frontmatter delimiters (<c>---</c>): Skipped (never commented)</description></item>
    /// <item><description>Blank lines (whitespace only): Skipped (not commented)</description></item>
    /// <item><description>Already-commented lines: Add another comment layer (one layer at a time)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The operation is performed as a single undoable action. If an error occurs, all changes are rolled back
    /// to maintain document consistency.
    /// </para>
    /// </remarks>
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
            int startLine = document.GetLineByOffset(selectionStart).LineNumber;
            int endLine = document.GetLineByOffset(selectionStart + selectionLength).LineNumber;

            // Begin document update to batch changes together so that only one undo step is created
            document.BeginUpdate();

            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                DocumentLine line = document.GetLineByNumber(lineNumber);

                // Determine the document context for this line
                DocumentContext context = _documentAnalyzer.DetermineLineContext(document, lineNumber);

                // Skip frontmatter delimiter lines - they must never be commented
                if (context == DocumentContext.FrontmatterStart || context == DocumentContext.FrontmatterEnd)
                {
                    continue;
                }

                // Skip blank lines (whitespace only)
                if (DocumentAnalyzer.IsLineBlank(document, line))
                {
                    continue;
                }

                // Determine which comment token to use based on context
                string commentToken = context == DocumentContext.Frontmatter
                    ? CommentTokens.Frontmatter
                    : CommentTokens.Diagram;

                // Insert the comment token at the start of the line
                // This adds one layer at a time, even if the line is already commented
                document.Insert(line.Offset, commentToken);
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
    /// Removes one layer of line comment tokens from the currently selected lines in the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uncommenting rules:
    /// <list type="bullet">
    /// <item><description>Removes the first valid comment token found at the start of each line (after whitespace)</description></item>
    /// <item><description>Valid comment tokens: <c>%%</c> (two characters) or <c>#</c> (one character)</description></item>
    /// <item><description>Invalid tokens (e.g., single <c>%</c>) are not removed</description></item>
    /// <item><description>Removes only ONE layer at a time: <c>%%%%</c> becomes <c>%%</c>, <c>###</c> becomes <c>##</c></description></item>
    /// <item><description>Permissive: Removes whichever valid token is found, regardless of document context</description></item>
    /// <item><description>Lines without valid comment tokens are skipped unchanged</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The operation is performed as a single undoable action. If an error occurs, all changes are rolled back
    /// to maintain document consistency.
    /// </para>
    /// </remarks>
    /// <param name="editorContext">The editor context containing the document and selection information. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="editorContext"/> is null.</exception>
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
            int startLine = document.GetLineByOffset(selectionStart).LineNumber;
            int endLine = document.GetLineByOffset(selectionStart + selectionLength).LineNumber;

            // Begin document update to batch changes together so that only one undo step is created
            document.BeginUpdate();

            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                DocumentLine line = document.GetLineByNumber(lineNumber);
                ReadOnlySpan<char> lineSpan = document.GetText(line).AsSpan();
                ReadOnlySpan<char> trimmedLine = lineSpan.TrimStart();

                // Try to remove %% comment token first (two characters)
                // This handles diagram comments and is checked first because it's more specific
                if (trimmedLine.StartsWith(CommentTokens.Diagram))
                {
                    // Find the position of %% in the original line (after leading whitespace)
                    int commentIndex = lineSpan.Length - trimmedLine.Length;
                    document.Remove(line.Offset + commentIndex, CommentTokens.Diagram.Length);
                    continue;
                }

                // Try to remove # comment token (one character). This handles frontmatter comments
                if (trimmedLine.StartsWith(CommentTokens.Frontmatter))
                {
                    // Find the position of # in the original line (after leading whitespace)
                    int commentIndex = lineSpan.Length - trimmedLine.Length;
                    document.Remove(line.Offset + commentIndex, CommentTokens.Frontmatter.Length);
                }

                // No valid comment token found - skip this line
                // Single % or other content is not considered a valid comment
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
    /// <param name="document">The text document on which to end the update and, if necessary, perform an undo operation. Cannot be null.</param>
    /// <param name="success">A value indicating whether the update operation completed successfully.
    /// If <see langword="false"/>, the changes are undone.</param>
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
