using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Core.Ingest;

public sealed class AgentProgressParser
{
    private readonly string _vaultRoot;

    public AgentProgressParser(string vaultRoot)
    {
        _vaultRoot = PathValidator.NormalizeRoot(vaultRoot);
    }

    public IngestProgressEvent? FromToolUse(
        string toolName,
        IReadOnlyDictionary<string, object> input)
    {
        var rel = TryRelative(input);

        return toolName switch
        {
            "Read" =>
                new IngestProgressEvent(IngestProgressKind.Read, rel, toolName, null),
            "Write" =>
                new IngestProgressEvent(IngestProgressKind.Write, rel, toolName, null),
            "Edit" or "MultiEdit" =>
                new IngestProgressEvent(IngestProgressKind.Edit, rel, toolName, null),
            "NotebookEdit" =>
                new IngestProgressEvent(IngestProgressKind.Notebook, rel, toolName, null),
            "Glob" =>
                new IngestProgressEvent(IngestProgressKind.Glob, rel, toolName,
                    TryGetString(input, "pattern")),
            "Grep" =>
                new IngestProgressEvent(IngestProgressKind.Grep, rel, toolName,
                    TryGetString(input, "pattern")),
            "WebSearch" =>
                new IngestProgressEvent(IngestProgressKind.Search, rel, toolName,
                    TryGetString(input, "query")),
            "WebFetch" =>
                new IngestProgressEvent(IngestProgressKind.WebFetch, rel, toolName,
                    TryGetString(input, "url")),
            "TodoWrite" =>
                new IngestProgressEvent(IngestProgressKind.Todo, rel, toolName, null),
            "Task" or "Agent" =>
                new IngestProgressEvent(IngestProgressKind.Subagent, rel, toolName,
                    TryGetString(input, "subagent_type") ?? TryGetString(input, "description")),
            "Bash" or "BashOutput" or "KillShell" => null,
            _ => new IngestProgressEvent(IngestProgressKind.OtherTool, rel, toolName, null),
        };
    }

    private static string? TryGetString(
        IReadOnlyDictionary<string, object> input, string key) =>
        input.TryGetValue(key, out var v) && v is string s ? s : null;

    public IngestProgressEvent? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var snippet = text.Length > 240 ? text[..240] + "…" : text;
        return new IngestProgressEvent(IngestProgressKind.Text, null, null, snippet);
    }

    private string? TryRelative(IReadOnlyDictionary<string, object> input)
    {
        foreach (var key in new[] { "file_path", "path", "notebook_path" })
        {
            if (input.TryGetValue(key, out var value) && value is string s)
                return ToVaultRelative(s);
        }
        return null;
    }

    private string? ToVaultRelative(string path)
    {
        try
        {
            var combined = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(_vaultRoot, path));

            if (!PathValidator.IsWithin(_vaultRoot, combined))
                return path;

            return Path.GetRelativePath(_vaultRoot, combined).Replace('\\', '/');
        }
        catch
        {
            return path;
        }
    }
}
