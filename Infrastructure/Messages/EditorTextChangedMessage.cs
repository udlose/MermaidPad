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

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MermaidPad.Infrastructure.Messages;

/// <summary>
/// Message sent when the editor text content has changed.
/// </summary>
/// <remarks>
/// <para>
/// This message is published by <see cref="ViewModels.UserControls.MermaidEditorViewModel"/> when the
/// underlying <see cref="AvaloniaEdit.Document.TextDocument"/> content changes. It decouples the
/// notification mechanism from the View layer, allowing subscribers like
/// <see cref="ViewModels.MainWindowViewModel"/> to react to text changes without tight coupling.
/// </para>
/// <para>
/// <b>Architecture:</b> This message uses the document-scoped messenger (keyed by <see cref="MessengerKeys.Document"/>)
/// rather than the application-wide messenger. This design prepares for MDI migration where each document
/// has its own messenger instance, preventing cross-document message interference.
/// </para>
/// <para>
/// <b>Base Class:</b> Inherits from <see cref="ValueChangedMessage{T}"/> where T is <see cref="EditorTextChangeInfo"/>.
/// This follows CommunityToolkit.Mvvm conventions and allows the message value to be extended for MDI scenarios
/// (e.g., adding <c>DocumentId</c>) without breaking existing subscribers.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Publishing (in MermaidEditorViewModel.OnDocumentTextChanged):
/// Messenger.Send(new EditorTextChangedMessage(new EditorTextChangeInfo(Document.TextLength)));
///
/// // Subscribing (via ObservableRecipient.OnActivated override):
/// Messenger.Register&lt;DocumentViewModelBase, EditorTextChangedMessage&gt;(
///     this,
///     static (recipient, message) =&gt; recipient.Receive(message));
/// </code>
/// </example>
internal sealed class EditorTextChangedMessage : ValueChangedMessage<EditorTextChangeInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorTextChangedMessage"/> class.
    /// </summary>
    /// <param name="value">The text change information containing the new text length and derived properties.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public EditorTextChangedMessage(EditorTextChangeInfo value) : base(value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}
