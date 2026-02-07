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
using Avalonia.Threading;
using Serilog;

using System.ComponentModel;

namespace MermaidPad.Views.Dialogs;

/// <summary>
/// Provides a base class for modal dialog windows with common dialog behaviors and lifecycle management.
/// </summary>
/// <remarks>
/// Inherit from this class to implement custom dialogs that require consistent close handling and
/// default window settings. By default, dialogs derived from this class are not resizable.
/// </remarks>
internal abstract class DialogBase : Window
{
    private const int FlagNotSet = 0;
    private const int FlagSet = 1;

    private readonly Dispatcher _dispatcher;

    // Prevents repeated dispatcher queueing per close-attempt
    private int _closeWorkScheduledFlag;

    // Prevents reentrancy inside CloseDialogPosted
    private int _closeWorkExecutingFlag;
    private int _isClosedFlag;
    private int _dispatcherShutdownStartedFlag;

    // Ensures dispatcher events are unhooked exactly once.
    private int _dispatcherEventsUnhookedFlag;

    private object? _dialogResultBox;
    private int _hasDialogResultFlag;

    protected DialogBase()
    {
        // The XAML previewer has no running dispatcher — skip runtime
        // lifecycle management in design mode to avoid "Invalid Markup" errors.
        if (Design.IsDesignMode)
        {
            _dispatcher = null!;
            return;
        }

        CanResize = false;

        _dispatcher = Dispatcher.UIThread;
        _dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
    }

    /// <summary>
    /// Requests that the dialog be closed on the UI thread.
    /// </summary>
    /// <remarks>
    /// This method is safe to call from any thread. Only the first close request is honored
    /// until the close either succeeds or is canceled.
    /// </remarks>
    protected void CloseDialog()
    {
        RequestCloseCore(dialogResult: null, hasDialogResult: false);
    }

    /// <summary>
    /// Requests that the dialog be closed and specifies the result value to return to the caller.
    /// </summary>
    /// <remarks>This method is thread-safe and can be called from any thread. Only the first call to close
    /// the dialog is honored until the close operation completes or is canceled.</remarks>
    /// <typeparam name="T">The type of the result value to be returned when the dialog is closed.</typeparam>
    /// <param name="dialogResult">The value to return as the dialog result. This value is passed to any
    /// listeners when the dialog is closed.</param>
    protected void CloseDialog<T>(T dialogResult)
    {
        RequestCloseCore(dialogResult: dialogResult, hasDialogResult: true);
    }

    /// <summary>
    /// Initiates the process to close the dialog, optionally providing a dialog result to be published with the close
    /// request.
    /// </summary>
    /// <remarks>
    /// If multiple close requests are made, only the first request is processed until the current
    /// close operation completes. The actual closing is deferred to the next dispatcher tick to ensure thread safety
    /// and to avoid reentrancy issues during dialog teardown.
    /// </remarks>
    /// <param name="dialogResult">
    /// An optional result value to associate with the dialog closure.
    /// This value is published for consumers to retrieve after the dialog is closed. May be null if no result is required.
    /// </param>
    /// <param name="hasDialogResult">true to indicate that a dialog result is being provided; otherwise, false.</param>
    private void RequestCloseCore(object? dialogResult, bool hasDialogResult)
    {
        // "First request wins" until this close attempt resolves.
        // Acquire the latch first, then check the closed state to remove the pre-check race window.
        if (Interlocked.CompareExchange(ref _closeWorkScheduledFlag, FlagSet, FlagNotSet) != FlagNotSet)
        {
            return;
        }

        // Window could have already closed before we acquired the latch.
        if (Volatile.Read(ref _isClosedFlag) != FlagNotSet)
        {
            ClearPendingCloseRequest();
            return;
        }

        // Publish payload first, then publish flag
        // The Volatile.Write to the flag is the "publish" step: after observing the flag as set,
        // the UI thread can safely read the payload. (This is the classic publish/consume pattern.)
        Volatile.Write(ref _dialogResultBox, dialogResult);
        Volatile.Write(ref _hasDialogResultFlag, hasDialogResult ? FlagSet : FlagNotSet);

        // If dispatcher is shutting down, posted work may never run; best-effort fallback
        if (Volatile.Read(ref _dispatcherShutdownStartedFlag) != FlagNotSet)
        {
            if (_dispatcher.CheckAccess())
            {
                CloseDialogPosted();
            }
            else
            {
                ClearPendingCloseRequest();
            }

            return;
        }

        try
        {
            // Even when invoked from the UI thread (e.g., a Button Click handler), defer the actual Close() to the next
            // dispatcher tick. This avoids closing the window while Avalonia is still in the middle of input/focus routing
            // (reduces teardown reentrancy and related debug noise). It also makes CloseDialog() safe to call from any thread.
            // The latch coalesces concurrent close requests into a single queued work item per close-attempt.
            // Clearing the latch enables retries by design
            _dispatcher.Post(CloseDialogPosted);
        }
        catch (ObjectDisposedException objectDisposedException)
        {
            // Dispatcher is gone; ensure we don't stay rooted via dispatcher event subscriptions.
            UnhookDispatcherEventsOnce();

            ClearPendingCloseRequest();
            OnCloseDispatchFailed(objectDisposedException);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            // Dispatcher is in an invalid state; ensure we don't stay rooted via dispatcher event subscriptions.
            UnhookDispatcherEventsOnce();

            ClearPendingCloseRequest();
            OnCloseDispatchFailed(invalidOperationException);
        }
    }

