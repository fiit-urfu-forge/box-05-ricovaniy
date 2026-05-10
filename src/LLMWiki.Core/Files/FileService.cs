using System.Text;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Infrastructure;
using LLMWiki.Core.Vault;

namespace LLMWiki.Core.Files;

public sealed class FileService : IFileService
{
    private readonly IVaultService _vaultService;

    public FileService(IVaultService vaultService)
    {
        _vaultService = vaultService;
    }

    public async Task<FileAddResult> AddFileAsync(
        FileAddRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vault = _vaultService.Current
            ?? throw new InvalidOperationException("Vault is not open");

        var sourcePath = Path.GetFullPath(request.SourcePath);

        if (IsSymlink(sourcePath))
            return new FileAddResult(
                FileAddOutcome.SkippedSymlink, sourcePath, null,
                "Symlinks are not added to the vault");

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source file not found", sourcePath);

        var fileName = Path.GetFileName(sourcePath).Normalize(NormalizationForm.FormC);

        if (fileName.Length > FileLimits.MaxFileNameLength)
            return new FileAddResult(
                FileAddOutcome.SkippedNameTooLong, sourcePath, null,
                $"File name longer than {FileLimits.MaxFileNameLength} chars");

        if (!PathValidator.IsValidFileName(fileName))
            return new FileAddResult(
                FileAddOutcome.SkippedInvalidName, sourcePath, null,
                "File name contains invalid characters");

        if (!FileTypeClassifier.IsSupported(fileName))
            return new FileAddResult(
                FileAddOutcome.SkippedUnsupported, sourcePath, null,
                "Unsupported file type");

        var info = new FileInfo(sourcePath);
        if (info.Length > FileLimits.MaxIngestSizeBytes)
            return new FileAddResult(
                FileAddOutcome.SkippedTooLarge, sourcePath, null,
                $"File exceeds {FileLimits.MaxIngestSizeBytes / (1024 * 1024)} MB");

        var subdir = NormalizeSubdirectory(request.RelativeSubdirectory);
        var targetDir = string.IsNullOrEmpty(subdir)
            ? vault.RawDirectory
            : Path.Combine(vault.RawDirectory, subdir);

        Directory.CreateDirectory(targetDir);
        _vaultService.EnsureWithinVault(targetDir);

        var targetPath = Path.Combine(targetDir, fileName);

        var conflict = FindCaseInsensitiveConflict(targetDir, fileName);
        var outcome = FileAddOutcome.Added;

        if (conflict is not null)
        {
            switch (request.OnConflict)
            {
                case NameConflictResolution.Fail:
                    return new FileAddResult(
                        FileAddOutcome.NameConflict,
                        sourcePath,
                        ToRelative(vault.Path, conflict),
                        "A file with the same name already exists");

                case NameConflictResolution.Replace:
                    targetPath = conflict;
                    outcome = FileAddOutcome.Replaced;
                    break;

                case NameConflictResolution.Rename:
                    targetPath = MakeUniquePath(targetDir, fileName);
                    outcome = FileAddOutcome.Renamed;
                    break;
            }
        }

        _vaultService.EnsureWithinVault(targetPath);
        await CopyFileAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);

        return new FileAddResult(
            outcome,
            sourcePath,
            ToRelative(vault.Path, targetPath),
            null);
    }

    public async Task<FolderAddResult> AddFolderAsync(
        string folderPath,
        NameConflictResolution onConflict = NameConflictResolution.Rename,
        CancellationToken cancellationToken = default)
    {
        var vault = _vaultService.Current
            ?? throw new InvalidOperationException("Vault is not open");

        var rootPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException(folderPath);

        var results = new List<FileAddResult>();

        foreach (var sourceFile in EnumerateNonSymlinkFiles(rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relDir = Path.GetRelativePath(rootPath, Path.GetDirectoryName(sourceFile)!);
            relDir = relDir == "." ? string.Empty : relDir;

            var result = await AddFileAsync(
                new FileAddRequest(sourceFile, onConflict, relDir),
                cancellationToken).ConfigureAwait(false);

            results.Add(result);
        }

        var added = results.Count(r =>
            r.Outcome is FileAddOutcome.Added or FileAddOutcome.Replaced or FileAddOutcome.Renamed);
        var skippedUnsupported = results.Count(r =>
            r.Outcome == FileAddOutcome.SkippedUnsupported);
        var skippedOther = results.Count - added - skippedUnsupported;

        return new FolderAddResult(results, added, skippedUnsupported, skippedOther);
    }

    public IEnumerable<RawFile> EnumerateRawFiles()
    {
        var vault = _vaultService.Current;
        if (vault is null) yield break;
        if (!Directory.Exists(vault.RawDirectory)) yield break;

        foreach (var path in Directory.EnumerateFiles(
                     vault.RawDirectory, "*", SearchOption.AllDirectories))
        {
            if (IsSymlink(path)) continue;

            var fileName = Path.GetFileName(path);
            if (fileName.StartsWith('.')) continue;

            var info = new FileInfo(path);
            var rel = Path.GetRelativePath(vault.Path, path).Replace('\\', '/');

            yield return new RawFile(
                rel,
                fileName,
                Path.GetExtension(fileName),
                info.LastWriteTimeUtc,
                info.Length,
                FileTypeClassifier.Classify(fileName));
        }
    }

    private static IEnumerable<string> EnumerateNonSymlinkFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        var seenRealPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (IsSymlink(current)) continue;

            string? real;
            try { real = Path.GetFullPath(current); }
            catch { continue; }
            if (!seenRealPaths.Add(real)) continue;

            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(current); }
            catch { continue; }

            foreach (var entry in entries)
            {
                if (IsSymlink(entry)) continue;

                if (Directory.Exists(entry))
                {
                    stack.Push(entry);
                }
                else if (File.Exists(entry))
                {
                    yield return entry;
                }
            }
        }
    }

    private static string? FindCaseInsensitiveConflict(string directory, string fileName)
    {
        if (!Directory.Exists(directory)) return null;
        return Directory.EnumerateFiles(directory)
            .FirstOrDefault(p => string.Equals(
                Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string MakeUniquePath(string directory, string fileName)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);

        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(directory, $"{nameNoExt} ({i}){ext}");
            if (FindCaseInsensitiveConflict(directory, Path.GetFileName(candidate)) is null)
                return candidate;
        }

        throw new IOException("Failed to find a unique file name");
    }

    private static string NormalizeSubdirectory(string? sub)
    {
        if (string.IsNullOrWhiteSpace(sub)) return string.Empty;

        var normalized = sub.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0) return string.Empty;

        if (normalized.Split('/').Any(s =>
                s == ".." || s == "." || !PathValidator.IsValidFileName(s)))
            throw new ArgumentException(
                "Subdirectory contains invalid segments", nameof(sub));

        return normalized.Replace('/', Path.DirectorySeparatorChar);
    }

    private static async Task CopyFileAsync(
        string source, string destination, CancellationToken cancellationToken)
    {
        await using var src = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var dst = new FileStream(
            destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dst, cancellationToken).ConfigureAwait(false);
        dst.Flush(flushToDisk: true);
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            if (Directory.Exists(path))
            {
                var dInfo = new DirectoryInfo(path);
                if ((dInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    private static string ToRelative(string vaultRoot, string absolutePath)
    {
        return Path.GetRelativePath(vaultRoot, absolutePath).Replace('\\', '/');
    }
}
