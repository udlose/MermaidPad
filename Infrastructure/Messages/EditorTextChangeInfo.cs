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

namespace MermaidPad.Infrastructure.Messages;

/// <summary>
/// Represents information about an editor text change event.
/// </summary>
/// <remarks>
/// <para>
/// This record serves as the type for <see cref="EditorTextChangedMessage"/> and encapsulates
/// the relevant state after a text change occurs in the editor.
/// </para>
/// <para>
/// <b>MDI Migration Note:</b> When migrating to MDI, extend this record with additional properties
/// such as <c>DocumentId</c> to identify which document's text changed. Using a record allows
/// easy extension via inheritance while maintaining immutability.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Current SDI usage:
/// var info = new EditorTextChangeInfo(document.TextLength);
///
/// // Future MDI extension:
/// public sealed record MdiEditorTextChangeInfo(int TextLength, Guid DocumentId)
///     : EditorTextChangeInfo(TextLength);
/// </code>
/// </example>
internal sealed record class EditorTextChangeInfo
{
    /// <summary>
    /// Reference to the TextDocument that changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Important:</b> This is a reference to the live document. The document state
    /// may have changed since this message was published.
    /// </para>
    /// <para>
    /// <b>Performance Note:</b> Accessing <see cref="TextDocument.Text"/> allocates a new string.
    /// Use <see cref="TextDocument.TextLength"/> for length checks to avoid allocations.
    /// </para>
    /// </remarks>
    internal TextDocument Document { get; init; }

    /// <summary>
    /// Gets the total number of characters in the document text.
    /// </summary>
    internal int TextLength => Document.TextLength;

    /// <summary>
    /// Gets a value indicating whether the document has any text content.
    /// </summary>
    /// <remarks>
    /// Convenience property equivalent to <c>TextLength &gt; 0</c>.
    /// Useful for updating command states without string allocation.
    /// </remarks>
    internal bool HasText => Document.TextLength > 0;

    /// <summary>
    /// Initializes a new instance of the EditorTextChangeInfo class for the specified text document.
    /// </summary>
    /// <param name="textDocument">The text document associated with the text change information. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="textDocument"/> is null.</exception>
    internal EditorTextChangeInfo(TextDocument textDocument)
    {
        ArgumentNullException.ThrowIfNull(textDocument);
        Document = textDocument;
    }
}
