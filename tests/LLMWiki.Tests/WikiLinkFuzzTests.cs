using System.Text;
using LLMWiki.Core.Parsing;

namespace LLMWiki.Tests;

[TestFixture]
public class WikiLinkFuzzTests
{
    [Test]
    public void ExtractLinks_HandlesRandomGarbage_NoExceptions()
    {
        var rng = new Random(123);
        var bag = new[]
        {
            "[", "]", "|", "[[", "]]", "][", "[]", "[|]",
            "[[no-close", "no-open]]", "[[\n]]", "[[ ]]",
            "[[a|b|c]]", "[[a||b]]", "[[[[a]]]]",
            "```\n[[InsideFence]]\n```",
            "`[[inline]]`",
            "[[a]] text [[b|alias]]",
            "[[a/b/c.md]]",
            "[[unicode тест]]",
            "[[😀]]",
            new string('[', 50),
            new string(']', 50),
        };

        for (var i = 0; i < 500; i++)
        {
            var sb = new StringBuilder();
            var len = rng.Next(0, 20);
            for (var k = 0; k < len; k++)
                sb.Append(bag[rng.Next(bag.Length)]);

            Action act = () => _ = WikiLinkParser.ExtractLinks(sb.ToString());
            act.Should().NotThrow();
        }
    }

    [Test]
    public void ExtractLinks_DoesNotHangOnPathologicalNesting()
    {
        var input = string.Concat(Enumerable.Repeat("[[", 1000))
            + "x"
            + string.Concat(Enumerable.Repeat("]]", 1000));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var task = Task.Run(() => WikiLinkParser.ExtractLinks(input), cts.Token);
        task.Wait(cts.Token);
    }

    [Test]
    public void ExtractLinks_VeryLongInput_DoesNotCrash()
    {
        var input = string.Join('\n', Enumerable.Range(0, 5000)
            .Select(i => i % 7 == 0 ? $"[[page{i}]]" : $"line {i}"));

        var links = WikiLinkParser.ExtractLinks(input);
        links.Count.Should().BeGreaterThan(700);
    }
}
