using System.Runtime.Versioning;
using Pty.Net;

namespace LLMWiki.App.Pty;

[SupportedOSPlatform("windows")]
internal sealed class WindowsConPty : IPtyTerminal
{
    private IPtyConnection? _conn;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    public bool IsRunning => _conn is not null;

    public Task StartAsync(
        string command,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        int rows = 30,
        int columns = 100,
        CancellationToken cancellationToken = default)
    {
        if (_conn is not null)
            throw new InvalidOperationException("PTY is already running");

        var fullCommand = command;
        foreach (var a in arguments)
            fullCommand += " " + (a.Contains(' ') ? $"\"{a}\"" : a);

        _conn = PtyProvider.Spawn(
            fullCommand,
            rows,
            columns,
            workingDirectory ?? Environment.CurrentDirectory,
            BackendOptions.Default);

        _conn.PtyData += (_, data) => OutputReceived?.Invoke(this, data);
        _conn.PtyDisconnected += _ =>
        {
            Exited?.Invoke(this, 0);
            _conn = null;
        };
        return Task.CompletedTask;
    }

    public Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        if (_conn is null) throw new InvalidOperationException("PTY not started");
        return _conn.WriteAsync(data);
    }

    public void Resize(int rows, int columns)
    {
        _conn?.Resize(columns, rows);
    }

    public void Cancel()
    {
        // Pty.Net 0.1.16 has no Kill on IPtyConnection — disconnect by disposing.
        if (_conn is IDisposable disposable)
        {
            try { disposable.Dispose(); } catch { }
        }
        _conn = null;
    }

    public ValueTask DisposeAsync()
    {
        Cancel();
        return ValueTask.CompletedTask;
    }
}
