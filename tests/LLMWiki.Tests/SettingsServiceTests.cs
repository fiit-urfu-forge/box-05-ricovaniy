using LLMWiki.Core.Domain;
using LLMWiki.Core.Settings;

namespace LLMWiki.Tests;

[TestFixture]
public class SettingsServiceTests
{
    private string _tempDir = null!;
    private string _settingsFile = null!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "llmwiki-st-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _settingsFile = Path.Combine(_tempDir, "settings.json");
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Test]
    public async Task LoadAsync_ReturnsDefaults_WhenFileMissing()
    {
        var svc = new SettingsService(_settingsFile);
        var s = await svc.LoadAsync();

        s.WikiOnlyMode.Should().BeTrue();
        s.GitAutoSyncIntervalMinutes.Should().Be(15);
        svc.WasResetToDefaults.Should().BeFalse();
    }

    [Test]
    public async Task SaveThenLoad_RoundTrips()
    {
        var svc = new SettingsService(_settingsFile);
        var settings = AppSettings.Default();
        settings.VaultPath = "/some/vault";
        settings.GitRemoteUrl = "https://github.com/u/r";
        settings.GitAutoSync = true;
        settings.GitAutoSyncIntervalMinutes = 30;
        settings.WikiOnlyMode = false;

        await svc.SaveAsync(settings);

        var svc2 = new SettingsService(_settingsFile);
        var loaded = await svc2.LoadAsync();
        loaded.VaultPath.Should().Be("/some/vault");
        loaded.GitRemoteUrl.Should().Be("https://github.com/u/r");
        loaded.GitAutoSync.Should().BeTrue();
        loaded.GitAutoSyncIntervalMinutes.Should().Be(30);
        loaded.WikiOnlyMode.Should().BeFalse();
    }

    [Test]
    public async Task LoadAsync_RecoversFromMalformedJson()
    {
        await File.WriteAllTextAsync(_settingsFile, "{ not valid json");

        var svc = new SettingsService(_settingsFile);
        var loaded = await svc.LoadAsync();
        loaded.Should().NotBeNull();
        svc.WasResetToDefaults.Should().BeTrue();
    }

    [Test]
    public async Task SaveAsync_AppliesDefaultsForZeroInterval()
    {
        var svc = new SettingsService(_settingsFile);
        var s = AppSettings.Default();
        s.GitAutoSyncIntervalMinutes = 0;
        await svc.SaveAsync(s);

        var loaded = await new SettingsService(_settingsFile).LoadAsync();
        loaded.GitAutoSyncIntervalMinutes.Should().Be(15);
    }
}
