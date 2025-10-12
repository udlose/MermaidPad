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

using System.Text.Json;

namespace MermaidPad.Services.Export;
/// <summary>
/// Service for exporting Mermaid diagrams to various formats
/// </summary>
public sealed class ExportService
{
    private readonly MermaidRenderer _mermaidRenderer;

    public ExportService(MermaidRenderer mermaidRenderer)
    {
        _mermaidRenderer = mermaidRenderer;
    }

    /// <summary>
    /// Exports the current diagram as SVG
    /// </summary>
    public async Task ExportSvgAsync(string targetPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        SimpleLogger.Log($"Starting SVG export to: {targetPath}");

        try
        {
            // Get SVG content from the WebView - this must run on UI thread
            string? svgContent = await GetSvgContentAsync();

            if (string.IsNullOrWhiteSpace(svgContent))
            {
                throw new InvalidOperationException("Failed to extract SVG content from diagram");
            }

            // File I/O can be done on background thread
            await Task.Run(async () =>
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write SVG content - ConfigureAwait(false) is OK here
                await File.WriteAllTextAsync(targetPath, svgContent, Encoding.UTF8, cancellationToken)
                    .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            SimpleLogger.Log($"SVG exported successfully: {new FileInfo(targetPath).Length:N0} bytes");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"SVG export failed: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Exports the current diagram as PNG with configurable options
    /// </summary>
    public async Task ExportPngAsync(
        string targetPath,
        PngExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        options ??= new PngExportOptions();
        SimpleLogger.Log($"Starting PNG export to: {targetPath} (DPI: {options.Dpi}, Scale: {options.ScaleFactor}x)");

        try
        {
            // Report initial progress
            progress?.Report(new ExportProgress
            {
                Step = ExportStep.Initializing,
                PercentComplete = 0,
                Message = "Initializing export..."
            });

            // Get SVG content from the WebView
            string? svgContent = await GetSvgContentAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(svgContent))
            {
                throw new InvalidOperationException("Failed to extract SVG content from diagram");
            }

            // Convert to PNG on background thread
            byte[] pngData = await Task.Run(() =>
                ConvertSvgToPng(svgContent, options, progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            // Ensure directory exists
            string? directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write PNG data
            await File.WriteAllBytesAsync(targetPath, pngData, cancellationToken)
                .ConfigureAwait(false);

            // Report completion
            progress?.Report(new ExportProgress
            {
                Step = ExportStep.Complete,
                PercentComplete = 100,
                Message = "Export completed successfully!"
            });

            SimpleLogger.Log($"PNG exported successfully: {pngData.Length:N0} bytes");
        }
        catch (OperationCanceledException)
        {
            SimpleLogger.Log("PNG export cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"PNG export failed: {ex.Message}", ex);
            throw;
        }
    }

    public async Task<string?> GetCurrentSvgContentAsync()
    {
        // This method needs to run on UI thread (WebView access)
        return await GetSvgContentAsync();
    }

    private async Task<string?> GetSvgContentAsync()
    {
        const string script =
            """
            (function() {
                const svgElement = document.querySelector('#output svg');
                if (!svgElement) return null;
                
                // Clone to avoid modifying the original
                const clone = svgElement.cloneNode(true);
                
                // Add XML declaration and doctype
                const serializer = new XMLSerializer();
                const svgString = serializer.serializeToString(clone);
                
                // Return complete SVG document
                return '<?xml version="1.0" encoding="UTF-8"?>' + svgString;
            })();
            """;

        // ExecuteScriptAsync needs UI thread - NO ConfigureAwait(false) here
        string? result = await _mermaidRenderer.ExecuteScriptAsync(script);

        // Remove any JSON escaping if present
        if (!string.IsNullOrWhiteSpace(result) && result.StartsWith('"') && result.EndsWith('"'))
        {
            result = JsonSerializer.Deserialize<string>(result) ?? result;
        }

        return result;
    }

    private static byte[] ConvertSvgToPng(
        string svgContent,
        PngExportOptions options,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Parse SVG
            progress?.Report(new ExportProgress
            {
                Step = ExportStep.ParsingSvg,
                PercentComplete = 10,
                Message = "Parsing SVG content..."
            });

            cancellationToken.ThrowIfCancellationRequested();

            using SKSvg svg = new SKSvg();
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));
            using SKPicture picture = svg.Load(stream)
                ?? throw new InvalidOperationException("Failed to parse SVG content");

            // Step 2: Calculate dimensions
            progress?.Report(new ExportProgress
            {
                Step = ExportStep.CalculatingDimensions,
                PercentComplete = 20,
                Message = "Calculating output dimensions..."
            });

            SKRect bounds = picture.CullRect;
            (int width, int height) = CalculateDimensions(bounds, options);

            SimpleLogger.Log($"PNG dimensions: {width}x{height} (from SVG {bounds.Width}x{bounds.Height})");
            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Create canvas
            progress?.Report(new ExportProgress
            {
                Step = ExportStep.CreatingCanvas,
                PercentComplete = 30,
                Message = $"Creating {width}x{height} canvas..."
            });

            SKImageInfo imageInfo = new SKImageInfo(
                width,
                height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul,
                SKColorSpace.CreateSrgb());

            using SKSurface surface = CreateSurface(imageInfo) ?? throw new InvalidOperationException("Failed to create rendering surface");
            using SKCanvas canvas = surface.Canvas;
            cancellationToken.ThrowIfCancellationRequested();

            // Step 4: Render
            progress?.Report(new ExportProgress
            {
                Step = ExportStep.Rendering,
                PercentComplete = 50,
                Message = "Rendering diagram..."
            });

            ConfigureCanvas(canvas, options, width, height, bounds);
            canvas.DrawPicture(picture);
            canvas.Flush();

            cancellationToken.ThrowIfCancellationRequested();

            // Step 5: Encode to PNG
            progress?.Report(new ExportProgress
            {
                Step = ExportStep.Encoding,
                PercentComplete = 80,
                Message = "Encoding PNG..."
            });

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, options.Quality)
                ?? throw new InvalidOperationException("Failed to encode PNG");

            byte[] result = data.ToArray();

            stopwatch.Stop();
            SimpleLogger.Log($"PNG conversion completed in {stopwatch.ElapsedMilliseconds}ms");

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SimpleLogger.LogError($"PNG conversion failed: {ex.Message}", ex);
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
        if (options.MaxWidth > 0 || options.MaxHeight > 0)
        {
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
        }

        return ((int)Math.Ceiling(baseWidth), (int)Math.Ceiling(baseHeight));
    }

    private static SKSurface? CreateSurface(SKImageInfo imageInfo)
    {
        // Try hardware acceleration first on supported platforms
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                SKSurface? surface = SKSurface.Create(imageInfo);
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

    private static void ConfigureCanvas(SKCanvas canvas, PngExportOptions options,
        int width, int height, SKRect bounds)
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

        // Configure anti-aliasing
        if (options.AntiAlias)
        {
            canvas.Save();
            // Note: The paint object was created but never used in the original code
            // Keeping the canvas.Save() for potential future use
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

            // Try named colors
            return colorString.ToLowerInvariant() switch
            {
                "white" => SKColors.White,
                "black" => SKColors.Black,
                "transparent" => SKColors.Transparent,
                _ => SKColors.White
            };
        }
        catch
        {
            return SKColors.White;
        }
    }
}

///// <summary>
///// Extension methods for ExportService
///// </summary>
//public static class ExportServiceExtensions
//{
//    /// <summary>
//    /// Exports a diagram with the specified options, showing a progress dialog if requested
//    /// </summary>
//    public static async Task ExportDiagramAsync(this ExportService exportService, Window window, ExportOptions options)
//    {
//        ArgumentNullException.ThrowIfNull(options);
//        ArgumentNullException.ThrowIfNull(window);

//        try
//        {
//            if (options.ShowProgress && options.Format == ExportFormat.PNG)
//            {
//                // Create and show progress dialog
//                ProgressDialog progressDialog = new ProgressDialog();
//                ProgressDialogViewModel progressViewModel = new ProgressDialogViewModel
//                {
//                    Title = "Exporting PNG",
//                    StatusMessage = "Preparing export..."
//                };

//                progressDialog.DataContext = progressViewModel;

//                // Set up cancellation
//                CancellationTokenSource cts = new CancellationTokenSource();
//                if (options.AllowCancellation)
//                {
//                    progressViewModel.SetCancellationTokenSource(cts);
//                }

//                // Start export task
//                Task exportTask = Task.Run(async () =>
//                {
//                    await exportService.ExportPngAsync(
//                        options.FilePath,
//                        options.PngOptions,
//                        progressViewModel,
//                        cts.Token).ConfigureAwait(false);
//                });

//                // Show dialog and wait for export
//                progressDialog.ShowDialog(window);
//                await exportTask.ConfigureAwait(false);

//                progressDialog.Close();
//            }
//            else if (options.Format == ExportFormat.PNG)
//            {
//                // Export PNG without progress
//                await exportService.ExportPngAsync(
//                    options.FilePath,
//                    options.PngOptions).ConfigureAwait(false);
//            }
//            else if (options.Format == ExportFormat.SVG)
//            {
//                // Export SVG
//                await exportService.ExportSvgAsync(options.FilePath).ConfigureAwait(false);
//            }
//            else
//            {
//                throw new NotSupportedException($"Export format {options.Format} is not supported");
//            }
//        }
//        catch (Exception ex)
//        {
//            SimpleLogger.LogError($"Export failed: {ex.Message}", ex);
//            throw;
//        }
//    }
//}
