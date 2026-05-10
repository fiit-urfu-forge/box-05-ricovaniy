using LLMWiki.Core.Git;

namespace LLMWiki.Tests;

[TestFixture]
public class GitPorcelainParserTests
{
    [Test]
    public void Parse_NoOutput_EmptyResult()
    {
        GitPorcelainParser.Parse(string.Empty).Should().BeEmpty();
    }

    [Test]
    public void Parse_DetectsConflictUU()
    {
        var output = "UU wiki/page.md\n M wiki/clean.md\n";
        var entries = GitPorcelainParser.Parse(output);
        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.IsConflict && e.RelativePath == "wiki/page.md");
        entries.Should().Contain(e => !e.IsConflict && e.Status == GitFileStatus.Modified);
    }

    [Test]
    public void GetConflictingPaths_ReturnsAllUnmerged()
    {
        var output = "UU a.md\nAA b.md\nDD c.md\n M d.md\n";
        var paths = GitPorcelainParser.GetConflictingPaths(output);
        paths.Should().BeEquivalentTo(new[] { "a.md", "b.md", "c.md" });
    }

    [Test]
    public void Parse_ClassifiesUntrackedAndAdded()
    {
        var output = "?? new.md\nA  staged.md\n";
        var entries = GitPorcelainParser.Parse(output);
        entries.Should().Contain(e => e.Status == GitFileStatus.Untracked);
        entries.Should().Contain(e => e.Status == GitFileStatus.Added);
    }

    [Test]
    public void HasConflicts_FalseWhenAllClean()
    {
        var output = " M a.md\n M b.md\n";
        GitPorcelainParser.HasConflicts(output).Should().BeFalse();
    }
}
