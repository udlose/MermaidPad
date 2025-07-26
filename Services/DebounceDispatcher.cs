namespace MermaidPad.Services;

public interface IDebounceDispatcher
{
    void Debounce(string key, TimeSpan delay, Action action);

    void Cancel(string key);
}

public sealed class DebounceDispatcher : IDebounceDispatcher
{
    public const int DefaultDebounceMilliseconds = 500;

    private readonly Lock _gate = new Lock();
    private readonly Dictionary<string, CancellationTokenSource> _tokens = new Dictionary<string, CancellationTokenSource>();

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
