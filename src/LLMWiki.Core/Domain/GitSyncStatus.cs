namespace LLMWiki.Core.Domain;

public sealed record GitSyncStatus(
    GitSyncState State,
    DateTime? LastSyncAt,
    IReadOnlyList<ConflictEntry>? ConflictingFiles)
{
    public static readonly GitSyncStatus NotConfigured =
        new(GitSyncState.NotConfigured, null, null);

    public static GitSyncStatus Idle(DateTime? lastSyncAt = null) =>
        new(GitSyncState.Idle, lastSyncAt, null);
}

public sealed record ConflictEntry(
    string RelativePath,
    string LocalContent,
    string RemoteContent,
    DateTime LocalCommitTime,
    DateTime RemoteCommitTime);
