using LLMWiki.Core.Domain;
using LLMWiki.Core.Graph;
using LLMWiki.Core.Vault;

namespace LLMWiki.Tests;

[TestFixture]
public class GraphBuilderTests
{
    private string _root = null!;
    private VaultService _vault = null!;
    private GraphBuilder _graph = null!;

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "llmwiki-gb-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        _vault = new VaultService();
        await _vault.OpenAsync(_root);
        _graph = new GraphBuilder(_vault);
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private void WikiFile(string name, string content) =>
        File.WriteAllText(Path.Combine(_root, "wiki", name), content);

    [Test]
    public void Build_CreatesNodesAndEdgesForExistingPages()
    {
        WikiFile("a.md", "# A\nlinks to [[b]]");
        WikiFile("b.md", "# B\nback to [[a]]");

        var g = _graph.Build();

        g.Nodes.Where(n => n.Type == NodeType.WikiPage).Should().HaveCount(2);
        g.Edges.Should().Contain(e => e.Source == "wiki/a.md" && e.Target == "wiki/b.md");
        g.Edges.Should().Contain(e => e.Source == "wiki/b.md" && e.Target == "wiki/a.md");
    }

    [Test]
    public void Build_CreatesGhostNodeForBrokenLink()
    {
        WikiFile("a.md", "# A\nlinks to [[missing]]");

        var g = _graph.Build();

        g.Nodes.Should().Contain(n => n.IsGhost && n.Id.Equals("wiki/missing.md"));
    }

    [Test]
    public void Build_DeduplicatesEdgesWithSameTarget()
    {
        WikiFile("a.md", "# A\n[[b]] [[b]] [[b|alias]]");
        WikiFile("b.md", "# B");

        var g = _graph.Build();
        g.Edges.Count(e => e.Source == "wiki/a.md" && e.Target == "wiki/b.md").Should().Be(1);
    }

    [Test]
    public void Build_IgnoresSelfLinks()
    {
        WikiFile("a.md", "# A\nself [[a]]");
        var g = _graph.Build();
        g.Edges.Should().NotContain(e =>
            e.Source.Equals(e.Target, StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void Build_MarksOrphanWhenSourceMissing()
    {
        WikiFile("a.md",
"""
---
source: raw/missing.pdf
---

# A
""");
        var g = _graph.Build();
        g.Nodes.Should().Contain(n => n.Id == "wiki/a.md" && n.IsOrphan);
    }

    [Test]
    public void Build_DoesNotMarkOrphanWhenSourceExists()
    {
        File.WriteAllText(Path.Combine(_root, "raw", "real.pdf"), "x");
        WikiFile("a.md",
"""
---
source: raw/real.pdf
---

# A
""");
        var g = _graph.Build();
        g.Nodes.Should().Contain(n => n.Id == "wiki/a.md" && !n.IsOrphan);
    }

    [Test]
    public void Build_IncludesIndexAndLogAsIndexNodes()
    {
        var g = _graph.Build();
        g.Nodes.Where(n => n.Type == NodeType.IndexPage).Should()
            .HaveCount(2);
    }
}
