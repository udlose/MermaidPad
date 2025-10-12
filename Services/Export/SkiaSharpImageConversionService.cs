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
using JetBrains.Annotations;
using SkiaSharp;
using Svg.Skia;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace MermaidPad.Services.Export;
/// <summary>
/// SkiaSharp-based implementation of image conversion service
/// </summary>
public sealed partial class SkiaSharpImageConversionService : IImageConversionService
{
    private static readonly char[] _separators = [',', ' ', '\t', '/'];

    /// <summary>
    /// Converts the specified SVG content to a PNG image asynchronously.
    /// </summary>
    /// <remarks>The method validates the provided SVG content before performing the conversion. If the SVG
    /// content is invalid, an exception is thrown. The conversion process is performed on a background thread to avoid
    /// blocking the calling thread.</remarks>
    /// <param name="svgContent">The SVG content to be converted. This must be a valid SVG string and cannot be null or empty.</param>
    /// <param name="options">The options specifying how the PNG image should be exported, such as resolution and scaling. This cannot be
    /// null.</param>
    /// <param name="progress">An optional progress reporter that provides updates on the export progress. Can be <see langword="null"/> if
    /// progress reporting is not needed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will be canceled if the token is triggered.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is a byte array containing the PNG image
    /// data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the provided SVG content is invalid.</exception>
    public async Task<byte[]> ConvertSvgToPngAsync(string svgContent, PngExportOptions options, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(svgContent);
        ArgumentNullException.ThrowIfNull(options);

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
    /// Validates the provided SVG content to ensure it is well-formed and meets specific criteria.
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
    public async Task<ValidationResult> ValidateSvgAsync(string svgContent)
    {
        if (string.IsNullOrWhiteSpace(svgContent))
        {
            return ValidationResult.Failure("SVG content is empty");
        }

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
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously retrieves the dimensions of an SVG image from its content.
    /// </summary>
    /// <remarks>This method validates the provided SVG content to ensure it starts with an XML declaration or
    /// an <c>&lt;svg&gt;</c> tag. If the content is invalid or an error occurs during parsing, the method logs the
    /// error and returns (0, 0).</remarks>
    /// <param name="svgContent">The SVG content as a string. Must not be null, empty, or whitespace.</param>
    /// <returns>A tuple containing the width and height of the SVG image. Returns (0, 0) if the SVG content is invalid, the
    /// dimensions are non-positive, or an error occurs during processing.</returns>
    public async Task<(float Width, float Height)> GetSvgDimensionsAsync(string svgContent)
    {
        // Validate SVG content before parsing to prevent XML exceptions
        if (string.IsNullOrWhiteSpace(svgContent))
        {
            SimpleLogger.LogError("GetSvgDimensionsAsync called with null or empty SVG content");
            return (0, 0);
        }

        // Basic validation - check if it looks like SVG
        ReadOnlySpan<char> svgSpan = svgContent.AsSpan().TrimStart();
        if (!svgSpan.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
            !svgSpan.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        {
            SimpleLogger.LogError("SVG content does not start with XML declaration or <svg> tag");
            return (0, 0);
        }

        return await Task.Run(() =>
        {
            try
            {
                using SKSvg svg = new SKSvg();
                using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

                using SKPicture? picture = svg.Load(stream);
                if (picture is null)
                {
                    SimpleLogger.LogError("Failed to load SVG picture - picture is null");
                    return (0, 0);
                }

                SKRect bounds = picture.CullRect;

                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    SimpleLogger.LogError($"SVG has invalid dimensions: {bounds.Width}x{bounds.Height}");
                    return (0, 0);
                }

                return (bounds.Width, bounds.Height);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("Failed to get SVG dimensions", ex);
                return (0, 0);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts the specified SVG content into a PNG image using the provided export options.
    /// </summary>
    /// <remarks>This method performs a multi-step process to convert SVG content into a PNG image. It parses
    /// the SVG, calculates dimensions, renders the image onto a canvas, and encodes the result as a PNG. The method
    /// supports progress reporting and cancellation, making it suitable for long-running operations.</remarks>
    /// <param name="svgContent">The SVG content to be converted, represented as a string.</param>
    /// <param name="options">The options that configure the PNG export, such as dimensions, DPI, and quality.</param>
    /// <param name="progress">An optional progress reporter that provides updates on the conversion process. Can be <see langword="null"/> if
    /// progress reporting is not required.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation. If cancellation is requested, the method will throw an <see
    /// cref="OperationCanceledException"/>.</param>
    /// <returns>A byte array containing the PNG image data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the SVG content cannot be parsed, the rendering surface cannot be created, or the PNG encoding fails.</exception>
    private static byte[] PerformConversion(string svgContent, PngExportOptions options, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Initialize
            ReportProgress(progress, ExportStep.Initializing, 0, "Initializing conversion...");
            cancellationToken.ThrowIfCancellationRequested();

            // Step 2: Parse SVG
            ReportProgress(progress, ExportStep.ParsingSvg, 10, "Parsing SVG content...");

            using SKSvg svg = new SKSvg();
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

            using SKPicture picture = svg.Load(stream) ??
                throw new InvalidOperationException("Failed to parse SVG content");
            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Calculate dimensions
            ReportProgress(progress, ExportStep.CalculatingDimensions, 20, "Calculating dimensions...");

            SKRect bounds = picture.CullRect;
            (int width, int height) = CalculateDimensions(bounds, options);

            SimpleLogger.Log($"Converting SVG: Original {bounds.Width}x{bounds.Height}, Target {width}x{height} @ {options.Dpi} DPI");

            cancellationToken.ThrowIfCancellationRequested();

            // Step 4: Create canvas
            ReportProgress(progress, ExportStep.CreatingCanvas, 30,
                $"Creating {width}x{height} canvas...");

            using SKColorSpace colorSpace = SKColorSpace.CreateSrgb();
            SKImageInfo imageInfo = new SKImageInfo(
                width,
                height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul,
                colorSpace);

            using SKSurface surface = CreateSurface(imageInfo) ??
                throw new InvalidOperationException("Failed to create rendering surface");

            using SKCanvas canvas = surface.Canvas;
            cancellationToken.ThrowIfCancellationRequested();

            // Step 5: Render
            ReportProgress(progress, ExportStep.Rendering, 50, "Rendering SVG...");

            // FIXED: Improved rendering configuration for better quality
            ConfigureCanvas(canvas, options, width, height, bounds);

            // Draw the SVG with high quality paint
            using (SKPaint paint = CreateHighQualityPaint(options))
            {
                canvas.DrawPicture(picture, paint);
            }

            canvas.Flush();

            cancellationToken.ThrowIfCancellationRequested();

            // Step 6: Encode to PNG
            ReportProgress(progress, ExportStep.Encoding, 80, "Encoding PNG...");

            using SKImage image = surface.Snapshot();

            // Use high quality PNG encoding
            using SKData data = image.Encode(SKEncodedImageFormat.Png, options.Quality) ??
                throw new InvalidOperationException("Failed to encode PNG");

            byte[] result = data.ToArray();

            // Step 7: Complete
            TimeSpan elapsed = stopwatch.Elapsed;
            ReportProgress(progress, ExportStep.Complete, 100, $"Conversion complete in {elapsed.TotalSeconds:F2}s");

            SimpleLogger.Log($"PNG conversion successful: {result.Length:N0} bytes, took {elapsed.TotalMilliseconds:F0}ms");

            return result;
        }
        catch (OperationCanceledException)
        {
            SimpleLogger.Log("PNG conversion cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("PNG conversion failed", ex);
            throw new InvalidOperationException($"Failed to convert SVG to PNG: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculates the dimensions of an image based on the specified bounds and export options.
    /// </summary>
    /// <remarks>The method applies scaling based on the <see cref="PngExportOptions.ScaleFactor"/> and DPI
    /// specified in the  <paramref name="options"/> parameter. If maximum dimensions are provided, the method ensures
    /// the calculated  dimensions do not exceed these limits. If <see cref="PngExportOptions.PreserveAspectRatio"/> is
    /// set to  <see langword="true"/>, the aspect ratio of the original bounds is maintained while respecting the
    /// maximum dimensions.</remarks>
    /// <param name="bounds">The bounding rectangle that defines the original dimensions of the image.</param>
    /// <param name="options">The export options that specify scaling factors, DPI, maximum dimensions, and aspect ratio preservation.</param>
    /// <returns>A tuple containing the calculated width and height of the image, adjusted according to the provided options.</returns>
    private static (int Width, int Height) CalculateDimensions(SKRect bounds, PngExportOptions options)
    {
        // Calculate base dimensions with scale factor
        float baseWidth = bounds.Width * options.ScaleFactor;
        float baseHeight = bounds.Height * options.ScaleFactor;

        // Apply DPI scaling (assuming 96 DPI as baseline)
        float dpiScale = options.Dpi / 96f;
        baseWidth *= dpiScale;
        baseHeight *= dpiScale;

        // Apply maximum dimensions if specified
        if (options is { MaxWidth: <= 0, MaxHeight: <= 0 })
        {
            return ((int)Math.Ceiling(baseWidth), (int)Math.Ceiling(baseHeight));
        }

        if (options.PreserveAspectRatio)
        {
            float aspectRatio = bounds.Width / bounds.Height;

            if (options.MaxWidth > 0 && baseWidth > options.MaxWidth)
            {
                baseWidth = options.MaxWidth;
                baseHeight = baseWidth / aspectRatio;
            }

            if (options.MaxHeight > 0 && baseHeight > options.MaxHeight)
            {
                baseHeight = options.MaxHeight;
                baseWidth = baseHeight * aspectRatio;
            }
        }
        else
        {
            if (options.MaxWidth > 0 && baseWidth > options.MaxWidth)
            {
                baseWidth = options.MaxWidth;
            }

            if (options.MaxHeight > 0 && baseHeight > options.MaxHeight)
            {
                baseHeight = options.MaxHeight;
            }
        }

        return ((int)Math.Ceiling(baseWidth), (int)Math.Ceiling(baseHeight));
    }

    /// <summary>
    /// Creates an <see cref="SKSurface"/> for rendering using software rendering.
    /// </summary>
    /// <remarks>
    /// This method uses software rendering only. Hardware acceleration (GPU) is not implemented because:
    ///     1. It requires OpenGL/Vulkan context setup which adds significant complexity
    ///     2. For server-side/offline SVG-to-PNG conversion, software rendering is more reliable and portable
    ///     3. Hardware acceleration provides minimal benefit for single-image conversion vs. real-time rendering
    ///     4. Software rendering produces consistent, high-quality results across all platforms
    /// </remarks>
    [MustDisposeResource(true)]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transferred to caller")]
    private static SKSurface CreateSurface(SKImageInfo imageInfo)
    {
        SimpleLogger.Log("Using software rendering for SVG conversion");
        return SKSurface.Create(imageInfo);
    }

    /// <summary>
    /// Creates a high-quality <see cref="SKPaint"/> object configured for rendering with antialiasing, high filter
    /// quality, and optimized text rendering settings.
    /// </summary>
    /// <param name="options">The <see cref="PngExportOptions"/> that specify rendering options, including whether antialiasing is
    /// enabled.</param>
    /// <returns>A configured <see cref="SKPaint"/> instance with high-quality rendering settings, including antialiasing,
    /// subpixel text rendering, and full hinting.</returns>
    [MustDisposeResource(true)]
    private static SKPaint CreateHighQualityPaint(PngExportOptions options)
    {
        SKPaint paint = new SKPaint
        {
            // IsAntialias is the primary quality control for DrawPicture - this is NOT obsolete
            IsAntialias = options.AntiAlias,

            // IsDither helps with smooth color gradients
            IsDither = false,

            // Color and blending
            Color = SKColors.Black,
            BlendMode = SKBlendMode.SrcOver
        };

        return paint;
    }

    /// <summary>
    /// Configures the specified <see cref="SKCanvas"/> for rendering by applying background color, scaling, and
    /// translation.
    /// </summary>
    /// <remarks>This method clears the canvas with the specified background color from <paramref
    /// name="options"/> or makes it transparent if no color is provided. It then applies scaling to fit the content
    /// within the specified dimensions and translates the canvas to align the content's origin with the top-left
    /// corner.</remarks>
    /// <param name="canvas">The <see cref="SKCanvas"/> to configure.</param>
    /// <param name="options">The export options that specify rendering settings, such as the background color.</param>
    /// <param name="width">The target width, in pixels, for the rendered output.</param>
    /// <param name="height">The target height, in pixels, for the rendered output.</param>
    /// <param name="bounds">The bounding rectangle of the content to be rendered, used to calculate scaling and translation.</param>
    private static void ConfigureCanvas(SKCanvas canvas, PngExportOptions options, int width, int height, SKRect bounds)
    {
        // Clear with background color
        if (!string.IsNullOrWhiteSpace(options.BackgroundColor))
        {
            SKColor color = ParseColor(options.BackgroundColor);
            canvas.Clear(color);
        }
        else
        {
            canvas.Clear(SKColors.Transparent);
        }

        // Save the current state
        canvas.Save();

        // Calculate and apply scaling to fill the target dimensions
        float scaleX = width / bounds.Width;
        float scaleY = height / bounds.Height;

        // Apply the scale transformation
        canvas.Scale(scaleX, scaleY);

        // Translate to origin if the SVG doesn't start at (0,0)
        const float epsilon = 0.0001F;
        if (Math.Abs(bounds.Left) > epsilon || Math.Abs(bounds.Top) > epsilon)
        {
            canvas.Translate(-bounds.Left, -bounds.Top);
        }
    }

    /// <summary>
    /// Parses a color string and returns the corresponding <see cref="SKColor"/> value.
    /// </summary>
    /// <remarks>The method supports the following formats: <list type="bullet">
    /// <item><description>Hexadecimal colors, starting with '#' (e.g., "#FF0000").</description></item>
    /// <item><description>RGB/RGBA colors, starting with "rgb" or "rgba" (e.g., "rgb(255,0,0)").</description></item>
    /// <item><description>Named colors, such as "red", "blue", "lightgray", and "transparent".</description></item>
    /// </list> If the input string does not match any of these formats, or if an error occurs during parsing,  the
    /// method logs the error and returns <see cref="SKColors.White"/>.</remarks>
    /// <param name="colorString">A string representing the color. This can be a hexadecimal color (e.g., "#FF0000"),  an RGB/RGBA color (e.g.,
    /// "rgb(255,0,0)" or "rgba(255,0,0,0.5)"), or a named color  (e.g., "red", "blue", "lightgray"). The string is
    /// case-insensitive and may include  leading or trailing whitespace.</param>
    /// <returns>The <see cref="SKColor"/> corresponding to the specified color string. If the string  is invalid or cannot be
    /// parsed, the method returns <see cref="SKColors.White"/>.</returns>
    private static SKColor ParseColor(string colorString)
    {
        ReadOnlySpan<char> colorSpan = colorString.AsSpan().Trim();

        try
        {
            // Handle hex colors (#RGB, #RRGGBB, #AARRGGBB)
            if (colorSpan.StartsWith('#'))
            {
                return SKColor.Parse(colorString);
            }

            // Handle rgb()/rgba() CSS formats - e.g.:
            // - rgb(255,0,0), rgba(255,0,0,0.5)
            // - rgb(255 0 0 / 50%), rgb(100% 0% 0%)
            if (colorSpan.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseRgbColor(colorSpan, out SKColor color))
                {
                    return color;
                }

                SimpleLogger.Log($"Failed to parse rgb/rgba format: {colorString}");
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
            SimpleLogger.Log($"Failed to parse color '{colorString}': {ex.Message}");
            return SKColors.White;
        }
    }

    /// <summary>
    /// Attempts to parse an RGB or RGBA color from a string representation and returns the result as an <see
    /// cref="SKColor"/>.
    /// </summary>
    /// <remarks>This method supports CSS-style color formats, including both "rgb" and "rgba" notations. The
    /// alpha component, if provided, must be a valid value between 0 and 1 (inclusive). If the input string is not in a
    /// valid format or contains invalid components, the method returns <see langword="false"/>.</remarks>
    /// <param name="colorSpan">A <see cref="ReadOnlySpan{T}"/> of characters representing the color in the format "rgb(r, g, b)" or "rgba(r, g,
    /// b, a)". The components can be separated by commas, spaces, tabs, or slashes, and the alpha component is
    /// optional.</param>
    /// <param name="color">When this method returns, contains the parsed <see cref="SKColor"/> if the parsing succeeds; otherwise, the
    /// default value of <see cref="SKColor"/>.</param>
    /// <returns><see langword="true"/> if the color was successfully parsed; otherwise, <see langword="false"/>.</returns>
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

        // Parse the color components
        // Split by comma, space, tab, or slash (CSS4 allows "rgb(255 0 0 / 0.5)")
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

        // Parse optional alpha component (defaults to fully opaque)
        byte a = 255;
        if (count == 4)
        {
            if (!TryParseAlphaComponent(content[ranges[3]], out a))
            {
                return false;
            }
        }

        color = new SKColor(r, g, b, a);
        return true;
    }

    /// <summary>
    /// Attempts to parse a color component from a string representation and convert it to a byte value.
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
    /// Attempts to parse an alpha (opacity) component from a string representation.
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
    private static void ReportProgress(IProgress<ExportProgress>? progress, ExportStep step, int percent, string message)
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
                    SimpleLogger.LogError($"Progress report failed: {ex.Message}", ex);
                }
            });
        }
    }
}
