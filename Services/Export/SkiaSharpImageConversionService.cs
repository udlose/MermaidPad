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

using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Svg.Skia;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MermaidPad.Services.Export;

/// <summary>
/// Provides services for converting SVG images to PNG format and for validating and extracting information from SVG
/// content using the Svg.Skia library.
/// </summary>
/// <remarks>This service offers high-level, asynchronous methods for SVG-to-PNG conversion, SVG validation, and
/// dimension extraction. It is designed for use in applications that require reliable SVG image processing, leveraging
/// Svg.Skia's recommended APIs for optimal compatibility and performance. The service is thread-safe and suitable for
/// both UI and server environments.</remarks>
internal sealed partial class SkiaSharpImageConversionService : IImageConversionService
{
    private static readonly char[] _separators = [',', ' ', '\t', '/'];
    private readonly ILogger<SkiaSharpImageConversionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaSharpImageConversionService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for this service.</param>
    public SkiaSharpImageConversionService(ILogger<SkiaSharpImageConversionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts the specified SVG content to a PNG image asynchronously using Svg.Skia's high-level API.
    /// </summary>
    /// <remarks>
    /// This implementation uses the Svg.Skia library's built-in ToImage() method which handles all
    /// rendering internally, including surface creation, canvas configuration, scaling, and encoding.
    /// This is the recommended approach per the Svg.Skia documentation.
    /// </remarks>
    /// <param name="svgContent">The SVG content to be converted. This must be a valid SVG string and cannot be null or empty.</param>
    /// <param name="options">The options specifying how the PNG image should be exported, such as resolution and scaling.</param>
    /// <param name="progress">An optional progress reporter that provides updates on the export progress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is a read-only memory buffer containing the PNG image data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the provided SVG content is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="svgContent"/> is null or empty.</exception>
    public Task<ReadOnlyMemory<byte>> ConvertSvgToPngAsync(string svgContent, PngExportOptions options, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(svgContent);
        ArgumentNullException.ThrowIfNull(options);

        return ConvertSvgToPngCoreAsync(svgContent, options, progress, cancellationToken);
    }

    /// <summary>
    /// Converts SVG content to a PNG image asynchronously using the specified export options.
    /// </summary>
    /// <remarks>The conversion is performed on a background thread to avoid blocking the calling thread. This
    /// method validates the SVG content before conversion and reports progress if a progress reporter is
    /// provided.</remarks>
    /// <param name="svgContent">The SVG markup to convert. Must be valid SVG; otherwise, an exception is thrown.</param>
    /// <param name="options">The options that control PNG export settings, such as image size, background color, and quality.</param>
    /// <param name="progress">An optional progress reporter that receives updates about the export operation. If null, progress is not
    /// reported.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the conversion operation.</param>
    /// <returns>A read-only memory buffer containing the PNG image data generated from the SVG content.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the provided SVG content is invalid.</exception>
    private async Task<ReadOnlyMemory<byte>> ConvertSvgToPngCoreAsync(string svgContent, PngExportOptions options, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        //TODO - DaveBlack: (bug) will using this implementation solve the canvas size limit of 32k x 32k???

        ValidationResult validation = await ValidateSvgAsync(svgContent)
            .ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid SVG content: {validation.ErrorMessage}");
        }

        // Run conversion on thread pool to keep UI responsive
        return await Task.Run(() => PerformConversion(svgContent, options, progress, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the provided SVG content and returns the result of the validation asynchronously.
    /// </summary>
    /// <remarks>This method performs the following checks: <list type="bullet"> <item><description>Ensures
    /// the SVG content is not null, empty, or whitespace.</description></item> <item><description>Validates that the
    /// SVG is well-formed and can be parsed successfully.</description></item> <item><description>Checks that the SVG
    /// has visible content with positive width and height.</description></item> <item><description>Issues a warning if
    /// the SVG dimensions exceed 10,000 units, as this may impact performance.</description></item> </list> If the SVG
    /// cannot be parsed or is invalid, the returned <see cref="ValidationResult"/> will contain an appropriate error
    /// message.</remarks>
    /// <param name="svgContent">The SVG content to validate, represented as a string. Cannot be null, empty, or whitespace.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating the outcome of the validation.  Returns <see
    /// cref="ValidationResult.Success"/> if the SVG is valid,  <see cref="ValidationResult.Failure(string)"/> if the
    /// SVG is invalid, or a warning message if the SVG has very large dimensions.</returns>
    public static async Task<ValidationResult> ValidateSvgAsync(string svgContent)
    {
        if (string.IsNullOrWhiteSpace(svgContent))
        {
            return ValidationResult.Failure("SVG content is empty");
        }

        // Run validation on thread pool to avoid blocking UI
        return await Task.Run(() =>
        {
            try
            {
                using SKSvg svg = new SKSvg();
                using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

                using SKPicture? picture = svg.Load(stream);
                if (picture is null)
                {
                    return ValidationResult.Failure("Invalid SVG format");
                }

                SKRect bounds = picture.CullRect;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return ValidationResult.Failure("SVG has no visible content");
                }

                // Check for extremely large dimensions
                const int maxDimension = 10_000;
                if (bounds.Width > maxDimension || bounds.Height > maxDimension)
                {
                    return new ValidationResult
                    {
                        IsValid = true,
                        WarningMessage = "SVG has very large dimensions. Conversion may be slow."
                    };
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to parse SVG: {ex.Message}");
            }
        })
        .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously retrieves the width and height of an SVG image from its XML content.
    /// </summary>
    /// <remarks>This method performs basic validation on the SVG content before attempting to parse it. If
    /// the content is not a valid SVG or cannot be parsed, the method returns (0, 0) and logs an error. The operation
    /// is performed asynchronously and is suitable for use in UI or server applications where blocking the calling
    /// thread is undesirable.</remarks>
    /// <param name="svgContent">The SVG image content as a string. Must be a valid SVG XML document. Cannot be null or empty.</param>
    /// <returns>A tuple containing the width and height of the SVG image, in pixels. Returns (0, 0) if the SVG content is
    /// invalid or dimensions cannot be determined.</returns>
    public async Task<(float Width, float Height)> GetSvgDimensionsAsync(ReadOnlyMemory<char> svgContent)
    {
        //TODO what should i do with this?
        throw new NotImplementedException("TODO");
        //// Validate SVG content before parsing to prevent XML exceptions
        //if (svgContent.IsEmpty)
        //{
        //    _logger.LogError("GetSvgDimensionsAsync called with null or empty SVG content");
        //    return (0, 0);
        //}

        //// Basic validation - check if it looks like SVG
        //ReadOnlySpan<char> svgSpan = svgContent.Span.TrimStart();
        //if (!svgSpan.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
        //    !svgSpan.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        //{
        //    _logger.LogError("SVG content does not start with XML declaration or <svg> tag");
        //    return (0, 0);
        //}

        //return await Task.Run(() =>
        //{
        //    try
        //    {
        //        //TODO this is ridiculous to load the whole SVG just to get dimensions - find a better way!!!


        //        using SKSvg svg = new SKSvg();
        //        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent.Span.));

        //        using SKPicture? picture = svg.Load(stream);
        //        if (picture is null)
        //        {
        //            _logger.LogError("Failed to load SVG picture - picture is null");
        //            return (0, 0);
        //        }

        //        SKRect bounds = picture.CullRect;

        //        if (bounds.Width <= 0 || bounds.Height <= 0)
        //        {
        //            _logger.LogError("SVG has invalid dimensions: {Width}x{Height}", bounds.Width, bounds.Height);
        //            return (0, 0);
        //        }

        //        return (bounds.Width, bounds.Height);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to get SVG dimensions");
        //        return (0, 0);
        //    }
        //})
        //.ConfigureAwait(false);
    }

    /// <summary>
    /// Converts SVG content to a PNG image using the specified export options.
    /// </summary>
    /// <remarks>The conversion process is performed in memory and does not write to disk. The method supports
    /// cancellation and progress reporting. The resulting PNG uses the sRGB color space and premultiplied alpha
    /// channel.</remarks>
    /// <param name="svgContent">The SVG markup to convert. Must be a valid, well-formed SVG string.</param>
    /// <param name="options">The options that control PNG export settings, such as scale factor, background color, and quality.</param>
    /// <param name="progress">An optional progress reporter that receives updates about the export process. May be null if progress reporting
    /// is not required.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the conversion operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A read-only memory buffer containing the PNG image data. The buffer will be empty if the conversion fails.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the SVG content cannot be loaded or if the conversion to PNG fails due to invalid image data or export
    /// options.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the calculated image dimensions are invalid (zero or negative).</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the provided <paramref name="cancellationToken"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if memory allocation fails during the conversion process.</exception>
    private ReadOnlyMemory<byte> PerformConversion(string svgContent, PngExportOptions options, IProgress<ExportProgress>? progress, CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Load SVG
            ReportProgress(progress, ExportStep.Initializing, 0, "Loading SVG...");
            Stopwatch stopwatch = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();

            // Step 2: Parse SVG
            ReportProgress(progress, ExportStep.ParsingSvg, 13, "Parsing SVG content...");

            using SKSvg svg = new SKSvg();
            using MemoryStream svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));
            using SKPicture picture = svg.Load(svgStream)
                ?? throw new InvalidOperationException("Failed to parse SVG content");
            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Calculate dimensions
            ReportProgress(progress, ExportStep.CalculatingDimensions, 32, "Calculating dimensions...");
            SKRect bounds = picture.CullRect;
            ImageDimensions dims = CalculateDimensions(bounds, options);
            cancellationToken.ThrowIfCancellationRequested();

            float scaleX = options.ScaleFactor;
            float scaleY = options.ScaleFactor;

            SKColor backgroundColor = options.BackgroundColor is null or "transparent"
                ? SKColors.Transparent
                : ParseColor(options.BackgroundColor);

            if (dims.Width <= 0 || dims.Height <= 0)
            {
                throw new InvalidOperationException("Calculated image dimensions are invalid");
            }

            //TODO: write out dimension details to the UI????
            string calculatedDimensionMessage =
                $"""
                Converting original .svg to .png.
                Bounds: {bounds.Width}x{bounds.Height}, Scale: {options.ScaleFactor}, DPI: {options.Dpi}, 
                Max: {options.MaxWidth}x{options.MaxHeight}, PreserveAspect: {options.PreserveAspectRatio}
                Target: {dims.Width}x{dims.Height} @ {options.Dpi} dpi
                Pixels: {(dims.Width * dims.Height):N0}
                Uncompressed pixel buffer (RGBA, upper bound): {dims.RawSizeBytes:N0} bytes (~{dims.RawSizeBytes / 1048576.0:F2} MiB)
                """;
            Debug.WriteLine(calculatedDimensionMessage);

            // Step 4: Render to PNG
            ReportProgress(progress, ExportStep.Rendering, 51, calculatedDimensionMessage);
            cancellationToken.ThrowIfCancellationRequested();

            // Pre-size stream using a bounded heuristic (uses EstimatedCompressedSize if populated)
            int initialCapacity = ComputeInitialCapacity(dims);
            using MemoryStream outputStream = new MemoryStream(initialCapacity);

            using SKColorSpace colorSpace = SKColorSpace.CreateSrgb();

            ReportProgress(progress, ExportStep.CreatingImage, 77, "Creating image...");
            cancellationToken.ThrowIfCancellationRequested();

            bool success = picture.ToImage(
                stream: outputStream,
                background: backgroundColor,
                format: SKEncodedImageFormat.Png,
                quality: options.Quality,
                scaleX: scaleX,
                scaleY: scaleY,
                skColorType: SKImageInfo.PlatformColorType,     // Cross-platform optimal
                skAlphaType: SKAlphaType.Premul,                // Standard for PNG
                skColorSpace: colorSpace                        // Standard sRGB
            );

            if (!success)
            {
                throw new InvalidOperationException("Failed to convert SVG to PNG. The image may have invalid dimensions.");
            }

            // Zero-copy result if possible
            // NOTE: This optimization assumes that outputStream is a MemoryStream created with a publicly visible buffer
            // (e.g., via new MemoryStream(initialCapacity)), so TryGetBuffer() should succeed. If the construction changes,
            // this may fall back to copying the buffer below.
            ReadOnlyMemory<byte> result;
            if (outputStream.TryGetBuffer(out ArraySegment<byte> segment))
            {
                if (segment.Array is null)
                {
                    throw new InvalidOperationException("Failed to access PNG image buffer");
                }
                result = new ReadOnlyMemory<byte>(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                // Fallback: copy only if the buffer can't be exposed
                result = outputStream.ToArray().AsMemory();
            }

            stopwatch.Stop();
            TimeSpan elapsed = stopwatch.Elapsed;
            ReportProgress(progress, ExportStep.Complete, 100, $"Conversion complete in {elapsed.TotalMilliseconds:F2}ms");

            _logger.LogInformation("PNG conversion successful: {ResultLength} bytes, took {ElapsedMs}ms", result.Length, elapsed.TotalMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            return Memory<byte>.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PNG conversion failed");
            throw new InvalidOperationException($"Failed to convert SVG to PNG: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculates an appropriate initial buffer capacity for storing compressed image data based on the provided image
    /// dimensions and any available size estimates.
    /// </summary>
    /// <remarks>If an estimated compressed size is provided in <paramref name="dimensions"/>, it is used
    /// (clamped to a reasonable range) as the initial capacity. Otherwise, a heuristic based on the raw image size is
    /// applied. This method helps minimize memory reallocations and excessive initial allocations when preparing
    /// buffers for image compression.</remarks>
    /// <param name="dimensions">The dimensions and size estimates of the image, including raw and estimated compressed sizes, used to determine
    /// the initial capacity.</param>
    /// <returns>An integer representing the recommended initial buffer capacity, in bytes, for storing the compressed image
    /// data. The value is always at least 64 KiB and does not exceed the raw image size or 4 MiB, whichever is smaller.</returns>
    private static int ComputeInitialCapacity(ImageDimensions dimensions)
    {
        const int oneKilobyte = 1_024;

        // If caller provided a content-aware estimate, use it (clamped).
        if (dimensions.EstimatedCompressedSize > 0)
        {
            return (int)Math.Clamp(Math.Ceiling(dimensions.EstimatedCompressedSize), 256, dimensions.RawSizeBytes);
        }

        // Heuristic fallback when no estimate exists:
        // - Start around raw/40 (vector/line-art often compresses 10–100x).
        // - Lower bound: 64 KiB (to avoid many small growth steps), unless raw is smaller.
        // - Upper bound: min(raw, 4 MiB) to avoid large initial allocations.
        const int minCapacity = 64 * oneKilobyte;
        const int maxCapacity = 4 * oneKilobyte * oneKilobyte;

        int upperBound = Math.Min(dimensions.RawSizeBytes, maxCapacity);
        if (upperBound <= 0)
        {
            return minCapacity; // degenerate but safe
        }

        double guess = dimensions.RawSizeBytes / 40.0;
        int lowerBound = Math.Min(minCapacity, upperBound); // don't exceed upper bound for tiny images
        return (int)Math.Clamp(Math.Ceiling(guess), lowerBound, upperBound);
    }

    /// <summary>
    /// Calculates the pixel dimensions and estimated file size for an image export based on the specified bounds and
    /// export options.
    /// </summary>
    /// <remarks>The calculated dimensions take into account the scale factor and any maximum width or height
    /// constraints specified in the options. If aspect ratio preservation is enabled, the image is scaled uniformly to
    /// fit within the maximum dimensions. The estimated compressed size is a rough heuristic and may vary depending on
    /// image content.</remarks>
    /// <param name="bounds">The bounding rectangle, in logical units, that defines the area to be exported.</param>
    /// <param name="options">The export options that control scaling, maximum dimensions, and aspect ratio preservation for the output image.</param>
    /// <returns>A <see cref="ImageDimensions"/> object containing the calculated width, height, raw byte size, and an estimated compressed
    /// file size for the exported image.</returns>
    private static ImageDimensions CalculateDimensions(SKRect bounds, PngExportOptions options)
    {
        // Pixel dimensions are derived from bounds and ScaleFactor.
        // Do NOT multiply by DPI here; ToImage scales only by scaleX/scaleY.
        float baseWidth = bounds.Width * options.ScaleFactor;
        float baseHeight = bounds.Height * options.ScaleFactor;

        // Apply limits (only if provided)
        if (options.PreserveAspectRatio)
        {
            // Scale down uniformly to satisfy both constraints
            float scale = 1f;

            if (options.MaxWidth > 0 && baseWidth > 0)
            {
                scale = Math.Min(scale, options.MaxWidth / baseWidth);
            }

            if (options.MaxHeight > 0 && baseHeight > 0)
            {
                scale = Math.Min(scale, options.MaxHeight / baseHeight);
            }

            if (scale < 1f)
            {
                baseWidth *= scale;
                baseHeight *= scale;
            }
        }
        else
        {
            if (options.MaxWidth > 0)
            {
                baseWidth = Math.Min(baseWidth, options.MaxWidth);
            }

            if (options.MaxHeight > 0)
            {
                baseHeight = Math.Min(baseHeight, options.MaxHeight);
            }
        }

        int finalWidth = (int)Math.Ceiling(baseWidth);
        int finalHeight = (int)Math.Ceiling(baseHeight);

        // Uncompressed RGBA upper bound
        int rawSizeBytes = finalWidth * finalHeight * 4;

        // Lightweight, bounded estimate for PNG size to improve initial buffer sizing.
        // Baseline: raw/8. Adjust for image size, transparency intent, and PNG "quality" (zlib level).
        // Clamped to [32 KiB, rawSizeBytes].
        int pixels = finalWidth * finalHeight;

        // Start with a conservative baseline ratio (bigger divisor => smaller estimate).
        int ratio = pixels switch
        {
            // Smaller images often compress a bit better; very large images worse.
            < 512 * 512 => 10,
            > 4_096 * 4_096 => 6,
            _ => 8
        };

        // Transparent output tends to be a bit larger than opaque backgrounds.
        bool backgroundTransparent = options.BackgroundColor?.Equals("transparent", StringComparison.OrdinalIgnoreCase) != false;
        if (backgroundTransparent)
        {
            ratio = Math.Max(4, ratio - 2); // grow estimate (raw/6 -> raw/4)
        }

        // Map PNG "quality" to estimated compression ratio (higher quality => smaller output).
        // Keep ratio bounded to avoid extreme estimates.
        // Ratio is a divisor; larger ratio smaller estimate.
        ratio = options.Quality switch
        {
            >= 95 => Math.Min(16, ratio + 4),
            >= 90 => Math.Min(16, ratio + 3),
            >= 75 => Math.Min(16, ratio + 2),
            >= 50 => Math.Min(16, ratio + 1),
            >= 30 => ratio,                      // baseline
            >= 10 => Math.Max(4, ratio - 1),     // lower quality => larger estimate
            _ => Math.Max(4, ratio - 2)
        };

        double estimatedCompressedSize = rawSizeBytes <= 0 ? 0 : Math.Clamp(rawSizeBytes / (double)ratio, 32 * 1_024, rawSizeBytes);

        return new ImageDimensions(finalWidth, finalHeight, rawSizeBytes, estimatedCompressedSize);
    }

    /// <summary>
    /// Parses a color string in hexadecimal, CSS rgb/rgba, or common named color formats and returns the corresponding
    /// SKColor value.
    /// </summary>
    /// <remarks>If the input string does not match a supported format or a recognized color name, the method
    /// returns SKColors.White. The method is case-insensitive and trims whitespace from the input. Supported named
    /// colors include common CSS color names such as "white", "black", "red", "green", "blue", "yellow", "cyan",
    /// "magenta", "gray"/"grey", "lightgray"/"lightgrey", "darkgray"/"darkgrey", and "transparent".</remarks>
    /// <param name="colorString">A string representing the color to parse. Supported formats include hexadecimal notation (e.g., "#RRGGBB",
    /// "#AARRGGBB"), CSS rgb()/rgba() functions, and common color names such as "red", "blue", or "transparent". The
    /// comparison is case-insensitive and leading/trailing whitespace is ignored.</param>
    /// <remarks>
    /// The method supports the following formats: <list type="bullet">
    ///     <item><description>Hexadecimal colors, starting with '#' (e.g., "#FF0000").</description></item>
    ///     <item><description>RGB/RGBA colors, starting with "rgb" or "rgba" (e.g., "rgb(255,0,0)").</description></item>
    ///     <item><description>Named colors, such as "red", "blue", "lightgray", and "transparent".</description></item>
    /// </list>
    /// </remarks>
    /// <returns>A <see cref="SKColor"/> representing the parsed color. If the input string is invalid or unrecognized, returns <see cref="SKColors.White"/>.</returns>
    private SKColor ParseColor(string colorString)
    {
        ReadOnlySpan<char> colorSpan = colorString.AsSpan().Trim();

        try
        {
            // Handle hex colors (#RGB, #RRGGBB, #AARRGGBB)
            if (colorSpan.StartsWith('#'))
            {
                return SKColor.Parse(colorString);
            }

            // Handle rgb()/rgba() CSS formats
            if (colorSpan.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseRgbColor(colorSpan, out SKColor color))
                {
                    return color;
                }

                _logger.LogWarning("Failed to parse rgb/rgba format: {ColorString}. Falling back to default color of {DefaultColor}", colorString, SKColors.White);
                return SKColors.White;
            }

            // Try named colors
            return colorString switch
            {
                not null when colorString.Equals("white", StringComparison.OrdinalIgnoreCase) => SKColors.White,
                not null when colorString.Equals("black", StringComparison.OrdinalIgnoreCase) => SKColors.Black,
                not null when colorString.Equals("red", StringComparison.OrdinalIgnoreCase) => SKColors.Red,
                not null when colorString.Equals("green", StringComparison.OrdinalIgnoreCase) => SKColors.Green,
                not null when colorString.Equals("blue", StringComparison.OrdinalIgnoreCase) => SKColors.Blue,
                not null when colorString.Equals("yellow", StringComparison.OrdinalIgnoreCase) => SKColors.Yellow,
                not null when colorString.Equals("cyan", StringComparison.OrdinalIgnoreCase) => SKColors.Cyan,
                not null when colorString.Equals("magenta", StringComparison.OrdinalIgnoreCase) => SKColors.Magenta,
                not null when colorString.Equals("gray", StringComparison.OrdinalIgnoreCase) ||
                              colorString.Equals("grey", StringComparison.OrdinalIgnoreCase) => SKColors.Gray,
                not null when colorString.Equals("lightgray", StringComparison.OrdinalIgnoreCase) ||
                              colorString.Equals("lightgrey", StringComparison.OrdinalIgnoreCase) => SKColors.LightGray,
                not null when colorString.Equals("darkgray", StringComparison.OrdinalIgnoreCase) ||
                              colorString.Equals("darkgrey", StringComparison.OrdinalIgnoreCase) => SKColors.DarkGray,
                not null when colorString.Equals("transparent", StringComparison.OrdinalIgnoreCase) => SKColors.Transparent,
                _ => SKColors.White
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse color: {ColorString}", colorString);
            return SKColors.White;
        }
    }

    /// <summary>
    /// Attempts to parse an RGB or RGBA color value from the specified character span.
    /// </summary>
    /// <remarks>The method supports both three-component (RGB) and four-component (RGBA) color formats.
    /// Component values must be in the valid byte range (0–255). The alpha component is optional and defaults to 255
    /// (fully opaque) if not specified. Parsing is case-insensitive and ignores extra whitespace or separators between
    /// components.</remarks>
    /// <param name="colorSpan">A read-only span of characters containing the color value to parse. The expected format is 'rgb(r, g, b)' or
    /// 'rgba(r, g, b, a)', where components are separated by commas, spaces, tabs, or slashes.</param>
    /// <param name="color">When this method returns, contains the parsed SKColor value if parsing succeeded; otherwise, contains the
    /// default value.</param>
    /// <returns>true if the color value was successfully parsed; otherwise, false.</returns>
    private static bool TryParseRgbColor(ReadOnlySpan<char> colorSpan, out SKColor color)
    {
        color = default;

        // Find parentheses
        int openParen = colorSpan.IndexOf('(');
        int closeParen = colorSpan.LastIndexOf(')');

        if (openParen < 0 || closeParen <= openParen + 1)
        {
            return false;
        }

        // Extract content between parentheses
        ReadOnlySpan<char> content = colorSpan.Slice(openParen + 1, closeParen - openParen - 1).Trim();

        if (content.IsEmpty)
        {
            return false;
        }

        // Split by comma, space, tab, or slash
        const int minComponents = 3;
        const int maxComponents = 4;
        Span<Range> ranges = stackalloc Range[maxComponents + 1]; // Max 4 components + extra
        int count = content.Split(ranges, _separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (count is < minComponents or > maxComponents)
        {
            return false;
        }

        // Parse R, G, B components
        if (!TryParseColorComponent(content[ranges[0]], out byte r) ||
            !TryParseColorComponent(content[ranges[1]], out byte g) ||
            !TryParseColorComponent(content[ranges[2]], out byte b))
        {
            return false;
        }

        // Parse optional alpha component
        byte a = 255;
        if (count == 4 && !TryParseAlphaComponent(content[ranges[3]], out a))
        {
            return false;
        }

        color = new SKColor(r, g, b, a);
        return true;
    }

    /// <summary>
    /// Attempts to parse a color component value from the specified character span, supporting both integer (0–255) and
    /// percentage (0%–100%) formats.
    /// </summary>
    /// <remarks>This method supports two formats for the input: <list type="bullet"> <item> <description>An
    /// integer value in the range 0-255, which is directly converted to a byte.</description> </item> <item>
    /// <description>A percentage value (e.g., "75%"), which is clamped to the range 0-100 and scaled to the range
    /// 0-255.</description> </item> </list> If the input is empty, contains invalid characters, or is outside the
    /// supported formats, the method returns <see langword="false"/>.</remarks>
    /// <param name="component">The string representation of the color component. This can be an integer value in the range 0-255  or a
    /// percentage value (e.g., "50%") representing a proportion of the maximum byte value.</param>
    /// <param name="value">When this method returns, contains the parsed byte value of the color component, if the conversion succeeded;
    /// otherwise, the value is 0.</param>
    /// <returns><see langword="true"/> if the color component was successfully parsed and converted; otherwise, <see
    /// langword="false"/>.</returns>
    private static bool TryParseColorComponent(ReadOnlySpan<char> component, out byte value)
    {
        value = 0;

        if (component.IsEmpty)
        {
            return false;
        }

        // Check for percentage (e.g., "100%")
        if (component[^1] == '%')
        {
            ReadOnlySpan<char> numberPart = component[..^1].Trim();

            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double percentage))
            {
                return false;
            }

            // Clamp percentage to 0-100 and convert to 0-255
            percentage = Math.Clamp(percentage, 0, 100);
            value = (byte)Math.Round(255.0 * (percentage / 100.0));
            return true;
        }

        // Parse as integer (0-255)
        if (!int.TryParse(component, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
        {
            return false;
        }

        // Clamp to valid byte range
        value = (byte)Math.Clamp(intValue, 0, 255);
        return true;
    }

    /// <summary>
    /// Attempts to parse an alpha (opacity) component from the specified character span, supporting percentage,
    /// decimal, and integer formats.
    /// </summary>
    /// <remarks>This method supports parsing alpha components in various formats commonly used in CSS and
    /// other graphics-related contexts. Invalid formats or values outside the supported range will result in a return
    /// value of <see langword="false"/>.</remarks>
    /// <param name="component">The string representation of the alpha component. This can be: <list type="bullet"> <item>A percentage (e.g.,
    /// "50%") representing the opacity as a percentage (0% = fully transparent, 100% = fully opaque).</item> <item>A
    /// decimal value between 0.0 and 1.0 (e.g., "0.5") representing the opacity as a fraction (0.0 = fully transparent,
    /// 1.0 = fully opaque).</item> <item>An integer value between 0 and 255 (e.g., "128") representing the opacity
    /// directly (0 = fully transparent, 255 = fully opaque).</item> </list> If the string is empty, the alpha component
    /// defaults to fully opaque (255).</param>
    /// <param name="alpha">When this method returns, contains the parsed alpha value as a byte (0 = fully transparent, 255 = fully opaque),
    /// or 255 if the input is empty or invalid.</param>
    /// <returns><see langword="true"/> if the alpha component was successfully parsed; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseAlphaComponent(ReadOnlySpan<char> component, out byte alpha)
    {
        alpha = 255; // Default to fully opaque

        if (component.IsEmpty)
        {
            return true; // Empty alpha is valid (defaults to opaque)
        }

        // Check for percentage (e.g., "50%")
        if (component[^1] == '%')
        {
            ReadOnlySpan<char> numberPart = component[..^1].Trim();

            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double percentage))
            {
                return false;
            }

            // Clamp percentage to 0-100 and convert to 0-255
            percentage = Math.Clamp(percentage, 0, 100);
            alpha = (byte)Math.Round(255.0 * (percentage / 100.0));
            return true;
        }

        // Try parsing as double to handle both decimal and integer
        if (!double.TryParse(component, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return false;
        }

        // Determine format based on value range:
        // - Values 0.0-1.0 are treated as decimal alpha (CSS standard)
        // - Values > 1.0 are treated as 0-255 integer alpha
        if (value <= 1.0)
        {
            // Decimal format (0.0 = transparent, 1.0 = opaque)
            value = Math.Clamp(value, 0.0, 1.0);
            alpha = (byte)Math.Round(255.0 * value);
        }
        else
        {
            // Integer format (0 = transparent, 255 = opaque)
            int intValue = (int)Math.Round(value);
            alpha = (byte)Math.Clamp(intValue, 0, 255);
        }

        return true;
    }

    /// <summary>
    /// Reports the progress of an export operation to a provided <see cref="IProgress{T}"/> instance.
    /// </summary>
    /// <param name="progress">An <see cref="IProgress{T}"/> instance used to report progress updates. Can be <see langword="null"/> if
    /// progress reporting is not required.</param>
    /// <param name="step">The current step of the export operation.</param>
    /// <param name="percent">The percentage of the export operation that is complete. Must be between 0 and 100.</param>
    /// <param name="message">A message describing the current progress or step of the export operation.</param>
    private void ReportProgress(IProgress<ExportProgress>? progress, ExportStep step, int percent, string message)
    {
        if (progress is null)
        {
            return;
        }

        ExportProgress exportProgress = new ExportProgress
        {
            Step = step,
            PercentComplete = percent,
            Message = message
        };

        // Marshal to UI thread for ViewModel property updates
        if (Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread
            progress.Report(exportProgress);
        }
        else
        {
            // Post to UI thread
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    progress.Report(exportProgress);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    _logger.LogError(ex, "Progress report failed");
                }
            });
        }
    }
}

/// <summary>
/// Represents the dimensions and size information for an image, including width, height, raw size in bytes, and an
/// estimated compressed size.
/// </summary>
/// <remarks>This record is typically used to convey image metadata for processing, storage, or transmission
/// scenarios. All values are non-negative and represent the state of a specific image at a given point in
/// time.</remarks>
public sealed record ImageDimensions
{
    /// <summary>
    /// Gets the width of the object, measured in units appropriate to the context.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the object, measured in units appropriate to the context.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the raw size of the content, in bytes.
    /// </summary>
    public int RawSizeBytes { get; }

    /// <summary>
    /// Gets the estimated size, in bytes, of the content after compression.
    /// </summary>
    public double EstimatedCompressedSize { get; }

    /// <summary>
    /// Initializes a new instance of the ImageDimensions class with the specified width, height, raw size in bytes, and
    /// estimated compressed size.
    /// </summary>
    /// <param name="width">The width of the image, in pixels. Must be a non-negative integer.</param>
    /// <param name="height">The height of the image, in pixels. Must be a non-negative integer.</param>
    /// <param name="rawSizeBytes">The uncompressed size of the image data, in bytes. Must be a non-negative integer.</param>
    /// <param name="estimatedCompressedSize">The estimated size of the image after compression, in bytes. Must be a non-negative value.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if width, height, or rawSizeBytes is negative, or if estimatedCompressedSize is less than zero.</exception>
    public ImageDimensions(int width, int height, int rawSizeBytes, double estimatedCompressedSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(rawSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedCompressedSize);

        Width = width;
        Height = height;
        RawSizeBytes = rawSizeBytes;
        EstimatedCompressedSize = estimatedCompressedSize;
    }
}
