using LLMWiki.Core.Git;

namespace LLMWiki.Tests;

[TestFixture]
public class InMemoryPatStorageTests
{
    [Test]
    public void WriteReadDelete_RoundTrips()
    {
        var s = new InMemoryPatStorage();
        s.Read("a").Should().BeNull();
        s.Write("a", "value");
        s.Read("a").Should().Be("value");
        s.Delete("a");
        s.Read("a").Should().BeNull();
    }
}
