using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMWiki.App.Pty;
using LLMWiki.Core.Agent;
using LLMWiki.Core.Infrastructure;

namespace LLMWiki.App.ViewModels;

public partial class ClaudeLoginViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ICredentialsChecker _credentials;
    private readonly StringBuilder _buffer = new();
    private IPtyTerminal? _terminal;

    [ObservableProperty]
    private string _output = string.Empty;

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isAuthorized;

    [ObservableProperty]
    private string? _statusMessage;

    public ClaudeLoginViewModel(ICredentialsChecker credentials)
    {
        _credentials = credentials;
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsRunning) return;

        IsRunning = true;
        StatusMessage = "Запуск `claude login`…";
        _buffer.Clear();
        Output = string.Empty;

        _terminal = PtyTerminalFactory.Create();
        _terminal.OutputReceived += OnOutput;
        _terminal.Exited += OnExited;

        try
        {
            await _terminal.StartAsync(
                "claude",
                new[] { "login" },
                workingDirectory: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось запустить claude login: {ex.Message}";
            IsRunning = false;
        }
    }

    [RelayCommand]
    public async Task SendInputAsync()
    {
        if (_terminal is null || !IsRunning) return;
        var line = Input + "\n";
        Input = string.Empty;
        await _terminal.WriteAsync(line);
    }

    [RelayCommand]
    public void Cancel()
    {
        _terminal?.Cancel();
    }

    [RelayCommand]
    public Task RecheckAsync()
    {
        var state = _credentials.Check();
        IsAuthorized = state.Status == ClaudeAuthStatus.Authorized;
        StatusMessage = IsAuthorized
            ? "Авторизация успешна"
            : "Credentials всё ещё не найдены";
        return Task.CompletedTask;
    }

    private void OnOutput(object? sender, string raw)
    {
        var clean = AnsiStripper.Strip(raw);
        if (clean.Length == 0) return;

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            _buffer.Append(clean);
            // keep buffer bounded
            if (_buffer.Length > 64_000)
                _buffer.Remove(0, _buffer.Length - 64_000);
            Output = _buffer.ToString();
        });
    }

    private void OnExited(object? sender, int exitCode)
    {
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            IsRunning = false;
            StatusMessage = exitCode == 0
                ? "Процесс завершён"
                : $"Процесс завершён с кодом {exitCode}";

            // recheck credentials automatically once login finishes cleanly
            if (exitCode == 0)
                await RecheckAsync();
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_terminal is not null)
        {
            _terminal.OutputReceived -= OnOutput;
            _terminal.Exited -= OnExited;
            await _terminal.DisposeAsync();
            _terminal = null;
        }
    }
}
