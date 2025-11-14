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

namespace MermaidPad.Views.Dialogs;

public sealed partial class ExportDialog : Window
{
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

    public ExportDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Unsubscribe from previous ViewModel if any
        if (_viewModelPropertyChangedHandler is not null && DataContext is ExportDialogViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            _viewModelPropertyChangedHandler = null;
        }

        // Wire up dialog result handling for new ViewModel
        if (DataContext is ExportDialogViewModel viewModel)
        {
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
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Clean up event subscription to prevent memory leak
        if (_viewModelPropertyChangedHandler is not null && DataContext is ExportDialogViewModel viewModel)
        {
            viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            _viewModelPropertyChangedHandler = null;
        }
    }
}
