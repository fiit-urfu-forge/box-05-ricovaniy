using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMWiki.App.Views;
using LLMWiki.Core.Files;
using LLMWiki.Core.Vault;

namespace LLMWiki.App.ViewModels;

public sealed class FileTreeNode
{
    public required string Name { get; init; }
    public required string RelativePath { get; init; }
    public required bool IsDirectory { get; init; }
    public ObservableCollection<FileTreeNode> Children { get; } = new();
}

public partial class FilesViewModel : ViewModelBase
{
    private readonly IVaultService _vault;
    private readonly IFileService _files;

    [ObservableProperty]
    private string? _selectedFilePath;

    [ObservableProperty]
    private Control? _viewer;

    public ObservableCollection<FileTreeNode> Roots { get; } = new();

    public FilesViewModel(IVaultService vault, IFileService files)
    {
        _vault = vault;
        _files = files;
    }

    public Task RefreshAsync()
    {
        Roots.Clear();
        var vault = _vault.Current;
        if (vault is null) return Task.CompletedTask;

        var rawNode = BuildDirectoryNode(vault.RawDirectory, vault.Path, "raw");
        var wikiNode = BuildDirectoryNode(vault.WikiDirectory, vault.Path, "wiki");

        Roots.Add(rawNode);
        Roots.Add(wikiNode);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task AddFileAsync(string sourcePath)
    {
        await _files.AddFileAsync(new FileAddRequest(sourcePath, NameConflictResolution.Rename));
        await RefreshAsync();
    }

    [RelayCommand]
    public void OpenFile(FileTreeNode node)
    {
        if (node.IsDirectory) return;
        var vault = _vault.Current;
        if (vault is null) return;

        var absolute = System.IO.Path.Combine(vault.Path, node.RelativePath);
        SelectedFilePath = node.RelativePath;
        Viewer = BuildViewer(absolute);
    }

    public void OpenAbsolutePath(string absolutePath)
    {
        var vault = _vault.Current;
        if (vault is null) return;
        if (!System.IO.Path.IsPathRooted(absolutePath))
            absolutePath = System.IO.Path.Combine(vault.Path, absolutePath);
        if (!File.Exists(absolutePath)) return;

        SelectedFilePath = System.IO.Path.GetRelativePath(vault.Path, absolutePath)
            .Replace('\\', '/');
        Viewer = BuildViewer(absolutePath);
    }

    private static Control BuildViewer(string absolutePath)
    {
        if (!File.Exists(absolutePath))
            return new TextBlock { Text = "Файл не найден" };

        var info = new FileInfo(absolutePath);
        var type = FileTypeClassifier.Classify(absolutePath);

        switch (type)
        {
            case Core.Domain.FileType.Image:
                try
                {
                    return new Image
                    {
                        Source = new Bitmap(absolutePath),
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    };
                }
                catch (Exception ex)
                {
                    return new TextBlock { Text = $"Не удалось отобразить изображение: {ex.Message}" };
                }

            case Core.Domain.FileType.Pdf:
                return new PdfView { Source = absolutePath };

            case Core.Domain.FileType.Text:
                return BuildTextOrMarkdownView(absolutePath, info);

            default:
                return new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Неподдерживаемый тип файла",
                            FontWeight = FontWeight.Bold,
                        },
                        new TextBlock { Text = absolutePath, TextWrapping = TextWrapping.Wrap },
                    },
                };
        }
    }

    private static Control BuildTextOrMarkdownView(string absolutePath, FileInfo info)
    {
        if (info.Length > FileLimits.MaxViewerTextBytes)
        {
            using var fs = File.OpenRead(absolutePath);
            var buffer = new byte[FileLimits.MaxViewerTextBytes];
            var read = fs.Read(buffer, 0, buffer.Length);
            var truncated = System.Text.Encoding.UTF8.GetString(buffer, 0, read)
                + $"\n\n…\n_(файл обрезан, размер {info.Length / (1024 * 1024)} MB)_";
            return BuildBodyForExt(absolutePath, truncated);
        }

        var content = TryReadAllText(absolutePath);
        return BuildBodyForExt(absolutePath, content);
    }

    private static Control BuildBodyForExt(string absolutePath, string content)
    {
        var ext = System.IO.Path.GetExtension(absolutePath).ToLowerInvariant();
        if (ext == ".md")
            return new MarkdownView { Source = content };

        return new ScrollViewer
        {
            Content = new SelectableTextBlock
            {
                Text = content,
                FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                TextWrapping = TextWrapping.NoWrap,
            },
        };
    }

    private static string TryReadAllText(string path)
    {
        try { return File.ReadAllText(path); }
        catch
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                return System.Text.Encoding.GetEncoding("Windows-1252").GetString(bytes);
            }
            catch (Exception ex)
            {
                return $"Не удалось определить кодировку файла: {ex.Message}";
            }
        }
    }

    private static FileTreeNode BuildDirectoryNode(string absolutePath, string vaultRoot, string label)
    {
        var rel = System.IO.Path.GetRelativePath(vaultRoot, absolutePath).Replace('\\', '/');
        var node = new FileTreeNode
        {
            Name = label,
            RelativePath = rel,
            IsDirectory = true,
        };

        if (!Directory.Exists(absolutePath)) return node;

        foreach (var dir in Directory.EnumerateDirectories(absolutePath).OrderBy(d => d))
        {
            node.Children.Add(BuildDirectoryNode(dir, vaultRoot, System.IO.Path.GetFileName(dir)));
        }
        foreach (var file in Directory.EnumerateFiles(absolutePath).OrderBy(f => f))
        {
            var fileName = System.IO.Path.GetFileName(file);
            if (fileName.StartsWith('.')) continue;
            node.Children.Add(new FileTreeNode
            {
                Name = fileName,
                RelativePath = System.IO.Path.GetRelativePath(vaultRoot, file).Replace('\\', '/'),
                IsDirectory = false,
            });
        }
        return node;
    }
}
