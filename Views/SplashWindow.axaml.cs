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
using Serilog;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Represents a splash screen window that displays while the application is loading or performing initialization tasks.
/// </summary>
/// <remarks>The SplashWindow is typically used to provide visual feedback to users during application startup or
/// lengthy operations. When shown, it remains visible for a short period before optionally invoking a specified action
/// and closing itself. This window is intended to be displayed modally at the beginning of the application's
/// lifecycle.</remarks>
internal sealed partial class SplashWindow : Window
{
    private readonly Action? _mainAction;
    private readonly ILogger<SplashWindow>? _logger;
    private readonly SplashWindowViewModel _vm;

    private CancellationTokenSource? _loadCancellationTokenSource;
    private int _loadStarted;
    private int _mainActionInvoked;

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="splashWindowViewModel"/> or <paramref name="mainAction"/> is null.</exception>
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

    #region Overrides

    /// <summary>
    /// Handles the Loaded event by initiating asynchronous loading operations when the control is loaded into the
    /// visual tree.
    /// </summary>
    /// <remarks>This method ensures that the loading process is started only once, even if the Loaded event
    /// is raised multiple times. Any exceptions that occur during the asynchronous loading operation are logged.
    /// Overrides should call the base implementation to maintain correct loading behavior.</remarks>
    /// <param name="e">The event data associated with the Loaded event.</param>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        // Call the base class implementation first
        base.OnLoaded(e);

        if (Interlocked.Exchange(ref _loadStarted, 1) != 0)
        {
            return;
        }

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        _loadCancellationTokenSource = cancellationTokenSource;

        Task loadTask;
        try
        {
            loadTask = LoadAsync(cancellationTokenSource.Token);
        }
        catch
        {
            DisposeLoadCancellationTokenSource(loadTask: null, cancellationTokenSource);
            throw;
        }

        try
        {
            _ = loadTask.ContinueWith(
                continuationAction: task =>
                {
                    try
                    {
                        // No logging here: errors are handled by SafeFireAndForget(LogError).
                    }
                    catch (Exception ex)
                    {
                        // Extremely defensive: this should never throw, but if it does, log and swallow
                        // so the continuation itself never faults.
                        _logger?.LogError(ex, "Unexpected exception in load continuation");
                    }
                    finally
                    {
                        DisposeLoadCancellationTokenSource(task, cancellationTokenSource);
                    }
                },
                cancellationToken: CancellationToken.None,
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default);
        }
        catch
        {
            DisposeLoadCancellationTokenSource(loadTask: null, cancellationTokenSource);
            throw;
        }

        loadTask.SafeFireAndForget(LogError);

        void LogError(Exception ex) => _logger?.LogError(ex, "Error loading splash screen");
    }

    /// <summary>
    /// Handles the logic required when the window is closed.
    /// </summary>
    /// <remarks>This method cancels any ongoing load operations and releases associated resources before
    /// invoking the base class implementation. Override this method to perform additional cleanup when the window is
    /// closed.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
    protected override void OnClosed(EventArgs e)
    {
        // Disposal is done by the Load task continuation to ensure it's not disposed while LoadAsync is still using it
        try
        {
            CancellationTokenSource? cancellationTokenSource = Volatile.Read(ref _loadCancellationTokenSource);
            if (cancellationTokenSource is not null)
            {
                try
                {
                    cancellationTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // If the continuation already disposed it, Cancel can throw; ignore during shutdown
                }
            }
        }
        finally
        {
            base.OnClosed(e);
        }
    }

    #endregion Overrides

    /// <summary>
    /// Asynchronously performs background initialization and updates the UI upon completion.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <remarks>This method should be awaited to ensure that background processing and subsequent UI updates
    /// complete before proceeding. UI updates are dispatched to the main thread to maintain thread safety.</remarks>
    /// <returns>A task that represents the asynchronous load operation.</returns>
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        //TODO - DaveBlack: Add background initialization logic here: e.g., loading assets, initializing/loading grammar resources, checking for updates, etc.
        // 1. Loading must always run on a background thread to avoid blocking the UI.
        // 2. Any UI updates must be dispatched to the UI thread using Dispatcher.UIThread.InvokeAsync.
        // 3. Ensure proper exception handling to log errors without crashing the application.
        // 4. IMPORTANT: Background work may need to run longer than the splash screen display time!
        try
        {
            // Temporarily simulate background work Task.Delay
            const int simulatedWorkDurationMs = 2_500;
            await Task.Delay(simulatedWorkDurationMs, cancellationToken);

            // After background work is complete, update the UI on the main thread
            await Dispatcher.UIThread.InvokeAsync(OpenMainAndClose);
        }
        catch (OperationCanceledException)
        {
            // Expected if window closes early - ignore
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during splash screen background work");
        }
    }

    /// <summary>
    /// Invokes the main action if it has not already been executed, then closes the current context.
    /// </summary>
    /// <remarks>This method ensures that the main action is executed only once, even if called multiple times
    /// concurrently. After invoking the main action, it always calls the close operation, regardless of whether the
    /// action succeeds or throws an exception.</remarks>
    private void OpenMainAndClose()
    {
        if (Interlocked.Exchange(ref _mainActionInvoked, 1) != 0)
        {
            return;
        }

        try
        {
            _mainAction?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error opening main window");
        }
        finally
        {
            Close();
        }
    }

    /// <summary>
    /// Disposes the specified cancellation token source if it is the current load cancellation token source.
    /// </summary>
    /// <remarks>This method ensures that the cancellation token source is only disposed when it is no longer
    /// in use, preventing potential race conditions. It is intended for internal resource management and should be used
    /// with care to avoid disposing a token source that may still be in use elsewhere.</remarks>
    /// <param name="loadTask">The task associated with the load operation. Used to check for faulted state before disposing the cancellation
    /// token source.</param>
    /// <param name="cancellationTokenSource">The cancellation token source to be disposed if it matches the current load cancellation token source.</param>
    private void DisposeLoadCancellationTokenSource(Task? loadTask, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            // If we ever want extra diagnostics here...
            if (loadTask?.IsFaulted == true)
            {
                // Intentionally empty. Exceptions are logged via SafeFireAndForget
            }
        }
        finally
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _loadCancellationTokenSource, null, cancellationTokenSource),
                    cancellationTokenSource))
            {
                // Dispose after LoadAsync finished, satisfying CTS Dispose safety requirement
                cancellationTokenSource.Dispose();
            }
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
