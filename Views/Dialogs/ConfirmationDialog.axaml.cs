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
using MermaidPad.ViewModels.Dialogs.Configs;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// A confirmation dialog window that exposes Yes, No and Cancel actions.
/// </summary>
/// <remarks>
/// <para>
/// This window is intended to be used as a modal confirmation dialog. It provides
/// three explicit responses through the <see cref="ConfirmationResult"/> enum:
/// Yes, No and Cancel. The dialog disables resizing and will attempt to center
/// itself on its <see cref="WindowBase.Owner"/> when opened.
/// </para>
/// <para>
/// Created through <see cref="MermaidPad.Factories.IDialogFactory"/> which uses
/// <c>ActivatorUtilities.CreateInstance</c> to resolve constructor dependencies from DI
/// and pass the <see cref="ConfirmationDialogConfig"/> as an additional parameter.
/// </para>
/// </remarks>
/// <seealso cref="ConfirmationResult"/>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by DI through IDialogFactory and ActivatorUtilities.CreateInstance.")]
internal sealed partial class ConfirmationDialog : DialogBase
{
    /// <summary>
    /// Tracks whether a dialog result was explicitly set by clicking one of the action buttons.
    /// </summary>
    /// <remarks>
    /// This flag distinguishes between explicit button clicks (Yes/No/Cancel) and the window
    /// being closed via the title bar X button or other close mechanisms. When <see langword="false"/>,
    /// the <see cref="OnClosing"/> method will intercept the close and set <see cref="ConfirmationResult.Cancel"/>
    /// as the result to ensure consistent behavior.
    /// </remarks>
    private bool _isResultSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationDialog"/> class with the specified
    /// ViewModel and configuration.
    /// </summary>
    /// <param name="viewModel">The ViewModel resolved from DI. Cannot be null.</param>
    /// <param name="config">The configuration specifying title, message, icon, button text, and visibility.</param>
    /// <remarks>
    /// <para>
    /// Both parameters are resolved by <c>ActivatorUtilities.CreateInstance</c>:
    /// <list type="bullet">
    ///     <item><description><paramref name="viewModel"/> is resolved from the DI container (registered as transient).</description></item>
    ///     <item><description><paramref name="config"/> is passed as an additional parameter by the caller via
    ///     <see cref="MermaidPad.Factories.IDialogFactory.CreateDialog{T, TConfig}(TConfig)"/>.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewModel"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
    [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Used by the DI container through IDialogFactory and ActivatorUtilities.CreateInstance.")]
    public ConfirmationDialog(ConfirmationDialogViewModel viewModel, ConfirmationDialogConfig config)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(config);

        InitializeComponent();

        // Apply configuration to the ViewModel
        viewModel.Title = config.Title;
        viewModel.Message = config.Message;
        viewModel.IconData = config.IconData;
        viewModel.IconColor = config.IconColor;
        viewModel.ShowCancelButton = config.ShowCancelButton;
        viewModel.YesButtonText = config.YesButtonText;
        viewModel.NoButtonText = config.NoButtonText;
        viewModel.CancelButtonText = config.CancelButtonText;

        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the window closing event, ensuring that the X button returns <see cref="ConfirmationResult.Cancel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a user clicks the title bar X button or uses Alt+F4, this override redirects the close
    /// request to the Cancel button handler, ensuring consistent result handling. The natural close
    /// is canceled and replaced with a programmatic close that sets <see cref="ConfirmationResult.Cancel"/>.
    /// </para>
    /// <para>
    /// This pattern reuses existing button logic rather than duplicating the close mechanism.
    /// </para>
    /// </remarks>
    /// <param name="e">Event arguments containing cancellation state and close reason.</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // If no button was explicitly clicked, redirect to Cancel result
        if (!_isResultSet)
        {
            // Prevent the unhandled close
            e.Cancel = true;

            // Call base so DialogBase can see the cancellation and clear its pending request
            base.OnClosing(e);

            // Set flag BEFORE CloseDialog to prevent infinite loop on subsequent OnClosing call
            _isResultSet = true;

            // Now trigger a proper close with Cancel result (deferred to next dispatcher tick)
            CloseDialog(ConfirmationResult.Cancel);
            return;
        }

        // Allow the close to proceed with the explicitly set result
        base.OnClosing(e);
    }

    /// <summary>
    /// Handles the click event for the Yes button and closes the dialog with a <see cref="ConfirmationResult.Yes"/>.
    /// </summary>
    /// <param name="sender">The source of the event. May be <c>null</c> when invoked programmatically.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing event data.</param>
    private void OnYesClick(object? sender, RoutedEventArgs e)
    {
        _isResultSet = true;
        CloseDialog(ConfirmationResult.Yes);
    }

    /// <summary>
    /// Handles the click event for the No button and closes the dialog with a <see cref="ConfirmationResult.No"/>.
    /// </summary>
    /// <param name="sender">The source of the event. May be <c>null</c> when invoked programmatically.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing event data.</param>
    private void OnNoClick(object? sender, RoutedEventArgs e)
    {
        _isResultSet = true;
        CloseDialog(ConfirmationResult.No);
    }

    /// <summary>
    /// Handles the click event for the Cancel button and closes the dialog with a <see cref="ConfirmationResult.Cancel"/>.
    /// </summary>
    /// <param name="sender">The source of the event. May be <c>null</c> when invoked programmatically.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing event data.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _isResultSet = true;
        CloseDialog(ConfirmationResult.Cancel);
    }
}
