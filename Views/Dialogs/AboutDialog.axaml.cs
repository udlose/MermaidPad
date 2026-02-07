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
using MermaidPad.Factories;
using MermaidPad.ViewModels.Dialogs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// Represents the About dialog window that displays application information,
/// version details, environment info, links, and third-party library attributions.
/// </summary>
/// <remarks>
/// <para>
/// This dialog is read-only and requires no subscriptions, event handlers cleanup,
/// or <see cref="IDisposable"/> implementation. It follows the same pattern as <see cref="MessageDialog"/>.
/// </para>
/// <para>
/// Created through <see cref="IDialogFactory"/> which uses
/// <c>ActivatorUtilities.CreateInstance</c> to resolve all constructor dependencies from DI.
/// No additional config object is needed because <see cref="AboutDialogViewModel"/> is entirely
/// self-contained (populated from <c>AppMetadata</c> at construction time).
/// </para>
/// </remarks>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by DI through IDialogFactory and ActivatorUtilities.CreateInstance.")]
internal sealed partial class AboutDialog : DialogBase
{
    private readonly ILogger<AboutDialog> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutDialog"/> class with dependencies
    /// resolved from DI via <see cref="IDialogFactory"/>.
    /// </summary>
    /// <param name="viewModel">The ViewModel resolved from DI, pre-populated with app metadata.</param>
    /// <param name="logger">The logger instance for this dialog.</param>
    /// <remarks>
    /// Both parameters are resolved by <c>ActivatorUtilities.CreateInstance</c> from the DI container.
    /// This eliminates the previous dependency on the static <c>App.Services</c> service locator.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewModel"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger"/> is null.</exception>
    public AboutDialog(AboutDialogViewModel viewModel, ILogger<AboutDialog> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();

        _logger = logger;
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the Close button click by closing the dialog with a true result.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseDialog(true);
    }

    /// <summary>
    /// Handles link button clicks by opening the URL stored in the button's Tag property
    /// using the system's default browser.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="AboutDialogViewModel.OpenUrl(string?)"/> which follows the same
    /// cross-platform pattern as <c>ViewLogs()</c> and <c>OpenFileLocationAsync()</c>:
    /// <c>Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })</c>.
    /// </remarks>
    /// <param name="sender">The button that was clicked. Its Tag property contains the URL.</param>
    /// <param name="e">The event data.</param>
    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
        {
            try
            {
                AboutDialogViewModel.OpenUrl(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open URL: {Url}, from {View}", url, nameof(AboutDialog));
            }
        }
    }
}
