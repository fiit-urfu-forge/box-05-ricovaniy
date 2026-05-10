using System.Diagnostics;

namespace LLMWiki.App.Agent;

public sealed class ClaudeLoginRunner : IAsyncDisposable
{
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public Process Start(Action<string>? onOutput = null, Action<string>? onError = null)
    {
        if (IsRunning)
            throw new InvalidOperationException("`claude login` is already running");

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("login");

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (onOutput is not null)
            _process.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
        if (onError is not null)
            _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onError(e.Data); };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        return _process;
    }

    public Task SendInputAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_process is null) throw new InvalidOperationException("Process not started");
        return _process.StandardInput.WriteLineAsync(text.AsMemory(), cancellationToken);
    }

    public void Cancel()
    {
        if (_process is { HasExited: false } p)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
        }
    }

    public ValueTask DisposeAsync()
    {
        Cancel();
        _process?.Dispose();
        _process = null;
        return ValueTask.CompletedTask;
    }
}
