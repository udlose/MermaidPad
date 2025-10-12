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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Services.Export;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.Dialogs;

/// <summary>
/// View model for progress dialog
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class ProgressDialogViewModel : ViewModelBase, IProgress<ExportProgress>
{
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    public partial string Title { get; set; } = "Exporting Diagram";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Preparing...";

    [ObservableProperty]
    public partial double ProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial string ProgressText { get; set; } = "0%";

    [ObservableProperty]
    public partial string StepDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EstimatedTime { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowDetails { get; set; } = true;

    [ObservableProperty]
    public partial bool HasEstimatedTime { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool CanCancel { get; set; } = true;

    /// <summary>
    /// Indicates whether the export process is complete. When <c>false</c>, the Cancel button is shown; when
    /// <c>true</c>, the Close button is shown. This property transitions to <c>true</c> when
    /// the export finishes successfully or the user clicks the Close button.
    /// </summary>
    [ObservableProperty]
    public partial bool IsComplete { get; set; } = false;

    public void SetCancellationTokenSource(CancellationTokenSource cts)
    {
        _cancellationTokenSource = cts;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Cancelling...";
        CanCancel = false;
    }

    [RelayCommand]
    private void Close()
    {
        // Signals that the user clicked the Close button
        // The actual window closing is handled by MainViewModel
        IsComplete = true;
    }

    /// <summary>
    /// IProgress implementation for receiving progress updates
    /// </summary>
    public void Report(ExportProgress? value)
    {
        if (value is null)
        {
            return;
        }

        // Update progress value and text
        ProgressValue = value.PercentComplete;
        ProgressText = $"{value.PercentComplete}%";

        // Update status message
        StatusMessage = value.Message;

        // Update step description based on export step
        StepDescription = GetStepDescription(value.Step);

        // When complete, hide cancel button and show close button
        if (value.Step == ExportStep.Complete)
        {
            CanCancel = false;
            IsComplete = true;
        }
    }

    private static string GetStepDescription(ExportStep step)
    {
        return step switch
        {
            ExportStep.Initializing => "Setting up export process...",
            ExportStep.ParsingSvg => "Reading and parsing SVG content...",
            ExportStep.CalculatingDimensions => "Calculating output dimensions based on settings...",
            ExportStep.CreatingCanvas => "Creating rendering surface...",
            ExportStep.Rendering => "Rendering diagram to bitmap...",
            ExportStep.Encoding => "Encoding and compressing PNG data...",
            ExportStep.Complete => "Export completed successfully!",
            _ => string.Empty
        };
    }
}
