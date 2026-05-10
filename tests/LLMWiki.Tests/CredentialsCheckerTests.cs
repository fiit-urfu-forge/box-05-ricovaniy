using LLMWiki.Core.Agent;

namespace LLMWiki.Tests;

[TestFixture]
public class CredentialsCheckerTests
{
    private string _dir = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "llmwiki-cc-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_dir, true); } catch { } }

    [Test]
    public void Check_NoFile_NoApiKey_ReturnsNoCredentials()
    {
        var path = Path.Combine(_dir, ".credentials.json");
        var c = new CredentialsChecker(path);
        c.Check().Status.Should().Be(ClaudeAuthStatus.NoCredentials);
    }

    [Test]
    public async Task Check_WithFile_ReturnsAuthorized()
    {
        var path = Path.Combine(_dir, ".credentials.json");
        await File.WriteAllTextAsync(path, "{}");
        var c = new CredentialsChecker(path);
        c.Check().Status.Should().Be(ClaudeAuthStatus.Authorized);
    }

    [Test]
    public void Check_WithApiKey_ReturnsAuthorized()
    {
        var path = Path.Combine(_dir, ".missing.json");
        var c = new CredentialsChecker(path, () => "sk-ant-...");
        c.Check().Status.Should().Be(ClaudeAuthStatus.Authorized);
    }

    [Test]
    public async Task Check_EmptyFile_FallsBackToNoCredentials()
    {
        var path = Path.Combine(_dir, ".credentials.json");
        await File.WriteAllTextAsync(path, string.Empty);
        var c = new CredentialsChecker(path);
        c.Check().Status.Should().Be(ClaudeAuthStatus.NoCredentials);
    }
}
