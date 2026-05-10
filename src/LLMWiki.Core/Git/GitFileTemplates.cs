namespace LLMWiki.Core.Git;

public static class GitFileTemplates
{
    public const string GitIgnore =
"""
# LLM Wiki app settings (local only)
settings.json

# OS files
.DS_Store
Thumbs.db
desktop.ini

# Ingest state cache
raw/.ingest_state.json
""";

    public const string GitAttributes =
"""
* text=auto eol=lf
*.pdf binary
*.png binary
*.jpg binary
*.jpeg binary
*.gif binary
*.webp binary
""";
}
