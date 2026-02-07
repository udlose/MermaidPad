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

namespace MermaidPad.ViewModels.Dialogs.Configs;

/// <summary>
/// Configuration object for creating a <see cref="MermaidPad.ViewModels.Dialogs.ConfirmationDialogViewModel"/>
/// through the dialog factory.
/// </summary>
/// <remarks>
/// <para>
/// This record encapsulates all properties needed to configure a confirmation dialog,
/// including the tri-state button labels (Yes/No/Cancel) and whether the Cancel button
/// is visible. It is passed as an additional parameter to
/// <see cref="MermaidPad.Factories.IDialogFactory.CreateDialog{T, TConfig}(TConfig)"/>,
/// which forwards it to the dialog's constructor via <c>ActivatorUtilities.CreateInstance</c>.
/// </para>
/// <para>
/// Usage example:
/// <code>
/// ConfirmationDialog dialog = _dialogFactory.CreateDialog&lt;ConfirmationDialog, ConfirmationDialogConfig&gt;(
///     new ConfirmationDialogConfig
///     {
///         Title = "Unsaved Changes",
///         Message = "Do you want to save changes?",
///         IconData = DialogIconGeometries.GetGeometry(DialogIcon.Warning),
///         IconColor = Brushes.Orange,
///         ShowCancelButton = true,
///         YesButtonText = "Save",
///         NoButtonText = "Don't Save"
///     });
/// ConfirmationResult result = await dialog.ShowDialog&lt;ConfirmationResult&gt;(parentWindow);
/// </code>
/// </para>
/// </remarks>
internal sealed record ConfirmationDialogConfig
{
    /// <summary>
    /// Gets the dialog title displayed in the title bar.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the main message text displayed in the dialog body.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the geometry used to render the dialog icon.
    /// Provide a parsed <see cref="StreamGeometry"/> instance (for example, <c>DialogIconGeometries.GetGeometry(DialogIcon.Success)</c>)
    /// rather than raw SVG path data, and use constants from <see cref="DialogIconGeometries"/> for consistency.
    /// </summary>
    public required StreamGeometry IconData { get; init; }

    /// <summary>
    /// Gets the brush used to color the dialog icon.
    /// </summary>
    public required IBrush IconColor { get; init; }

    /// <summary>
    /// Gets a value indicating whether the Cancel button is visible.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool ShowCancelButton { get; init; }

    /// <summary>
    /// Gets the text for the Yes button. Defaults to "_Yes".
    /// The underscore prefix indicates the keyboard accelerator character.
    /// </summary>
    public string YesButtonText { get; init; } = "_Yes";

    /// <summary>
    /// Gets the text for the No button. Defaults to "_No".
    /// The underscore prefix indicates the keyboard accelerator character.
    /// </summary>
    public string NoButtonText { get; init; } = "_No";

    /// <summary>
    /// Gets the text for the Cancel button. Defaults to "_Cancel".
    /// The underscore prefix indicates the keyboard accelerator character.
    /// </summary>
    public string CancelButtonText { get; init; } = "_Cancel";
}
