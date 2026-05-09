namespace LLMWiki.Core.Domain;

public sealed record RawFile(
    string RelativePath,
    string FileName,
    string Extension,
    DateTime AddedAt,
    long SizeBytes,
    FileType Type);
