using LLMWiki.Core.Parsing;

namespace LLMWiki.Tests;

[TestFixture]
public class WikiLinkParserTests
{
    [Test]
    public void Extract_FindsSimpleAndAliasedLinks()
    {
        var links = WikiLinkParser.ExtractLinks(
            "see [[Page]] and [[Other|alias]] also [[folder/sub]]");
        links.Should().HaveCount(3);
        links[0].Should().Be(new WikiLink("Page", null));
        links[1].Should().Be(new WikiLink("Other", "alias"));
        links[2].Should().Be(new WikiLink("folder/sub", null));
    }

    [Test]
    public void Extract_IgnoresLinksInsideFencedCode()
    {
        var content = "before\n```\n[[InCode]]\n```\nafter [[Real]]";
        var links = WikiLinkParser.ExtractLinks(content);
        links.Should().HaveCount(1);
        links[0].Target.Should().Be("Real");
    }

    [Test]
    public void Extract_IgnoresInlineCode()
    {
        var content = "use `[[NotALink]]` but [[YesLink]] works";
        var links = WikiLinkParser.ExtractLinks(content);
        links.Should().ContainSingle();
        links[0].Target.Should().Be("YesLink");
    }

    [Test]
    public void Extract_HandlesMalformedGracefully()
    {
        WikiLinkParser.ExtractLinks("[[]] [[ok]] [[no").Should()
            .ContainSingle().Which.Target.Should().Be("ok");
    }

    [Test]
    public void Extract_HandlesEmptyInput()
    {
        WikiLinkParser.ExtractLinks("").Should().BeEmpty();
    }
}
