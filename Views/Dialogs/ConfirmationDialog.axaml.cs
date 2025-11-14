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
using Avalonia.Interactivity;
using MermaidPad.ViewModels.Dialogs;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// A confirmation dialog window that exposes Yes, No and Cancel actions.
/// </summary>
/// <remarks>
/// This window is intended to be used as a modal confirmation dialog. It provides
/// three explicit responses through the <see cref="ConfirmationResult"/> enum:
/// Yes, No and Cancel. The dialog disables resizing and will attempt to center
/// itself on its <see cref="Window.Owner"/> when opened.
/// </remarks>
/// <seealso cref="ConfirmationResult"/>
public sealed partial class ConfirmationDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationDialog"/> class.
    /// </summary>
    /// <remarks>
    /// Calls <see cref="InitializeComponent"/> to load the XAML-defined UI and
    /// disables window resizing by setting <see cref="Window.CanResize"/> to false.
    /// </remarks>
    public ConfirmationDialog()
    {
        InitializeComponent();
        CanResize = false;
    }

    /// <summary>
    /// Handles the click event for the Yes button and closes the dialog with a <see cref="ConfirmationResult.Yes"/>.
    /// </summary>
    /// <param name="sender">The source of the event. May be <c>null</c> when invoked programmatically.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing event data.</param>
    private void OnYesClick(object? sender, RoutedEventArgs e)
    {
        Close(ConfirmationResult.Yes);
    }

    /// <summary>
    /// Handles the click event for the No button and closes the dialog with a <see cref="ConfirmationResult.No"/>.
    /// </summary>
    /// <param name="sender">The source of the event. May be <c>null</c> when invoked programmatically.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing event data.</param>
    private void OnNoClick(object? sender, RoutedEventArgs e)
    {
        Close(ConfirmationResult.No);
    }

    /// <summary>
    /// Handles the click event for the Cancel button and closes the dialog with a <see cref="ConfirmationResult.Cancel"/>.
    /// </summary>
    /// <param name="sender">The source of the event. May be <c>null</c> when invoked programmatically.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing event data.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(ConfirmationResult.Cancel);
    }
}
