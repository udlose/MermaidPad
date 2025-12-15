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
}
