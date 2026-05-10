using ClaudeAgentSdk;
using ClaudeAgentSdk.Transport;
using LLMWiki.Core.Agent;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Ingest;
using LLMWiki.Core.Vault;
using DomainVault = LLMWiki.Core.Domain.Vault;

namespace LLMWiki.App.Agent;

public sealed class SdkClaudeAgent : IClaudeAgent, IAsyncDisposable
{
    private readonly DomainVault _vault;
    private readonly ClaudeToolGuard _guard;
    private readonly AgentProgressParser _progressParser;
    private readonly VaultPostOpCleanup _postOpCleanup;
    private readonly string? _resolvedCliPath;
    private ClaudeSdkClient? _chatClient;

    public SdkClaudeAgent(DomainVault vault)
    {
        _vault = vault;
        _guard = new ClaudeToolGuard(vault.Path);
        _progressParser = new AgentProgressParser(vault.Path);
        _postOpCleanup = new VaultPostOpCleanup(vault);
        _resolvedCliPath = ClaudeCliResolver.Resolve();
    }

    public async Task<IngestResult> IngestAsync(
        string rawFileRelativePath,
        IProgress<IngestProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var rollback = new IngestRollback(_vault.Path);
        var (created, updated) = (0, 0);

        using var timeoutCts = new CancellationTokenSource(AgentLimits.ClaudeTimeout);
        using var stalledCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token, stalledCts.Token);

        var prompt = $"Ingest the file: {rawFileRelativePath}\n\nFollow the instructions in CLAUDE.md.";
        var options = BuildOptions(SystemPrompts.IngestPrompt, AgentLimits.IngestMaxTurns);
        var lastChunkAt = DateTime.UtcNow;
        using var stalledTimer = new Timer(_ =>
        {
            if (DateTime.UtcNow - lastChunkAt > AgentLimits.StalledStreamTimeout)
                stalledCts.Cancel();
        }, null, AgentLimits.StalledStreamTimeout, AgentLimits.StalledStreamTimeout);

        try
        {
            var transport = BuildTransport(prompt, options);
            await foreach (var message in
                ClaudeAgent.QueryAsync(prompt, options, transport, cancellationToken: linked.Token))
            {
                lastChunkAt = DateTime.UtcNow;

                if (message is AssistantMessage assistant)
                    ProcessAssistantBlocks(assistant, progress, rollback, ref created, ref updated);

                if (message is ResultMessage { IsError: true } error)
                {
                    await rollback.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    _postOpCleanup.Run();
                    return new IngestResult(rawFileRelativePath, false, 0, 0,
                        error.Result ?? "Claude returned an error",
                        DateTime.UtcNow - startedAt);
                }
            }
        }
        catch (OperationCanceledException) when (stalledCts.IsCancellationRequested)
        {
            await rollback.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _postOpCleanup.Run();
            return new IngestResult(rawFileRelativePath, false, 0, 0,
                "Claude перестал отвечать (>60s без токенов)",
                DateTime.UtcNow - startedAt);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            await rollback.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _postOpCleanup.Run();
            return new IngestResult(rawFileRelativePath, false, 0, 0,
                "Operation timed out (>5 minutes)",
                DateTime.UtcNow - startedAt);
        }
        catch
        {
            await rollback.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _postOpCleanup.Run();
            throw;
        }

