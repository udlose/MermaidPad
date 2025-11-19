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
/// Converts a boolean value to a string representation based on converter parameters.
/// </summary>
/// <remarks>
/// The converter parameter should be a string in the format "TrueValue|FalseValue".
/// For example: "Hide|Show" will return "Hide" when the boolean is true, "Show" when false.
/// </remarks>
public sealed class BoolToStringConverter : IValueConverter
{
    /// <summary>
    /// Converts a Boolean value to a corresponding object based on a parameter string specifying true and false
    /// representations.
    /// </summary>
    /// <remarks>If <paramref name="parameter"/> does not contain exactly one '|' character separating two
    /// values, the method returns <see langword="null"/>. This method does not use <paramref name="targetType"/> or
    /// <paramref name="culture"/> in the conversion.</remarks>
    /// <param name="value">The value to convert. Must be a Boolean or <see langword="null"/>.</param>
    /// <param name="targetType">The type to convert the value to. This parameter is not used in the conversion.</param>
    /// <param name="parameter">A string in the format "TrueValue|FalseValue" that specifies the object to return for <see langword="true"/> and
    /// <see langword="false"/> values.</param>
    /// <param name="culture">The culture to use in the conversion. This parameter is not used in the conversion.</param>
    /// <returns>An object representing the converted value: the first part of <paramref name="parameter"/> if <paramref
    /// name="value"/> is <see langword="true"/>, the second part if <see langword="false"/>. Returns <see
    /// langword="null"/> if <paramref name="value"/> is not a Boolean, <paramref name="parameter"/> is not a string, or
    /// the parameter format is invalid.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string paramString)
        {
            return null;
        }

        // Parse parameter string in format "TrueValue|FalseValue"
        string[] parts = paramString.Split('|');
        if (parts.Length != 2)
        {
            return null;
        }

        return boolValue ? parts[0] : parts[1];
    }

    /// <summary>
    /// Converts a string value back to a boolean.
    /// </summary>
    /// <exception cref="NotImplementedException">This conversion is not supported.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ConvertBack is not supported for BoolToStringConverter.");
    }
}
