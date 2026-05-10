namespace LLMWiki.Core.Git;

public enum GitFileStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    Untracked,
    Ignored,
    Conflict,
}

public sealed record GitStatusEntry(
    GitFileStatus Status,
    string RelativePath,
    bool IsConflict);

public static class GitPorcelainParser
{
    public static IReadOnlyList<GitStatusEntry> Parse(string porcelainOutput)
    {
        if (string.IsNullOrEmpty(porcelainOutput))
            return Array.Empty<GitStatusEntry>();

        var entries = new List<GitStatusEntry>();
        foreach (var rawLine in porcelainOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 4) continue;

            var index = line[0];
            var working = line[1];
            var path = line[3..];
            if (path.StartsWith('"') && path.EndsWith('"'))
                path = path[1..^1];

            var conflict = IsUnmerged(index, working);
            var status = ResolveStatus(index, working, conflict);
            entries.Add(new GitStatusEntry(status, path, conflict));
        }
        return entries;
    }

    public static IReadOnlyList<string> GetConflictingPaths(string porcelainOutput) =>
        Parse(porcelainOutput).Where(e => e.IsConflict).Select(e => e.RelativePath).ToList();

    public static bool HasConflicts(string porcelainOutput) =>
        Parse(porcelainOutput).Any(e => e.IsConflict);

    private static bool IsUnmerged(char index, char working)
    {
        if (index == 'U' || working == 'U') return true;
        if (index == 'A' && working == 'A') return true;
        if (index == 'D' && working == 'D') return true;
        return false;
    }

    private static GitFileStatus ResolveStatus(char index, char working, bool conflict)
    {
        if (conflict) return GitFileStatus.Conflict;
        if (index == '?' && working == '?') return GitFileStatus.Untracked;
        if (index == '!' && working == '!') return GitFileStatus.Ignored;

        var primary = index != ' ' && index != '?' ? index : working;
        return primary switch
        {
            'M' => GitFileStatus.Modified,
            'A' => GitFileStatus.Added,
            'D' => GitFileStatus.Deleted,
            'R' => GitFileStatus.Renamed,
            'C' => GitFileStatus.Copied,
            _ => GitFileStatus.Modified,
        };
    }
}
