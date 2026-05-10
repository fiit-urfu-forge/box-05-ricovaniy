using LLMWiki.Core.Vault;

namespace LLMWiki.Core.Agent;

public interface IClaudeAgentFactory
{
    IClaudeAgent Create(IVaultService vaultService);
}
