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
    /// the export finishes successfully.
    /// </summary>
    [ObservableProperty]
    public partial bool IsComplete { get; set; } = false;

    /// <summary>
    /// Indicates that the user has clicked the Close button and wants to dismiss the dialog.
    /// This is separate from <see cref="IsComplete"/> to handle the case where the user
    /// manually closes the dialog after export completion.
    /// </summary>
    [ObservableProperty]
    public partial bool CloseRequested { get; set; } = false;

    /// <summary>
    /// Sets the <see cref="CancellationTokenSource"/> to be used for managing cancellation tokens.
    /// </summary>
    /// <param name="cts">The <see cref="CancellationTokenSource"/> instance to assign. Cannot be <see langword="null"/>.</param>
    public void SetCancellationTokenSource(CancellationTokenSource cts)
    {
        _cancellationTokenSource = cts;
    }

    /// <summary>
    /// Cancels the ongoing operation, if one is in progress.
    /// </summary>
    /// <remarks>This method signals the cancellation of the current operation by invoking the associated
    /// cancellation token. Once called, the operation cannot be resumed. The status message is updated to indicate the
    /// cancellation, and the ability to cancel is disabled.</remarks>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Cancelling...";
        CanCancel = false;
    }

    /// <summary>
    /// Signals that the dialog should be closed.
    /// </summary>
    /// <remarks>This method sets the <see cref="CloseRequested"/> property to <see langword="true"/>,
    /// indicating that the user has requested to close the dialog. The actual closing of the window is managed
    /// externally, typically by a handler in the <c>MainViewModel</c>.</remarks>
    [RelayCommand]
    private void Close()
    {
        // Signal that the user clicked the Close button and wants to dismiss the dialog
        // The actual window closing is handled by MainViewModel's PropertyChanged handler
        CloseRequested = true;
    }

    /// <summary>
    /// Updates the export progress by applying the specified value.
    /// </summary>
    /// <remarks>If called from a thread other than the UI thread, the update is posted to the UI thread for
    /// execution.</remarks>
    /// <param name="value">The progress value to apply. If <see langword="null"/>, the method does nothing.</param>
    public void Report(ExportProgress? value)
    {
        if (value is null)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply(value);
        }
        else
        {
            Dispatcher.UIThread.Post(() => Apply(value));
        }
    }

    /// <summary>
    /// Updates the progress, status, and step description based on the provided export progress value.
    /// </summary>
    /// <remarks>This method updates the progress percentage, status message, and step description to reflect
    /// the current state of the export operation. If the export step indicates completion, the cancel button is
    /// disabled, and the operation is marked as complete.</remarks>
    /// <param name="value">The current progress of the export operation, including percentage complete, status message, and step
    /// information.</param>
    private void Apply(ExportProgress value)
    {
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

    /// <summary>
    /// Provides a human-readable description of the specified export step.
    /// </summary>
    /// <param name="step">The <see cref="ExportStep"/> value representing the current stage of the export process.</param>
    /// <returns>A string describing the specified export step. Returns an empty string if the step is not recognized.</returns>
    private static string GetStepDescription(ExportStep step)
    {
        return step switch
        {
            ExportStep.Initializing => "Setting up export process...",
            ExportStep.ParsingSvg => "Reading and parsing SVG content...",
            ExportStep.CalculatingDimensions => "Calculating output dimensions based on settings...",
            ExportStep.Rendering => "Rendering diagram...",
            ExportStep.CreatingImage => "Creating image...",
            ExportStep.Complete => "Export completed successfully!",
            _ => string.Empty
        };
    }
}
