namespace LLMWiki.App.Pty;

public interface IPtyTerminal : IAsyncDisposable
{
    event EventHandler<string>? OutputReceived;
    event EventHandler<int>? Exited;

    bool IsRunning { get; }

    Task StartAsync(
        string command,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        int rows = 30,
        int columns = 100,
        CancellationToken cancellationToken = default);

    Task WriteAsync(string data, CancellationToken cancellationToken = default);

    void Resize(int rows, int columns);

    void Cancel();
}
