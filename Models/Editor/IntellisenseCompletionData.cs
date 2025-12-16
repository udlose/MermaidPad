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

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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
    /// Represents the ABC icon displayed in the CompletionWindow as a static <see cref="RenderTargetBitmap"/> resource.
    /// </summary>
    internal static readonly RenderTargetBitmap? AbcIcon = CreateAbcIconBitmap();

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
    /// Inserts the completion text into the specified text area, replacing the content within the given segment,
    /// and appends a trailing space if appropriate for convenient continued typing.
    /// </summary>
    /// <param name="textArea">The text area where the completion text will be inserted. Cannot be null.</param>
    /// <param name="completionSegment">The segment of text in the document to be replaced by the completion text. Must be a valid segment within the
    /// document.</param>
    /// <param name="insertionRequestEventArgs">The event arguments associated with the insertion request. Provides context for the completion operation.</param>
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract",
        Justification = "TextEditor API doesn't enable nullable; so using defensive programming for null checks")]
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        if (textArea is null || completionSegment is null)
        {
            Debug.Fail("textArea or completionSegment cannot be null");
            return;
        }

        const char singleSpaceChar = ' ';
        textArea.Document.Replace(completionSegment, Text);

        // Calculate where the caret will be after insertion
        int newCaretOffset = completionSegment.Offset + Text.Length;

        // Check if we should insert a space after the completion
        bool shouldInsertSpace = true;

        if (newCaretOffset < textArea.Document.TextLength)
        {
            char nextChar = textArea.Document.GetCharAt(newCaretOffset);

            // We suppress space insertion if:
            //  1. Next char is ALREADY a space (to avoid double spaces).
            //  2. Next char is Punctuation (e.g. ',' or '.') because we don't want "Keyword ,".
            //    EXCEPTION: We explicitly ALLOW space before '(' because Mermaid syntax often
            //    uses whitespace separators before parenthesized arguments (e.g. interaction callbacks).
            if (nextChar == singleSpaceChar || (char.IsPunctuation(nextChar) && nextChar != '('))
            {
                shouldInsertSpace = false;

                // If next char is a space, move caret past it for consistency
                if (nextChar == singleSpaceChar)
                {
                    newCaretOffset++;
                }
            }
        }

        // Add a space if appropriate and move caret past it
        if (shouldInsertSpace)
        {
            textArea.Document.Insert(newCaretOffset, " ");
            newCaretOffset++;
        }

        // Position the caret
        textArea.Caret.Offset = newCaretOffset;
    }

    /// <summary>
    /// Creates a 48x48 pixel bitmap containing the text "abc" rendered in a bold Segoe UI font and colored with the
    /// Visual Studio Code purple accent color.
    /// </summary>
    /// <remarks>The generated bitmap uses a color that is visually consistent in both light and dark themes.
    /// The text is centered within the bitmap for optimal icon appearance.</remarks>
    /// <returns>A RenderTargetBitmap containing the rendered "abc" icon.</returns>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Actual name of font family")]
    private static RenderTargetBitmap CreateAbcIconBitmap()
    {
        const int size = 48;
        RenderTargetBitmap bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));

        // Use VS Code Purple color which works in both ThemeModes (light and dark)
        SolidColorBrush vsCodePurpleBrush = SolidColorBrush.Parse("#B180D7");
        Typeface segoeUINormalBoldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyle.Normal, FontWeight.Bold);

        FormattedText formattedText = new FormattedText(
            "abc",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            segoeUINormalBoldTypeface,
            32,  // larger font for the bitmap
            vsCodePurpleBrush
        );

        // Render to bitmap
        using DrawingContext ctx = bitmap.CreateDrawingContext();

        // Center the text in the bitmap
        double x = (size - formattedText.Width) / 2;
        double y = (size - formattedText.Height) / 2;
        ctx.DrawText(formattedText, new Point(x, y));

        return bitmap;
    }
}
