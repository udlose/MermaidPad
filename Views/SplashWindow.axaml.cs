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

using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Represents a splash screen window that displays while the application is loading or performing initialization tasks.
/// </summary>
/// <remarks>The SplashWindow is typically used to provide visual feedback to users during application startup or
/// lengthy operations. When shown, it remains visible for a short period before optionally invoking a specified action
/// and closing itself. This window is intended to be displayed modally at the beginning of the application's
/// lifecycle.</remarks>
public sealed partial class SplashWindow : Window
{
    private readonly Action? _mainAction;
    private readonly ILogger<SplashWindow>? _logger;
    private readonly SplashWindowViewModel _vm;

    /// <summary>
    /// Initializes a new instance of the SplashWindow class. Needed for XAML designer support.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Needed for XAML designer support.")]
    public SplashWindow()
    {
        InitializeComponent();

        _vm = new SplashWindowViewModel();
        DataContext = _vm;
    }

    /// <summary>
    /// Initializes a new instance of the SplashWindow class with the specified action to execute after the splash
    /// screen.
    /// </summary>
    /// <remarks>Use this constructor to specify the main application action that should run after the splash
    /// window is closed. The provided action is typically used to launch the main application window or perform startup
    /// logic.</remarks>
    /// <param name="splashWindowViewModel">The view model that provides data and commands for the splash window.</param>
    /// <param name="mainAction">The action to invoke when the splash screen completes. Cannot be null.</param>
    public SplashWindow(SplashWindowViewModel splashWindowViewModel, Action mainAction) : this()
    {
        ArgumentNullException.ThrowIfNull(splashWindowViewModel);
        ArgumentNullException.ThrowIfNull(mainAction);

        IServiceProvider sp = App.Services;
        _logger = sp.GetRequiredService<ILogger<SplashWindow>>();

        _mainAction = mainAction;

        // Set the DataContext to the provided ViewModel and override the default one
        _vm = splashWindowViewModel;
        DataContext = _vm;
    }

    /// <summary>
    /// Invoked when the control has been loaded and is ready for interaction.
    /// </summary>
    /// <param name="e">The event data associated with the loaded event.</param>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        LoadAsync()
            .SafeFireAndForget(ex => _logger?.LogError(ex, "Error loading splash screen"));
    }

    /// <summary>
    /// Asynchronously performs background initialization and updates the UI upon completion.
    /// </summary>
    /// <remarks>This method should be awaited to ensure that background processing and subsequent UI updates
    /// complete before proceeding. UI updates are dispatched to the main thread to maintain thread safety.</remarks>
    /// <returns>A task that represents the asynchronous load operation.</returns>
    private async Task LoadAsync()
    {
        //TODO - DaveBlack: Add background initialization logic here: e.g., loading assets, initializing/loading grammar resources, checking for updates, etc.
        // 1. Loading must always run on a background thread to avoid blocking the UI.
        // 2. Any UI updates must be dispatched to the UI thread using Dispatcher.UIThread.InvokeAsync.
        // 3. Ensure proper exception handling to log errors without crashing the application.
        // 4. IMPORTANT: Background work may need to run longer than the splash screen display time!
        try
        {
            // Temporarily simulate background work Task.Delay
            const int simulatedWorkDurationMs = 3_000;
            await Task.Delay(simulatedWorkDurationMs);

            // After background work is complete, update the UI on the main thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mainAction?.Invoke();
                Close();
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during splash screen background work");
        }
    }

    /// <summary>
    /// Handles the pointer pressed event to initiate a window move operation when the left mouse button is pressed.
    /// </summary>
    /// <remarks>If the left mouse button is pressed, this method begins a drag operation to move the window
    /// and marks the event as handled to prevent further processing.</remarks>
    /// <param name="sender">The source of the event. This is typically the control that received the pointer press.</param>
    /// <param name="e">A PointerPressedEventArgs that contains the event data, including pointer information and button states.</param>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }
}
