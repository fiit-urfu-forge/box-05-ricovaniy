using LLMWiki.Core.Domain;
using LLMWiki.Core.Git;

namespace LLMWiki.Tests;

[TestFixture]
public class GitSyncStateMachineTests
{
    [Test]
    public void IsValidTransition_ValidPaths()
    {
        GitSyncStateMachine.IsValidTransition(GitSyncState.NotConfigured, GitSyncState.Idle)
            .Should().BeTrue();
        GitSyncStateMachine.IsValidTransition(GitSyncState.Idle, GitSyncState.Pulling)
            .Should().BeTrue();
        GitSyncStateMachine.IsValidTransition(GitSyncState.Pulling, GitSyncState.Pushing)
            .Should().BeTrue();
        GitSyncStateMachine.IsValidTransition(GitSyncState.Pulling, GitSyncState.Conflict)
            .Should().BeTrue();
        GitSyncStateMachine.IsValidTransition(GitSyncState.Conflict, GitSyncState.Pushing)
            .Should().BeTrue();
    }

    [Test]
    public void IsValidTransition_RejectsInvalidPaths()
    {
        GitSyncStateMachine.IsValidTransition(GitSyncState.NotConfigured, GitSyncState.Pushing)
            .Should().BeFalse();
        GitSyncStateMachine.IsValidTransition(GitSyncState.Conflict, GitSyncState.Pulling)
            .Should().BeFalse();
        GitSyncStateMachine.IsValidTransition(GitSyncState.Idle, GitSyncState.Idle)
            .Should().BeFalse();
    }

    [Test]
    public void MarkConflict_StoresConflicts()
    {
        var sm = new GitSyncStateMachine(GitSyncState.Idle);
        var conflicts = new List<ConflictEntry>
        {
            new("wiki/x.md", "local", "remote", DateTime.UtcNow, DateTime.UtcNow),
        };
        sm.MarkConflict(conflicts);

        var status = sm.CurrentStatus;
        status.State.Should().Be(GitSyncState.Conflict);
        status.ConflictingFiles.Should().BeEquivalentTo(conflicts);
    }

    [Test]
    public void MarkSuccess_ClearsConflicts()
    {
        var sm = new GitSyncStateMachine(GitSyncState.Idle);
        sm.MarkConflict(new List<ConflictEntry>
        {
            new("a", "l", "r", DateTime.UtcNow, DateTime.UtcNow),
        });
        sm.MarkSuccess(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        var status = sm.CurrentStatus;
        status.State.Should().Be(GitSyncState.Idle);
        status.ConflictingFiles.Should().BeNull();
        status.LastSyncAt.Should()
            .Be(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
    }
}
