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

namespace MermaidPad.Models.Constants;

/// <summary>
/// Provides a centralized collection of constant strings representing common CSS style property names and values for
/// use in styling operations.
/// </summary>
/// <remarks>This class is intended for internal use to standardize CSS property references throughout the
/// codebase, reducing the risk of typos and improving maintainability. All members are static and should be accessed
/// directly via the class name. The constants correspond to frequently used CSS properties such as color, font, margin,
/// and border attributes.</remarks>
internal static class CssStyles
{
    public const string CssStyle = "cssStyle";
    public const string BackgroundColor = "backgroundColor:";
    public const string BorderColor = "borderColor:";
    public const string BorderWidth = "borderWidth:";
    public const string Color = "color:";
    public const string Fill = "fill:";
    public const string FontFamily = "fontFamily:";
    public const string FontSize = "fontSize:";
    public const string FontWeight = "fontWeight:";
    public const string Height = "height:";
    public const string MarginTop = "marginTop:";
    public const string MarginBottom = "marginBottom:";
    public const string MarginLeft = "marginLeft:";
    public const string MarginRight = "marginRight:";
    public const string Padding = "padding:";
    public const string Radius = "radius:";
    public const string Stroke = "stroke:";
    public const string StrokeColor = "stroke-color:";
    public const string StrokeDasharray = "stroke-dasharray:";
    public const string StrokeDashoffset = "stroke-dashoffset:";
    public const string StrokeWidth = "stroke-width:";
    public const string TextColor = "textColor:";
    public const string TitleColor = "titleColor:";
    public const string Width = "width:";
}
