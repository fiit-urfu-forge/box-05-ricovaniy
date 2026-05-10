using LLMWiki.Core.Domain;
using LLMWiki.Core.Git;
using LLMWiki.Core.Infrastructure;
using DomainVault = LLMWiki.Core.Domain.Vault;

namespace LLMWiki.App.Git;

public enum GitOperationOutcome
{
    Ok,
    NothingToCommit,
    Conflict,
    Error,
    NetworkError,
    AuthError,
    BlockedByCircuitBreaker,
}

public sealed record GitOperationResult(
    GitOperationOutcome Outcome,
    string? Detail,
    IReadOnlyList<ConflictEntry>? Conflicts = null);

public sealed class GitSyncService
{
    private const string PatKeyPrefix = "git-pat:";

    private readonly DomainVault _vault;
    private readonly IPatStorage _patStorage;
    private readonly GitProcessRunner _runner;
    private readonly GitSyncStateMachine _stateMachine;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GitSyncService(
        DomainVault vault,
        IPatStorage patStorage,
        GitSyncStateMachine? stateMachine = null,
        CircuitBreaker? circuitBreaker = null)
    {
        _vault = vault;
        _patStorage = patStorage;
        _stateMachine = stateMachine ?? new GitSyncStateMachine();
        _circuitBreaker = circuitBreaker ?? new CircuitBreaker();
        _runner = new GitProcessRunner(vault.Path, () => GetPat(_vault.Path));
    }

    public GitSyncStatus Status => _stateMachine.CurrentStatus;

