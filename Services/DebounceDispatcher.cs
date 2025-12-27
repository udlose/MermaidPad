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

using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Services;

/// <summary>
/// Provides methods for debouncing actions, ensuring that actions are only executed after a specified delay
/// and canceling previous pending actions with the same key.
/// </summary>
public interface IDebounceDispatcher
{
    /// <summary>
    /// Executes the specified action after a delay, ensuring that only the most recent invocation for the given key is
    /// executed.
    /// </summary>
    /// <remarks>If this method is called multiple times with the same key before the delay elapses, only the
    /// action from the most recent call will be executed. This method is thread-safe.</remarks>
    /// <param name="key">A unique identifier for the debounce operation. Cannot be null, empty, or consist only of whitespace.</param>
    /// <param name="delay">The amount of time to wait before executing the action. Must be a non-negative <see cref="TimeSpan"/>.</param>
    /// <param name="action">The action to execute after the delay. Cannot be null.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null, empty, or consists only of whitespace.</exception>
    void Debounce(string key, TimeSpan delay, Action action);

    /// <summary>
    /// Cancels the operation associated with the specified key.
    /// </summary>
    /// <remarks>If the specified key is found, the associated cancellation token source is canceled.  If the
    /// key does not exist, no action is taken.</remarks>
    /// <param name="key">The unique identifier for the operation to cancel. Cannot be null, empty, or consist only of whitespace.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null, empty, or consists only of whitespace.</exception>
    void Cancel(string key);
}

/// <summary>
/// Implementation of <see cref="IDebounceDispatcher"/> that manages debounced actions using cancellation tokens.
/// </summary>
public sealed class DebounceDispatcher : IDebounceDispatcher
{
    /// <summary>
    /// The default debounce delay in milliseconds, for text input operations: typing, pasting, etc.
    /// </summary>
    public const int DefaultTextDebounceMilliseconds = 325;

    /// <summary>
    /// The default debounce delay, in milliseconds, for caret-related operations.
    /// </summary>
    public const int DefaultCaretDebounceMilliseconds = 200;

    private readonly ILogger<DebounceDispatcher> _logger;
    private readonly Lock _gate = new Lock();
    private readonly Dictionary<string, CancellationTokenSource> _tokens = new Dictionary<string, CancellationTokenSource>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DebounceDispatcher"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    public DebounceDispatcher(ILogger<DebounceDispatcher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the specified action after a delay, ensuring that only the most recent invocation for the given key is
    /// executed.
    /// </summary>
    /// <remarks>If this method is called multiple times with the same key before the delay elapses, only the
    /// action from the most recent call will be executed. This method is thread-safe.</remarks>
    /// <param name="key">A unique identifier for the debounce operation. Cannot be null, empty, or consist only of whitespace.</param>
    /// <param name="delay">The amount of time to wait before executing the action. Must be a non-negative <see cref="TimeSpan"/>.</param>
    /// <param name="action">The action to execute after the delay. Cannot be null.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null, empty, or consists only of whitespace.</exception>
    public void Debounce(string key, TimeSpan delay, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(action);

        CancellationTokenSource? ctsOld = null;
        lock (_gate)
        {
            if (_tokens.TryGetValue(key, out CancellationTokenSource? existing))
            {
                ctsOld = existing;
            }

            CancellationTokenSource cts = new CancellationTokenSource();
            _tokens[key] = cts;
            _ = RunAsync(key, delay, cts, action);
        }

        ctsOld?.Cancel();
        //Don't dispose here - the RunAsync task will dispose it in its finally block to avoid double-disposal race condition
    }

    /// <summary>
    /// Cancels the operation associated with the specified key.
    /// </summary>
    /// <remarks>If the specified key is found, the associated cancellation token source is canceled.  If the
    /// key does not exist, no action is taken. The CTS is not disposed here - the <see cref="RunAsync"/> task will dispose it
    /// in its finally block to avoid double-disposal.</remarks>
    /// <param name="key">The unique identifier for the operation to cancel. Cannot be null, empty, or consist only of whitespace.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null, empty, or consists only of whitespace.</exception>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The CTS is disposed in the RunAsync method to avoid double-disposal.")]
    public void Cancel(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            if (_tokens.Remove(key, out CancellationTokenSource? cts))
            {
                cts.Cancel();
                // Don't dispose - let RunAsync dispose it to avoid double-disposal
            }
        }
    }

    /// <summary>
    /// Executes the specified action after a delay, unless the operation is canceled.
    /// </summary>
    /// <remarks>If the operation is canceled before the delay elapses, the action will not be executed.  The
    /// cancellation token associated with the specified <paramref name="key"/> is removed  from the internal collection
    /// when the operation completes, regardless of whether it was  canceled or executed successfully.</remarks>
    /// <param name="key">A unique identifier associated with the operation, used to manage cancellation tokens.</param>
    /// <param name="delay">The amount of time to wait before executing the action.</param>
    /// <param name="cts">The <see cref="CancellationTokenSource"/> used to cancel the operation.</param>
    /// <param name="action">The action to execute after the delay, if the operation is not canceled.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task RunAsync(string key, TimeSpan delay, CancellationTokenSource cts, Action action)
    {
        try
        {
            await Task.Delay(delay, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    // Log exceptions from user actions to prevent them from escaping
                    // and potentially preventing cleanup in the finally block
                    _logger.LogError(ex, "Exception in debounced action for key {Key}", key);
                }
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
        finally
        {
            lock (_gate)
            {
                if (_tokens.TryGetValue(key, out CancellationTokenSource? current) && current == cts)
                {
                    _tokens.Remove(key);
                }
            }

            // Cleanup - caller is a fire & forget so we need to dispose of the CTS here
            cts.Dispose();
        }
    }
}

/// <summary>
/// Provides extension methods for <see cref="IDebounceDispatcher"/> to support UI-thread aware debouncing.
/// </summary>
public static class DebounceDispatcherExtensions
{
    /// <summary>
    /// Executes the specified action on the UI thread after a delay, ensuring that only the most recent invocation for the given key is executed.
    /// </summary>
    /// <remarks>This method is useful for debouncing actions that update the UI, ensuring that the UI is not updated too frequently.</remarks>
    /// <param name="dispatcher">The <see cref="IDebounceDispatcher"/> instance.</param>
    /// <param name="key">A unique identifier for the debounce operation. Cannot be null, empty, or consist only of whitespace.</param>
    /// <param name="delay">The amount of time to wait before executing the action. Must be a non-negative <see cref="TimeSpan"/>.</param>
    /// <param name="action">The action to execute after the delay, on the UI thread. Cannot be null.</param>
    /// <param name="priority">The priority with which the action is invoked on the UI thread. Defaults to <see cref="DispatcherPriority.Background"/>.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null, empty, or consists only of whitespace.</exception>
    public static void DebounceOnUI(this IDebounceDispatcher dispatcher, string key, TimeSpan delay, Action action, DispatcherPriority priority)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(action);

        dispatcher.Debounce(key, delay, () => Dispatcher.UIThread.Post(action, priority));
    }
}
