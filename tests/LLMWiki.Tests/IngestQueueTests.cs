using LLMWiki.Core.Ingest;

namespace LLMWiki.Tests;

[TestFixture]
public class IngestQueueTests
{
    [Test]
    public async Task TryEnqueue_DeduplicatesByRelativePath()
    {
        await using var q = new IngestQueue(capacity: 10);
        q.TryEnqueue(new IngestRequest("raw/a.md")).Should().BeTrue();
        q.TryEnqueue(new IngestRequest("raw/a.md")).Should().BeFalse();
        q.Pending.Should().Be(1);
    }

    [Test]
    public async Task ReadAllAsync_RemovesFromPendingSetSoSamePathReenters()
    {
        await using var q = new IngestQueue(capacity: 10);
        q.TryEnqueue(new IngestRequest("raw/a.md")).Should().BeTrue();

        var enumerator = q.ReadAllAsync().GetAsyncEnumerator();
        try
        {
            (await enumerator.MoveNextAsync()).Should().BeTrue();
            enumerator.Current.RelativePath.Should().Be("raw/a.md");
            q.Pending.Should().Be(0);

            q.TryEnqueue(new IngestRequest("raw/a.md")).Should().BeTrue();
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    [Test]
    public async Task Drain_ClearsPending()
    {
        await using var q = new IngestQueue(capacity: 10);
        q.TryEnqueue(new IngestRequest("raw/a.md"));
        q.TryEnqueue(new IngestRequest("raw/b.md"));
        q.Pending.Should().Be(2);

        q.Drain();
        q.Pending.Should().Be(0);
    }
}

[TestFixture]
public class IngestSchedulerTests
{
    [Test]
    public async Task ScheduleFile_AfterFullScheduled_IsRejected()
    {
        await using var q = new IngestQueue(capacity: 10);
        var s = new IngestScheduler(q);

        s.ScheduleFullReindex(new[] { "raw/a.md", "raw/b.md" });
        s.Mode.Should().Be(IngestSchedulerMode.FullPlanned);

        s.ScheduleFile("raw/c.md").Should().BeFalse();
    }

    [Test]
    public async Task ScheduleFile_DedupeViaQueue()
    {
        await using var q = new IngestQueue(capacity: 10);
        var s = new IngestScheduler(q);

        s.ScheduleFile("raw/a.md").Should().BeTrue();
        s.ScheduleFile("raw/a.md").Should().BeFalse();
    }

    [Test]
    public async Task ScheduleFullReindex_DrainsExistingQueue()
    {
        await using var q = new IngestQueue(capacity: 10);
        var s = new IngestScheduler(q);
        s.ScheduleFile("raw/a.md");

        s.ScheduleFullReindex(new[] { "raw/x.md" });
        q.Pending.Should().Be(1);
    }
}
