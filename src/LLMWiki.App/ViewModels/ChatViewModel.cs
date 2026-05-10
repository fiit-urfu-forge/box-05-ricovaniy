using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMWiki.Core.Agent;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Files;
using LLMWiki.Core.Settings;
using LLMWiki.Core.Vault;

namespace LLMWiki.App.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IVaultService _vault;
    private readonly IClaudeAgentFactory _agentFactory;
    private readonly Queue<string> _pendingPrompts = new();

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private bool _wikiPlusAi;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private long _currentContextBytes;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public ChatViewModel(
        ISettingsService settings,
        IVaultService vault,
        IClaudeAgentFactory agentFactory)
    {
        _settings = settings;
        _vault = vault;
        _agentFactory = agentFactory;
        _wikiPlusAi = !settings.Current.WikiOnlyMode;
    }

    public ChatMode CurrentMode => WikiPlusAi ? ChatMode.Extended : ChatMode.WikiOnly;

    [RelayCommand(CanExecute = nameof(CanSend))]
    public async Task SendAsync()
    {
        var text = Input.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (Messages.Count >= FileLimits.MaxChatMessages
            || CurrentContextBytes >= FileLimits.MaxChatContextBytes)
        {
            StatusMessage = "Лимит контекста сессии достигнут — начните новый чат";
            return;
        }

        _pendingPrompts.Enqueue(text);
        Input = string.Empty;
        await DrainQueueAsync();
    }

    private async Task DrainQueueAsync()
    {
        if (IsSending) return;
        IsSending = true;
        SendCommand.NotifyCanExecuteChanged();

        try
        {
            while (_pendingPrompts.Count > 0)
            {
                var prompt = _pendingPrompts.Dequeue();
                Messages.Add(new ChatMessage(MessageRole.User, prompt, DateTime.UtcNow));
                await StreamReplyAsync(prompt);
            }
        }
        finally
        {
            IsSending = false;
            SendCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task StreamReplyAsync(string prompt)
    {
        if (_vault.Current is null)
        {
            Messages.Add(new ChatMessage(
                MessageRole.Assistant, "(Vault не открыт.)", DateTime.UtcNow));
            return;
        }

        IClaudeAgent agent;
        try { agent = _agentFactory.Create(_vault); }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage(
                MessageRole.Assistant,
                $"(Не удалось создать агента: {ex.Message})",
                DateTime.UtcNow));
            return;
        }

        var streamingTimestamp = DateTime.UtcNow;
        Messages.Add(new ChatMessage(MessageRole.Assistant, string.Empty, streamingTimestamp));
        var assistantIndex = Messages.Count - 1;
        var sb = new StringBuilder();

        try
        {
            await foreach (var chunk in agent.QueryStreamAsync(prompt, CurrentMode))
            {
                sb.Append(chunk);
                var snapshot = sb.ToString();
                var chunkLen = chunk.Length;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Messages[assistantIndex] = new ChatMessage(
                        MessageRole.Assistant, snapshot, streamingTimestamp);
                    CurrentContextBytes += chunkLen;
                });
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Messages[assistantIndex] = new ChatMessage(
                    MessageRole.Assistant,
                    $"(Ошибка стриминга: {ex.Message})",
                    streamingTimestamp);
            });
        }
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(Input);

    partial void OnWikiPlusAiChanged(bool value)
    {
        var s = _settings.Current;
        s.WikiOnlyMode = !value;
        _ = _settings.SaveAsync(s);
    }

    partial void OnInputChanged(string value) =>
        SendCommand.NotifyCanExecuteChanged();
}
