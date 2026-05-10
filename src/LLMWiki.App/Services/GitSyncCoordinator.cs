using LLMWiki.App.Git;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Git;
using LLMWiki.Core.Settings;
using LLMWiki.Core.Vault;
using Microsoft.Extensions.Logging;

namespace LLMWiki.App.Services;

public sealed class GitSyncCoordinator : IAsyncDisposable
{
    private readonly IVaultService _vault;
    private readonly IPatStorage _patStorage;
    private readonly ISettingsService _settings;
    private readonly ILogger<GitSyncCoordinator> _logger;
    private GitSyncService? _service;
    private AutoSyncTimer? _timer;

    public event EventHandler<GitOperationResult>? OperationCompleted;
    public event EventHandler<IReadOnlyList<ConflictEntry>>? ConflictDetected;

    public GitSyncCoordinator(
        IVaultService vault,
        IPatStorage patStorage,
        ISettingsService settings,
        ILogger<GitSyncCoordinator> logger)
    {
        _vault = vault;
        _patStorage = patStorage;
        _settings = settings;
        _logger = logger;
    }

    public GitSyncStatus Status =>
        _service?.Status ?? GitSyncStatus.NotConfigured;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.Current.GitRemoteUrl) && _service is not null;

    public void EnsureService()
    {
        if (_service is not null) return;
        if (_vault.Current is null) return;
        _service = new GitSyncService(_vault.Current, _patStorage);
    }

    public async Task<GitOperationResult> SetupAsync(
        string remoteUrl, string pat, CancellationToken cancellationToken = default)
    {
        EnsureService();
        if (_service is null)
            return new GitOperationResult(GitOperationOutcome.Error, "Vault is not open");

        var result = await _service.SetupAsync(remoteUrl, pat, cancellationToken)
            .ConfigureAwait(false);
        OperationCompleted?.Invoke(this, result);
        StartAutoSyncIfEnabled();
        return result;
    }

    public async Task<GitOperationResult> PushAsync(CancellationToken cancellationToken = default)
    {
        EnsureService();
        if (_service is null)
            return new GitOperationResult(GitOperationOutcome.Error, "Vault is not open");

        var result = await _service.PushAsync(cancellationToken).ConfigureAwait(false);
        OperationCompleted?.Invoke(this, result);
        return result;
    }

    public async Task<GitOperationResult> PullAsync(CancellationToken cancellationToken = default)
    {
        EnsureService();
        if (_service is null)
            return new GitOperationResult(GitOperationOutcome.Error, "Vault is not open");

        var result = await _service.PullAsync(cancellationToken).ConfigureAwait(false);
        OperationCompleted?.Invoke(this, result);

        if (result.Outcome == GitOperationOutcome.Conflict && result.Conflicts is not null)
            ConflictDetected?.Invoke(this, result.Conflicts);

        return result;
    }

    public void StartAutoSyncIfEnabled()
    {
        if (!_settings.Current.GitAutoSync) return;
        if (_timer is not null) return;

        var interval = TimeSpan.FromMinutes(
            Math.Max(1, _settings.Current.GitAutoSyncIntervalMinutes));

        _timer = new AutoSyncTimer(interval, async ct =>
        {
            try
            {
                if (Status.State == GitSyncState.Conflict) return;
                var pull = await PullAsync(ct).ConfigureAwait(false);
                if (pull.Outcome == GitOperationOutcome.Ok)
                    await PushAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AutoSync iteration failed");
            }
        });
        _timer.Start();
    }

    public async Task StopAutoSyncAsync()
    {
        if (_timer is not null)
        {
            await _timer.DisposeAsync().ConfigureAwait(false);
            _timer = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAutoSyncAsync().ConfigureAwait(false);
    }
}
