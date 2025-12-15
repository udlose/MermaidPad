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

using Avalonia.Data;
using Avalonia.Data.Converters;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using MermaidPad.Models.Editor;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MermaidPad.Converters;

/// <summary>
/// Provides a value converter that creates an <see cref="EditorContext"/> object from a <see cref="TextEditor"/> instance for use in data binding
/// scenarios.
/// </summary>
/// <remarks>This converter is used in Avalonia data binding to extract contextual information from a <see cref="TextEditor"/>
/// such as the current document, selection, and caret position. If the input value is not a <see cref="TextEditor"/> or
/// if the selection or caret information is invalid, the converter returns <see cref="BindingOperations.DoNothing"/> or an
/// <see cref="EditorContext"/> marked as invalid. This class does not support reverse conversion.</remarks>
[SuppressMessage("ReSharper", "ReturnTypeCanBeNotNullable", Justification = "Interface requires nullable return type.")]
public sealed class EditorContextConverter : IValueConverter
{
    /// <summary>
    /// Converts a <see cref="TextEditor"/> instance and its current selection state into an <see cref="EditorContext"/> object for use in data
    /// binding scenarios.
    /// </summary>
    /// <remarks>If the selection or caret information is invalid, the returned <see cref="EditorContext"/> will have
    /// IsValid set to <see langword="false"/>. If the input value is not a <see cref="TextEditor"/> or its Document property is null,
    /// the method returns <see cref="BindingOperations.DoNothing"/> to indicate that no conversion should occur.</remarks>
    /// <param name="value">The value produced by the binding source. Expected to be a <see cref="TextEditor"/> instance; otherwise, a special value is
    /// returned to indicate no conversion.</param>
    /// <param name="targetType">The type of the binding target property. This parameter is not used in this implementation.</param>
    /// <param name="parameter">An optional parameter to be used in the converter logic. This parameter is not used in this implementation.</param>
    /// <param name="culture">The culture to use in the converter. This parameter is not used in this implementation.</param>
    /// <returns>An <see cref="EditorContext"/> object representing the current document and selection state if the input is a valid <see cref="TextEditor"/>
    /// with a document; otherwise, returns <see cref="BindingOperations.DoNothing"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TextEditor editor)
        {
            return BindingOperations.DoNothing;
        }

        Debug.WriteLine(editor.IsLoaded);

        TextDocument? document = editor.Document;
        if (document is null)
        {
            return BindingOperations.DoNothing;
        }

        int selectionStart = editor.SelectionStart;
        int selectionLength = editor.SelectionLength;
        int caretOffset = editor.CaretOffset;
        if (selectionStart < 0 || selectionLength < 0 || caretOffset < 0 || selectionStart + selectionLength > document.TextLength)
        {
            return new EditorContext(document, selectionStart, selectionLength, caretOffset)
            {
                IsValid = false
            };
        }

        // Emulate what AvaloniaEdit.TextEditor.SelectedText does - see: https://github.com/AvaloniaUI/AvaloniaEdit/blob/8dea781b49b09dedcf98ee7496d4e4a10b410ef0/src/AvaloniaEdit/TextEditor.cs#L971-L978
        // We'll get the text from the whole surrounding segment.
        // This is done to ensure that SelectedText.Length == SelectionLength.
        string selectedText = string.Empty;
        if (!editor.TextArea.Selection.IsEmpty)
        {
            selectedText = editor.TextArea.Document.GetText(editor.TextArea.Selection.SurroundingSegment);
        }

        return new EditorContext(document, selectionStart, selectionLength, caretOffset)
        {
            SelectedText = selectedText,
            IsValid = true
        };
    }

    /// <summary>
    /// Not supported. This method is not intended to be used for converting values back to the source type.
    /// </summary>
    /// <param name="value">The value produced by the binding target. This parameter is not used.</param>
    /// <param name="targetType">The type to convert to. This parameter is not used.</param>
    /// <param name="parameter">An optional parameter to be used in the converter logic. This parameter is not used.</param>
    /// <param name="culture">The culture to use in the converter. This parameter is not used.</param>
    /// <returns>This method does not return a value.</returns>
    /// <exception cref="NotSupportedException">Always thrown to indicate that reverse conversion is not supported.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
