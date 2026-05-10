using LLMWiki.Core.Ingest;

namespace LLMWiki.Tests;

[TestFixture]
public class IngestRollbackTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "llmwiki-rb-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_root, "wiki"));
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_root, true); } catch { } }

    [Test]
    public async Task Rollback_RemovesNewFiles()
    {
        var rb = new IngestRollback(_root);
        var path = Path.Combine(_root, "wiki", "new.md");
        rb.Track(path);
        await File.WriteAllTextAsync(path, "agent wrote this");

        await rb.RollbackAsync();
        File.Exists(path).Should().BeFalse();
    }

    [Test]
    public async Task Rollback_RestoresExistingContent()
    {
        var path = Path.Combine(_root, "wiki", "existing.md");
        await File.WriteAllTextAsync(path, "original");

        var rb = new IngestRollback(_root);
        rb.Track(path);
        await File.WriteAllTextAsync(path, "agent overwrite");

        await rb.RollbackAsync();
        (await File.ReadAllTextAsync(path)).Should().Be("original");
    }

    [Test]
    public async Task Commit_LeavesChangesAndClearsTracker()
    {
        var rb = new IngestRollback(_root);
        var path = Path.Combine(_root, "wiki", "kept.md");
        rb.Track(path);
        await File.WriteAllTextAsync(path, "kept");

        rb.Commit();
        await rb.RollbackAsync();

        File.Exists(path).Should().BeTrue();
        rb.TouchedFiles.Should().BeEmpty();
    }

    [Test]
    public void Track_RejectsPathOutsideVault()
    {
        var rb = new IngestRollback(_root);
        var outside = Path.Combine(Path.GetTempPath(), "outside.md");
        Action act = () => rb.Track(outside);
        act.Should().Throw<System.Security.SecurityException>();
    }
}
