using System.Security;
using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Tests;

[TestFixture]
public class PathValidatorPropertyTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "llmwiki-pvp-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_root, true); } catch { } }

    [Test]
    public void EnsureWithin_RandomNestedPaths_AlwaysWithin()
    {
        var rng = new Random(42);
        for (var i = 0; i < 200; i++)
        {
            var depth = rng.Next(1, 8);
            var segments = Enumerable
                .Range(0, depth)
                .Select(_ => $"seg{rng.Next(1000)}")
                .ToArray();

            var nested = Path.Combine(new[] { _root }.Concat(segments).ToArray());
            var resolved = PathValidator.EnsureWithin(_root, nested);
            PathValidator.IsWithin(_root, resolved).Should().BeTrue();
        }
    }

    [Test]
    public void EnsureWithin_RandomTraversalPatterns_AlwaysReject()
    {
        var rng = new Random(7);
        var traversalPatterns = new[]
        {
            "../escape",
            "a/../../escape",
            "a/b/../../../escape",
            "../" + new string('x', 64),
            "wiki/../../../../etc/passwd",
        };

        for (var i = 0; i < 50; i++)
        {
            var rel = traversalPatterns[rng.Next(traversalPatterns.Length)];
            var bad = Path.Combine(_root, rel);
            Action act = () => PathValidator.EnsureWithin(_root, bad);
            act.Should().Throw<SecurityException>();
        }
    }

    [Test]
    public void EnsureWithin_SiblingDirectoriesSharingPrefix_AreNotWithin()
    {
        var rng = new Random(13);
        for (var i = 0; i < 50; i++)
        {
            var suffix = $"-evil-{rng.Next(1000)}";
            var sibling = _root + suffix;
            Action act = () => PathValidator.EnsureWithin(_root, sibling);
            act.Should().Throw<SecurityException>();
        }
    }

    [Test]
    public void NormalizeRoot_IsIdempotent()
    {
        var rng = new Random(99);
        for (var i = 0; i < 100; i++)
        {
            var rel = string.Join('/', Enumerable
                .Range(0, rng.Next(1, 6))
                .Select(_ => $"d{rng.Next(100)}"));
            var path = Path.Combine(_root, rel);
            var n1 = PathValidator.NormalizeRoot(path);
            var n2 = PathValidator.NormalizeRoot(n1);
            n2.Should().Be(n1);
        }
    }
}
