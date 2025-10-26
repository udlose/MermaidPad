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
/// Defines the contract for image conversion services
/// </summary>
public interface IImageConversionService
{
    /// <summary>
    /// Asynchronously converts SVG content to a PNG image using the specified export options.
    /// </summary>
    /// <remarks>The returned PNG data is not written to disk; callers are responsible for saving or
    /// processing the image as needed. This method is thread-safe and may be called concurrently from multiple
    /// threads.</remarks>
    /// <param name="svgContent">The SVG markup to convert. Cannot be null or empty.</param>
    /// <param name="options">The options that control PNG export settings, such as image size and background color. Cannot be null.</param>
    /// <param name="progress">An optional progress reporter that receives updates about the export operation. May be null if progress
    /// reporting is not required.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation is canceled if the token is signaled.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a read-only memory buffer with the
    /// PNG image data.</returns>
    Task<ReadOnlyMemory<byte>> ConvertSvgToPngAsync(
        string svgContent,
        PngExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if the provided SVG content can be converted
    /// </summary>
    /// <param name="svgContent">The SVG content to validate</param>
    /// <returns>Validation result with any error messages</returns>
    Task<ValidationResult> ValidateSvgAsync(string svgContent);

    /// <summary>
    /// Gets the dimensions of an SVG for preview purposes
    /// </summary>
    /// <param name="svgContent">The SVG content</param>
    /// <returns>Width and height of the SVG</returns>
    Task<(float Width, float Height)> GetSvgDimensionsAsync(ReadOnlyMemory<char> svgContent);
}
