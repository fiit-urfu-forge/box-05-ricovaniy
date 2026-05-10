using LLMWiki.Core.Agent;
using LLMWiki.Core.Settings;
using LLMWiki.Core.Vault;

namespace LLMWiki.App.Agent;

public sealed class SdkClaudeAgentFactory : IClaudeAgentFactory
{
    private readonly ISettingsService _settings;

    public SdkClaudeAgentFactory(ISettingsService settings)
    {
        _settings = settings;
    }

    public IClaudeAgent Create(IVaultService vaultService)
    {
        var vault = vaultService.Current
            ?? throw new InvalidOperationException("Vault is not open");

        var s = _settings.Current;
        var timeout = TimeSpan.FromMinutes(Math.Max(1, s.ClaudeTimeoutMinutes));
        var stalled = TimeSpan.FromSeconds(Math.Max(15, s.StalledStreamSeconds));
        return new SdkClaudeAgent(vault, timeout, stalled);
    }
}
