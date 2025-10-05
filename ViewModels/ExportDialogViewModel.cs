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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Models;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels;

/// <summary>
/// View model for the export dialog.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
public sealed partial class ExportDialogViewModel : ViewModelBase
{
    private readonly Window _dialog;

    /// <summary>
    /// Gets or sets whether SVG format is selected.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSvgSelected { get; set; } = true;

    /// <summary>
    /// Gets or sets whether PNG format is selected.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPngSelected { get; set; }

    /// <summary>
    /// Gets or sets the selected scale for PNG export.
    /// </summary>
    [ObservableProperty]
    public partial ComboBoxItem? SelectedScale { get; set; }

    /// <summary>
    /// Gets or sets whether to use transparent background for PNG.
    /// </summary>
    [ObservableProperty]
    public partial bool TransparentBackground { get; set; }

    /// <summary>
    /// Gets the export options result.
    /// </summary>
    public ExportOptions? Result { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportDialogViewModel"/> class.
    /// </summary>
    /// <param name="dialog">The dialog window.</param>
    public ExportDialogViewModel(Window dialog)
    {
        _dialog = dialog;
    }

    /// <summary>
    /// Handles the export command.
    /// </summary>
    [RelayCommand]
    private void Export()
    {
        Result = new ExportOptions
        {
            Format = IsSvgSelected ? ExportFormat.SVG : ExportFormat.PNG,
            Scale = ParseScale(SelectedScale?.Content?.ToString() ?? "2x (Recommended)"),
            TransparentBackground = TransparentBackground,
            BackgroundColor = "#FFFFFF"
        };

        _dialog.Close(Result);
    }

    /// <summary>
    /// Handles the cancel command.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        Result = null;
        _dialog.Close(null);
    }

    private static int ParseScale(string scaleText)
    {
        ReadOnlySpan<char> scaleAsSpan = scaleText;
        if (scaleAsSpan.StartsWith("1x")) return 1;
        if (scaleAsSpan.StartsWith("2x")) return 2;
        if (scaleAsSpan.StartsWith("3x")) return 3;
        if (scaleAsSpan.StartsWith("4x")) return 4;

        return 2; // Default
    }
}