    public async Task<GitOperationResult> SetupAsync(
        string remoteUrl,
        string pat,
        CancellationToken cancellationToken = default)
    {
        var validation = GitRemoteUrlValidator.Validate(remoteUrl);
        if (!validation.IsValid)
            return new GitOperationResult(GitOperationOutcome.Error, validation.Detail);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _patStorage.Write(PatKeyPrefix + _vault.Path, pat);

            if (!Directory.Exists(_vault.GitDirectory))
            {
                var init = await _runner.RunAsync(new[] { "init", "-b", "main" }, cancellationToken)
                    .ConfigureAwait(false);
                if (!init.IsSuccess)
                    return Fail(init.FailureMessage);
            }

            await EnsureGitFilesAsync(cancellationToken).ConfigureAwait(false);

            var existing = await _runner.RunAsync(
                new[] { "remote", "get-url", "origin" }, cancellationToken).ConfigureAwait(false);

            if (existing.ExitCode == 0)
            {
                var setUrl = await _runner.RunAsync(
                    new[] { "remote", "set-url", "origin", remoteUrl },
                    cancellationToken).ConfigureAwait(false);
                if (!setUrl.IsSuccess) return Fail(setUrl.FailureMessage);
            }
            else
            {
                var add = await _runner.RunAsync(
                    new[] { "remote", "add", "origin", remoteUrl },
                    cancellationToken).ConfigureAwait(false);
                if (!add.IsSuccess) return Fail(add.FailureMessage);
            }

            _stateMachine.MarkConfigured();
            return new GitOperationResult(GitOperationOutcome.Ok, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GitOperationResult> PushAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.CanProceed())
            return CircuitBreakerBlocked();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stateMachine.MarkPushStarted();

            var status = await _runner.RunAsync(
                new[] { "status", "--porcelain" }, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(status.StdOut))
            {
                _stateMachine.MarkSuccess();
                return new GitOperationResult(GitOperationOutcome.NothingToCommit, null);
            }

            var add = await _runner.RunAsync(new[] { "add", "-A" }, cancellationToken)
                .ConfigureAwait(false);
            if (!add.IsSuccess) return RecordError(add.FailureMessage);

            var commit = await _runner.RunAsync(
                new[] { "commit", "-m", $"sync: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}" },
                cancellationToken).ConfigureAwait(false);
            if (!commit.IsSuccess) return RecordError(commit.FailureMessage);

            var push = await RunWithRetryAsync(
                new[] { "push", "-u", "origin", "main" }, cancellationToken)
                .ConfigureAwait(false);
            if (!push.IsSuccess) return RecordError(push.FailureMessage);

            _circuitBreaker.RecordSuccess();
            _stateMachine.MarkSuccess();
            return new GitOperationResult(GitOperationOutcome.Ok, null);
        }
        finally { _gate.Release(); }
    }

    public async Task<GitOperationResult> PullAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.CanProceed())
            return CircuitBreakerBlocked();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stateMachine.MarkPullStarted();

            var pull = await RunWithRetryAsync(
                new[] { "pull", "--no-rebase", "origin", "main" }, cancellationToken)
                .ConfigureAwait(false);

            if (!pull.IsSuccess)
            {
                var status = await _runner.RunAsync(
                    new[] { "status", "--porcelain" }, cancellationToken).ConfigureAwait(false);

                if (GitPorcelainParser.HasConflicts(status.StdOut))
                {
                    var conflicts = await ResolveConflictEntriesAsync(
                        GitPorcelainParser.GetConflictingPaths(status.StdOut),
                        cancellationToken).ConfigureAwait(false);

                    await _runner.RunAsync(new[] { "merge", "--abort" }, cancellationToken)
                        .ConfigureAwait(false);

                    _stateMachine.MarkConflict(conflicts);
                    _circuitBreaker.RecordSuccess();
                    return new GitOperationResult(
                        GitOperationOutcome.Conflict,
                        "Merge conflicts detected",
                        conflicts);
                }

                return RecordError(pull.FailureMessage);
            }

            _circuitBreaker.RecordSuccess();
            _stateMachine.MarkSuccess();
            return new GitOperationResult(GitOperationOutcome.Ok, null);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DisableAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _runner.RunAsync(new[] { "remote", "remove", "origin" }, cancellationToken)
                .ConfigureAwait(false);
            _patStorage.Delete(PatKeyPrefix + _vault.Path);
            _stateMachine.MarkNotConfigured();
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task EnsureGitFilesAsync(CancellationToken cancellationToken)
    {
        var ignorePath = Path.Combine(_vault.Path, ".gitignore");
        if (!File.Exists(ignorePath))
            await AtomicFile.WriteAllTextAsync(
                ignorePath, GitFileTemplates.GitIgnore, cancellationToken).ConfigureAwait(false);

        var attrPath = Path.Combine(_vault.Path, ".gitattributes");
        if (!File.Exists(attrPath))
            await AtomicFile.WriteAllTextAsync(
                attrPath, GitFileTemplates.GitAttributes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<ConflictEntry>> ResolveConflictEntriesAsync(
        IReadOnlyList<string> conflictingPaths,
        CancellationToken cancellationToken)
    {
        var entries = new List<ConflictEntry>();
        foreach (var path in conflictingPaths)
        {
            var local = await _runner.RunAsync(
                new[] { "show", $":2:{path}" }, cancellationToken).ConfigureAwait(false);
            var remote = await _runner.RunAsync(
                new[] { "show", $":3:{path}" }, cancellationToken).ConfigureAwait(false);

            entries.Add(new ConflictEntry(
                path,
                local.IsSuccess ? local.StdOut : string.Empty,
                remote.IsSuccess ? remote.StdOut : string.Empty,
                DateTime.UtcNow,
                DateTime.UtcNow));
        }
        return entries;
    }

    private async Task<GitProcessResult> RunWithRetryAsync(
        string[] args, CancellationToken cancellationToken)
    {
        var delays = new[]
        {
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
        };

        GitProcessResult? last = null;
        for (var attempt = 0; attempt < delays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = await _runner.RunAsync(args, cancellationToken).ConfigureAwait(false);
            if (last.IsSuccess) return last;

            if (!IsRetryable(last)) return last;
            await Task.Delay(delays[attempt], cancellationToken).ConfigureAwait(false);
        }
        return last!;
    }

    private static bool IsRetryable(GitProcessResult result) =>
        result.StdErr.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase)
        || result.StdErr.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
        || result.StdErr.Contains("timed out", StringComparison.OrdinalIgnoreCase);

    private GitOperationResult CircuitBreakerBlocked() =>
        new(GitOperationOutcome.BlockedByCircuitBreaker,
            $"Service temporarily unavailable. Retry in {_circuitBreaker.RemainingCooldown:mm\\:ss}");

    private GitOperationResult RecordError(string? message)
    {
        _circuitBreaker.RecordFailure();
        _stateMachine.MarkError();
        return new GitOperationResult(GitOperationOutcome.Error, message);
    }

    private GitOperationResult Fail(string? message)
    {
        _stateMachine.MarkError();
        return new GitOperationResult(GitOperationOutcome.Error, message);
    }

    private string? GetPat(string vaultPath) => _patStorage.Read(PatKeyPrefix + vaultPath);
}
