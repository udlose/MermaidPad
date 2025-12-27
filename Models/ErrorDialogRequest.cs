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

namespace MermaidPad.Models;

/// <summary>
/// Represents a request to show an error dialog in the UI.
/// </summary>
/// <remarks>
/// This model is used to centralize information required when presenting
/// error dialogs to the user. It carries an <see cref="Exception"/> instance
/// (if available) and an optional user-facing message that can be displayed
/// directly in the UI. Both values are intentionally allowed to be <c>null</c>
/// to provide flexibility for different error-reporting scenarios.
/// </remarks>
internal sealed class ErrorDialogRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorDialogRequest"/> class.
    /// </summary>
    /// <param name="exception">
    /// The exception that caused the error, or <c>null</c> if there is no exception
    /// object to provide. This may be used to show detailed information or to
    /// perform logging.
    /// </param>
    /// <param name="userMessage">
    /// A short, user-friendly message describing the error, or <c>null</c> if no
    /// user-facing message is provided. The consumer should handle a <c>null</c>
    /// value appropriately (for example, by showing a generic message).
    /// </param>
    public ErrorDialogRequest(Exception? exception, string? userMessage)
    {
        // Since this is used for error reporting, we allow null exception and userMessage
        // to provide flexibility in reporting different kinds of errors.
        // So we do not perform null checks here.
        Exception = exception;
        UserMessage = userMessage;
    }

    /// <summary>
    /// Gets the exception associated with the error, if any.
    /// </summary>
    /// <value>
    /// An <see cref="Exception"/> instance that caused the error, or <c>null</c>.
    /// </value>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the user-facing message describing the error, if provided.
    /// </summary>
    /// <value>
    /// A string intended for display to the user, or <c>null</c> when no specific
    /// message is available.
    /// </value>
    public string? UserMessage { get; }
}
