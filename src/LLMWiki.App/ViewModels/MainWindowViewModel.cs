using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMWiki.App.Git;
using LLMWiki.App.Services;
using LLMWiki.Core.Agent;
using LLMWiki.Core.Files;
using LLMWiki.Core.Lint;
using LLMWiki.Core.Settings;
using LLMWiki.Core.Vault;
using Microsoft.Extensions.Logging;

namespace LLMWiki.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IVaultService _vault;
    private readonly IFileService _files;
    private readonly IServiceProvider _services;
    private readonly IngestService _ingestService;
    private readonly GitSyncCoordinator _git;
    private readonly LocalLintRunner _lint;
    private readonly ICredentialsChecker _credentials;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private string? _vaultPath;

    [ObservableProperty]
    private bool _isVaultOpen;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _activityMessage;

    [ObservableProperty]
    private FilesViewModel? _filesTab;

    [ObservableProperty]
    private ChatViewModel? _chat;

    [ObservableProperty]
    private GraphViewModel? _graph;

    [ObservableProperty]
    private SettingsViewModel? _settingsTab;

    public FilesViewModel? Files
    {
        get => FilesTab;
        set => FilesTab = value;
    }

    public IngestService Ingest => _ingestService;
    public GitSyncCoordinator Git => _git;
    public ICredentialsChecker Credentials => _credentials;

    public MainWindowViewModel(
        ISettingsService settings,
        IVaultService vault,
        IFileService files,
        IServiceProvider services,
        IngestService ingestService,
        GitSyncCoordinator git,
        LocalLintRunner lint,
        ICredentialsChecker credentials,
        ILogger<MainWindowViewModel> logger)
    {
        _settings = settings;
        _vault = vault;
        _files = files;
        _services = services;
        _ingestService = ingestService;
        _git = git;
        _lint = lint;
        _credentials = credentials;
        _logger = logger;

        _ingestService.Progress += (_, ev) =>
            Dispatcher.UIThread.Post(() => ActivityMessage = ev.Describe());
        _ingestService.StatusChanged += (_, msg) =>
            Dispatcher.UIThread.Post(() => StatusMessage = msg);
        _ingestService.Completed += (_, result) =>
            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = result.Success
                    ? $"✓ {result.RelativePath} проиндексирован"
                    : $"✗ {result.RelativePath}: {result.ErrorMessage}";
                if (result.Success) Graph?.Refresh();
            });

        _git.OperationCompleted += (_, op) =>
            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = op.Outcome switch
                {
                    GitOperationOutcome.Ok => "✓ Git: операция завершена",
                    GitOperationOutcome.NothingToCommit => "Git: нет изменений",
                    GitOperationOutcome.Conflict => "Git: требуется разрешение конфликтов",
                    _ => $"Git: {op.Detail}",
                };
            });
    }

    public async Task InitializeAsync()
    {
        var loaded = await _settings.LoadAsync();
        if (!string.IsNullOrWhiteSpace(loaded.VaultPath))
            await OpenVaultInternalAsync(loaded.VaultPath);
    }

    [RelayCommand]
    public async Task OpenVaultAsync(string path)
    {
        await OpenVaultInternalAsync(path);
        var settings = _settings.Current;
        settings.VaultPath = path;
        await _settings.SaveAsync(settings);
    }

    private async Task OpenVaultInternalAsync(string path)
    {
        var result = await _vault.OpenAsync(path);
        VaultPath = result.Vault.Path;
        IsVaultOpen = true;

        Files = (FilesViewModel)_services.GetService(typeof(FilesViewModel))!;
        Chat = (ChatViewModel)_services.GetService(typeof(ChatViewModel))!;
        Graph = (GraphViewModel)_services.GetService(typeof(GraphViewModel))!;
        SettingsTab = (SettingsViewModel)_services.GetService(typeof(SettingsViewModel))!;

        await Files.RefreshAsync();
        Graph.Refresh();

        await _ingestService.StartAsync();
        _git.EnsureService();
        _git.StartAutoSyncIfEnabled();

        StatusMessage = result.IsFreshVault
            ? $"Создан новый vault: {result.Vault.Name}"
            : $"Открыт vault: {result.Vault.Name}";
    }

    [RelayCommand]
    public async Task AddFileAsync(string path)
    {
        var added = await _files.AddFileAsync(
            new FileAddRequest(path, NameConflictResolution.Rename));
        if (added.Outcome is FileAddOutcome.Added or FileAddOutcome.Replaced or FileAddOutcome.Renamed)
        {
            _ingestService.ScheduleFile(added.CopiedToRelativePath!);
            if (Files is not null) await Files.RefreshAsync();
        }
        else
        {
            StatusMessage = added.Reason ?? added.Outcome.ToString();
        }
    }

    [RelayCommand]
    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            try
            {
                if (Directory.Exists(p))
                {
                    var folderResult = await _files.AddFolderAsync(p);
                    foreach (var f in folderResult.Files)
                        if (f.CopiedToRelativePath is not null)
                            _ingestService.ScheduleFile(f.CopiedToRelativePath);
                }
                else
                {
                    await AddFileAsync(p);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add path {Path}", p);
                StatusMessage = $"Ошибка добавления {p}: {ex.Message}";
            }
        }
        if (Files is not null) await Files.RefreshAsync();
    }

    [RelayCommand]
    public Task ReindexAsync()
    {
        _ingestService.ScheduleFullReindex();
        StatusMessage = "Запущена полная переиндексация";
        return Task.CompletedTask;
    }

    public LocalLintReport RunLocalLint()
    {
        if (_vault.Current is null)
            return new LocalLintReport(
                Array.Empty<BrokenLinkIssue>(),
                Array.Empty<OrphanPageIssue>(),
                Array.Empty<IsolatedNodeIssue>(),
                Array.Empty<DuplicateGroupIssue>());

        var report = _lint.Run(_vault.Current);
        StatusMessage = report.IssueCount == 0
            ? "✓ Lint: проблем не найдено"
            : $"Lint: {report.BrokenLinks.Count} битых ссылок, " +
              $"{report.OrphanPages.Count} orphan, " +
              $"{report.IsolatedNodes.Count} isolated, " +
              $"{report.Duplicates.Count} duplicates";
        return report;
    }
}
