using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Git;

public sealed class GitSyncStateMachine
{
    private readonly object _lock = new();
    private GitSyncState _state;
    private DateTime? _lastSyncAt;
    private IReadOnlyList<ConflictEntry>? _conflicts;

    public GitSyncStateMachine(GitSyncState initial = GitSyncState.NotConfigured)
    {
        _state = initial;
    }

    public GitSyncStatus CurrentStatus
    {
        get
        {
            lock (_lock) return new GitSyncStatus(_state, _lastSyncAt, _conflicts);
        }
    }

    public bool TryTransition(GitSyncState target)
    {
        lock (_lock)
        {
            if (!IsValidTransition(_state, target)) return false;
            _state = target;
            return true;
        }
    }

    public void MarkPullStarted() => Force(GitSyncState.Pulling);

    public void MarkPushStarted() => Force(GitSyncState.Pushing);

    public void MarkConflict(IReadOnlyList<ConflictEntry> conflicts)
    {
        lock (_lock)
        {
            _state = GitSyncState.Conflict;
            _conflicts = conflicts;
        }
    }

    public void MarkSuccess(DateTime? syncedAt = null)
    {
        lock (_lock)
        {
            _state = GitSyncState.Idle;
            _lastSyncAt = syncedAt ?? DateTime.UtcNow;
            _conflicts = null;
        }
    }

    public void MarkError() => Force(GitSyncState.Error);

    public void MarkConfigured() => Force(GitSyncState.Idle);

    public void MarkNotConfigured() => Force(GitSyncState.NotConfigured);

    private void Force(GitSyncState target)
    {
        lock (_lock) _state = target;
    }

    public static bool IsValidTransition(GitSyncState from, GitSyncState to)
    {
        if (from == to) return false;

        return (from, to) switch
        {
            (GitSyncState.NotConfigured, GitSyncState.Idle) => true,
            (GitSyncState.Idle, GitSyncState.Pulling) => true,
            (GitSyncState.Idle, GitSyncState.Pushing) => true,
            (GitSyncState.Idle, GitSyncState.NotConfigured) => true,
            (GitSyncState.Pulling, GitSyncState.Pushing) => true,
            (GitSyncState.Pulling, GitSyncState.Idle) => true,
            (GitSyncState.Pulling, GitSyncState.Conflict) => true,
            (GitSyncState.Pulling, GitSyncState.Error) => true,
            (GitSyncState.Pushing, GitSyncState.Idle) => true,
            (GitSyncState.Pushing, GitSyncState.Error) => true,
            (GitSyncState.Conflict, GitSyncState.Pushing) => true,
            (GitSyncState.Conflict, GitSyncState.Idle) => true,
            (GitSyncState.Error, GitSyncState.Idle) => true,
            _ => false,
        };
    }
}
