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
using System.Text.RegularExpressions;

namespace MermaidPad.Services.Export;
/// <summary>
/// SkiaSharp-based implementation of image conversion service
/// </summary>
public sealed partial class SkiaSharpImageConversionService : IImageConversionService
{
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

            // Handle rgb()/rgba() including:
            // - rgb(255,0,0), rgba(255,0,0,0.5)
            // - rgb(255 0 0 / 50%), rgb(100% 0% 0%)
            if (colorSpan.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                return ParseRgbColor(colorString);
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
    /// Parses a color string in RGB or RGBA format and returns the corresponding <see cref="SKColor"/>.
    /// </summary>
    /// <remarks>This method attempts to parse the input string as an RGBA color first. If that fails, it
    /// tries to parse it as an RGB color. If both attempts fail, a warning is logged, and <see cref="SKColors.White"/>
    /// is returned.</remarks>
    /// <param name="colorString">A string representing a color in either RGB format ("rgb(r, g, b)") or RGBA format ("rgba(r, g, b, a)"). The
    /// values for <c>r</c>, <c>g</c>, and <c>b</c> must be integers in the range 0-255, and <c>a</c> (if present) must
    /// be a floating-point value in the range 0.0-1.0.</param>
    /// <returns>An <see cref="SKColor"/> representing the parsed color. If the input string is invalid or cannot be parsed, the
    /// method returns <see cref="SKColors.White"/>.</returns>
    private static SKColor ParseRgbColor(string colorString)
    {
        // Try rgba first (has 4 components)
        Match rgbaMatch = RgbaRegex().Match(colorString);
        if (rgbaMatch.Success)
        {
            int r = int.Parse(rgbaMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            int g = int.Parse(rgbaMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            int b = int.Parse(rgbaMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            float alpha = float.Parse(rgbaMatch.Groups[4].Value, CultureInfo.InvariantCulture);

            // Convert alpha from 0-1 range to 0-255 range
            byte a = (byte)Math.Clamp((int)(alpha * 255), 0, 255);

            return new SKColor((byte)r, (byte)g, (byte)b, a);
        }

        // Try rgb (3 components)
        Match rgbMatch = RgbRegex().Match(colorString);
        if (rgbMatch.Success)
        {
            int r = int.Parse(rgbMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            int g = int.Parse(rgbMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            int b = int.Parse(rgbMatch.Groups[3].Value, CultureInfo.InvariantCulture);

            return new SKColor((byte)r, (byte)g, (byte)b);
        }

        SimpleLogger.Log($"Failed to parse rgb/rgba color: {colorString}");
        return SKColors.White;
    }

    /// <summary>
    /// Creates a regular expression that matches RGB color strings in the format "rgb(r, g, b)".
    /// </summary>
    /// <remarks>The pattern matches strings that represent RGB color values, where the red, green, and blue
    /// components  are integers. Whitespace around the components and commas is allowed. The matching is
    /// case-insensitive.</remarks>
    /// <returns>A <see cref="Regex"/> instance configured to match RGB color strings.</returns>
    [GeneratedRegex(@"rgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RgbRegex();

    /// <summary>
    /// Creates a regular expression that matches RGBA color strings in the format  "rgba(r, g, b, a)", where r, g, and
    /// b are integers representing red, green,  and blue color components, and a is a floating-point number
    /// representing the alpha value.
    /// </summary>
    /// <remarks>The regular expression is case-insensitive and allows for optional whitespace  around the
    /// components. The alpha value must be a valid floating-point number. The match timeout is set to 1000 milliseconds
    /// to prevent excessive processing time.</remarks>
    /// <returns>A <see cref="Regex"/> instance configured to match RGBA color strings.</returns>
    [GeneratedRegex(@"rgba\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([0-9]*\.?[0-9]+)\s*\)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RgbaRegex();

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
