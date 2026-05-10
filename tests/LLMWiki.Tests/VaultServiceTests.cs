using LLMWiki.Core.Vault;

namespace LLMWiki.Tests;

[TestFixture]
public class VaultServiceTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "llmwiki-vs-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_tempRoot, true); } catch { }
    }

    [Test]
    public async Task OpenAsync_FreshVault_CreatesStructure()
    {
        var svc = new VaultService();
        var result = await svc.OpenAsync(_tempRoot);

        result.IsFreshVault.Should().BeTrue();
        Directory.Exists(result.Vault.RawDirectory).Should().BeTrue();
        Directory.Exists(result.Vault.WikiDirectory).Should().BeTrue();
        File.Exists(result.Vault.ClaudeMdPath).Should().BeTrue();
        File.Exists(result.Vault.IndexMdPath).Should().BeTrue();
        File.Exists(result.Vault.LogMdPath).Should().BeTrue();
    }

    [Test]
    public async Task OpenAsync_OnExistingVault_PreservesContent()
    {
        var svc = new VaultService();
        await svc.OpenAsync(_tempRoot);

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, "CLAUDE.md"), "# user customised");

        var svc2 = new VaultService();
        var result = await svc2.OpenAsync(_tempRoot);

        result.CreatedClaudeMd.Should().BeFalse();
        var contents = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "CLAUDE.md"));
        contents.Should().Contain("user customised");
    }

    [Test]
    public async Task OpenAsync_RestoresEmptyServiceFiles()
    {
        var svc = new VaultService();
        var first = await svc.OpenAsync(_tempRoot);
        await File.WriteAllTextAsync(first.Vault.IndexMdPath, string.Empty);

        var svc2 = new VaultService();
        var result = await svc2.OpenAsync(_tempRoot);
        result.RestoredFiles.Should().Contain("index.md");
        new FileInfo(first.Vault.IndexMdPath).Length.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetRelativePath_TreatsPathInsideVaultAsPosix()
    {
        var svc = new VaultService();
        var result = await svc.OpenAsync(_tempRoot);
        var inside = Path.Combine(result.Vault.WikiDirectory, "foo.md");
        var rel = svc.GetRelativePath(inside);
        rel.Should().Be("wiki/foo.md");
    }

    [Test]
    public async Task GetRelativePath_RejectsOutsideVault()
    {
        var svc = new VaultService();
        await svc.OpenAsync(_tempRoot);
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "x.md");
        Action act = () => svc.GetRelativePath(outside);
        act.Should().Throw<System.Security.SecurityException>();
    }

    [Test]
    public async Task OpenAsync_DetectsVaultInsideAnotherVault()
    {
        var outer = new VaultService();
        await outer.OpenAsync(_tempRoot);

        var nested = Path.Combine(_tempRoot, "nested-vault");
        Directory.CreateDirectory(nested);

        var inner = new VaultService();
        var res = await inner.OpenAsync(nested);
        res.VaultInsideAnotherVault.Should().BeTrue();
    }
}
