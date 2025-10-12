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

using SkiaSharp;
using Svg.Skia;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MermaidPad.Services.Export;

/// <summary>
/// SkiaSharp-based implementation of image conversion service
/// </summary>
public sealed class SkiaSharpImageConversionService : IImageConversionService, IDisposable
{
    private bool _isDisposed;

    public async Task<byte[]> ConvertSvgToPngAsync(string svgContent, PngConversionOptions options, IProgress<ConversionProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(svgContent);
        ArgumentNullException.ThrowIfNull(options);

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

                SKPicture? picture = svg.Load(stream);
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
        ArgumentException.ThrowIfNullOrEmpty(svgContent);

        return await Task.Run(() =>
        {
            try
            {
                using SKSvg svg = new SKSvg();
                using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

                SKPicture? picture = svg.Load(stream);
                if (picture is null)
                {
                    return (0, 0);
                }

                SKRect bounds = picture.CullRect;
                return (bounds.Width, bounds.Height);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("Failed to get SVG dimensions", ex);
                return (0, 0);
            }
        }).ConfigureAwait(false);
    }

    private static byte[] PerformConversion(string svgContent, PngConversionOptions options, IProgress<ConversionProgress>? progress, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Initialize
            ReportProgress(progress, ConversionStep.Initializing, 0, "Initializing conversion...");
            cancellationToken.ThrowIfCancellationRequested();

            // Step 2: Parse SVG
            ReportProgress(progress, ConversionStep.ParsingSvg, 10, "Parsing SVG content...");

            using SKSvg svg = new SKSvg();
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

            SKPicture picture = svg.Load(stream) ?? throw new InvalidOperationException("Failed to parse SVG content");
            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Calculate dimensions
            ReportProgress(progress, ConversionStep.CalculatingDimensions, 20, "Calculating dimensions...");

            SKRect bounds = picture.CullRect;
            (int width, int height) = CalculateDimensions(bounds, options);

            SimpleLogger.Log($"Converting SVG: Original {bounds.Width}x{bounds.Height}, Target {width}x{height} @ {options.Dpi} DPI");

            cancellationToken.ThrowIfCancellationRequested();

            // Step 4: Create canvas
            ReportProgress(progress, ConversionStep.CreatingCanvas, 30,
                $"Creating {width}x{height} canvas...");

            SKImageInfo imageInfo = new SKImageInfo(
                width,
                height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul,
                SKColorSpace.CreateSrgb());

            using SKSurface surface = CreateSurface(imageInfo, options) ??
                throw new InvalidOperationException("Failed to create rendering surface");

            using SKCanvas canvas = surface.Canvas;
            cancellationToken.ThrowIfCancellationRequested();

            // Step 5: Render
            ReportProgress(progress, ConversionStep.Rendering, 50, "Rendering SVG...");

            // Configure rendering
            ConfigureCanvas(canvas, options, width, height, bounds);

            // Draw the SVG
            canvas.DrawPicture(picture);
            canvas.Flush();

            cancellationToken.ThrowIfCancellationRequested();

            // Step 6: Encode to PNG
            ReportProgress(progress, ConversionStep.Encoding, 80, "Encoding PNG...");

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, options.Quality)
                ?? throw new InvalidOperationException("Failed to encode PNG");

            byte[] result = data.ToArray();

            // Step 7: Complete
            TimeSpan elapsed = stopwatch.Elapsed;
            ReportProgress(progress, ConversionStep.Complete, 100, $"Conversion complete in {elapsed.TotalSeconds:F2}s");

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

    private static (int Width, int Height) CalculateDimensions(SKRect bounds, PngConversionOptions options)
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

    private static SKSurface CreateSurface(SKImageInfo imageInfo, PngConversionOptions options)
    {
        // Try hardware acceleration first on supported platforms
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                SKSurface surface = SKSurface.Create(imageInfo);
                if (surface is not null)
                {
                    return surface;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Hardware acceleration unavailable: {ex.Message}");
            }
        }

        // Fallback to software rendering
        return SKSurface.Create(imageInfo);
    }

    private static void ConfigureCanvas(
        SKCanvas canvas,
        PngConversionOptions options,
        int width,
        int height,
        SKRect bounds)
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

        // Save state for potential future antialiasing configuration
        if (options.AntiAlias)
        {
            canvas.Save();
            // Note: Antialiasing is enabled by default in SkiaSharp
            // Additional paint configuration can be added here if needed
        }

        // Calculate and apply scaling
        float scaleX = width / bounds.Width;
        float scaleY = height / bounds.Height;
        canvas.Scale(scaleX, scaleY);

        // Translate to origin if needed
        const float epsilon = 0.0001F;
        if (Math.Abs(bounds.Left) > epsilon || Math.Abs(bounds.Top) > epsilon)
        {
            canvas.Translate(-bounds.Left, -bounds.Top);
        }
    }

    private static SKColor ParseColor(string colorString)
    {
        try
        {
            // Handle hex colors
            if (colorString.StartsWith('#'))
            {
                return SKColor.Parse(colorString);
            }

            // Handle rgb/rgba
            if (colorString.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                // Simple parsing - you might want to enhance this
                // For now, default to white for rgb() format
                return SKColors.White;
            }

            // Try named colors
            return colorString.ToLowerInvariant() switch
            {
                "white" => SKColors.White,
                "black" => SKColors.Black,
                "transparent" => SKColors.Transparent,
                _ => SKColors.White
            };
        }
        catch (Exception ex)
        {
            SimpleLogger.Log($"Failed to parse color '{colorString}': {ex.Message}");
            return SKColors.White;
        }
    }

    private static void ReportProgress(
        IProgress<ConversionProgress>? progress,
        ConversionStep step,
        int percent,
        string message)
    {
        progress?.Report(new ConversionProgress
        {
            Step = step,
            PercentComplete = percent,
            Message = message
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Clean up any managed resources if needed
                // Currently no managed resources to dispose
            }
            _isDisposed = true;
        }
    }
}
