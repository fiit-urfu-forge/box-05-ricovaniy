using LLMWiki.Core.Agent;
using LLMWiki.Core.Files;
using LLMWiki.Core.Git;
using LLMWiki.Core.Ingest;
using LLMWiki.Core.Vault;
using Microsoft.Extensions.Logging;

namespace LLMWiki.App.Services;

public sealed class IngestService : IAsyncDisposable
{
    private readonly IVaultService _vault;
    private readonly IFileService _files;
    private readonly IClaudeAgentFactory _agentFactory;
    private readonly ILogger<IngestService> _logger;
    private readonly IngestQueue _queue = new();
    private readonly IngestScheduler _scheduler;
    private readonly CircuitBreaker _breaker = new(
        failureThreshold: AgentLimits.CircuitBreakerFailureThreshold,
        cooldown: AgentLimits.CircuitBreakerCooldown);
    private IngestStateCache? _stateCache;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;

    public event EventHandler<IngestProgressEvent>? Progress;
    public event EventHandler<IngestResult>? Completed;
    public event EventHandler<string>? StatusChanged;

    public IngestService(
        IVaultService vault,
        IFileService files,
        IClaudeAgentFactory agentFactory,
        ILogger<IngestService> logger)
    {
        _vault = vault;
        _files = files;
        _agentFactory = agentFactory;
        _logger = logger;
        _scheduler = new IngestScheduler(_queue);
    }

    public bool IsRunning => _workerTask is { IsCompleted: false };
    public int Pending => _queue.Pending;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var current = _vault.Current
            ?? throw new InvalidOperationException("Vault is not open");

        _stateCache = new IngestStateCache(current.IngestStatePath);
        await _stateCache.LoadAsync(cancellationToken).ConfigureAwait(false);

        _workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _workerTask = Task.Run(() => RunWorkerAsync(_workerCts.Token), _workerCts.Token);
    }

    public bool ScheduleFile(string relativePath) => _scheduler.ScheduleFile(relativePath);

    public void ScheduleFullReindex()
    {
        var paths = _files.EnumerateRawFiles().Select(r => r.RelativePath).ToList();
        _scheduler.ScheduleFullReindex(paths);
    }

    public async Task StopAsync()
    {
        if (_workerCts is null) return;
        _workerCts.Cancel();
        _queue.Complete();
        if (_workerTask is not null)
        {
            try { await _workerTask.ConfigureAwait(false); } catch { }
        }
        _workerCts.Dispose();
        _workerCts = null;
        _workerTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await _queue.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RunWorkerAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _queue.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_breaker.CanProceed())
            {
                StatusChanged?.Invoke(this,
                    $"Сервис временно недоступен — повтор через {_breaker.RemainingCooldown:mm\\:ss}");
                await Task.Delay(_breaker.RemainingCooldown, cancellationToken)
                    .ConfigureAwait(false);
            }

            await ProcessOneAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(IngestRequest request, CancellationToken cancellationToken)
    {
        var vault = _vault.Current;
        if (vault is null) return;
        if (_stateCache is null) return;

        var absolute = Path.Combine(vault.Path, request.RelativePath);
        if (!File.Exists(absolute))
        {
            _stateCache.Remove(request.RelativePath);
            await _stateCache.SaveAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var info = new FileInfo(absolute);

        if (request.Mode == IngestMode.Incremental
            && !_stateCache.ShouldIngest(request.RelativePath, info.LastWriteTimeUtc, info.Length))
        {
            StatusChanged?.Invoke(this,
                $"Пропущен (без изменений): {request.RelativePath}");
            return;
        }

        if (info.Length > FileLimits.MaxIngestSizeBytes)
        {
            StatusChanged?.Invoke(this,
                $"Пропущен — слишком большой: {request.RelativePath}");
            return;
        }

        StatusChanged?.Invoke(this, $"Обработка: {request.RelativePath}");

        IClaudeAgent agent;
        try { agent = _agentFactory.Create(_vault); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Claude agent");
            StatusChanged?.Invoke(this, $"Не удалось создать агента: {ex.Message}");
            _breaker.RecordFailure();
            return;
        }

        var progressReporter = new Progress<IngestProgressEvent>(ev =>
            Progress?.Invoke(this, ev));

        IngestResult result;
        try
        {
            result = await agent
                .IngestAsync(request.RelativePath, progressReporter, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during ingest of {Path}", request.RelativePath);
            _breaker.RecordFailure();
            result = new IngestResult(request.RelativePath, false, 0, 0,
                ex.Message, TimeSpan.Zero);
        }

        if (result.Success)
        {
            _breaker.RecordSuccess();
            _stateCache.MarkIngested(request.RelativePath, info.LastWriteTimeUtc, info.Length);
            await _stateCache.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _breaker.RecordFailure();
            _logger.LogWarning("Ingest failed: {Path} — {Error}",
                request.RelativePath, result.ErrorMessage);
        }

        Completed?.Invoke(this, result);
    }
}
