using LLMWiki.Core.Ingest;

namespace LLMWiki.Tests;

[TestFixture]
public class AgentProgressParserTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "llmwiki-app-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_root, true); } catch { } }

    [Test]
    public void FromToolUse_ReadAbsolutePath_BecomesVaultRelative()
    {
        var parser = new AgentProgressParser(_root);
        var ev = parser.FromToolUse("Read", new Dictionary<string, object>
        {
            ["file_path"] = Path.Combine(_root, "raw", "doc.md"),
        });
        ev!.Kind.Should().Be(IngestProgressKind.Read);
        ev.RelativePath.Should().Be("raw/doc.md");
    }

    [Test]
    public void FromToolUse_WriteRelativePath_RemainsVaultRelative()
    {
        var parser = new AgentProgressParser(_root);
        var ev = parser.FromToolUse("Write", new Dictionary<string, object>
        {
            ["file_path"] = "wiki/sub/page.md",
        });
        ev!.Kind.Should().Be(IngestProgressKind.Write);
        ev.RelativePath.Should().Be("wiki/sub/page.md");
    }

    [Test]
    public void FromToolUse_Bash_ReturnsNull()
    {
        var parser = new AgentProgressParser(_root);
        var ev = parser.FromToolUse("Bash", new Dictionary<string, object>());
        ev.Should().BeNull();
    }

    [Test]
    public void FromText_TruncatesLongSnippet()
    {
        var parser = new AgentProgressParser(_root);
        var long_ = new string('a', 500);
        var ev = parser.FromText(long_);
        ev!.Snippet!.Length.Should().Be(241);
        ev.Snippet.Should().EndWith("…");
    }

    [Test]
    public void FromText_Empty_ReturnsNull()
    {
        var parser = new AgentProgressParser(_root);
        parser.FromText("").Should().BeNull();
        parser.FromText(null).Should().BeNull();
    }
}
