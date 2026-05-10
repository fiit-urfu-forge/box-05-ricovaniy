namespace LLMWiki.Core.Ingest;

public enum IngestSchedulerMode
{
    Idle,
    Incremental,
    FullPlanned
}

public sealed class IngestScheduler
{
    private readonly IngestQueue _queue;
    private readonly object _lock = new();
    private IngestSchedulerMode _mode = IngestSchedulerMode.Idle;

    public IngestScheduler(IngestQueue queue)
    {
        _queue = queue;
    }

    public IngestSchedulerMode Mode
    {
        get { lock (_lock) return _mode; }
    }

    public bool ScheduleFile(string relativePath)
    {
        lock (_lock)
        {
            if (_mode == IngestSchedulerMode.FullPlanned) return false;
            _mode = IngestSchedulerMode.Incremental;
        }

        return _queue.TryEnqueue(new IngestRequest(relativePath, IngestMode.Incremental));
    }

    public void ScheduleFullReindex(IEnumerable<string> relativePaths)
    {
        lock (_lock)
        {
            _queue.Drain();
            _mode = IngestSchedulerMode.FullPlanned;
        }

        foreach (var path in relativePaths)
            _queue.TryEnqueue(new IngestRequest(path, IngestMode.Full));
    }

    public void Reset()
    {
        lock (_lock) _mode = IngestSchedulerMode.Idle;
    }
}
