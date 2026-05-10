namespace LLMWiki.Core.Ingest;

public enum IngestMode
{
    Incremental,
    Full
}

public sealed record IngestRequest(string RelativePath, IngestMode Mode = IngestMode.Incremental);
