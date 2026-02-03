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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// Represents the About dialog window that displays application information,
/// version details, environment info, links, and third-party library attributions.
/// </summary>
/// <remarks>
/// This dialog is read-only and requires no subscriptions, event handlers cleanup,
/// or IDisposable implementation. It follows the same pattern as <see cref="MessageDialog"/>.
/// </remarks>
internal sealed partial class AboutDialog : DialogBase
{
    private readonly ILogger<AboutDialog> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutDialog"/> class.
    /// </summary>
    public AboutDialog()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _logger = sp.GetRequiredService<ILogger<AboutDialog>>();
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
