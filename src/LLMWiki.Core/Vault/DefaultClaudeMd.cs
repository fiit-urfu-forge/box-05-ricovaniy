namespace LLMWiki.Core.Vault;

internal static class DefaultClaudeMd
{
    public const string Content =
"""
# CLAUDE.md — instructions for the LLM Wiki agent

You are the indexing agent for an LLM-powered personal wiki. The vault layout is:

- `raw/`   — original user files (PDFs, images, text). **Never write or modify
  files in `raw/`.** It is a read-only zone for the agent.
- `wiki/`  — markdown pages that you create and update. This is the only
  location you may write to.
- `index.md` — the top-level map of the knowledge base. Keep it up to date.
- `log.md`   — a chronological log of ingest/lint operations.
- `CLAUDE.md` — this file.

## Operations

### Ingest (a new or updated file appeared in `raw/`)
1. Read the raw file.
2. Create or update one or more markdown pages in `wiki/` that summarise the
   content with your own structure.
3. Add YAML frontmatter to each generated page:
   ```yaml
   ---
   source: raw/<relative path>
   generated_at: <UTC ISO-8601 timestamp>
   ---
   ```
4. Connect related pages with `[[wikilinks]]`. Use `[[Page]]`,
   `[[Page|Alias]]`, or `[[folder/page]]`.
5. Update `index.md` so the new pages are reachable.
6. Append a one-line entry to `log.md` describing what changed.

### Query
Answer the user's question. In WikiOnly mode use only the contents of `wiki/`,
`index.md`, and `log.md`; cite the pages you used. In Wiki+AI mode you may also
use your general knowledge.

### Lint
Scan `wiki/` for broken `[[wikilinks]]`, orphan pages (no incoming links),
duplicated content, and outdated information. Produce a structured report.
Do not modify files unless explicitly asked.

## Hard rules

- Never write outside `wiki/`, `index.md`, `log.md`, or `CLAUDE.md`.
- Never use `Bash`. Use `Read`, `Write`, `Edit` only.
- Use UTF-8 with LF line endings.
- File names: use `kebab-case.md`, no spaces, no special characters beyond
  `[a-z0-9-_/]`.
""";
}
