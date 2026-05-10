using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LLMWiki.App.Pty;

/// <summary>
/// Cross-platform PTY for Linux/macOS using the standard `script` utility.
/// `script` runs the wrapped command inside a real PTY, allowing interactive
/// programs (like `claude login`) to work as if attached to a terminal.
/// </summary>
internal sealed class UnixScriptPty : IPtyTerminal
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(
        string command,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        int rows = 30,
        int columns = 100,
        CancellationToken cancellationToken = default)
    {
        if (_process is not null)
            throw new InvalidOperationException("PTY is already running");

        var psi = new ProcessStartInfo
        {
            FileName = "script",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        psi.Environment["TERM"] = "xterm-256color";
        psi.Environment["LINES"] = rows.ToString();
        psi.Environment["COLUMNS"] = columns.ToString();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // BSD script: `script -q /dev/null <command> [args...]`
            psi.ArgumentList.Add("-q");
            psi.ArgumentList.Add("/dev/null");
            psi.ArgumentList.Add(command);
            foreach (var a in arguments) psi.ArgumentList.Add(a);
        }
        else
        {
            // util-linux script: `script -qfc "command args..." /dev/null`
            psi.ArgumentList.Add("-qfc");
            psi.ArgumentList.Add(BuildShellCommand(command, arguments));
            psi.ArgumentList.Add("/dev/null");
        }

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.Exited += (_, _) =>
            Exited?.Invoke(this, _process?.ExitCode ?? -1);

        if (!_process.Start())
            throw new InvalidOperationException("Failed to start `script`");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readTask = Task.WhenAll(
            ReadStreamAsync(_process.StandardOutput, _cts.Token),
            ReadStreamAsync(_process.StandardError, _cts.Token));
        await Task.Yield();
    }

    private async Task ReadStreamAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[2048];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (read <= 0) break;
                OutputReceived?.Invoke(this, new string(buffer, 0, read));
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        catch (IOException) { /* stream closed by child exit */ }
    }

    public Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        if (_process is null) throw new InvalidOperationException("PTY not started");
        return _process.StandardInput.WriteAsync(data.AsMemory(), cancellationToken);
    }

    public void Resize(int rows, int columns)
    {
        // util-linux/BSD `script` wrapper does not expose live resize;
        // a SIGWINCH would need to be sent to the child, but the child PID
        // is not directly exposed. Best-effort env update only.
        if (_process is null) return;
    }

    public void Cancel()
    {
        if (_process is { HasExited: false } p)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        if (_readTask is not null)
        {
            try { await _readTask.ConfigureAwait(false); } catch { }
            _readTask = null;
        }
        _process?.Dispose();
        _process = null;
    }

    private static string BuildShellCommand(string command, IReadOnlyList<string> arguments)
    {
        var sb = new StringBuilder();
        sb.Append(EscapeForShell(command));
        foreach (var a in arguments)
        {
            sb.Append(' ');
            sb.Append(EscapeForShell(a));
        }
        return sb.ToString();
    }

    private static string EscapeForShell(string value) =>
        "'" + value.Replace("'", "'\\''") + "'";
}
