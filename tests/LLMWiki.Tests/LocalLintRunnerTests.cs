using LLMWiki.Core.Graph;
using LLMWiki.Core.Lint;
using LLMWiki.Core.Vault;

namespace LLMWiki.Tests;

[TestFixture]
public class LocalLintRunnerTests
{
    private string _root = null!;
    private VaultService _vaultService = null!;
    private LLMWiki.Core.Domain.Vault _vault = null!;
    private LocalLintRunner _runner = null!;

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "llmwiki-lint-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        _vaultService = new VaultService();
        var r = await _vaultService.OpenAsync(_root);
        _vault = r.Vault;

        var graph = new GraphBuilder(_vaultService);
        _runner = new LocalLintRunner(graph);
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_root, true); } catch { } }

    private void Wiki(string name, string content) =>
        File.WriteAllText(Path.Combine(_vault.WikiDirectory, name), content);

    [Test]
    public void Run_DetectsBrokenLinksThroughGhostNodes()
    {
        Wiki("a.md", "# A\nrefers to [[missing]]");

        var report = _runner.Run(_vault);
        report.BrokenLinks.Should().ContainSingle()
            .Which.SourcePage.Should().Be("wiki/a.md");
    }

    [Test]
    public void Run_DetectsOrphansWithMissingSource()
    {
        Wiki("orph.md",
"""
---
source: raw/missing.pdf
---

# Orphan
""");
        var report = _runner.Run(_vault);
        report.OrphanPages.Should().ContainSingle()
            .Which.Page.Should().Be("wiki/orph.md");
    }

    [Test]
    public void Run_DetectsIsolatedNode()
    {
        Wiki("a.md", "# A");

        var report = _runner.Run(_vault);
        report.IsolatedNodes.Should().Contain(i => i.Page == "wiki/a.md");
    }

    [Test]
    public void Run_DetectsDuplicateTitles()
    {
        Wiki("a.md", "# Same Title\n[[b]]");
        Wiki("b.md", "# Same Title\n[[a]]");

        var report = _runner.Run(_vault);
        report.Duplicates.Should().ContainSingle();
        report.Duplicates[0].Pages.Should().BeEquivalentTo(new[] { "wiki/a.md", "wiki/b.md" });
    }

    [Test]
    public void Run_NoIssuesWhenWikiIsHealthy()
    {
        File.WriteAllText(Path.Combine(_vault.RawDirectory, "src.md"), "x");
        Wiki("a.md",
"""
---
source: raw/src.md
---

# A

[[b]]
""");
        Wiki("b.md",
"""
---
source: raw/src.md
---

# B

[[a]]
""");
        var report = _runner.Run(_vault);
        report.IssueCount.Should().Be(0);
    }
}
