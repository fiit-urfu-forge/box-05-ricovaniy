using LLMWiki.Core.Files;
using LLMWiki.Core.Vault;

namespace LLMWiki.Tests;

[TestFixture]
public class FileServiceTests
{
    private string _tempRoot = null!;
    private string _sourceRoot = null!;
    private VaultService _vault = null!;
    private FileService _files = null!;

    [SetUp]
    public async Task Setup()
    {
        var prefix = "llmwiki-fs-" + Guid.NewGuid();
        _tempRoot = Path.Combine(Path.GetTempPath(), prefix, "vault");
        _sourceRoot = Path.Combine(Path.GetTempPath(), prefix, "src");
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_sourceRoot);
        _vault = new VaultService();
        await _vault.OpenAsync(_tempRoot);
        _files = new FileService(_vault);
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(Path.GetDirectoryName(_tempRoot)!, true); } catch { }
    }

    private async Task<string> WriteSourceAsync(string fileName, string content = "x")
    {
        var dir = Path.Combine(_sourceRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var src = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(src, content);
        return src;
    }

    [Test]
    public async Task AddFileAsync_AcceptsSupportedTypeAndCopies()
    {
        var src = await WriteSourceAsync("doc.md", "# hello");
        var result = await _files.AddFileAsync(new FileAddRequest(src));

        result.Outcome.Should().Be(FileAddOutcome.Added);
        result.CopiedToRelativePath.Should().Be("raw/doc.md");
        File.Exists(Path.Combine(_tempRoot, "raw/doc.md")).Should().BeTrue();
    }

    [Test]
    public async Task AddFileAsync_RejectsUnsupportedExtension()
    {
        var src = await WriteSourceAsync("malware.exe");
        var result = await _files.AddFileAsync(new FileAddRequest(src));
        result.Outcome.Should().Be(FileAddOutcome.SkippedUnsupported);
    }

    [Test]
    public async Task AddFileAsync_DetectsCaseInsensitiveConflict_Default()
    {
        await _files.AddFileAsync(new FileAddRequest(await WriteSourceAsync("Note.md")));
        var second = await WriteSourceAsync("note.md", "different");

        var result = await _files.AddFileAsync(new FileAddRequest(second));
        result.Outcome.Should().Be(FileAddOutcome.NameConflict);
    }

    [Test]
    public async Task AddFileAsync_RenamesOnConflict_WhenRequested()
    {
        await _files.AddFileAsync(new FileAddRequest(await WriteSourceAsync("note.md")));
        var second = await WriteSourceAsync("note.md", "v2");

        var result = await _files.AddFileAsync(
            new FileAddRequest(second, NameConflictResolution.Rename));
        result.Outcome.Should().Be(FileAddOutcome.Renamed);
        result.CopiedToRelativePath.Should().Be("raw/note (1).md");
    }

    [Test]
    public async Task AddFileAsync_ReplacesOnConflict_WhenRequested()
    {
        await _files.AddFileAsync(new FileAddRequest(await WriteSourceAsync("note.md", "v1")));
        var second = await WriteSourceAsync("note.md", "v2");

        var result = await _files.AddFileAsync(
            new FileAddRequest(second, NameConflictResolution.Replace));
        result.Outcome.Should().Be(FileAddOutcome.Replaced);

        var content = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "raw/note.md"));
        content.Should().Be("v2");
    }

    [Test]
    public async Task EnumerateRawFiles_FindsCopiedFiles()
    {
        await _files.AddFileAsync(new FileAddRequest(await WriteSourceAsync("a.md")));
        await _files.AddFileAsync(new FileAddRequest(await WriteSourceAsync("b.txt")));

        var enumerated = _files.EnumerateRawFiles().ToList();
        enumerated.Should().HaveCount(2);
        enumerated.Select(r => r.FileName).Should().BeEquivalentTo(new[] { "a.md", "b.txt" });
    }
}
