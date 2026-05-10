namespace LLMWiki.Core.Vault;

public sealed record VaultInitializationResult(
    Domain.Vault Vault,
    bool CreatedRaw,
    bool CreatedWiki,
    bool CreatedClaudeMd,
    bool CreatedIndexMd,
    bool CreatedLogMd,
    bool VaultInsideAnotherVault,
    string? RestoredFiles)
{
    public bool IsFreshVault =>
        CreatedRaw && CreatedWiki && CreatedClaudeMd && CreatedIndexMd && CreatedLogMd;
}
