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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels;

/// <summary>
/// Provides a base class for view models that supports property change notification and common functionality for
/// Avalonia applications.
/// </summary>
/// <remarks>
/// Inherit from this class to implement view models that require observable properties and integration
/// with Avalonia's application and window lifetime management. This class is intended for use in MVVM architectures
/// within Avalonia desktop applications.
/// </remarks>
#pragma warning disable IDE0200     // Convert lambda expression to method group
[SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod", Justification = "Enforces consistency and eliminates ambiguity.")]
internal abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Retrieves the main window of the current desktop-style Avalonia application, if available.
    /// </summary>
    /// <remarks>
    /// This returns <see langword="null"/> if the application is not using a classic desktop-style lifetime
    /// or if no main window is set.
    /// </remarks>
    protected static Window? GetParentWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopStyleLifetime)
        {
            return desktopStyleLifetime.MainWindow;
        }

        return null;
    }

    #region Dispatcher wrappers

    /// <summary>
    /// Gets whether the current thread has access to Avalonia's UI thread dispatcher.
    /// </summary>
    protected static bool IsOnUIThread => Dispatcher.UIThread.CheckAccess();

    #region Dispatcher.UIThread.Post wrappers (fire-and-forget)

    /// <summary>
    /// Posts the specified action to be executed on the UI thread with the given priority
    /// and returns immediately (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// <para>Use this method to marshal work onto the UI thread from a background thread. This is
    /// typically required when updating UI elements from non-UI threads.
    /// </para>
    /// <para>
    /// Use this overload only when you do not need to observe completion or exceptions.
    /// If you need to await completion (and reliably observe exceptions), prefer
    /// <see cref="InvokeOnUIThreadAsync(Action, DispatcherPriority, CancellationToken)"/> or
    /// <see cref="RunOnUIThreadAsync(Action, DispatcherPriority, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <param name="action">The action to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    protected static void PostToUIThread(Action action, DispatcherPriority priority = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        Dispatcher.UIThread.Post(action, priority);
    }

    /// <summary>
    /// Posts the specified callback to be executed on the UI thread with the given priority
    /// and returns immediately (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// <para>Use this method to marshal work onto the UI thread from a background thread. This is
    /// typically required when updating UI elements from non-UI threads.
    /// </para>
    /// <para>
    /// This overload is useful to avoid capturing closures when posting work items.
    /// </para>
    /// </remarks>
    /// <param name="callback">The delegate to invoke on the UI thread. Cannot be null.</param>
    /// <param name="state">An object containing data to be used by the callback, or null if no data is needed.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
    protected static void PostToUIThread(SendOrPostCallback callback, object? state, DispatcherPriority priority = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        Dispatcher.UIThread.Post(callback, state, priority);
    }

    #endregion Dispatcher.UIThread.Post wrappers (fire-and-forget)

    #region Dispatcher.UIThread.InvokeAsync wrappers (Task-returning)

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread with the given priority and cancellation token,
    /// and returns a <see cref="Task"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>Use this method to schedule work on the UI thread from a background thread or when thread
    /// affinity is required.</para>
    /// <para>
    /// Cancellation is dispatcher-level: if <paramref name="cancellationToken"/> is canceled before the invocation
    /// begins, the queued operation is aborted and awaiting the returned task will throw
    /// <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    /// <param name="action">The action to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.
    /// If the operation has not started, it will be aborted; if it has started, the invoked code can
    /// cooperate with the cancellation request. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> representing the scheduled operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before or during execution.</exception>
    protected static Task InvokeOnUIThreadAsync(Action action, DispatcherPriority priority = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return Dispatcher.UIThread.InvokeAsync(action, priority, cancellationToken).GetTask();
    }

    /// <summary>
    /// Queues <paramref name="func"/> to run on the UI thread with the given priority and cancellation token,
    /// and returns a <see cref="Task{TResult}"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>Use this method to schedule work on the UI thread from a background thread or when thread
    /// affinity is required.</para>
    /// <para>
    /// Cancellation is dispatcher-level: if <paramref name="cancellationToken"/> is canceled before the invocation
    /// begins, the queued operation is aborted and awaiting the returned task will throw
    /// <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The type of the result produced by the function.</typeparam>
    /// <param name="func">The function to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.
    /// If the operation has not started, it will be aborted; if it has started, the invoked code can
    /// cooperate with the cancellation request. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the scheduled operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before or during execution.</exception>
    protected static Task<TResult> InvokeOnUIThreadAsync<TResult>(Func<TResult> func, DispatcherPriority priority = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<TResult>(cancellationToken);
        }

        return Dispatcher.UIThread.InvokeAsync<TResult>(func, priority, cancellationToken).GetTask();
    }

    /// <summary>
    /// Queues the specified asynchronous delegate to start on the UI thread with the given priority
    /// and returns a <see cref="Task"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Avalonia's <c>InvokeAsync(Func&lt;Task&gt;, DispatcherPriority)</c> overload does not accept a
    /// <see cref="CancellationToken"/>. If you need dispatcher-level cancellation before the delegate starts,
    /// use <see cref="InvokeOnUIThreadAsync(Func{CancellationToken, Task}, DispatcherPriority, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <param name="funcAsync">The asynchronous delegate to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <returns>A <see cref="Task"/> representing the scheduled operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    protected static Task InvokeOnUIThreadAsync(Func<Task> funcAsync, DispatcherPriority priority = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        return Dispatcher.UIThread.InvokeAsync(funcAsync, priority);
    }

    /// <summary>
    /// Queues the specified asynchronous delegate to start on the UI thread with the given priority and cancellation token,
    /// and returns a <see cref="Task"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload enables dispatcher-level cancellation (before the delegate starts) while still supporting async code.
    /// If <paramref name="cancellationToken"/> is canceled before execution begins, the delegate is not invoked.
    /// </para>
    /// <para>
    /// Once execution begins, cancellation is cooperative. Because <paramref name="funcAsync"/> does not accept a token,
    /// it can only observe <paramref name="cancellationToken"/> if it captures it (e.g., via closure) or otherwise participates
    /// in cooperative cancellation.
    /// </para>
    /// </remarks>
    /// <param name="funcAsync">The asynchronous delegate to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.
    /// If the operation has not started, it will be aborted when the token is canceled. If the operation has started,
    /// the invoked code can cooperate with the cancellation request. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> representing the scheduled operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before execution, or <paramref name="funcAsync"/> / the returned task cooperatively
    /// observed cancellation during execution.</exception>
    protected static Task InvokeOnUIThreadAsync(Func<Task> funcAsync, DispatcherPriority priority, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Task<Task> outerTask = Dispatcher.UIThread
            .InvokeAsync<Task>(() => funcAsync(), priority, cancellationToken)
            .GetTask();

        return outerTask.Unwrap();
    }

    /// <summary>
    /// Queues the specified asynchronous function to start on the UI thread with the given priority
    /// and returns a <see cref="Task{TResult}"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Avalonia's <c>InvokeAsync(Func&lt;Task&lt;TResult&gt;&gt;, DispatcherPriority)</c> overload does not accept a
    /// <see cref="CancellationToken"/>. If you need dispatcher-level cancellation before the delegate starts,
    /// use <see cref="InvokeOnUIThreadAsync{TResult}(Func{CancellationToken, Task{TResult}}, DispatcherPriority, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The type of the result produced by the asynchronous function.</typeparam>
    /// <param name="funcAsync">The asynchronous function to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the scheduled operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    protected static Task<TResult> InvokeOnUIThreadAsync<TResult>(Func<Task<TResult>> funcAsync, DispatcherPriority priority = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        return Dispatcher.UIThread.InvokeAsync<TResult>(funcAsync, priority);
    }

    /// <summary>
    /// Queues the specified asynchronous delegate to start on the UI thread with the given priority and cancellation token,
    /// and returns a <see cref="Task"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload enables dispatcher-level cancellation (before the delegate starts) while still supporting async code.
    /// If <paramref name="cancellationToken"/> is canceled before execution begins, the delegate is not invoked.
    /// </para>
    /// <para>
    /// Once execution begins, cancellation is cooperative and must be honored by <paramref name="funcAsync"/>
    /// (typically by observing <paramref name="cancellationToken"/> and throwing <see cref="OperationCanceledException"/>).
    /// </para>
    /// </remarks>
    /// <param name="funcAsync">The asynchronous delegate to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.
    /// If the operation has not started, it will be aborted; if it has started, the invoked code can
    /// cooperate with the cancellation request. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> representing the scheduled operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before execution, or <paramref name="funcAsync"/> / the returned task cooperatively
    /// observed cancellation during execution.</exception>
    protected static Task InvokeOnUIThreadAsync(Func<CancellationToken, Task> funcAsync, DispatcherPriority priority = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Task<Task> outerTask = Dispatcher.UIThread
            .InvokeAsync<Task>(() => funcAsync(cancellationToken), priority, cancellationToken)
            .GetTask();

        return outerTask.Unwrap();
    }

    /// <summary>
    /// Queues the specified asynchronous function to start on the UI thread with the given priority and cancellation token,
    /// and returns a <see cref="Task{TResult}"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload enables dispatcher-level cancellation (before the delegate starts) while still supporting async code.
    /// If <paramref name="cancellationToken"/> is canceled before execution begins, the delegate is not invoked.
    /// </para>
    /// <para>
    /// Once execution begins, cancellation is cooperative and must be honored by <paramref name="funcAsync"/>
    /// (typically by observing <paramref name="cancellationToken"/> and throwing <see cref="OperationCanceledException"/>).
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The type of the result produced by the asynchronous function.</typeparam>
    /// <param name="funcAsync">The asynchronous function to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.
    /// If the operation has not started, it will be aborted; if it has started, the invoked code can
    /// cooperate with the cancellation request. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the scheduled operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before execution, or <paramref name="funcAsync"/> / the returned task cooperatively
    /// observed cancellation during execution.</exception>
    protected static Task<TResult> InvokeOnUIThreadAsync<TResult>(Func<CancellationToken, Task<TResult>> funcAsync,
        DispatcherPriority priority = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<TResult>(cancellationToken);
        }

        Task<Task<TResult>> outerTask = Dispatcher.UIThread
            .InvokeAsync<Task<TResult>>(() => funcAsync(cancellationToken), priority, cancellationToken)
            .GetTask();

        return outerTask.Unwrap();
    }

    #endregion Dispatcher.UIThread.InvokeAsync wrappers (Task-returning)

    #endregion Dispatcher wrappers

    #region RunOnUIThreadAsync (inline if on UI thread, otherwise dispatched to UI thread via InvokeAsync)

    /// <summary>
    /// Executes the specified action on the UI thread, using the given dispatcher priority and cancellation token,
    /// and returns a <see cref="Task"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>If called from the UI thread, the action is executed synchronously before the method returns.</para>
    /// <para>If called from a background thread, the action is queued on the UI thread and executed asynchronously.</para>
    /// </remarks>
    /// <param name="action">The action to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The dispatcher priority to use when scheduling the action. The default value is used if not specified.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation before the action is invoked.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before or during execution.</exception>
    protected static Task RunOnUIThreadAsync(Action action, DispatcherPriority priority = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (IsOnUIThread)
        {
            action();
            return Task.CompletedTask;
        }

        return InvokeOnUIThreadAsync(action, priority, cancellationToken);
    }

    /// <summary>
    /// Executes the specified function on the UI thread with the given priority and cancellation token,
    /// and returns a <see cref="Task{TResult}"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>If called from the UI thread, the function is executed synchronously and the result is returned immediately.</para>
    /// <para>If called from a background thread, the function is queued on the UI thread and executed asynchronously.</para>
    /// </remarks>
    /// <typeparam name="TResult">The type of the result produced by the function.</typeparam>
    /// <param name="func">A delegate that represents the function to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the function on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before or during execution.</exception>
    protected static Task<TResult> RunOnUIThreadAsync<TResult>(Func<TResult> func, DispatcherPriority priority = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<TResult>(cancellationToken);
        }

        if (IsOnUIThread)
        {
            TResult resultInline = func();
            return Task.FromResult(resultInline);
        }

        return InvokeOnUIThreadAsync<TResult>(func, priority, cancellationToken);
    }

    /// <summary>
    /// Executes the specified asynchronous delegate on the UI thread with the given priority.
    /// </summary>
    /// <remarks>
    /// <para>If called from the UI thread, the delegate is executed directly; otherwise, it is queued to the UI thread.</para>
    /// <para>
    /// This overload does not support dispatcher-level cancellation. If you need dispatcher-level cancellation before the delegate
    /// starts, use <see cref="RunOnUIThreadAsync(Func{CancellationToken, Task}, DispatcherPriority, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <param name="funcAsync">The asynchronous delegate to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the delegate on the UI thread. The default value is used if not specified.</param>
    /// <returns>A task that represents the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    protected static Task RunOnUIThreadAsync(Func<Task> funcAsync, DispatcherPriority priority = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (IsOnUIThread)
        {
            return funcAsync();
        }

        return InvokeOnUIThreadAsync(funcAsync, priority);
    }

    /// <summary>
    /// Executes the specified asynchronous delegate on the UI thread with the given priority and cancellation token,
    /// and returns a <see cref="Task"/> representing the operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If called from the UI thread, the delegate is invoked immediately and the returned task represents the delegate's
    /// asynchronous completion.
    /// </para>
    /// <para>
    /// If called from a background thread, the delegate is queued to the UI thread via
    /// <see cref="InvokeOnUIThreadAsync(Func{Task}, DispatcherPriority, CancellationToken)"/> and the returned task completes
    /// when the delegate's returned task completes.
    /// </para>
    /// <para>
    /// If <paramref name="cancellationToken"/> is already canceled when this method is called, this method returns a canceled task
    /// and does not invoke or queue the delegate. <see cref="Task.FromCanceled(CancellationToken)"/> requires an already-canceled token,
    /// which is why the cancellation check occurs before any dispatching logic.
    /// </para>
    /// <para>
    /// If the delegate is queued, cancellation is dispatcher-level before the queued invocation begins (the queued operation can be
    /// aborted when canceled), and cooperative after it begins (the invoked code can observe cancellation). If the delegate runs inline
    /// (already on the UI thread), there is no dispatcher-level cancellation involved; any “during execution” cancellation remains purely
    /// cooperative inside the delegate (e.g., by capturing and observing <paramref name="cancellationToken"/>).
    /// </para>
    /// </remarks>
    /// <param name="funcAsync">The asynchronous delegate to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the delegate on the UI thread. If not specified,
    /// the default value of <see cref="DispatcherPriority.Default"/> is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. If the queued operation has not
    /// started, it can be aborted when the token is canceled; if it has started, the invoked code can cooperate with the cancellation
    /// request. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> representing the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before execution, or the delegate / returned task cooperatively observed cancellation during execution.</exception>
    protected static Task RunOnUIThreadAsync(Func<Task> funcAsync, DispatcherPriority priority, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (IsOnUIThread)
        {
            return funcAsync();
        }

        return InvokeOnUIThreadAsync(funcAsync, priority, cancellationToken);
    }

    /// <summary>
    /// Executes the specified asynchronous function on the UI thread with the given priority.
    /// </summary>
    /// <remarks>
    /// <para>If called from the UI thread, the function is executed directly; otherwise, it is queued to the UI thread.</para>
    /// <para>
    /// This overload does not support dispatcher-level cancellation. If you need dispatcher-level cancellation before the delegate
    /// starts, use <see cref="RunOnUIThreadAsync{TResult}(Func{CancellationToken, Task{TResult}}, DispatcherPriority, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The type of the result returned by the asynchronous function.</typeparam>
    /// <param name="funcAsync">The asynchronous function to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The dispatcher priority to use when scheduling the function. If not specified, the default priority is used.</param>
    /// <returns>A task that represents the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    protected static Task<TResult> RunOnUIThreadAsync<TResult>(Func<Task<TResult>> funcAsync, DispatcherPriority priority = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (IsOnUIThread)
        {
            return funcAsync();
        }

        return InvokeOnUIThreadAsync<TResult>(funcAsync, priority);
    }

    /// <summary>
    /// Executes the specified asynchronous delegate on the UI thread with the given priority and cancellation token.
    /// </summary>
    /// <remarks>
    /// <para>If called from the UI thread, the delegate is executed directly; otherwise, it is queued to the UI thread.</para>
    /// <para>
    /// If <paramref name="cancellationToken"/> is canceled before the queued invocation begins, the delegate is not invoked.
    /// Once invoked, cancellation is cooperative and must be honored by the delegate.
    /// </para>
    /// </remarks>
    /// <param name="funcAsync">The asynchronous delegate to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority at which to invoke the delegate on the UI thread. The default value is used if not specified.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation before it begins.</param>
    /// <returns>A task that represents the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before execution, or <paramref name="funcAsync"/> / the returned task cooperatively
    /// observed cancellation during execution.</exception>
    protected static Task RunOnUIThreadAsync(Func<CancellationToken, Task> funcAsync, DispatcherPriority priority = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (IsOnUIThread)
        {
            return funcAsync(cancellationToken);
        }

        return InvokeOnUIThreadAsync(funcAsync, priority, cancellationToken);
    }

    /// <summary>
    /// Executes the specified asynchronous function on the UI thread with the given priority and cancellation token.
    /// </summary>
    /// <remarks>
    /// <para>If called from the UI thread, the function is executed directly; otherwise, it is queued to the UI thread.</para>
    /// <para>
    /// If <paramref name="cancellationToken"/> is canceled before the queued invocation begins, the delegate is not invoked.
    /// Once invoked, cancellation is cooperative and must be honored by the delegate.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The type of the result returned by the asynchronous function.</typeparam>
    /// <param name="funcAsync">The asynchronous function to execute on the UI thread. Cannot be null.</param>
    /// <param name="priority">The dispatcher priority to use when scheduling the function. If not specified, the default priority is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation before it begins.</param>
    /// <returns>A task that represents the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="funcAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the provided
    /// <paramref name="cancellationToken"/> before execution, or <paramref name="funcAsync"/> / the returned task cooperatively
    /// observed cancellation during execution.</exception>
    protected static Task<TResult> RunOnUIThreadAsync<TResult>(Func<CancellationToken, Task<TResult>> funcAsync,
        DispatcherPriority priority = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<TResult>(cancellationToken);
        }

        if (IsOnUIThread)
        {
            return funcAsync(cancellationToken);
        }

        return InvokeOnUIThreadAsync<TResult>(funcAsync, priority, cancellationToken);
    }

    #endregion RunOnUIThreadAsync (inline if on UI thread, otherwise dispatched)
}
