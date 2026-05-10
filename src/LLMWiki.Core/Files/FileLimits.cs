namespace LLMWiki.Core.Files;

public static class FileLimits
{
    public const long MaxIngestSizeBytes = 50L * 1024 * 1024;

    public const long MaxImageSizeBytes = 20L * 1024 * 1024;

    public const long MaxGithubFileSizeBytes = 100L * 1024 * 1024;

    public const long MaxWikiFileSizeBytes = 10L * 1024 * 1024;

    public const long MaxViewerTextBytes = 10L * 1024 * 1024;

    public const int MaxFileNameLength = 255;

    public const int IngestQueueCapacity = 100;

    public const int VaultFileCountWarningThreshold = 500;

    public const int GraphNodeWarningThreshold = 200;

    public const int GraphEdgeSimplifiedThreshold = 10_000;

    public const int MaxChatMessages = 200;

    public const long MaxChatContextBytes = 2L * 1024 * 1024;
}