    /// <summary>
    /// Attempts to close the dialog window if it is in a valid state to do so.
    /// </summary>
    /// <remarks>This method ensures that the dialog is only closed if it is visible and not already in the
    /// process of closing. If a dialog result has been set, it is passed to the close operation. Multiple concurrent
    /// calls are safely handled to prevent duplicate close attempts.</remarks>
    private void CloseDialogPosted()
    {
        if (Volatile.Read(ref _isClosedFlag) != FlagNotSet)
        {
            ClearPendingCloseRequest();
            return;
        }

        if (Interlocked.CompareExchange(ref _closeWorkExecutingFlag, FlagSet, FlagNotSet) != FlagNotSet)
        {
            // Another CloseDialogPosted invocation is already executing.
            // Important: do not clear _closeWorkScheduledFlag here.
            return;
        }

        try
        {
            // If not visible yet, don't burn the close attempt forever—allow retry.
            if (!IsVisible)
            {
                ClearPendingCloseRequest();
                return;
            }

            if (Volatile.Read(ref _hasDialogResultFlag) != FlagNotSet)
            {
                object? result = Volatile.Read(ref _dialogResultBox);
                Close(result);
                return;
            }

            Close();
        }
        finally
        {
            Interlocked.Exchange(ref _closeWorkExecutingFlag, FlagNotSet);

            // Intentionally do NOT clear _closeWorkScheduledFlag here.
            // It is cleared when:
            //  - close is canceled (OnClosing sees e.Cancel == true), or
            //  - the window actually closes (OnClosed), or
            //  - we returned early (not visible / already closed / dispatcher shutdown).
        }
    }

    /// <summary>
    /// Invoked when the window is in the process of closing.
    /// </summary>
    /// <remarks>
    /// <para>Override this method to perform custom logic when the window is closing. If the closing
    /// operation is canceled by setting <see cref="CancelEventArgs.Cancel"/> to <see langword="true"/>,
    /// any pending close requests are cleared.
    /// </para>
    /// <para>
    /// If closing is canceled (e.Cancel = true), this implementation clears the close latch so
    /// callers can re-request close later (for example, after async validation completes).
    /// </para>
    /// </remarks>
    /// <param name="e">A <see cref="WindowClosingEventArgs"/> that contains
    /// the event data for the closing operation.</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (e.Cancel)
        {
            ClearPendingCloseRequest();
        }
    }

    /// <summary>
    /// Handles additional cleanup or state updates when the window is closed.
    /// </summary>
    /// <remarks>Override this method to perform custom actions when the window is closed.
    /// Always calls the base implementation.</remarks>
    /// <param name="e">An <see cref="EventArgs"/> that contains the event data
    /// associated with the window close event.</param>
    protected override void OnClosed(EventArgs e)
    {
        Volatile.Write(ref _isClosedFlag, FlagSet);

        ClearPendingCloseRequest();
        UnhookDispatcherEventsOnce();

        base.OnClosed(e);
    }

    /// <summary>
    /// Handles the event that is raised when the dispatcher shutdown process starts.
    /// </summary>
    /// <param name="sender">The source of the event, typically the dispatcher that is shutting down.</param>
    /// <param name="e">An object that contains the event data.</param>
    private void Dispatcher_ShutdownStarted(object? sender, EventArgs e)
    {
        Volatile.Write(ref _dispatcherShutdownStartedFlag, FlagSet);
    }

    /// <summary>
    /// Optional diagnostics hook for dispatch failures (e.g., dispatcher disposed/shutting down).
    /// </summary>
    /// <remarks>
    /// Base implementation logs the exception at Error level.
    /// </remarks>
    protected virtual void OnCloseDispatchFailed(Exception exception)
    {
        Log.Error(exception, "Failed to dispatch dialog close request.");
    }

    /// <summary>
    /// Clears any pending close request state, resetting related flags and dialog result values.
    /// </summary>
    /// <remarks>
    /// This internal helper is used by <see cref="DialogBase"/> to ensure that any previous close
    /// request is fully cleared before initiating a new close operation. It uses thread-safe
    /// operations (<see cref="Volatile.Write{T}(ref T, T)"/> and <see cref="Interlocked.Exchange(ref int, int)"/>)
    /// to update its internal state, but overall dialog and UI thread-safety is managed by the
    /// surrounding dialog lifecycle and UI framework usage.
    /// </remarks>
    private void ClearPendingCloseRequest()
    {
        Volatile.Write(ref _dialogResultBox, null);
        Volatile.Write(ref _hasDialogResultFlag, FlagNotSet);
        Interlocked.Exchange(ref _closeWorkScheduledFlag, FlagNotSet);
    }

    /// <summary>
    /// Unhooks the dispatcher shutdown event handler if it has not already been unhooked.
    /// </summary>
    /// <remarks>This method ensures that the dispatcher shutdown event handler is removed only once, even if
    /// called multiple times. It is thread-safe and prevents duplicate unhooking in concurrent scenarios.</remarks>
    private void UnhookDispatcherEventsOnce()
    {
        if (Interlocked.Exchange(ref _dispatcherEventsUnhookedFlag, FlagSet) != FlagNotSet)
        {
            return;
        }

        _dispatcher.ShutdownStarted -= Dispatcher_ShutdownStarted;
    }
}
