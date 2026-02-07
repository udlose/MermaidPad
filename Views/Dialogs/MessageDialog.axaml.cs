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

using Avalonia.Interactivity;
using MermaidPad.ViewModels.Dialogs;
using MermaidPad.ViewModels.Dialogs.Configs;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// A modal message dialog that displays an icon, title, and message with an OK button.
/// </summary>
/// <remarks>
/// <para>
/// This dialog is created through <see cref="MermaidPad.Factories.IDialogFactory"/> which uses
/// <c>ActivatorUtilities.CreateInstance</c> to resolve constructor dependencies from DI
/// and pass the <see cref="MessageDialogConfig"/> as an additional parameter.
/// </para>
/// <para>
/// The dialog is read-only and requires no subscriptions, event handler cleanup,
/// or <see cref="IDisposable"/> implementation.
/// </para>
/// </remarks>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by DI through IDialogFactory and ActivatorUtilities.CreateInstance.")]
internal sealed partial class MessageDialog : DialogBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageDialog"/> class with the specified
    /// ViewModel and configuration.
    /// </summary>
    /// <param name="viewModel">The ViewModel resolved from DI. Cannot be null.</param>
    /// <param name="config">The configuration specifying title, message, and icon properties.</param>
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
    public MessageDialog(MessageDialogViewModel viewModel, MessageDialogConfig config)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(config);

        InitializeComponent();

        // Apply configuration to the ViewModel
        viewModel.Title = config.Title;
        viewModel.Message = config.Message;
        viewModel.IconData = config.IconData;
        viewModel.IconColor = config.IconColor;

        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the OK button click by closing the dialog with a <see langword="true"/> result.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        CloseDialog(true);
    }
}
