using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Tests;

[TestFixture]
public class AnsiStripperTests
{
    private const char Esc = '';
    private const char Bel = '';

    [Test]
    public void Strip_RemovesCsiSequences()
    {
        AnsiStripper.Strip($"hello{Esc}[31mworld{Esc}[0m!").Should().Be("helloworld!");
    }

    [Test]
    public void Strip_RemovesCursorMovement()
    {
        AnsiStripper.Strip($"a{Esc}[2;5Hb{Esc}[1Ac").Should().Be("abc");
    }

    [Test]
    public void Strip_RemovesOscSequences()
    {
        AnsiStripper.Strip($"before{Esc}]0;title{Bel}after").Should().Be("beforeafter");
    }

    [Test]
    public void Strip_RemovesOscSequencesTerminatedByEscBackslash()
    {
        AnsiStripper.Strip($"before{Esc}]0;title{Esc}\\after").Should().Be("beforeafter");
    }

    [Test]
    public void Strip_RemovesSimpleEscapes()
    {
        AnsiStripper.Strip($"a{Esc}7b{Esc}8c").Should().Be("abc");
    }

    [Test]
    public void Strip_RemovesNonPrintableExceptNewlinesAndTabs()
    {
        AnsiStripper.Strip("line1\nline2\tcol\rline3").Should().Be("line1\nline2\tcolline3");
    }

    [Test]
    public void Strip_EmptyAndNull_ReturnsEmpty()
    {
        AnsiStripper.Strip(string.Empty).Should().BeEmpty();
        AnsiStripper.Strip(null!).Should().BeEmpty();
    }

    [Test]
    public void Strip_PreservesPlainText()
    {
        const string s = "https://claude.ai/login?code=abc";
        AnsiStripper.Strip(s).Should().Be(s);
    }

    [Test]
    public void Strip_HandlesPartialEscapeAtEnd()
    {
        AnsiStripper.Strip($"text{Esc}").Should().Be("text");
        AnsiStripper.Strip($"text{Esc}[").Should().Be("text");
    }
}
