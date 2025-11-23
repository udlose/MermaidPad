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

//public partial class MessageDialog : Window
//{
//    private MessageDialog()
//    {
//        InitializeComponent();
//    }

//    private void OnOkClick(object? sender, RoutedEventArgs e)
//    {
//        Close();
//    }

//    public static Task ShowErrorAsync(Window parent, string title, string message)
//    {
//        MessageDialog dialog = new MessageDialog
//        {
//            DataContext = new MessageDialogViewModel
//            {
//                Title = title,
//                Message = message,
//                IconData = "M12,2 C17.53,2 22,6.47 22,12 C22,17.53 17.53,22 12,22 C6.47,22 2,17.53 2,12 C2,6.47 6.47,2 12,2 M15.59,7 L12,10.59 L8.41,7 L7,8.41 L10.59,12 L7,15.59 L8.41,17 L12,13.41 L15.59,17 L17,15.59 L13.41,12 L17,8.41 L15.59,7 Z",
//                IconColor = Brushes.Red
//            }
//        };
//        return dialog.ShowDialog(parent);
//    }

//    public static Task ShowSuccessAsync(Window parent, string title, string message)
//    {
//        MessageDialog dialog = new MessageDialog
//        {
//            DataContext = new MessageDialogViewModel
//            {
//                Title = title,
//                Message = message,
//                IconData = "M12,2 C17.52,2 22,6.48 22,12 C22,17.52 17.52,22 12,22 C6.48,22 2,17.52 2,12 C2,6.48 6.48,2 12,2 M9,16.17 L4.83,12 L3.41,13.41 L9,19 L21,7 L19.59,5.59 L9,16.17 Z",
//                IconColor = Brushes.Green
//            }
//        };
//        return dialog.ShowDialog(parent);
//    }
//}

/// <summary>
/// Represents a modal dialog window for displaying messages to the user, such as error or success notifications.
/// </summary>
/// <remarks>Use MessageDialog to present important information or feedback that requires user acknowledgment. The
/// dialog is non-resizable and can be initialized with a specific view model to customize its content and appearance.
/// This class is typically shown as a modal window and is intended for scenarios where user attention is required
/// before proceeding.</remarks>
public sealed partial class MessageDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the MessageDialog class.
    /// </summary>
    /// <remarks>This constructor creates a non-resizable dialog window. Use this constructor when you need a
    /// standard message dialog that cannot be resized by the user.
    /// <para>
    /// This constructor lives specifically for the purpose of avoiding this warning:
    ///     AVLN3001: XAML resource "avares://MermaidPad/Views/Dialogs/MessageDialog.axaml" won't be reachable via runtime loader, as no public constructor was found
    /// </para>
    /// </remarks>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Needed for XAML runtime.")]
    public MessageDialog()
    {
        InitializeComponent();

        // Make dialog non-resizable
        CanResize = false;
    }

    /// <summary>
    /// Initializes a new instance of the MessageDialog class with the specified view model.
    /// </summary>
    /// <param name="viewModel">The view model for this dialog.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel"/> is <c>null</c>.</exception>
    public MessageDialog(MessageDialogViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the OK button click event and closes the dialog, indicating a successful result.
    /// </summary>
    /// <param name="sender">The source of the event, typically the OK button that was clicked.</param>
    /// <param name="e">An object that contains the event data for the routed event.</param>
    private void OnOkClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }
}
