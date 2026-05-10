using LLMWiki.Core.Infrastructure;
using DomainVault = LLMWiki.Core.Domain.Vault;

namespace LLMWiki.Core.Vault;

public sealed record CleanupReport(
    IReadOnlyList<string> RemovedFiles,
    IReadOnlyList<string> RemovedDirectories)
{
    public bool HadIncident => RemovedFiles.Count > 0 || RemovedDirectories.Count > 0;

    public static readonly CleanupReport Empty =
        new(Array.Empty<string>(), Array.Empty<string>());
}

public sealed class VaultPostOpCleanup
{
    private static readonly HashSet<string> AllowedRootFiles = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "CLAUDE.md", "index.md", "log.md", ".gitignore", ".gitattributes",
    };

    private static readonly HashSet<string> AllowedRootDirectories = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "raw", "wiki", ".git",
    };

    private readonly DomainVault _vault;

    public VaultPostOpCleanup(DomainVault vault)
    {
        _vault = vault;
    }

    public CleanupReport Run()
    {
        var removedFiles = new List<string>();
        var removedDirs = new List<string>();

        if (!Directory.Exists(_vault.Path)) return CleanupReport.Empty;

        foreach (var path in Directory.EnumerateFiles(_vault.Path))
        {
            var name = Path.GetFileName(path);
            if (AllowedRootFiles.Contains(name)) continue;
            if (name.StartsWith('.')) continue;

            TryDelete(path, removedFiles);
        }

        foreach (var dir in Directory.EnumerateDirectories(_vault.Path))
        {
            var name = Path.GetFileName(dir);
            if (AllowedRootDirectories.Contains(name)) continue;
            if (name.StartsWith('.')) continue;

            TryDeleteDir(dir, removedDirs);
        }

        return new CleanupReport(removedFiles, removedDirs);
    }

    public CleanupReport RunWithRollbackProtection(
        IReadOnlySet<string> recentlyTrackedAbsolutePaths)
    {
        var report = Run();
        return report;
    }

    private void TryDelete(string absolutePath, List<string> log)
    {
        try
        {
            PathValidator.EnsureWithin(_vault.Path, absolutePath);
            File.Delete(absolutePath);
            log.Add(Path.GetRelativePath(_vault.Path, absolutePath).Replace('\\', '/'));
        }
        catch
        {
            // best-effort
        }
    }

    private void TryDeleteDir(string absolutePath, List<string> log)
    {
        try
        {
            PathValidator.EnsureWithin(_vault.Path, absolutePath);
            Directory.Delete(absolutePath, recursive: true);
            log.Add(Path.GetRelativePath(_vault.Path, absolutePath).Replace('\\', '/'));
        }
        catch
        {
            // best-effort
        }
    }
}
