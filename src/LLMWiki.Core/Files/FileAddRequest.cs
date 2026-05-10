namespace LLMWiki.Core.Files;

public enum NameConflictResolution
{
    Fail,
    Replace,
    Rename
}

public sealed record FileAddRequest(
    string SourcePath,
    NameConflictResolution OnConflict = NameConflictResolution.Fail,
    string? RelativeSubdirectory = null);

public enum FileAddOutcome
{
    Added,
    Replaced,
    Renamed,
    SkippedUnsupported,
    SkippedTooLarge,
    SkippedNameTooLong,
    SkippedInvalidName,
    SkippedSymlink,
    NameConflict
}

public sealed record FileAddResult(
    FileAddOutcome Outcome,
    string SourcePath,
    string? CopiedToRelativePath,
    string? Reason);

public sealed record FolderAddResult(
    IReadOnlyList<FileAddResult> Files,
    int Added,
    int SkippedUnsupported,
    int SkippedOther);
