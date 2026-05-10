using System.Runtime.InteropServices;

namespace LLMWiki.App.Agent;

/// <summary>
/// Mirrors how claude-agent-sdk-dotnet locates the `claude` CLI:
/// PATH lookup with platform-correct extension, then common npm/local fallbacks.
/// On Windows the npm shim is `claude.cmd`, which Process.Start with
/// UseShellExecute=false will NOT auto-resolve from a bare "claude" name.
/// </summary>
public static class ClaudeCliResolver
{
    public static string ExecutableName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "claude.cmd" : "claude";

    public static string? Resolve()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar is not null)
        {
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (var candidate in CandidateNames())
                {
                    var full = Path.Combine(dir, candidate);
                    if (File.Exists(full)) return full;
                }
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var locations = new[]
        {
            Path.Combine(home, ".npm-global", "bin"),
            Path.Combine(home, "AppData", "Roaming", "npm"),
            Path.Combine(home, ".local", "bin"),
            Path.Combine(home, "node_modules", ".bin"),
            Path.Combine(home, ".yarn", "bin"),
            "/usr/local/bin",
            "/opt/homebrew/bin",
        };

        foreach (var dir in locations)
        {
            foreach (var candidate in CandidateNames())
            {
                var full = Path.Combine(dir, candidate);
                if (File.Exists(full)) return full;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateNames()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return "claude.cmd";
            yield return "claude.exe";
            yield return "claude.bat";
            yield return "claude";
        }
        else
        {
            yield return "claude";
        }
    }
}
