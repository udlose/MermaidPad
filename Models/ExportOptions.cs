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

namespace MermaidPad.Models;

/// <summary>
/// Contains options for exporting diagrams.
/// </summary>
public sealed class ExportOptions
{
    /// <summary>
    /// Gets or sets the export format.
    /// </summary>
    public ExportFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the scale factor for PNG export (1-4).
    /// </summary>
    public int Scale { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether the PNG should have a transparent background.
    /// </summary>
    public bool TransparentBackground { get; set; }

    /// <summary>
    /// Gets or sets the background color for PNG export when not transparent.
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";
}
