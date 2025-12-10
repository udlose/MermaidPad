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

using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System.Diagnostics;

namespace MermaidPad.Models.Editor;

/// <summary>
/// Represents a single completion item for use in IntelliSense code completion, providing display text, description,
/// and priority information.
/// </summary>
/// <remarks>This class is typically used to supply completion suggestions in text editors or IDEs that support
/// IntelliSense functionality. Each instance encapsulates the text to insert, a description, and an optional priority
/// value to influence suggestion ordering. Instances are immutable and thread-safe.</remarks>
public sealed class IntellisenseCompletionData : ICompletionData
{
    /// <summary>
    /// Gets the text content associated with this instance.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the content associated with the current instance and prepends a space for formatting.
    /// </summary>
    public object Content => " " + Text;

    /// <summary>
    /// Gets the description of the node.
    /// </summary>
    public object Description => "Keyword/Defined Node";

    /// <summary>
    /// Gets the priority value associated with this instance.
    /// </summary>
    public double Priority { get; }

    /// <summary>
    /// Gets the image associated with this instance, if available.
    /// </summary>
    public IImage? Image { get; }

    /// <summary>
    /// Initializes a new instance of the IntellisenseCompletionData class with the specified completion text and
    /// priority.
    /// </summary>
    /// <param name="text">The text to display for the completion suggestion. Cannot be null.</param>
    /// <param name="priority">The priority value used to rank this completion suggestion. Higher values indicate higher priority. The default
    /// is 0.</param>
    /// <param name="image">The image associated with this completion suggestion. Can be null.</param>
    public IntellisenseCompletionData(string text, double priority = 0, IImage? image = null)
    {
        Text = text;
        Priority = priority;
        Image = image;
    }

    /// <summary>
    /// Inserts the completion text into the specified text area, replacing the content within the given segment.
    /// </summary>
    /// <param name="textArea">The text area where the completion text will be inserted. Cannot be null.</param>
    /// <param name="completionSegment">The segment of text in the document to be replaced by the completion text. Must be a valid segment within the
    /// document.</param>
    /// <param name="insertionRequestEventArgs">The event arguments associated with the insertion request. Provides context for the completion operation.</param>
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        Debug.Assert(textArea is not null, "textArea cannot be null");

        // if TextArea is null, just return without failure
        textArea?.Document.Replace(completionSegment, Text);
    }
}
