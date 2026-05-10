using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Parsing;

public sealed record ParsedWikiPage(
    string Title,
    string Body,
    WikiFrontmatter? Frontmatter,
    bool MalformedFrontmatter);
