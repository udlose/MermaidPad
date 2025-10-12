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


using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Services.Export;
/// <summary>
/// Represents options for exporting an image in PNG format.
/// </summary>
/// <remarks>This class provides various settings to control the output of PNG image exports, including
/// resolution, scaling, background color, compression quality, and size constraints. By adjusting these properties, you
/// can customize the appearance and performance characteristics of the exported image.</remarks>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "Properties are used for data binding")]
public sealed class PngExportOptions
{
    /// <summary>
    /// DPI for the output image (72, 150, 300, 600)
    /// </summary>
    public int Dpi { get; set; } = 150;

    /// <summary>
    /// Scale factor for the output (1.0 = 100%, 2.0 = 200%, etc.)
    /// </summary>
    public float ScaleFactor { get; set; } = 2.0f;

    /// <summary>
    /// Background color (null for transparent)
    /// </summary>
    public string? BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// PNG compression quality (0-100, where 100 is the best quality)
    /// </summary>
    public int Quality { get; set; } = 95;

    /// <summary>
    /// Whether to apply antialiasing
    /// </summary>
    public bool AntiAlias { get; set; } = true;

    /// <summary>
    /// Maximum width in pixels (0 = no limit)
    /// </summary>
    public int MaxWidth { get; set; } = 0;

    /// <summary>
    /// Maximum height in pixels (0 = no limit)
    /// </summary>
    public int MaxHeight { get; set; } = 0;

    /// <summary>
    /// Whether to preserve aspect ratio when using max dimensions
    /// </summary>
    public bool PreserveAspectRatio { get; set; } = true;
}
