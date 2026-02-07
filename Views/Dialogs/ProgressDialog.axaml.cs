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

using Avalonia;
using MermaidPad.Factories;
using MermaidPad.ViewModels.Dialogs;
using MermaidPad.ViewModels.Dialogs.Configs;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// A modal progress dialog that displays export progress with an optional cancel/close button.
/// </summary>
/// <remarks>
/// <para>
/// Created through <see cref="MermaidPad.Factories.IDialogFactory"/> which uses
/// <c>ActivatorUtilities.CreateInstance</c> to resolve constructor dependencies from DI
/// and pass the <see cref="ProgressDialogConfig"/> as an additional parameter.
/// </para>
/// <para>
/// The dialog transitions between Cancel and Close buttons based on the
/// <see cref="ProgressDialogViewModel.IsComplete"/> property.
/// </para>
/// <para>
/// The <see cref="ViewModel"/> property provides strongly-typed access to the dialog's
/// ViewModel without requiring callers to cast <see cref="StyledElement.DataContext"/>.
/// </para>
/// </remarks>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by DI through IDialogFactory and ActivatorUtilities.CreateInstance.")]
internal sealed partial class ProgressDialog : DialogBase
{
    /// <summary>
    /// Gets the strongly-typed ViewModel associated with this dialog.
    /// </summary>
    /// <remarks>
    /// This property provides type-safe access to the ViewModel for callers that need to
    /// subscribe to property changes, configure cancellation, or report progress. It eliminates
    /// the need to cast <see cref="StyledElement.DataContext"/>.
    /// </remarks>
    public ProgressDialogViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressDialog"/> class with the specified
    /// ViewModel and configuration.
    /// </summary>
    /// <param name="viewModel">The ViewModel resolved from DI. Cannot be null.</param>
    /// <param name="config">The configuration specifying the title and initial status message.</param>
    /// <remarks>
    /// <para>
    /// Both parameters are resolved by <c>ActivatorUtilities.CreateInstance</c>:
    /// <list type="bullet">
    ///     <item><description><paramref name="viewModel"/> is resolved from the DI container
    ///     (registered as <seealso cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient"/>).</description></item>
    ///     <item><description><paramref name="config"/> is passed as an additional parameter by the caller
    ///     via <see cref="MermaidPad.Factories.IDialogFactory.CreateDialog{T, TConfig}(TConfig)"/>.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <c>CanResize = false</c> is already set by <see cref="DialogBase"/>. <c>Topmost = true</c> is
    /// specific to the progress dialog to ensure visibility during long-running export operations.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewModel"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
    [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Used by the DI container through IDialogFactory and ActivatorUtilities.CreateInstance.")]
    public ProgressDialog(ProgressDialogViewModel viewModel, ProgressDialogConfig config)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(config);

        InitializeComponent();

        // Progress dialog stays on top during long-running operations
        // CanResize = false is already set by DialogBase
        Topmost = true;

        // Apply configuration to the ViewModel
        viewModel.Title = config.Title;
        viewModel.StatusMessage = config.StatusMessage;

        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
