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

using Avalonia.Media;

namespace MermaidPad.ViewModels.Dialogs;

/// <summary>
/// Represents the view model for a confirmation dialog with Yes, No, and Cancel options.
/// </summary>
/// <remarks>
/// This dialog is used for scenarios like "Save changes before closing?" where the user
/// needs to make a tri-state decision. The result is determined by which button was clicked.
/// </remarks>
internal sealed class ConfirmationDialogViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets the dialog title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the main message text.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Gets or sets the icon path data for SVG rendering.
    /// </summary>
    public string IconData { get; set; } = "";

    /// <summary>
    /// Gets or sets the icon color brush.
    /// </summary>
    public IBrush IconColor { get; set; } = Brushes.Gray;

    /// <summary>
    /// Gets or sets the text for the Yes button.
    /// </summary>
    public string YesButtonText { get; set; } = "_Yes";

    /// <summary>
    /// Gets or sets the text for the No button.
    /// </summary>
    public string NoButtonText { get; set; } = "_No";

    /// <summary>
    /// Gets or sets the text for the Cancel button.
    /// </summary>
    public string CancelButtonText { get; set; } = "_Cancel";

    /// <summary>
    /// Gets or sets a value indicating whether the Cancel button is visible in the user interface.
    /// </summary>
    public bool ShowCancelButton { get; set; }
}

/// <summary>
/// Represents the result of a confirmation dialog.
/// </summary>
internal enum ConfirmationResult
{
    /// <summary>
    /// User clicked Yes or confirmed the action.
    /// </summary>
    Yes,

    /// <summary>
    /// User clicked No or declined the action.
    /// </summary>
    No,

    /// <summary>
    /// User clicked Cancel or closed the dialog without making a choice.
    /// </summary>
    Cancel
}
