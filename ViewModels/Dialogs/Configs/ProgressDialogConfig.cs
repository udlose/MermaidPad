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

namespace MermaidPad.ViewModels.Dialogs.Configs;

/// <summary>
/// Configuration object for creating a <see cref="MermaidPad.ViewModels.Dialogs.ProgressDialogViewModel"/>
/// through the dialog factory.
/// </summary>
/// <remarks>
/// <para>
/// This record encapsulates the initial configuration for a progress dialog, including the
/// title and initial status message. It is passed as an additional
/// parameter to <see cref="MermaidPad.Factories.IDialogFactory.CreateDialog{T, TConfig}(TConfig)"/>,
/// which forwards it to the dialog's constructor via <c>ActivatorUtilities.CreateInstance</c>.
/// </para>
/// <para>
/// Note: The <see cref="System.Threading.CancellationTokenSource"/> is not part of this config
/// because it is set after dialog creation via <see cref="ProgressDialogViewModel.SetCancellationTokenSource"/>.
/// This separation exists because the CTS is typically created by the caller and shared with the
/// export operation, which is a runtime concern rather than a configuration concern.
/// </para>
/// </remarks>
internal sealed record ProgressDialogConfig
{
    /// <summary>
    /// Gets the dialog title displayed in the title bar.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the initial status message displayed below the progress bar.
    /// Defaults to "Preparing...".
    /// </summary>
    public string StatusMessage { get; init; } = "Preparing...";
}
