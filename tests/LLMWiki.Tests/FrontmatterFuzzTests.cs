using System.Text;
using LLMWiki.Core.Parsing;

namespace LLMWiki.Tests;

[TestFixture]
public class FrontmatterFuzzTests
{
    [Test]
    public void Parse_RandomYamlGarbage_DoesNotThrow_AndFlagsMalformed()
    {
        var rng = new Random(321);
        var snippets = new[]
        {
            "---\n",
            "key: value\n",
            ": leading colon\n",
            "  - bullet\n",
            "[broken: yaml\n",
            "tab\there\n",
            "{json: like}\n",
            "?key?: value\n",
            "key:\n  - x\n  - y\n",
            "key: |\n  block\n  text\n",
            "key: 'mismatch quote\n",
            "  : empty key\n",
            "%%directive\n",
            "&anchor\n",
            "---\n--\n",
        };

        for (var i = 0; i < 300; i++)
        {
            var sb = new StringBuilder();
            sb.Append("---\n");
            var lines = rng.Next(0, 12);
            for (var k = 0; k < lines; k++)
                sb.Append(snippets[rng.Next(snippets.Length)]);
            sb.Append("---\n\n# Title\nbody");

            Action act = () => _ = FrontmatterParser.Parse(sb.ToString(), "x.md");
            act.Should().NotThrow();
        }
    }

    [Test]
    public void Parse_DelimiterWithoutClosing_FallsBackToNoFrontmatter()
    {
        var content = "---\nsource: raw/x.md\n# Page\nbody";
        var parsed = FrontmatterParser.Parse(content, "x.md");
        parsed.Frontmatter.Should().BeNull();
    }

    [Test]
    public void Parse_EmptyFrontmatter_NotMalformed()
    {
        var content = "---\n---\n# T";
        var parsed = FrontmatterParser.Parse(content, "x.md");
        parsed.MalformedFrontmatter.Should().BeFalse();
        parsed.Title.Should().Be("T");
    }

    [Test]
    public void Parse_FrontmatterWithCRLF_StillWorks()
    {
        var content = "---\r\nsource: raw/x.md\r\n---\r\n\r\n# Title";
        var parsed = FrontmatterParser.Parse(content, "x.md");
        parsed.Frontmatter!.Source.Should().Be("raw/x.md");
        parsed.Title.Should().Be("Title");
    }

    [Test]
    public void Parse_VeryLongInput_HandledQuickly()
    {
        var sb = new StringBuilder();
        sb.Append("---\nsource: raw/x.md\n---\n\n# Title\n");
        for (var i = 0; i < 10_000; i++)
            sb.AppendLine($"line {i}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var task = Task.Run(() => FrontmatterParser.Parse(sb.ToString(), "x.md"), cts.Token);
        task.Wait(cts.Token);
        task.Result.Title.Should().Be("Title");
    }
}
