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
using MermaidPad.ViewModels.Dialogs;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// Represents a modal dialog window that displays progress information for a long-running operation.
/// </summary>
/// <remarks>Use ProgressDialog to provide users with feedback during operations that may take significant time to
/// complete. The dialog is non-resizable and remains on top of other windows to ensure visibility. It is typically
/// initialized with a ProgressDialogViewModel to supply progress data and status messages.</remarks>
public sealed partial class ProgressDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the ProgressDialog class.
    /// </summary>
    /// <remarks>This constructor configures the dialog to be non-resizable and always displayed on top of
    /// other windows.
    /// <para>
    /// This constructor lives specifically for the purpose of avoiding this warning:
    ///     AVLN3001: XAML resource "avares://MermaidPad/Views/Dialogs/ProgressDialog.axaml" won't be reachable via runtime loader, as no public constructor was found
    /// </para>
    /// </remarks>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Needed for XAML runtime.")]
    public ProgressDialog()
    {
        InitializeComponent();

        // Make dialog non-resizable and always on top
        CanResize = false;
        Topmost = true;
    }

    /// <summary>
    /// Initializes a new instance of the ProgressDialog class with the specified view model.
    /// </summary>
    /// <param name="viewModel">The view model for this dialog.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel"/> is <c>null</c>.</exception>
    public ProgressDialog(ProgressDialogViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }
}
