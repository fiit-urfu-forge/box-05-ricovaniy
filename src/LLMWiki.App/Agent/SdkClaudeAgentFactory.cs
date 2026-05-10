using LLMWiki.Core.Agent;
using LLMWiki.Core.Vault;

namespace LLMWiki.App.Agent;

public sealed class SdkClaudeAgentFactory : IClaudeAgentFactory
{
    public IClaudeAgent Create(IVaultService vaultService)
    {
        var vault = vaultService.Current
            ?? throw new InvalidOperationException("Vault is not open");
        return new SdkClaudeAgent(vault);
    }
}
