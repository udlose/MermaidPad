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
/// Converts a boolean value to an object, returning the parameter when false and null when true.
/// </summary>
/// <remarks>
/// This converter is useful for scenarios like password masking where you want to return
/// a value (like '•') when a boolean is false, and null when it's true.
/// </remarks>
public sealed class InverseBoolToObjectConverter : IValueConverter
{
    /// <summary>
    /// Converts a Boolean value to either <paramref name="parameter"/> or <see langword="null"/> based on its value.
    /// </summary>
    /// <remarks>This method is typically used in data binding scenarios to convert Boolean values to a
    /// specific object or <see langword="null"/>. If <paramref name="value"/> is not a Boolean, the method always
    /// returns <see langword="null"/>.</remarks>
    /// <param name="value">The value to convert. If the value is a Boolean, its value determines the conversion result; otherwise, <see
    /// langword="null"/> is returned.</param>
    /// <param name="targetType">The type to convert the value to. This parameter is provided for compatibility but is not used in the
    /// conversion.</param>
    /// <param name="parameter">An optional parameter to return when the input value is <see langword="false"/>.</param>
    /// <param name="culture">The culture information to use in the conversion. This parameter is provided for compatibility but is not used
    /// in the conversion.</param>
    /// <returns>Returns <see langword="null"/> if <paramref name="value"/> is <see langword="true"/> or not a Boolean;
    /// otherwise, returns <paramref name="parameter"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? null : parameter;
        }
        return null;
    }

    /// <summary>
    /// Converts an object value back to a boolean.
    /// </summary>
    /// <exception cref="NotImplementedException">This conversion is not supported.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ConvertBack is not supported for InverseBoolToObjectConverter.");
    }
}
