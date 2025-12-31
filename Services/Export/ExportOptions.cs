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
/// Options for exporting diagrams
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "Properties are used for data binding")]
internal sealed class ExportOptions
{
    /// <summary>
    /// The full file path where the export will be saved
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The export format
    /// </summary>
    public ExportFormat Format { get; set; } = ExportFormat.SVG;

    /// <summary>
    /// PNG-specific export options
    /// </summary>
    public PngExportOptions? PngOptions { get; set; }

    /// <summary>
    /// SVG-specific export options
    /// </summary>
    public SvgExportOptions? SvgOptions { get; set; }

    /// <summary>
    /// Whether to show progress during export
    /// </summary>
    public bool ShowProgress { get; set; } = true;

    /// <summary>
    /// Whether the export can be cancelled
    /// </summary>
    public bool AllowCancellation { get; set; } = true;
}
