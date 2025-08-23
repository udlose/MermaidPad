namespace MermaidPad.Services;

/// <summary>
/// Provides methods for debouncing actions, ensuring that actions are only executed after a specified delay
/// and canceling previous pending actions with the same key.
/// </summary>
public interface IDebounceDispatcher
{
    /// <summary>
    /// Debounces the specified action using the provided key and delay.
    /// If another action with the same key is already pending, it will be canceled.
    /// </summary>
    /// <param name="key">A unique key to identify the debounced action.</param>
    /// <param name="delay">The delay after which the action should be executed.</param>
    /// <param name="action">The action to execute after the delay.</param>
    void Debounce(string key, TimeSpan delay, Action action);

    /// <summary>
    /// Cancels any pending debounced action associated with the specified key.
    /// </summary>
    /// <param name="key">The key identifying the debounced action to cancel.</param>
    void Cancel(string key);
}

/// <summary>
/// Implementation of <see cref="IDebounceDispatcher"/> that manages debounced actions using cancellation tokens.
/// </summary>
public sealed class DebounceDispatcher : IDebounceDispatcher
{
    /// <summary>
    /// The default debounce delay in milliseconds.
    /// </summary>
    public const int DefaultDebounceMilliseconds = 500;

    private readonly Lock _gate = new Lock();
    private readonly Dictionary<string, CancellationTokenSource> _tokens = new Dictionary<string, CancellationTokenSource>();

    /// <inheritdoc />
    public void Debounce(string key, TimeSpan delay, Action action)
    {
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
    }

    /// <inheritdoc />
    public void Cancel(string key)
    {
        lock (_gate)
        {
            if (_tokens.Remove(key, out CancellationTokenSource? cts))
            {
                cts.Cancel();
            }
        }
    }

    /// <summary>
    /// Runs the specified action asynchronously after the given delay, unless canceled.
    /// </summary>
    /// <param name="key">The key identifying the debounced action.</param>
    /// <param name="delay">The delay before executing the action.</param>
    /// <param name="cts">The cancellation token source for the action.</param>
    /// <param name="action">The action to execute.</param>
    private async Task RunAsync(string key, TimeSpan delay, CancellationTokenSource cts, Action action)
    {
        try
        {
            await Task.Delay(delay, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                action();
            }
        }
        catch (TaskCanceledException) { /* ignore */ }
        finally
        {
            lock (_gate)
            {
                if (_tokens.TryGetValue(key, out CancellationTokenSource? current) && current == cts)
                {
                    _tokens.Remove(key);
                }
            }
        }
    }
}
