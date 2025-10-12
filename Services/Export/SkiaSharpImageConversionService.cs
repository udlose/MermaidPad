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

using JetBrains.Annotations;
using SkiaSharp;
using Svg.Skia;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace MermaidPad.Services.Export;
/// <summary>
/// SkiaSharp-based implementation of image conversion service
/// </summary>
public sealed class SkiaSharpImageConversionService : IImageConversionService
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

            using SKSurface surface = CreateSurface(imageInfo, options) ??
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
    /// Creates a rendering surface with optional hardware acceleration
    /// </summary>
    /// <remarks>
    /// FIXED: Now uses PngExportOptions to determine quality settings.
    /// Future enhancement: Could use options.Quality to determine hardware vs software rendering.
    /// </remarks>
    [MustDisposeResource(true)]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transferred to caller")]
    private static SKSurface CreateSurface(SKImageInfo imageInfo, PngExportOptions options)
    {
        // For highest quality (95+), prefer software rendering which can be more accurate
        // For lower quality, hardware acceleration can be faster
        bool preferSoftware = options.Quality >= 95;

        if (!preferSoftware && (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
        {
            try
            {
                SKSurface? surface = SKSurface.Create(imageInfo);
                if (surface is not null)
                {
                    SimpleLogger.Log("Using hardware-accelerated rendering");
                    return surface;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Hardware acceleration unavailable: {ex.Message}");
            }
        }

        // Fallback to software rendering
        SimpleLogger.Log("Using software rendering");
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
    private static SKPaint CreateHighQualityPaint(PngExportOptions options)
    {
        SKPaint paint = new SKPaint
        {
            IsAntialias = options.AntiAlias,
            FilterQuality = SKFilterQuality.High,
            IsDither = false,

            // These settings are critical for text rendering
            SubpixelText = true,
            LcdRenderText = true,
            HintingLevel = SKPaintHinting.Full,

            // Color and blending
            Color = SKColors.Black,
            BlendMode = SKBlendMode.SrcOver
        };

        return paint;
    }

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

        // FIXED: Proper canvas configuration for high-quality rendering
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

    private static SKColor ParseColor(string colorString)
    {
        ReadOnlySpan<char> colorSpan = colorString.AsSpan().Trim();

        try
        {
            // Handle hex colors
            if (colorSpan.StartsWith('#'))
            {
                return SKColor.Parse(colorString);
            }

            // Handle rgb/rgba
            if (colorSpan.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Implement full rgb()/rgba() parsing if needed
                // Simple parsing - you might want to enhance this
                // For now, default to white for rgb() format
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

    private static void ReportProgress(IProgress<ExportProgress>? progress, ExportStep step, int percent, string message)
    {
        progress?.Report(new ExportProgress
        {
            Step = step,
            PercentComplete = percent,
            Message = message
        });
    }
}
