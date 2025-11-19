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
/// Converts a value to a boolean indicating whether it is not equal to the parameter value.
/// </summary>
/// <remarks>
/// This converter compares the input value with the converter parameter and returns true
/// if they are not equal, false if they are equal.
/// </remarks>
public sealed class IsNotEqualConverter : IValueConverter
{
    /// <summary>
    /// Compares the value with the parameter and returns true if not equal.
    /// </summary>
    /// <param name="value">The value to compare.</param>
    /// <param name="targetType">The type to convert to (should be bool).</param>
    /// <param name="parameter">The value to compare against.</param>
    /// <param name="culture">The culture to use in the conversion.</param>
    /// <returns>True if value is not equal to parameter, false otherwise.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !Equals(value, parameter);
    }

    /// <summary>
    /// Converts a boolean value back to the original value.
    /// </summary>
    /// <exception cref="NotImplementedException">This conversion is not supported.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
