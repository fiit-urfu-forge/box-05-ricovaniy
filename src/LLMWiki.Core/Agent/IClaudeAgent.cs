using LLMWiki.Core.Domain;
using LLMWiki.Core.Ingest;

namespace LLMWiki.Core.Agent;

public sealed record IngestResult(
    string RelativePath,
    bool Success,
    int CreatedFiles,
    int UpdatedFiles,
    string? ErrorMessage,
    TimeSpan Duration);

public sealed record LintReport(
    int BrokenLinks,
    int OrphanPages,
    int IsolatedNodes,
    int DuplicateGroups,
    string Summary);

public interface IClaudeAgent
{
    Task<IngestResult> IngestAsync(
        string rawFileRelativePath,
        IProgress<IngestProgressEvent>? progress = null,
        CancellationToken cancellationToken = default);

    Task<LintReport> LintAsync(
        IProgress<IngestProgressEvent>? progress = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> QueryStreamAsync(
        string prompt,
        ChatMode mode,
        CancellationToken cancellationToken = default);
}
