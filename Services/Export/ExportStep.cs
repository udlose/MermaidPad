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

namespace MermaidPad.Services.Export;

/// <summary>
/// Specifies the stages of the export process.
/// </summary>
/// <remarks>Use this enumeration to track or report the current step when exporting data or images. The values
/// represent the sequential phases typically involved in an export operation, from initialization to
/// completion.</remarks>
public enum ExportStep
{
    /// <summary>
    /// The export process is initializing resources and preparing required state.
    /// </summary>
    Initializing,

    /// <summary>
    /// The SVG output produced by the renderer is being parsed for further processing.
    /// </summary>
    ParsingSvg,

    /// <summary>
    /// Measurements and layout calculations (width, height, scaling) are being computed.
    /// </summary>
    CalculatingDimensions,

    /// <summary>
    /// The diagram is being rendered (drawing shapes, layout engines running).
    /// </summary>
    Rendering,

    /// <summary>
    /// A bitmap or canvas surface is being created to draw the rendered output onto.
    /// </summary>
    CreatingCanvas,

    /// <summary>
    /// Any image encoding (for example, encoding to PNG or JPEG formats) is occurring.
    /// </summary>
    Encoding,

    /// <summary>
    /// The final image artifact is being constructed and finalized.
    /// </summary>
    CreatingImage,

    /// <summary>
    /// The export process has completed successfully (or reached its final state).
    /// </summary>
    Complete,
}
