using LLMWiki.Core.Parsing;

namespace LLMWiki.Tests;

[TestFixture]
public class FrontmatterParserTests
{
    [Test]
    public void Parse_NoFrontmatter_FallsBackToFileNameAndExtractsH1()
    {
        var parsed = FrontmatterParser.Parse("# Hello\nbody", "fallback.md");
        parsed.Frontmatter.Should().BeNull();
        parsed.MalformedFrontmatter.Should().BeFalse();
        parsed.Title.Should().Be("Hello");
    }

    [Test]
    public void Parse_NoH1_UsesFileName()
    {
        var parsed = FrontmatterParser.Parse("just text", "concept.md");
        parsed.Title.Should().Be("concept");
    }

    [Test]
    public void Parse_ValidFrontmatter_ExtractsFields()
    {
        var content =
"""
---
source: raw/article.pdf
generated_at: 2026-01-01T12:00:00Z
orphaned: true
---

# My Page

content
""";
        var parsed = FrontmatterParser.Parse(content, "x.md");

        parsed.Frontmatter.Should().NotBeNull();
        parsed.Frontmatter!.Source.Should().Be("raw/article.pdf");
        parsed.Frontmatter.GeneratedAt.Should()
            .Be(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        parsed.Frontmatter.Orphaned.Should().BeTrue();
        parsed.Title.Should().Be("My Page");
    }

    [Test]
    public void Parse_MalformedFrontmatter_FlagsAndContinues()
    {
        var content = "---\n: bad: yaml: \n  - x\nfoo\n---\n\n# title";
        var parsed = FrontmatterParser.Parse(content, "x.md");

        parsed.MalformedFrontmatter.Should().BeTrue();
        parsed.Title.Should().Be("title");
    }

    [Test]
    public void Compose_RoundTripsRequiredFields()
    {
        var fm = new LLMWiki.Core.Domain.WikiFrontmatter(
            "raw/x.md", new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), false);
        var composed = FrontmatterParser.Compose(fm, "# Body\n");
        composed.Should().StartWith("---\n");
        composed.Should().Contain("source: raw/x.md");
        composed.Should().Contain("generated_at: 2026-05-10T00:00:00Z");
    }
}
