using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LLMWiki.App.Git;
using LLMWiki.App.ViewModels;
using LLMWiki.App.Views;
using LLMWiki.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace LLMWiki.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private async void OnOpenVaultClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку vault",
            AllowMultiple = false,
        });

        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        await vm.OpenVaultAsync(path);
    }

    private async void OnAddFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Добавить файл",
            AllowMultiple = true,
        });

        if (files.Count == 0) return;
        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();

        await vm.AddFilesAsync(paths);
    }

    private async void OnReindexClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        await vm.ReindexAsync();
    }

    private void OnLintClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.RunLocalLint();
    }

    private async void OnGitPushClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        await vm.Git.PushAsync();
    }

    private async void OnGitPullClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var result = await vm.Git.PullAsync();
        if (result.Outcome == GitOperationOutcome.Conflict && result.Conflicts is not null)
            await OpenConflictWindowAsync(result.Conflicts);
    }

    private async void OnGitSetupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.SettingsTab is null) return;

        var url = vm.SettingsTab.GitRemoteUrl;
        var pat = vm.SettingsTab.GithubPat;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(pat)) return;

        await vm.Git.SetupAsync(url, pat);
        vm.SettingsTab.GithubPat = string.Empty;
    }

    private async void OnGitDisableClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        await vm.Git.StopAutoSyncAsync();
    }

    private async void OnClaudeLoginClick(object? sender, RoutedEventArgs e)
    {
        var loginVm = App.Services.GetRequiredService<ClaudeLoginViewModel>();
        var window = new ClaudeLoginWindow { DataContext = loginVm };
        await window.ShowDialog(this);
    }

    private void OnFileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.Files is null) return;
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is FileTreeNode node)
            vm.Files.OpenFileCommand.Execute(node);
    }

    private void OnGraphNodeActivated(object? sender, GraphNodeActivatedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.Files is null) return;
        vm.Files.OpenAbsolutePath(e.Node.Id);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (HasFiles(e))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!HasFiles(e)) return;

        var paths = ExtractFilePaths(e);
        if (paths.Count == 0) return;
        await vm.AddFilesAsync(paths);
    }

    private static bool HasFiles(DragEventArgs e)
    {
        var transfer = e.DataTransfer;
        if (transfer is null) return false;
        foreach (var f in transfer.Formats)
            if (f == DataFormat.File) return true;
        return false;
    }

    private static List<string> ExtractFilePaths(DragEventArgs e)
    {
        var paths = new List<string>();
        var transfer = e.DataTransfer;
        if (transfer is null) return paths;

        foreach (var item in transfer.Items)
        {
            var raw = item.TryGetRaw(DataFormat.File);
            if (raw is Avalonia.Platform.Storage.IStorageItem storage)
            {
                var p = storage.TryGetLocalPath();
                if (!string.IsNullOrEmpty(p)) paths.Add(p);
            }
        }
        return paths;
    }

    private async Task OpenConflictWindowAsync(IReadOnlyList<ConflictEntry> conflicts)
    {
        var vm = App.Services.GetRequiredService<ConflictResolutionViewModel>();
        vm.Load(conflicts);
        var window = new ConflictResolutionWindow { DataContext = vm };
        await window.ShowDialog(this);
    }
}
