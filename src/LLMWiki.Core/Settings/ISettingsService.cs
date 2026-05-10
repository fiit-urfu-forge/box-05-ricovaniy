using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }

    bool WasResetToDefaults { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
