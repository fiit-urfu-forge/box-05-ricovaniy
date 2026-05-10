using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Vault;

public interface IVaultService
{
    Domain.Vault? Current { get; }

    Task<VaultInitializationResult> OpenAsync(
        string path,
        CancellationToken cancellationToken = default);

    void EnsureWithinVault(string absolutePath);

    string GetRelativePath(string absolutePath);

    string GetAbsolutePath(string relativePath);

    void Clear();
}
