using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMWiki.Core.Git;
using LLMWiki.Core.Settings;

namespace LLMWiki.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IPatStorage _patStorage;

    [ObservableProperty]
    private string? _vaultPath;

    [ObservableProperty]
    private string? _gitRemoteUrl;

    [ObservableProperty]
    private string _githubPat = string.Empty;

    [ObservableProperty]
    private bool _gitAutoSync;

    [ObservableProperty]
    private int _gitAutoSyncIntervalMinutes;

    [ObservableProperty]
    private bool _wikiOnlyMode;

    [ObservableProperty]
    private int _claudeTimeoutMinutes;

    [ObservableProperty]
    private int _stalledStreamSeconds;

    [ObservableProperty]
    private string? _validationMessage;

    public SettingsViewModel(ISettingsService settings, IPatStorage patStorage)
    {
        _settings = settings;
        _patStorage = patStorage;
        Load();
    }

    private void Load()
    {
        var s = _settings.Current;
        VaultPath = s.VaultPath;
        GitRemoteUrl = s.GitRemoteUrl;
        GitAutoSync = s.GitAutoSync;
        GitAutoSyncIntervalMinutes = s.GitAutoSyncIntervalMinutes;
        WikiOnlyMode = s.WikiOnlyMode;
        ClaudeTimeoutMinutes = s.ClaudeTimeoutMinutes;
        StalledStreamSeconds = s.StalledStreamSeconds;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        var validation = string.IsNullOrWhiteSpace(GitRemoteUrl)
            ? null
            : GitRemoteUrlValidator.Validate(GitRemoteUrl);

        if (validation is { IsValid: false })
        {
            ValidationMessage = validation.Detail;
            return;
        }

        var s = _settings.Current;
        s.GitRemoteUrl = GitRemoteUrl;
        s.GitAutoSync = GitAutoSync;
        s.GitAutoSyncIntervalMinutes = GitAutoSyncIntervalMinutes;
        s.WikiOnlyMode = WikiOnlyMode;
        s.ClaudeTimeoutMinutes = ClaudeTimeoutMinutes;
        s.StalledStreamSeconds = StalledStreamSeconds;
        await _settings.SaveAsync(s);

        if (!string.IsNullOrEmpty(GithubPat) && !string.IsNullOrEmpty(VaultPath))
        {
            _patStorage.Write($"git-pat:{VaultPath}", GithubPat);
            GithubPat = string.Empty;
        }
        ValidationMessage = "Сохранено";
    }
}
