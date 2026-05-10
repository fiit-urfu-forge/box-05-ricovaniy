using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Core.Ingest;

public sealed class IngestRollback
{
    private readonly string _vaultRoot;
    private readonly Dictionary<string, FileSnapshot> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public IngestRollback(string vaultRoot)
    {
        _vaultRoot = PathValidator.NormalizeRoot(vaultRoot);
    }

    public IReadOnlyCollection<string> TouchedFiles => _snapshots.Keys;

    public void Track(string absolutePath)
    {
        var resolved = PathValidator.EnsureWithin(_vaultRoot, absolutePath);
        if (_snapshots.ContainsKey(resolved)) return;

        _snapshots[resolved] = File.Exists(resolved)
            ? new FileSnapshot(File.ReadAllText(resolved), Existed: true)
            : new FileSnapshot(string.Empty, Existed: false);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (path, snapshot) in _snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!snapshot.Existed)
            {
                try { File.Delete(path); } catch { }
                continue;
            }

            await AtomicFile
                .WriteAllTextAsync(path, snapshot.Content, cancellationToken)
                .ConfigureAwait(false);
        }

        _snapshots.Clear();
    }

    public void Commit() => _snapshots.Clear();

    private sealed record FileSnapshot(string Content, bool Existed);
}
