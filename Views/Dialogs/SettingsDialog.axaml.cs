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
/// Represents a modal dialog window for displaying and editing application settings using a view model-based data
/// context.
/// </summary>
/// <remarks>SettingsDialog provides a user interface for configuring application options. It supports data
/// binding to a SettingsDialogViewModel, enabling dynamic updates and command handling. The dialog observes changes to
/// the view model's properties, such as DialogResult, to manage its lifecycle and closure. This class is typically used
/// in scenarios where user-driven configuration is required, and integrates with MVVM patterns for separation of
/// concerns.</remarks>
[SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Event handler signature requires these parameters")]
public sealed partial class SettingsDialog : Window
{
    private SettingsDialogViewModel? _viewModel;

    /// <summary>
    /// Initializes a new instance of the SettingsDialog class.
    /// </summary>
    /// <remarks>This constructor sets up the dialog's user interface and prepares it for display. Use this
    /// constructor when you need to present the settings dialog to the user.</remarks>
    public SettingsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the SettingsDialog class using the specified view model as its data context.
    /// </summary>
    /// <remarks>The dialog observes changes to the view model's properties to update its state, such as
    /// closing the window when the dialog result changes.</remarks>
    /// <param name="viewModel">The view model that provides data and command bindings for the dialog. Cannot be null.</param>
    public SettingsDialog(SettingsDialogViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
        _viewModel = (SettingsDialogViewModel)DataContext;

        // Observe DialogResult changes and close window accordingly
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Handles the logic required when the window is closed, including cleanup of event subscriptions.
    /// </summary>
    /// <remarks>Overrides the base implementation to unsubscribe from ViewModel events, helping to prevent
    /// memory leaks. This method is typically called by the framework when the window is closed.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> instance containing the event data associated with the window close operation.</param>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Ensure we unsubscribe from ViewModel events to prevent memory leaks
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    /// <summary>
    /// Handles property change notifications from the view model, closing the dialog when the DialogResult property is
    /// set.
    /// </summary>
    /// <remarks>This method listens for changes to the DialogResult property of the view model and closes the
    /// dialog when a result is available. It should be connected to the PropertyChanged event of the view model to
    /// enable dialog closure based on user actions or view model logic.</remarks>
    /// <param name="sender">The source of the property change event, typically the view model instance.</param>
    /// <param name="e">An object that contains the event data, including the name of the property that changed.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is not null && e.PropertyName == nameof(SettingsDialogViewModel.DialogResult) && _viewModel.DialogResult.HasValue)
        {
            Close(_viewModel.DialogResult.Value);
        }
    }
}
