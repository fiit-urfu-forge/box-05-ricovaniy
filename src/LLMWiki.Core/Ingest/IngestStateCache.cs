using System.Text.Json;
using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Core.Ingest;

public sealed record IngestStateEntry(
    DateTime LastWriteTimeUtc,
    long SizeBytes,
    DateTime IngestedAt);

public sealed class IngestStateCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _statePath;
    private Dictionary<string, IngestStateEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public IngestStateCache(string statePath)
    {
        _statePath = statePath;
    }

    public IReadOnlyDictionary<string, IngestStateEntry> Entries => _entries;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statePath))
        {
            _entries = new(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_statePath);
            var doc = await JsonSerializer
                .DeserializeAsync<StateDocument>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (doc?.Files is null)
            {
                _entries = new(StringComparer.OrdinalIgnoreCase);
                return;
            }

            _entries = new Dictionary<string, IngestStateEntry>(
                doc.Files,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _entries = new(StringComparer.OrdinalIgnoreCase);
            try { File.Delete(_statePath); } catch { /* best-effort */ }
        }
    }

    public bool ShouldIngest(string relativePath, DateTime lastWriteTimeUtc, long sizeBytes)
    {
        if (!_entries.TryGetValue(relativePath, out var entry))
            return true;

        if (entry.SizeBytes != sizeBytes) return true;

        var diff = (entry.LastWriteTimeUtc - lastWriteTimeUtc).Duration();
        if (diff > TimeSpan.FromSeconds(1)) return true;

        return false;
    }

    public void MarkIngested(string relativePath, DateTime lastWriteTimeUtc, long sizeBytes)
    {
        _entries[relativePath] = new IngestStateEntry(
            lastWriteTimeUtc, sizeBytes, DateTime.UtcNow);
    }

    public void Remove(string relativePath) => _entries.Remove(relativePath);

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var doc = new StateDocument
        {
            Files = new Dictionary<string, IngestStateEntry>(
                _entries, StringComparer.OrdinalIgnoreCase),
        };
        var json = JsonSerializer.Serialize(doc, SerializerOptions);
        await AtomicFile.WriteAllTextAsync(_statePath, json, cancellationToken).ConfigureAwait(false);
    }

    private sealed class StateDocument
    {
        public Dictionary<string, IngestStateEntry>? Files { get; set; }
    }
}
