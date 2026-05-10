using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LLMWiki.App.Git;

public sealed record GitProcessResult(
    int ExitCode,
    string StdOut,
    string StdErr)
{
    public bool IsSuccess => ExitCode == 0 && !ContainsFatalLine(StdErr);

    public string? FailureMessage => IsSuccess
        ? null
        : !string.IsNullOrWhiteSpace(StdErr) ? StdErr.Trim() : $"git exited with code {ExitCode}";

    private static bool ContainsFatalLine(string stderr) =>
        stderr.Contains("fatal:", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("error:", StringComparison.OrdinalIgnoreCase);
}

public sealed class GitProcessRunner
{
    private readonly string _workingDirectory;
    private readonly string? _patAskPassScript;
    private readonly Func<string?>? _patProvider;

    public GitProcessRunner(
        string workingDirectory,
        Func<string?>? patProvider = null)
    {
        _workingDirectory = workingDirectory;
        _patProvider = patProvider;

        if (patProvider is not null)
            _patAskPassScript = CreateAskPassScript();
    }

    public Task<GitProcessResult> RunAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default) =>
        RunAsync(arguments.ToArray(), cancellationToken);

    public async Task<GitProcessResult> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        if (_patAskPassScript is not null && _patProvider is not null)
        {
            psi.Environment["GIT_ASKPASS"] = _patAskPassScript;
            psi.Environment["LLMWIKI_PAT"] = _patProvider() ?? string.Empty;
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        return new GitProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string CreateAskPassScript()
    {
        var dir = Path.Combine(Path.GetTempPath(), "llmwiki-askpass");
        Directory.CreateDirectory(dir);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var path = Path.Combine(dir, "askpass.cmd");
            if (!File.Exists(path))
                File.WriteAllText(path, "@echo %LLMWIKI_PAT%\r\n");
            return path;
        }

        var script = Path.Combine(dir, "askpass.sh");
        if (!File.Exists(script))
        {
            File.WriteAllText(script, "#!/bin/sh\necho \"$LLMWIKI_PAT\"\n");
            try { File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            catch { }
        }
        return script;
    }
}
