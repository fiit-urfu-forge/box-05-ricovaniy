using System.Security;
using System.Text;
using LLMWiki.Core.Infrastructure;
using DomainVault = LLMWiki.Core.Domain.Vault;

namespace LLMWiki.Core.Vault;

public sealed class VaultService : IVaultService
{
    private const string WriteCheckFileName = ".llmwiki_write_check";

    private DomainVault? _current;

    public DomainVault? Current => _current;

    public async Task<VaultInitializationResult> OpenAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var normalized = PathValidator.NormalizeRoot(path);
        Directory.CreateDirectory(normalized);
        await EnsureWritableAsync(normalized, cancellationToken).ConfigureAwait(false);

        var vault = new DomainVault(normalized, Path.GetFileName(normalized));
        var insideAnotherVault = DetectVaultInsideVault(normalized);
        var restored = new List<string>();

        var createdRaw = EnsureDirectory(vault.RawDirectory);
        var createdWiki = EnsureDirectory(vault.WikiDirectory);

        var createdClaudeMd = await EnsureFileAsync(
            vault.ClaudeMdPath, DefaultClaudeMd.Content, restored, "CLAUDE.md", cancellationToken)
            .ConfigureAwait(false);

        var createdIndexMd = await EnsureFileAsync(
            vault.IndexMdPath, DefaultIndexMd, restored, "index.md", cancellationToken)
            .ConfigureAwait(false);

        var createdLogMd = await EnsureFileAsync(
            vault.LogMdPath, DefaultLogMd, restored, "log.md", cancellationToken)
            .ConfigureAwait(false);

        _current = vault;

        return new VaultInitializationResult(
            vault,
            createdRaw,
            createdWiki,
            createdClaudeMd,
            createdIndexMd,
            createdLogMd,
            insideAnotherVault,
            restored.Count == 0 ? null : string.Join(", ", restored));
    }

    public void EnsureWithinVault(string absolutePath)
    {
        var vault = RequireCurrent();
        PathValidator.EnsureWithin(vault.Path, absolutePath);
    }

    public string GetRelativePath(string absolutePath)
    {
        var vault = RequireCurrent();
        EnsureWithinVault(absolutePath);
        var rel = Path.GetRelativePath(vault.Path, absolutePath);
        return rel.Replace('\\', '/').Normalize(NormalizationForm.FormC);
    }

    public string GetAbsolutePath(string relativePath)
    {
        var vault = RequireCurrent();
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Expected a relative path", nameof(relativePath));

        var combined = Path.GetFullPath(Path.Combine(vault.Path, relativePath));
        PathValidator.EnsureWithin(vault.Path, combined);
        return combined;
    }

    public void Clear() => _current = null;

    private DomainVault RequireCurrent() =>
        _current ?? throw new InvalidOperationException("No vault is currently open");

    private static async Task EnsureWritableAsync(string root, CancellationToken cancellationToken)
    {
        var probe = Path.Combine(root, WriteCheckFileName);
        try
        {
            await AtomicFile.WriteAllTextAsync(probe, "ok", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new SecurityException(
                $"Vault folder is not writable: {root}", ex);
        }
        finally
        {
            try { File.Delete(probe); } catch { /* best-effort */ }
        }
    }

    private static bool EnsureDirectory(string path)
    {
        if (Directory.Exists(path)) return false;
        Directory.CreateDirectory(path);
        return true;
    }

    private static async Task<bool> EnsureFileAsync(
        string path,
        string defaultContent,
        List<string> restored,
        string label,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            await AtomicFile
                .WriteAllTextAsync(path, defaultContent, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var info = new FileInfo(path);
        if (info.Length == 0)
        {
            await AtomicFile
                .WriteAllTextAsync(path, defaultContent, cancellationToken)
                .ConfigureAwait(false);
            restored.Add(label);
        }

        return false;
    }

    private static bool DetectVaultInsideVault(string root)
    {
        try
        {
            var parent = Directory.GetParent(root);
            while (parent is not null)
            {
                if (LooksLikeVault(parent.FullName))
                    return true;
                parent = parent.Parent;
            }
        }
        catch
        {
            // ignore — best-effort heuristic
        }
        return false;
    }

    private static bool LooksLikeVault(string path)
    {
        return Directory.Exists(Path.Combine(path, "raw"))
            && Directory.Exists(Path.Combine(path, "wiki"))
            && File.Exists(Path.Combine(path, "CLAUDE.md"));
    }

    private const string DefaultIndexMd =
"""
# Index

This file is the top-level map of the knowledge base. The agent updates it
as new pages are added to `wiki/`.
""";

    private const string DefaultLogMd =
"""
# Log

Chronological journal of ingest and lint operations performed by the agent.
""";
}
