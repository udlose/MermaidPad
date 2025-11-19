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
/// Persisted layout configuration for the docking system.
/// Supports three flexible columns that can contain any panel.
/// </summary>
public sealed class DockLayoutSettings
{
    /// <summary>
    /// Which panel is in column 1 (left).
    /// Valid values: "Editor", "Preview", "AI", "None"
    /// </summary>
    public string Column1Panel { get; set; } = "Editor";

    /// <summary>
    /// Which panel is in column 2 (center).
    /// Valid values: "Editor", "Preview", "AI", "None"
    /// </summary>
    public string Column2Panel { get; set; } = "Preview";

    /// <summary>
    /// Which panel is in column 3 (right).
    /// Valid values: "Editor", "Preview", "AI", "None"
    /// </summary>
    public string Column3Panel { get; set; } = "AI";

    /// <summary>
    /// Proportional width of column 1 (Star sizing).
    /// </summary>
    public double Column1Width { get; set; } = 1.0;

    /// <summary>
    /// Proportional width of column 2 (Star sizing).
    /// </summary>
    public double Column2Width { get; set; } = 2.0;

    /// <summary>
    /// Proportional width of column 3 (Star sizing).
    /// </summary>
    public double Column3Width { get; set; } = 1.0;
}
