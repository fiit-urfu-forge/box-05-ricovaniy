using System.Threading.Channels;
using LLMWiki.Core.Files;

namespace LLMWiki.Core.Ingest;

public sealed class IngestQueue : IAsyncDisposable
{
    private readonly Channel<IngestRequest> _channel;
    private readonly HashSet<string> _enqueued = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public IngestQueue(int capacity = FileLimits.IngestQueueCapacity)
    {
        _channel = Channel.CreateBounded<IngestRequest>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    public int Pending
    {
        get
        {
            lock (_lock) return _enqueued.Count;
        }
    }

    public bool TryEnqueue(IngestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_lock)
        {
            if (!_enqueued.Add(request.RelativePath)) return false;
        }

        if (_channel.Writer.TryWrite(request)) return true;

        lock (_lock) _enqueued.Remove(request.RelativePath);
        return false;
    }

    public async ValueTask EnqueueAsync(
        IngestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_lock)
        {
            if (!_enqueued.Add(request.RelativePath)) return;
        }

        try
        {
            await _channel.Writer
                .WriteAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            lock (_lock) _enqueued.Remove(request.RelativePath);
            throw;
        }
    }

    public IAsyncEnumerable<IngestRequest> ReadAllAsync(
        CancellationToken cancellationToken = default) =>
        ReadAllInternal(cancellationToken);

    private async IAsyncEnumerable<IngestRequest> ReadAllInternal(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (var request in _channel.Reader
                           .ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            lock (_lock) _enqueued.Remove(request.RelativePath);
            yield return request;
        }
    }

    public void Drain()
    {
        while (_channel.Reader.TryRead(out var request))
        {
            lock (_lock) _enqueued.Remove(request.RelativePath);
        }
    }

    public void Complete() => _channel.Writer.TryComplete();

    public ValueTask DisposeAsync()
    {
        Complete();
        return ValueTask.CompletedTask;
    }
}