        rollback.Commit();
        _postOpCleanup.Run();
        return new IngestResult(rawFileRelativePath, true, created, updated, null,
            DateTime.UtcNow - startedAt);
    }

    public async Task<LintReport> LintAsync(
        IProgress<IngestProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var options = BuildOptions(SystemPrompts.LintPrompt, AgentLimits.LintMaxTurns);
        var summary = new System.Text.StringBuilder();

        var lintPrompt = "Run lint on the wiki.";
        var lintTransport = BuildTransport(lintPrompt, options);
        await foreach (var message in
            ClaudeAgent.QueryAsync(lintPrompt, options, lintTransport,
                cancellationToken: cancellationToken))
        {
            if (message is AssistantMessage assistant)
            {
                foreach (var block in assistant.Content)
                {
                    switch (block)
                    {
                        case TextBlock t:
                            summary.AppendLine(t.Text);
                            progress?.Report(_progressParser.FromText(t.Text)
                                ?? EmptyEvent);
                            break;
                        case ToolUseBlock tool:
                            var ev = _progressParser.FromToolUse(tool.Name, tool.Input);
                            if (ev is not null) progress?.Report(ev);
                            break;
                    }
                }
            }
        }

        _postOpCleanup.Run();
        return new LintReport(0, 0, 0, 0, summary.ToString());
    }

    public async IAsyncEnumerable<string> QueryStreamAsync(
        string prompt,
        ChatMode mode,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        if (_chatClient is null)
        {
            var options = BuildOptions(SystemPrompts.ForChatMode(mode), AgentLimits.QueryMaxTurns);
            // Streaming/multi-turn uses an empty-array prompt as initial.
            var transport = BuildTransport(Array.Empty<object>(), options);
            _chatClient = new ClaudeSdkClient(options, transport);
            await _chatClient.ConnectAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        await _chatClient.QueryAsync(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await foreach (var message in
            _chatClient.ReceiveResponseAsync(cancellationToken).ConfigureAwait(false))
        {
            if (message is AssistantMessage assistant)
            {
                foreach (var block in assistant.Content)
                {
                    if (block is TextBlock t) yield return t.Text;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_chatClient is not null)
        {
            await _chatClient.DisposeAsync().ConfigureAwait(false);
            _chatClient = null;
        }
    }

    private void ProcessAssistantBlocks(
        AssistantMessage assistant,
        IProgress<IngestProgressEvent>? progress,
        IngestRollback rollback,
        ref int created,
        ref int updated)
    {
        foreach (var block in assistant.Content)
        {
            switch (block)
            {
                case TextBlock t:
                    progress?.Report(_progressParser.FromText(t.Text) ?? EmptyEvent);
                    break;

                case ToolUseBlock tool:
                    if (tool.Name.Equals("Write", StringComparison.OrdinalIgnoreCase))
                        created++;
                    else if (tool.Name.Equals("Edit", StringComparison.OrdinalIgnoreCase)
                             || tool.Name.Equals("MultiEdit", StringComparison.OrdinalIgnoreCase))
                        updated++;

                    if (TryGetWritePath(tool, out var absolutePath))
                    {
                        try { rollback.Track(absolutePath); }
                        catch { /* outside vault — guard rejects */ }
                    }

                    var ev = _progressParser.FromToolUse(tool.Name, tool.Input);
                    if (ev is not null) progress?.Report(ev);
                    break;
            }
        }
    }

    private SubprocessCliTransport BuildTransport(object prompt, ClaudeAgentOptions options) =>
        new(prompt, options, _resolvedCliPath);

    private ClaudeAgentOptions BuildOptions(string systemPrompt, int maxTurns)
    {
        return new ClaudeAgentOptions
        {
            Cwd = _vault.Path,
            SystemPrompt = systemPrompt,
            MaxTurns = maxTurns,
            DisallowedTools = new List<string> { "Bash" },
            CanUseTool = (toolName, input, _) =>
            {
                var decision = _guard.Decide(toolName, input);
                if (decision.Decision == ToolDecision.Allow)
                    return Task.FromResult<object>(new PermissionResultAllow());
                return Task.FromResult<object>(new PermissionResultDeny
                {
                    Message = decision.Reason ?? "Denied by tool guard",
                });
            },
        };
    }

    private bool TryGetWritePath(ToolUseBlock tool, out string absolutePath)
    {
        absolutePath = string.Empty;
        if (!IsWriteTool(tool.Name)) return false;

        foreach (var key in new[] { "file_path", "path", "notebook_path" })
        {
            if (tool.Input.TryGetValue(key, out var value) && value is string s)
            {
                absolutePath = Path.IsPathRooted(s) ? s : Path.Combine(_vault.Path, s);
                return true;
            }
        }
        return false;
    }

    private static bool IsWriteTool(string name) =>
        name.Equals("Write", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Edit", StringComparison.OrdinalIgnoreCase)
        || name.Equals("MultiEdit", StringComparison.OrdinalIgnoreCase)
        || name.Equals("NotebookEdit", StringComparison.OrdinalIgnoreCase);

    private static readonly IngestProgressEvent EmptyEvent =
        new(IngestProgressKind.Text, null, null, string.Empty);
}
