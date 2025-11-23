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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// Represents a modal dialog window that enables users to configure and initiate an export operation using a provided
/// view model.
/// </summary>
/// <remarks>The ExportDialog is designed to be used with an ExportDialogViewModel, which supplies the data,
/// commands, and export options for the dialog. The dialog automatically closes when the view model's DialogResult
/// property is set, returning the selected export options if the operation is confirmed. This class is sealed and
/// cannot be inherited.</remarks>
public sealed partial class ExportDialog : Window
{
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private ExportDialogViewModel? _currentViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportDialog"/> class.
    /// </summary>
    /// <remarks>
    /// Calls <see cref="InitializeComponent"/> to load the XAML-defined UI and
    /// enables window resizing by setting <see cref="Window.CanResize"/> to true.
    /// <para>
    /// This constructor lives specifically for the purpose of avoiding this warning:
    ///     AVLN3001: XAML resource "avares://MermaidPad/Views/Dialogs/ExportDialog.axaml" won't be reachable via runtime loader, as no public constructor was found
    /// </para>
    /// </remarks>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Needed for XAML runtime.")]
    public ExportDialog()
    {
        InitializeComponent();
        CanResize = true;   // Allow resizing for export options
    }

    /// <summary>
    /// Initializes a new instance of the ExportDialog class with the specified view model as its data context.
    /// </summary>
    /// <param name="viewModel">The view model that provides data and commands for the export dialog. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel"/> is null.</exception>
    public ExportDialog(ExportDialogViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles changes to the data context by updating event subscriptions and dialog state as needed.
    /// </summary>
    /// <remarks>If the new data context is an <see cref="ExportDialogViewModel"/>, this method subscribes to
    /// its <c>PropertyChanged</c> event to monitor dialog result changes and close the dialog accordingly. Previous
    /// event subscriptions are removed to prevent memory leaks. This override is typically called by the Avalonia framework
    /// when the data context changes.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> instance containing the event data for the data context change.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Unsubscribe from previous ViewModel if any
        if (_viewModelPropertyChangedHandler is not null && _currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            _viewModelPropertyChangedHandler = null;
        }

        if (DataContext is ExportDialogViewModel viewModel)
        {
            _currentViewModel = viewModel;
            _viewModelPropertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(ExportDialogViewModel.DialogResult) && viewModel.DialogResult.HasValue)
                {
                    // Close the dialog with the result
                    if (viewModel.DialogResult == true)
                    {
                        Close(viewModel.GetExportOptions());
                    }
                    else
                    {
                        Close(null);
                    }
                }
            };
            viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
        }
        else
        {
            _currentViewModel = null;
        }
    }

    /// <summary>
    /// Raises the Closed event and performs cleanup when the window is closed.
    /// </summary>
    /// <remarks>Overrides the base implementation to detach event handlers and release resources associated
    /// with the current view model. This helps prevent memory leaks and ensures proper disposal of window-related
    /// resources.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_viewModelPropertyChangedHandler is not null && _currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            _viewModelPropertyChangedHandler = null;
            _currentViewModel = null;
        }
    }
}
