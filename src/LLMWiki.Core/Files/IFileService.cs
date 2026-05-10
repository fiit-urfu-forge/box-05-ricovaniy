using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Files;

public interface IFileService
{
    Task<FileAddResult> AddFileAsync(
        FileAddRequest request,
        CancellationToken cancellationToken = default);

    Task<FolderAddResult> AddFolderAsync(
        string folderPath,
        NameConflictResolution onConflict = NameConflictResolution.Rename,
        CancellationToken cancellationToken = default);

    IEnumerable<RawFile> EnumerateRawFiles();
}
