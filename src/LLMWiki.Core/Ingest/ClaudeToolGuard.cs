using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Core.Ingest;

public enum ToolDecision
{
    Allow,
    Deny
}

public sealed record ToolDecisionResult(ToolDecision Decision, string? Reason)
{
    public static readonly ToolDecisionResult Allowed = new(ToolDecision.Allow, null);

    public static ToolDecisionResult Denied(string reason) =>
        new(ToolDecision.Deny, reason);
}

public sealed class ClaudeToolGuard
{
    private readonly string _vaultRoot;
    private readonly string _wikiRoot;
    private readonly bool _allowEditingClaudeMd;
    private readonly bool _allowEditingIndexAndLog;

    public ClaudeToolGuard(
        string vaultRoot,
        bool allowEditingClaudeMd = true,
        bool allowEditingIndexAndLog = true)
    {
        _vaultRoot = PathValidator.NormalizeRoot(vaultRoot);
        _wikiRoot = PathValidator.NormalizeRoot(Path.Combine(_vaultRoot, "wiki"));
        _allowEditingClaudeMd = allowEditingClaudeMd;
        _allowEditingIndexAndLog = allowEditingIndexAndLog;
    }

    public ToolDecisionResult Decide(string toolName, IReadOnlyDictionary<string, object> input)
    {
        if (string.Equals(toolName, "Bash", StringComparison.OrdinalIgnoreCase))
            return ToolDecisionResult.Denied("Bash tool is not allowed");

        if (!IsWriteTool(toolName))
            return ToolDecisionResult.Allowed;

        var path = ExtractPath(input);
        if (string.IsNullOrWhiteSpace(path))
            return ToolDecisionResult.Denied("Path was not provided to the write tool");

        string resolved;
        try { resolved = Path.GetFullPath(Path.Combine(_vaultRoot, path)); }
        catch { return ToolDecisionResult.Denied("Invalid path"); }

        if (!PathValidator.IsWithin(_vaultRoot, resolved))
            return ToolDecisionResult.Denied(
                $"Write outside vault is forbidden: {resolved}");

        if (PathValidator.IsWithin(_wikiRoot, resolved))
            return ToolDecisionResult.Allowed;

        if (_allowEditingIndexAndLog && IsAllowedRootFile(resolved, "index.md", "log.md"))
            return ToolDecisionResult.Allowed;

        if (_allowEditingClaudeMd && IsAllowedRootFile(resolved, "CLAUDE.md"))
            return ToolDecisionResult.Allowed;

        return ToolDecisionResult.Denied(
            $"Writing outside wiki/ is not allowed: {resolved}");
    }

    private static bool IsWriteTool(string toolName) =>
        toolName.Equals("Write", StringComparison.OrdinalIgnoreCase)
        || toolName.Equals("Edit", StringComparison.OrdinalIgnoreCase)
        || toolName.Equals("MultiEdit", StringComparison.OrdinalIgnoreCase)
        || toolName.Equals("NotebookEdit", StringComparison.OrdinalIgnoreCase);

    private bool IsAllowedRootFile(string resolved, params string[] names)
    {
        foreach (var name in names)
        {
            var allowed = Path.Combine(_vaultRoot, name);
            if (string.Equals(resolved, allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? ExtractPath(IReadOnlyDictionary<string, object> input)
    {
        foreach (var key in new[] { "file_path", "path", "notebook_path" })
        {
            if (input.TryGetValue(key, out var value) && value is string s)
                return s;
        }
        return null;
    }
}
