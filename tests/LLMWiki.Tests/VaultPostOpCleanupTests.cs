using LLMWiki.Core.Vault;

namespace LLMWiki.Tests;

[TestFixture]
public class VaultPostOpCleanupTests
{
    private string _root = null!;
    private VaultService _vaultService = null!;
    private LLMWiki.Core.Domain.Vault _vault = null!;

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "llmwiki-cleanup-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        _vaultService = new VaultService();
        var result = await _vaultService.OpenAsync(_root);
        _vault = result.Vault;
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_root, true); } catch { } }

    [Test]
    public void Run_DoesNothing_WhenVaultIsClean()
    {
        var report = new VaultPostOpCleanup(_vault).Run();
        report.HadIncident.Should().BeFalse();
    }

    [Test]
    public void Run_RemovesUnexpectedRootFiles()
    {
        File.WriteAllText(Path.Combine(_root, "exfiltrated.md"), "agent escape");

        var report = new VaultPostOpCleanup(_vault).Run();
        report.RemovedFiles.Should().Contain("exfiltrated.md");
        File.Exists(Path.Combine(_root, "exfiltrated.md")).Should().BeFalse();
    }

    [Test]
    public void Run_KeepsAllowedRootFiles()
    {
        File.WriteAllText(Path.Combine(_root, ".gitignore"), "x");

        var report = new VaultPostOpCleanup(_vault).Run();
        report.RemovedFiles.Should().NotContain(".gitignore");
        File.Exists(Path.Combine(_root, ".gitignore")).Should().BeTrue();
    }

    [Test]
    public void Run_RemovesUnexpectedRootDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "stolen"));

        var report = new VaultPostOpCleanup(_vault).Run();
        report.RemovedDirectories.Should().Contain("stolen");
    }

    [Test]
    public void Run_KeepsRawWikiAndGitDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var report = new VaultPostOpCleanup(_vault).Run();
        report.RemovedDirectories.Should().BeEmpty();
        Directory.Exists(_vault.RawDirectory).Should().BeTrue();
        Directory.Exists(_vault.WikiDirectory).Should().BeTrue();
        Directory.Exists(Path.Combine(_root, ".git")).Should().BeTrue();
    }
}
