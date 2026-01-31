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
/// Message sent when a Mermaid file (.mmd) is dropped onto the <see cref="Views.UserControls.MermaidEditorView"/>.
/// </summary>
/// <remarks>
/// <para>
/// This message enables drag-and-drop file opening when files are dropped specifically onto the
/// TextEditor control. Files dropped on the WebView control are intercepted by the browser's
/// native drag/drop handling and cannot be reliably captured by Avalonia.
/// </para>
/// <para>
/// <b>Flow:</b>
/// <list type="number">
///     <item><description>User drags a .mmd file over the MermaidEditorView (TextEditor control)</description></item>
///     <item><description>MermaidEditorView.OnDragOver() validates the file extension and sets DragDropEffects.Copy</description></item>
///     <item><description>User drops the file on the editor</description></item>
///     <item><description>MermaidEditorView.OnDrop() extracts the file path and sends FileDroppedMessage</description></item>
///     <item><description>MainWindowViewModel.Receive() opens the file via OpenRecentFileCommand</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Architecture:</b> This message uses the document-scoped messenger (keyed by <see cref="MessengerKeys.Document"/>)
/// rather than the application-wide messenger. This design prepares for MDI migration where each document
/// has its own messenger instance.
/// </para>
/// <para>
/// <b>Design Decision:</b> Drag-and-drop is limited to the TextEditor area because the WebView control
/// intercepts drag events at the native browser level before they can reach Avalonia's event system.
/// This is a pragmatic solution that works cross-platform without adding visual tree complexity.
/// </para>
/// </remarks>
internal sealed class FileDroppedMessage : ValueChangedMessage<string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileDroppedMessage"/> class.
    /// </summary>
    /// <param name="filePath">The full path to the dropped .mmd file.</param>
    public FileDroppedMessage(string filePath)
        : base(filePath)
    {
    }
}
