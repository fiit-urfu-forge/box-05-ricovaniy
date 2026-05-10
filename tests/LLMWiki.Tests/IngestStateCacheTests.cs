using LLMWiki.Core.Ingest;

namespace LLMWiki.Tests;

[TestFixture]
public class IngestStateCacheTests
{
    private string _file = null!;
    private string _dir = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "llmwiki-isc-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, ".ingest_state.json");
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Test]
    public async Task ShouldIngest_TrueWhenNoEntry()
    {
        var cache = new IngestStateCache(_file);
        await cache.LoadAsync();
        cache.ShouldIngest("raw/x.md", DateTime.UtcNow, 100).Should().BeTrue();
    }

    [Test]
    public async Task ShouldIngest_FalseAfterMark_AndPersistsAcrossLoads()
    {
        var cache = new IngestStateCache(_file);
        await cache.LoadAsync();
        var ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        cache.MarkIngested("raw/x.md", ts, 100);
        await cache.SaveAsync();

        var cache2 = new IngestStateCache(_file);
        await cache2.LoadAsync();
        cache2.ShouldIngest("raw/x.md", ts, 100).Should().BeFalse();
        cache2.ShouldIngest("raw/x.md", ts.AddHours(1), 100).Should().BeTrue();
        cache2.ShouldIngest("raw/x.md", ts, 200).Should().BeTrue();
    }

    [Test]
    public async Task LoadAsync_RecoversFromCorruptedJson()
    {
        await File.WriteAllTextAsync(_file, "not-json");
        var cache = new IngestStateCache(_file);
        await cache.LoadAsync();
        cache.Entries.Should().BeEmpty();
        File.Exists(_file).Should().BeFalse();
    }
}
