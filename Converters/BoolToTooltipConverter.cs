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

using Avalonia.Data.Converters;
using System.Globalization;

namespace MermaidPad.Converters;

/// <summary>
/// Provides a value converter that converts a <see langword="bool"/> value to a tooltip string representation.
/// </summary>
/// <remarks>This converter is typically used in data binding scenarios where a tooltip needs to be displayed
/// based on a boolean value. When the value is <see langword="true"/>, the converter returns the string
/// "Initializing...". For any other value, it returns <see langword="null"/>.</remarks>
public sealed class BoolToTooltipConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean value to a string representation based on its state.
    /// </summary>
    /// <param name="value">The value to convert. Expected to be a <see langword="bool"/>.</param>
    /// <param name="targetType">The type to convert to. This parameter is not used in the conversion logic.</param>
    /// <param name="parameter">An optional parameter for the conversion. This parameter is not used in the conversion logic.</param>
    /// <param name="culture">The culture to use in the conversion. This parameter is not used in the conversion logic.</param>
    /// <returns>A string with the value "Initializing..." if <paramref name="value"/> is <see langword="true"/>; otherwise, <see
    /// langword="null"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool and true)
        {
            return "Initializing...";
        }

        return null; // No tooltip once initialization is complete
    }

    /// <summary>
    /// Converts a value back to its source type.
    /// </summary>
    /// <param name="value">The value produced by the binding target. This value is to be converted back to the source type.</param>
    /// <param name="targetType">The type to which the value is being converted.</param>
    /// <param name="parameter">An optional parameter to use during the conversion. This can be <see langword="null"/>.</param>
    /// <param name="culture">The culture to use in the conversion process.</param>
    /// <returns>The converted value. The exact return type depends on the implementation.</returns>
    /// <exception cref="NotImplementedException">This method is not implemented and will always throw this exception.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
