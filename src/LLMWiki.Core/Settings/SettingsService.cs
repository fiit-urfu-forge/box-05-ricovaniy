using System.Text.Json;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Core.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _settingsPath;
    private AppSettings _current = AppSettings.Default();

    public SettingsService() : this(LLMWikiPaths.SettingsFile)
    {
    }

    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Current => _current;

    public bool WasResetToDefaults { get; private set; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        WasResetToDefaults = false;

        if (!File.Exists(_settingsPath))
        {
            _current = AppSettings.Default();
            return _current;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var loaded = await JsonSerializer
                .DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            _current = loaded ?? AppSettings.Default();
            ApplyDefaults(_current);
            return _current;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            WasResetToDefaults = true;
            _current = AppSettings.Default();
            return _current;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ApplyDefaults(settings);

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await AtomicFile
            .WriteAllTextAsync(_settingsPath, json, cancellationToken)
            .ConfigureAwait(false);

        _current = settings;
    }

    private static void ApplyDefaults(AppSettings settings)
    {
        if (settings.GitAutoSyncIntervalMinutes <= 0)
            settings.GitAutoSyncIntervalMinutes = 15;
        if (settings.ClaudeTimeoutMinutes <= 0)
            settings.ClaudeTimeoutMinutes = 15;
        if (settings.StalledStreamSeconds <= 0)
            settings.StalledStreamSeconds = 90;
    }
}
