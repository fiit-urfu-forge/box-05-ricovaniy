using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Agent;

public static class SystemPrompts
{
    public const string IngestPrompt =
"""
You are running an Ingest pass on a personal wiki. The user just added a new
file in `raw/`. Read it and produce one or more pages in `wiki/` that
summarise the content. Add YAML frontmatter (`source`, `generated_at`) to
every generated page. Connect related pages with `[[wikilinks]]`. Update
`index.md` so the new pages are reachable. Append a one-line entry to
`log.md`. Never write outside `wiki/`, `index.md`, `log.md`, or `CLAUDE.md`.
Never use `Bash`.
""";

    public const string LintPrompt =
"""
You are running a Lint pass on the wiki. Scan `wiki/` for broken
`[[wikilinks]]`, orphan pages (no incoming links), isolated nodes, and
duplicated content. Produce a structured report. Do NOT modify any files.
""";

    public const string WikiOnlyQueryPrompt =
"""
Answer the user's question using ONLY the contents of the `wiki/`,
`index.md`, and `log.md` files. Cite the wiki pages you used by their
relative path. If the wiki does not contain the answer, say so explicitly.
Do not use information that is not in these files.
""";

    public const string ExtendedQueryPrompt =
"""
Answer the user's question. You may use the contents of the `wiki/`,
`index.md`, and `log.md` files when relevant, and you may also use your
general knowledge. When you cite information from the wiki, reference the
file by its relative path.
""";

    public static string ForChatMode(ChatMode mode) => mode switch
    {
        ChatMode.WikiOnly => WikiOnlyQueryPrompt,
        ChatMode.Extended => ExtendedQueryPrompt,
        _ => WikiOnlyQueryPrompt,
    };
}
