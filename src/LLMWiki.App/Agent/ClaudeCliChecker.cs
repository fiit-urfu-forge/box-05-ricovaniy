using System.Diagnostics;

namespace LLMWiki.App.Agent;

public static class ClaudeCliChecker
{
    public static string? LastResolvedPath { get; private set; }

    public static async Task<bool> IsInstalledAsync(
        CancellationToken cancellationToken = default)
    {
        var resolved = ClaudeCliResolver.Resolve();
        LastResolvedPath = resolved;

        if (resolved is null)
        {
            // file not found via path/common locations — definitively missing
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = resolved,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--version");

            using var process = Process.Start(psi);
            if (process is null) return false;

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            // file exists but couldn't run (permissions, broken shim, etc) —
            // treat as installed but let downstream surface the actual error.
            return true;
        }
    }
}
