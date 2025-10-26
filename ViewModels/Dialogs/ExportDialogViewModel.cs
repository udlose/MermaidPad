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

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Services;
using MermaidPad.Services.Export;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.Dialogs;

/// <summary>
/// Represents the view model for the export dialog, providing properties and commands for configuring and executing
/// export operations such as file format, quality, dimensions, and destination.
/// </summary>
/// <remarks>This view model is designed for use with MVVM frameworks and supports data binding to UI elements in
/// the export dialog. It exposes export configuration options, manages validation and user prompts, and provides
/// methods for browsing directories and confirming export actions. The available export formats and DPI values are
/// initialized at construction. The view model updates estimated file size and dimensions based on the current settings
/// and selected format. All properties and commands are intended to be accessed by the view for user
/// interaction.</remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class ExportDialogViewModel : ViewModelBase
{
    private static readonly string[] _fileSizes = ["B", "KB", "MB", "GB", "TB"];
    private readonly IImageConversionService? _imageConversionService;
    private readonly ExportService? _exportService;
    internal IStorageProvider? StorageProvider { get; private set; }

    // Cached SVG dimensions
    private float _actualSvgWidth;
    private float _actualSvgHeight;
    private bool _dimensionsLoaded;

    [ObservableProperty]
    public partial string FileName { get; set; } = "diagram";

    [ObservableProperty]
    public partial string Directory { get; set; } = GetValidDocumentsPath();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPngSelected))]
    [NotifyPropertyChangedFor(nameof(IsSvgSelected))]
    [NotifyPropertyChangedFor(nameof(FullFilePath))]
    [NotifyPropertyChangedFor(nameof(HasDimensionInfo))]
    public partial ExportFormatItem? SelectedFormat { get; set; }

    [ObservableProperty]
    public partial int SelectedDpi { get; set; } = 150;

    [ObservableProperty]
    public partial float ScaleFactor { get; set; } = 2.0f;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseTransparentBackground))]
    public partial bool UseWhiteBackground { get; set; } = true;

    [ObservableProperty]
    public partial int Quality { get; set; } = 95;

    [ObservableProperty]
    public partial bool AntiAlias { get; set; } = true;

    [ObservableProperty]
    public partial int MaxWidth { get; set; } = 0;

    [ObservableProperty]
    public partial int MaxHeight { get; set; } = 0;

    [ObservableProperty]
    public partial bool PreserveAspectRatio { get; set; } = true;

    [ObservableProperty]
    public partial bool OptimizeSvg { get; set; } = false;

    [ObservableProperty]
    public partial string EstimatedDimensions { get; set; } = "Loading...";

    [ObservableProperty]
    public partial string EstimatedFileSize { get; set; } = "Loading...";

    [ObservableProperty]
    public partial bool? DialogResult { get; private set; }

    public ObservableCollection<ExportFormatItem> AvailableFormats { get; }

    public ObservableCollection<int> AvailableDpiValues { get; }

    public bool IsPngSelected => SelectedFormat?.Format == ExportFormat.PNG;

    public bool IsSvgSelected => SelectedFormat?.Format == ExportFormat.SVG;

    public bool UseTransparentBackground => !UseWhiteBackground;

    public bool HasDimensionInfo => IsPngSelected;

    /// <summary>
    /// Gets the full file path, including the file name and extension, for the export operation based on the selected
    /// format.
    /// </summary>
    public string FullFilePath
    {
        get
        {
            string extension = SelectedFormat?.Format == ExportFormat.PNG ? ".png" : ".svg";
            string cleanFileName = Path.GetFileNameWithoutExtension(FileName);
            return Path.Combine(Directory, $"{cleanFileName}{extension}");
        }
    }

    /// <summary>
    /// Initializes a new instance of the ExportDialogViewModel class with the specified image conversion and export
    /// services.
    /// </summary>
    /// <remarks>The constructor initializes the available export formats and DPI values for the export
    /// dialog. If either service is null, related functionality may be limited.</remarks>
    /// <param name="imageConversionService">The service used to perform image format conversions. Can be null if image conversion is not required.</param>
    /// <param name="exportService">The service responsible for handling export operations. Can be null if export functionality is not needed.</param>
    public ExportDialogViewModel(IImageConversionService? imageConversionService, ExportService? exportService)
    {
        _imageConversionService = imageConversionService;
        _exportService = exportService;
        AvailableFormats = new ObservableCollection<ExportFormatItem>
        {
            new ExportFormatItem { Format = ExportFormat.SVG, Description= "SVG (Scalable Vector Graphics)" },
            new ExportFormatItem{ Format = ExportFormat.PNG, Description= "PNG (Portable Network Graphics)" }
        };

        SelectedFormat = AvailableFormats[0];

        // Initialize available DPI values
#pragma warning disable IDE0028
        AvailableDpiValues = new ObservableCollection<int> { 72, 150, 300, 600 };
#pragma warning restore IDE0028

        // Load actual SVG dimensions asynchronously
        _ = LoadActualSvgDimensionsAsync();
    }

    /// <summary>
    /// Sets the storage provider to be used for subsequent storage operations.
    /// </summary>
    /// <param name="storageProvider">The storage provider to use for data storage operations. Specify <see langword="null"/> to remove the current
    /// storage provider.</param>
    public void SetStorageProvider(IStorageProvider? storageProvider)
    {
        StorageProvider = storageProvider;
    }

    /// <summary>
    /// Creates and returns an ExportOptions object that reflects the current export settings.
    /// </summary>
    /// <remarks>The returned ExportOptions object includes only the options relevant to the selected export
    /// format. For example, PngOptions is populated only if a PNG export is selected, and SvgOptions is populated only
    /// if an SVG export is selected.</remarks>
    /// <returns>An ExportOptions instance containing the current file path, format, and any applicable PNG or SVG export
    /// options.</returns>
    public ExportOptions GetExportOptions()
    {
        return new ExportOptions
        {
            FilePath = FullFilePath,
            Format = SelectedFormat?.Format ?? ExportFormat.SVG,
            PngOptions = IsPngSelected ? new PngExportOptions
            {
                Dpi = SelectedDpi,
                ScaleFactor = ScaleFactor,
                BackgroundColor = UseWhiteBackground ? "#FFFFFF" : null,
                Quality = Quality,
                AntiAlias = AntiAlias,
                MaxWidth = MaxWidth,
                MaxHeight = MaxHeight,
                PreserveAspectRatio = PreserveAspectRatio
            } : null,
            SvgOptions = IsSvgSelected ? new SvgExportOptions
            {
                Optimize = OptimizeSvg
            } : null
        };
    }

    /// <summary>
    /// Asynchronously loads the actual dimensions of the current SVG content and updates the internal state.
    /// </summary>
    /// <remarks>This method retrieves the current SVG content using the export service and calculates its
    /// dimensions  using the image conversion service. If the dimensions are successfully retrieved and valid, they are
    /// stored internally and used to update estimates. If the operation fails or the dimensions are invalid,  default
    /// dimensions of 800x600 are used as a fallback.</remarks>
    /// <returns></returns>
    private async Task LoadActualSvgDimensionsAsync()
    {
        //TODO - DaveBlack: what should i do with this?
        //try
        //{
        //    if (_exportService is not null)
        //    {
        //        // Get the current SVG content
        //        ReadOnlyMemory<char> svgContent = await _exportService.GetSvgContentAsync();

        //        if (!svgContent.IsEmpty && _imageConversionService is not null)
        //        {
        //            // Get actual dimensions from the SVG
        //            (float width, float height) = await _imageConversionService.GetSvgDimensionsAsync(svgContent);

        //            if (width > 0 && height > 0)
        //            {
        //                _actualSvgWidth = width;
        //                _actualSvgHeight = height;
        //                _dimensionsLoaded = true;

        //                // Update estimates with real dimensions
        //                UpdateEstimates();
        //                return;
        //            }
        //        }
        //    }
        //}
        //catch (Exception ex)
        //{
        //    Debug.WriteLine($"Failed to load SVG dimensions: {ex}");
        //    SimpleLogger.LogError($"Failed to load SVG dimensions: {ex}");
        //}

        // Fallback to default dimensions if loading fails
        _actualSvgWidth = 800;
        _actualSvgHeight = 600;
        _dimensionsLoaded = true;
        UpdateEstimates();
    }

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnFileNameChanged(string value) => OnPropertyChanged(nameof(FullFilePath));

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnDirectoryChanged(string value) => OnPropertyChanged(nameof(FullFilePath));

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnSelectedFormatChanged(ExportFormatItem? value) => UpdateEstimates();

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnSelectedDpiChanged(int value) => UpdateEstimates();

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnScaleFactorChanged(float value) => UpdateEstimates();

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnQualityChanged(int value) => UpdateEstimates();

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnMaxWidthChanged(int value) => UpdateEstimates();

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnMaxHeightChanged(int value) => UpdateEstimates();

    [SuppressMessage("ReSharper", "UnusedParameterInPartialMethod", Justification = "Used implicitly for property change notification.")]
    partial void OnUseWhiteBackgroundChanged(bool value) => UpdateEstimates();

    /// <summary>
    /// Opens a folder picker dialog to allow the user to select a directory.
    /// </summary>
    /// <remarks>This method uses the configured <see cref="StorageProvider"/> to display a folder picker
    /// dialog.  If the user selects a folder, the <see cref="Directory"/> property is updated with the path of the
    /// selected folder. If no folder is selected or the <see cref="StorageProvider"/> is <see langword="null"/>, the
    /// method does nothing.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task BrowseForDirectoryAsync()
    {
        if (StorageProvider is null)
        {
            return;
        }

        FolderPickerOpenOptions options = new FolderPickerOpenOptions
        {
            Title = "Select Export Directory",
            AllowMultiple = false
        };

        IReadOnlyList<IStorageFolder> result = await StorageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0)
        {
            Directory = result[0].Path.LocalPath;
        }
    }

    /// <summary>
    /// Validates the export file name and prompts the user to confirm overwriting if the file already exists before
    /// proceeding with the export operation.
    /// </summary>
    /// <remarks>The export operation will not proceed if the file name is invalid or contains invalid
    /// characters. If a file with the specified name already exists, the user is prompted to confirm overwriting the
    /// file. The export is only initiated if all validations pass and the user confirms any necessary
    /// overwrite.</remarks>
    /// <returns>A task that represents the asynchronous export validation operation.</returns>
    [RelayCommand]
    private async Task ExportAsync()
    {
        // Validate filename is not empty
        string cleanFileName = Path.GetFileNameWithoutExtension(FileName);
        if (string.IsNullOrWhiteSpace(cleanFileName))
        {
            await ShowErrorMessageAsync("Invalid Filename", "Please enter a valid filename.");
            return;
        }

        // Validate filename doesn't contain invalid characters
        if (cleanFileName.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await ShowErrorMessageAsync("Invalid Filename", "Filename contains invalid characters. Please use only letters, numbers, spaces, hyphens, and underscores.");
            return;
        }

        // Check if file already exists and prompt for overwrite
        string fullPath = FullFilePath;
        if (File.Exists(fullPath))
        {
            bool overwrite = await ShowOverwriteConfirmationAsync(fullPath);
            if (!overwrite)
            {
                return; // User cancelled overwrite
            }
        }

        // Validation passed, proceed with export
        DialogResult = true;
    }

    /// <summary>
    /// Cancels the current operation and closes the dialog with a negative result.
    /// </summary>
    /// <remarks>Use this method to dismiss the dialog without applying any changes. This typically sets the
    /// dialog's result to indicate that the user chose to cancel or exit the operation.</remarks>
    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }

    /// <summary>
    /// Displays an error message dialog with the specified title and message on the UI thread.
    /// </summary>
    /// <remarks>This method must be called from a context where the UI thread is available. The dialog is
    /// modal and blocks interaction with the parent window until dismissed.</remarks>
    /// <param name="title">The title to display in the error dialog window. Cannot be null.</param>
    /// <param name="message">The error message to display in the dialog. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation of displaying the error dialog.</returns>
    private static async Task ShowErrorMessageAsync(string title, string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                // Create a simple error window
                Window errorWindow = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                StackPanel stackPanel = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 15
                };

                stackPanel.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                });

                Button okButton = new Button
                {
                    Content = "OK",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Width = 100
                };

                okButton.Click += (_, _) => errorWindow.Close();
                stackPanel.Children.Add(okButton);

                errorWindow.Content = stackPanel;

                // Get parent window
                Window? parentWindow = GetParentWindow();
                if (parentWindow is not null)
                {
                    await errorWindow.ShowDialog(parentWindow);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show error message: {ex}");
                SimpleLogger.LogError($"Failed to show error message: {ex}");
            }
        });
    }

    /// <summary>
    /// Displays a confirmation dialog to the user when attempting to overwrite an existing file.
    /// </summary>
    /// <remarks>If the dialog cannot be displayed due to an error, the method returns <see langword="false"/>
    /// by default. The dialog is shown on the UI thread and is modal to the parent window, if available.</remarks>
    /// <param name="filePath">The full path of the file that may be overwritten. Used to display the file name in the confirmation dialog.
    /// Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the user
    /// confirms to overwrite; otherwise, <see langword="false"/>.</returns>
    private static async Task<bool> ShowOverwriteConfirmationAsync(string filePath)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                // Create confirmation window
                Window confirmWindow = new Window
                {
                    Title = "Confirm Overwrite",
                    Width = 450,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                StackPanel stackPanel = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 15
                };

                stackPanel.Children.Add(new TextBlock
                {
                    Text = "File Already Exists",
                    FontSize = 16,
                    FontWeight = Avalonia.Media.FontWeight.Bold
                });

                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"The file already exists:{Environment.NewLine}{Environment.NewLine}{Path.GetFileName(filePath)}{Environment.NewLine}{Environment.NewLine}Do you want to overwrite it?",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                });

                StackPanel buttonPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 10
                };

                bool result = false;

                Button yesButton = new Button
                {
                    Content = "Yes, Overwrite",
                    Width = 120
                };
                yesButton.Click += (_, _) =>
                {
                    result = true;
                    confirmWindow.Close();
                };

                Button noButton = new Button
                {
                    Content = "No, Cancel",
                    Width = 120
                };
                noButton.Click += (_, _) =>
                {
                    result = false;
                    confirmWindow.Close();
                };

                buttonPanel.Children.Add(yesButton);
                buttonPanel.Children.Add(noButton);
                stackPanel.Children.Add(buttonPanel);

                confirmWindow.Content = stackPanel;

                // Get parent window
                Window? parentWindow = GetParentWindow();
                if (parentWindow is not null)
                {
                    await confirmWindow.ShowDialog(parentWindow);
                }

                return result;
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError($"Failed to show overwrite confirmation: {ex}");
                Debug.WriteLine($"Failed to show overwrite confirmation: {ex.Message}");
                return false; // Default to not overwriting on error
            }
        });
    }

    /// <summary>
    /// Retrieves the main window of the current Avalonia desktop application, if available.
    /// </summary>
    /// <remarks>This method returns <see langword="null"/> if the application is not running or does not use
    /// a classic desktop lifetime. Use this method to access the main window in scenarios where the application
    /// lifetime is known to be desktop-based.</remarks>
    /// <returns>The main <see cref="Window"/> instance if the application is running with a classic desktop lifetime; otherwise,
    /// <see langword="null"/>.</returns>
    private static Window? GetParentWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    /// <summary>
    /// Updates the estimated output dimensions and file size for the PNG export based on the current settings and
    /// source SVG properties.
    /// </summary>
    /// <remarks>This method recalculates the estimates whenever relevant export parameters change, such as
    /// scale factor, DPI, maximum dimensions, or quality settings. The results are reflected in the EstimatedDimensions
    /// and EstimatedFileSize properties. If the PNG export is not selected or the SVG dimensions are not yet loaded,
    /// the estimates are set to placeholder values.</remarks>
    private void UpdateEstimates()
    {
        if (!IsPngSelected)
        {
            EstimatedDimensions = "N/A";
            EstimatedFileSize = "N/A";
            return;
        }

        if (!_dimensionsLoaded)
        {
            EstimatedDimensions = "Loading...";
            EstimatedFileSize = "Loading...";
            return;
        }

        // Calculate estimated dimensions using ACTUAL SVG dimensions
        float baseWidth = _actualSvgWidth;
        float baseHeight = _actualSvgHeight;

        // Apply scale factor
        baseWidth *= ScaleFactor;
        baseHeight *= ScaleFactor;

        // Apply DPI scaling (96 DPI is standard screen resolution)
        float dpiScale = SelectedDpi / 96f;
        baseWidth *= dpiScale;
        baseHeight *= dpiScale;

        // Apply maximum dimension constraints
        if (MaxWidth > 0 && baseWidth > MaxWidth)
        {
            if (PreserveAspectRatio)
            {
                float ratio = MaxWidth / baseWidth;
                baseWidth = MaxWidth;
                baseHeight *= ratio;
            }
            else
            {
                baseWidth = MaxWidth;
            }
        }

        if (MaxHeight > 0 && baseHeight > MaxHeight)
        {
            if (PreserveAspectRatio)
            {
                float ratio = MaxHeight / baseHeight;
                baseHeight = MaxHeight;
                baseWidth *= ratio;
            }
            else
            {
                baseHeight = MaxHeight;
            }
        }

        int finalWidth = (int)Math.Ceiling(baseWidth);
        int finalHeight = (int)Math.Ceiling(baseHeight);

        EstimatedDimensions = $"{finalWidth} × {finalHeight} pixels (from {_actualSvgWidth:F0} × {_actualSvgHeight:F0} SVG)";

        // Estimate file size based on actual dimensions
        int bytesPerPixel = UseTransparentBackground ? 4 : 3;
        long uncompressedSize = (long)finalWidth * finalHeight * bytesPerPixel;

        // PNG compression estimation (more accurate based on quality setting)
        float compressionRatio = CalculateCompressionRatio(Quality, UseTransparentBackground);
        long estimatedBytes = (long)(uncompressedSize * compressionRatio);

        EstimatedFileSize = FormatFileSize(estimatedBytes);
    }

    /// <summary>
    /// Estimates the compression ratio for a PNG image based on the specified quality level and transparency setting.
    /// </summary>
    /// <remarks>The returned ratio is an empirical estimate and may vary depending on the actual image
    /// content. Transparent PNGs generally compress less efficiently than opaque ones.</remarks>
    /// <param name="quality">An integer representing the desired image quality, typically in the range 0 to 100. Higher values indicate
    /// better image quality and less aggressive compression.</param>
    /// <param name="hasTransparency">A value indicating whether the image contains transparency. Set to <see langword="true"/> if the image has
    /// transparency; otherwise, <see langword="false"/>.</param>
    /// <returns>A floating-point value representing the estimated compression ratio, where lower values indicate higher
    /// compression. The value reflects typical compression outcomes for diagram content.</returns>
    private static float CalculateCompressionRatio(int quality, bool hasTransparency)
    {
        // PNG compression is lossless but varies with content complexity
        // These are empirical estimates based on typical diagram content

        if (hasTransparency)
        {
            // Transparent PNGs typically compress less efficiently
            return quality switch
            {
                >= 95 => 0.35f,  // High quality, less aggressive compression
                >= 85 => 0.30f,  // Good quality
                >= 70 => 0.25f,  // Medium quality
                >= 50 => 0.20f,  // Lower quality
                _ => 0.15f       // Minimum quality
            };
        }
        else
        {
            // Opaque PNGs compress better
            return quality switch
            {
                >= 95 => 0.25f,  // High quality
                >= 85 => 0.20f,  // Good quality
                >= 70 => 0.15f,  // Medium quality
                >= 50 => 0.12f,  // Lower quality
                _ => 0.10f       // Minimum quality
            };
        }
    }

    /// <summary>
    /// Formats a file size, specified in bytes, into a human-readable string using appropriate size units (e.g., KB, MB, GB).
    /// </summary>
    /// <remarks>The returned string uses binary units (multiples of 1,024) and includes a tilde (~) to
    /// indicate an approximate value. For example, an input of 1,500 bytes returns "~1.46 KB".</remarks>
    /// <param name="bytes">The file size in bytes to format. Must be zero or greater.</param>
    /// <returns>A string representing the formatted file size with an approximate value and the appropriate unit.</returns>
    private static string FormatFileSize(long bytes)
    {
        int order = 0;
        double size = bytes;

        while (size >= 1_024 && order < _fileSizes.Length - 1)
        {
            order++;
            size /= 1_024;
        }

        // More precise formatting for better estimates
        return order == 0
            ? $"~{size:F0} {_fileSizes[order]}"
            : $"~{size:F2} {_fileSizes[order]}";
    }

    /// <summary>
    /// Retrieves a valid file system path to the user's Documents directory, ensuring the directory exists.
    /// </summary>
    /// <remarks>If the Documents directory does not exist or cannot be determined, the method falls back to
    /// the user's home directory and creates the directory if necessary. The returned path is guaranteed to exist when
    /// the method completes.</remarks>
    /// <returns>A string containing the full path to the user's Documents directory. If the Documents directory is unavailable,
    /// returns the path to the user's home directory instead.</returns>
    private static string GetValidDocumentsPath()
    {
        string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        // If MyDocuments is empty or doesn't exist, fallback to the user's home directory.
        if (string.IsNullOrWhiteSpace(myDocuments) || !System.IO.Directory.Exists(myDocuments))
        {
            myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        // Optionally, ensure the directory exists.
        if (!System.IO.Directory.Exists(myDocuments))
        {
            System.IO.Directory.CreateDirectory(myDocuments);
        }
        return myDocuments;
    }
}
