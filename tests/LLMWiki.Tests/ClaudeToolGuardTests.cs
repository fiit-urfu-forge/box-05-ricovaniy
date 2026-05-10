using LLMWiki.Core.Ingest;

namespace LLMWiki.Tests;

[TestFixture]
public class ClaudeToolGuardTests
{
    private string _vaultRoot = null!;

    [SetUp]
    public void Setup()
    {
        _vaultRoot = Path.Combine(Path.GetTempPath(), "llmwiki-tg-" + Guid.NewGuid());
        Directory.CreateDirectory(_vaultRoot);
        Directory.CreateDirectory(Path.Combine(_vaultRoot, "wiki"));
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_vaultRoot, true); } catch { } }

    private ClaudeToolGuard NewGuard() => new(_vaultRoot);

    [Test]
    public void Bash_AlwaysDenied()
    {
        var d = NewGuard().Decide("Bash", new Dictionary<string, object>());
        d.Decision.Should().Be(ToolDecision.Deny);
    }

    [Test]
    public void Read_AlwaysAllowed()
    {
        var d = NewGuard().Decide("Read", new Dictionary<string, object>
        {
            ["file_path"] = Path.Combine(_vaultRoot, "raw", "x.md"),
        });
        d.Decision.Should().Be(ToolDecision.Allow);
    }

    [Test]
    public void Write_InsideWiki_Allowed()
    {
        var d = NewGuard().Decide("Write", new Dictionary<string, object>
        {
            ["file_path"] = Path.Combine(_vaultRoot, "wiki", "concept.md"),
        });
        d.Decision.Should().Be(ToolDecision.Allow);
    }

    [Test]
    public void Write_InsideRaw_Denied()
    {
        var d = NewGuard().Decide("Write", new Dictionary<string, object>
        {
            ["file_path"] = Path.Combine(_vaultRoot, "raw", "stolen.md"),
        });
        d.Decision.Should().Be(ToolDecision.Deny);
    }

    [Test]
    public void Write_OutsideVault_Denied()
    {
        var d = NewGuard().Decide("Write", new Dictionary<string, object>
        {
            ["file_path"] = Path.Combine(Path.GetTempPath(), "elsewhere.md"),
        });
        d.Decision.Should().Be(ToolDecision.Deny);
    }

    [Test]
    public void Write_PathTraversal_Denied()
    {
        var d = NewGuard().Decide("Write", new Dictionary<string, object>
        {
            ["file_path"] = "wiki/../../escape.md",
        });
        d.Decision.Should().Be(ToolDecision.Deny);
    }

    [Test]
    public void Edit_RootIndexMd_Allowed()
    {
        var d = NewGuard().Decide("Edit", new Dictionary<string, object>
        {
            ["file_path"] = Path.Combine(_vaultRoot, "index.md"),
        });
        d.Decision.Should().Be(ToolDecision.Allow);
    }

    [Test]
    public void Write_NoPath_Denied()
    {
        var d = NewGuard().Decide("Write", new Dictionary<string, object>());
        d.Decision.Should().Be(ToolDecision.Deny);
    }
}
