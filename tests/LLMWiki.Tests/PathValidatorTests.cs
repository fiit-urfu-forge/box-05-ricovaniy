using System.Security;
using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Tests;

[TestFixture]
public class PathValidatorTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "llmwiki-pv-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_tempRoot, true); } catch { }
    }

    [Test]
    public void EnsureWithin_RejectsTraversal()
    {
        var sub = Path.Combine(_tempRoot, "child", "..", "..", "outside");
        Action act = () => PathValidator.EnsureWithin(_tempRoot, sub);
        act.Should().Throw<SecurityException>();
    }

    [Test]
    public void EnsureWithin_AcceptsNestedPath()
    {
        var nested = Path.Combine(_tempRoot, "a", "b", "c.md");
        var resolved = PathValidator.EnsureWithin(_tempRoot, nested);
        PathValidator.IsWithin(_tempRoot, resolved).Should().BeTrue();
    }

    [Test]
    public void EnsureWithin_AcceptsRootItself()
    {
        var resolved = PathValidator.EnsureWithin(_tempRoot, _tempRoot);
        resolved.Should().Be(PathValidator.NormalizeRoot(_tempRoot));
    }

    [Test]
    public void EnsureWithin_RejectsSiblingPathSharingPrefix()
    {
        var sibling = _tempRoot + "-evil";
        Action act = () => PathValidator.EnsureWithin(_tempRoot, sibling);
        act.Should().Throw<SecurityException>();
    }

    [TestCase("file.md", true)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    public void IsValidFileName_HandlesBasics(string name, bool expected)
    {
        PathValidator.IsValidFileName(name).Should().Be(expected);
    }

    [Test]
    public void IsValidFileName_RejectsLongName()
    {
        PathValidator.IsValidFileName(new string('a', 256)).Should().BeFalse();
    }
}
