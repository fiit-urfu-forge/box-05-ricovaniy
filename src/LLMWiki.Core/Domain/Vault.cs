namespace LLMWiki.Core.Domain;

public sealed record Vault(string Path, string Name)
{
    public string RawDirectory => System.IO.Path.Combine(Path, "raw");
    public string WikiDirectory => System.IO.Path.Combine(Path, "wiki");
    public string ClaudeMdPath => System.IO.Path.Combine(Path, "CLAUDE.md");
    public string IndexMdPath => System.IO.Path.Combine(Path, "index.md");
    public string LogMdPath => System.IO.Path.Combine(Path, "log.md");
    public string IngestStatePath => System.IO.Path.Combine(RawDirectory, ".ingest_state.json");
    public string GitDirectory => System.IO.Path.Combine(Path, ".git");
}
