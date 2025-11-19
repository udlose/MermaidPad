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
    /// Converts a boolean value to a string representation.
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <param name="targetType">The type to convert to (should be string).</param>
    /// <param name="parameter">A string in the format "TrueValue|FalseValue".</param>
    /// <param name="culture">The culture to use in the conversion.</param>
    /// <returns>The true or false string value based on the boolean input.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string paramString)
            return value;

        // Parse parameter string in format "TrueValue|FalseValue"
        string[] parts = paramString.Split('|');
        if (parts.Length != 2)
            return value;

        return boolValue ? parts[0] : parts[1];
    }

    /// <summary>
    /// Converts a string value back to a boolean.
    /// </summary>
    /// <exception cref="NotImplementedException">This conversion is not supported.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
