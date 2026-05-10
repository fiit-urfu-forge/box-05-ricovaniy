namespace LLMWiki.App.Git;

public sealed class AutoSyncTimer : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _onTick;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public AutoSyncTimer(TimeSpan interval, Func<CancellationToken, Task> onTick)
    {
        _interval = interval;
        _onTick = onTick;
    }

    public void Start()
    {
        if (_loop is not null) return;
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await Task.Delay(_interval, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            try { await _onTick(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch { /* swallow — next tick will try again */ }
        }
    }
}
