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

using MermaidPad.Factories;
using MermaidPad.Services.Export;
using MermaidPad.ViewModels.Dialogs;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// A modal dialog for configuring export options (format, DPI, scale, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Created through <see cref="IDialogFactory"/> which uses
/// <c>ActivatorUtilities.CreateInstance</c> to resolve the <see cref="ExportDialogViewModel"/>
/// from DI. No additional config object is needed because the ViewModel is self-configuring.
/// </para>
/// <para>
/// The dialog closes itself when the ViewModel's <see cref="ExportDialogViewModel.DialogResult"/>
/// property is set, either returning the <see cref="ExportOptions"/> or <see langword="null"/>
/// for cancellation.
/// </para>
/// </remarks>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by DI through IDialogFactory and ActivatorUtilities.CreateInstance.")]
internal sealed partial class ExportDialog : DialogBase
{
    private ExportDialogViewModel? _currentViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportDialog"/> class with the specified ViewModel.
    /// </summary>
    /// <param name="viewModel">The ViewModel resolved from DI with all services injected.</param>
    /// <remarks>
    /// <para>
    /// The ViewModel is resolved by <c>ActivatorUtilities.CreateInstance</c> from the DI container
    /// when <see cref="IDialogFactory.CreateDialog{T}()"/> is called.
    /// </para>
    /// <para>
    /// The ViewModel reference is stored explicitly rather than relying on the
    /// <see cref="OnDataContextChanged"/> side-effect to populate <c>_currentViewModel</c>.
    /// The <see cref="AttachToViewModel"/> call subscribes to <c>PropertyChanged</c> for
    /// dialog result handling. Note: <see cref="OnDataContextChanged"/> will also fire when
    /// <c>DataContext</c> is set, but <see cref="AttachToViewModel"/> is idempotent
    /// (it unsubscribes before subscribing).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewModel"/> is null.</exception>
    public ExportDialog(ExportDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        // Explicitly attach to the ViewModel for PropertyChanged subscription.
        // This is more robust than relying on OnDataContextChanged firing during construction.
        AttachToViewModel(viewModel);

        DataContext = viewModel;
    }

    /// <summary>
    /// Handles changes to the data context by detaching from the previous view model and attaching to the new one, if
    /// applicable.
    /// </summary>
    /// <remarks>Override this method to respond to changes in the data context, such as updating bindings or
    /// event subscriptions. This method ensures that the control interacts with the correct view model when the data
    /// context changes.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> instance containing the event data for the data context change.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        DetachFromViewModel();

        if (DataContext is ExportDialogViewModel viewModel)
        {
            AttachToViewModel(viewModel);
        }

        // Call the base class implementation last
        base.OnDataContextChanged(e);
    }

    /// <summary>
    /// Handles additional cleanup when the window is closed.
    /// </summary>
    /// <remarks>Overrides the base implementation to detach from the view model before completing the window
    /// close process. Callers should ensure any necessary cleanup is performed prior to closing the window.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> that contains the event data associated with the window closing.</param>
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            DetachFromViewModel();
        }
        finally
        {
            // Ensure the base class cleanup is always performed
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Associates the specified view model with the dialog and subscribes to its property change notifications.
    /// </summary>
    /// <remarks>
    /// This method is idempotent — it unsubscribes before subscribing to prevent duplicate handlers.
    /// This is important because it may be called both from the constructor and from
    /// <see cref="OnDataContextChanged"/>.
    /// </remarks>
    /// <param name="viewModel">The view model to attach to the dialog. Cannot be null.</param>
    private void AttachToViewModel(ExportDialogViewModel viewModel)
    {
        _currentViewModel = viewModel;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Detaches the current view model by unsubscribing from its property change notifications and clearing the
    /// reference.
    /// </summary>
    /// <remarks>Call this method to safely disconnect from the view model when it is no longer needed or
    /// before attaching a new one. This helps prevent memory leaks and ensures that property change events are not
    /// handled after detachment.</remarks>
    private void DetachFromViewModel()
    {
        if (_currentViewModel is null)
        {
            return;
        }

        _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _currentViewModel = null;
    }

    /// <summary>
    /// Handles changes to the ViewModel's properties, closing the dialog when the dialog result is set.
    /// </summary>
    /// <remarks>This method listens for changes to the DialogResult property of the associated
    /// ExportDialogViewModel. When DialogResult is set, the dialog is closed with the appropriate result. This method
    /// should be connected to the PropertyChanged event of the ViewModel to enable dialog closure based on user
    /// actions.</remarks>
    /// <param name="sender">The source of the property change event. Expected to be an instance of ExportDialogViewModel.</param>
    /// <param name="e">The event data containing information about the changed property.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ExportDialogViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName != nameof(ExportDialogViewModel.DialogResult))
        {
            return;
        }

        if (!viewModel.DialogResult.HasValue)
        {
            return;
        }

        // Close the dialog with the result
        if (viewModel.DialogResult == true)
        {
            ExportOptions exportOptions = viewModel.GetExportOptions();
            CloseDialog(exportOptions);
        }
        else
        {
            CloseDialog<ExportOptions?>(null);
        }
    }
}
