namespace LLMWiki.Core.Domain;

public sealed record WikiPage(
    string RelativePath,
    string Title,
    string Content,
    IReadOnlyList<string> Links,
    DateTime LastModified,
    WikiFrontmatter? Frontmatter);

public sealed record WikiFrontmatter(
    string? Source,
    DateTime? GeneratedAt,
    bool Orphaned);
