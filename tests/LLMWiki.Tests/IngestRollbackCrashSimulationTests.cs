using LLMWiki.Core.Ingest;

namespace LLMWiki.Tests;

[TestFixture]
public class IngestRollbackCrashSimulationTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "llmwiki-rbcrash-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_root, "wiki"));
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_root, true); } catch { } }

    [Test]
    public async Task Crash_AfterMultipleWrites_RollbackRestoresAll()
    {
        var rb = new IngestRollback(_root);
        var existing = Path.Combine(_root, "wiki", "existing.md");
        await File.WriteAllTextAsync(existing, "v1");

        var newOne = Path.Combine(_root, "wiki", "new.md");
        var nested = Path.Combine(_root, "wiki", "sub", "page.md");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);

        rb.Track(existing);
        rb.Track(newOne);
        rb.Track(nested);

        await File.WriteAllTextAsync(existing, "agent v2");
        await File.WriteAllTextAsync(newOne, "agent created");
        await File.WriteAllTextAsync(nested, "agent nested");

        // Simulate crash mid-operation -> rollback
        await rb.RollbackAsync();

        (await File.ReadAllTextAsync(existing)).Should().Be("v1");
        File.Exists(newOne).Should().BeFalse();
        File.Exists(nested).Should().BeFalse();
    }

    [Test]
    public async Task RollbackAfterPartialFailure_OnlyAffectsTrackedFiles()
    {
        var rb = new IngestRollback(_root);
        var tracked = Path.Combine(_root, "wiki", "tracked.md");
        var untracked = Path.Combine(_root, "wiki", "untracked.md");

        await File.WriteAllTextAsync(untracked, "user content");
        rb.Track(tracked);
        await File.WriteAllTextAsync(tracked, "agent wrote");

        await rb.RollbackAsync();

        File.Exists(tracked).Should().BeFalse();
        (await File.ReadAllTextAsync(untracked)).Should().Be("user content");
    }

    [Test]
    public async Task TrackAfterRollback_StartsClean()
    {
        var rb = new IngestRollback(_root);
        var path = Path.Combine(_root, "wiki", "x.md");
        rb.Track(path);
        await File.WriteAllTextAsync(path, "first");
        await rb.RollbackAsync();

        rb.Track(path);
        await File.WriteAllTextAsync(path, "second");
        rb.Commit();
        File.Exists(path).Should().BeTrue();
    }
}
