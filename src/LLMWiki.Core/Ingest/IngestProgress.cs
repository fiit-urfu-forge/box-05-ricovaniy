namespace LLMWiki.Core.Ingest;

public enum IngestProgressKind
{
    Read,
    Write,
    Edit,
    Text,
    OtherTool
}

public sealed record IngestProgressEvent(
    IngestProgressKind Kind,
    string? RelativePath,
    string? ToolName,
    string? Snippet)
{
    public string Describe() => Kind switch
    {
        IngestProgressKind.Read =>  $"Читает: {RelativePath}",
        IngestProgressKind.Write => $"Создаёт: {RelativePath}",
        IngestProgressKind.Edit =>  $"Обновляет: {RelativePath}",
        IngestProgressKind.Text =>  Snippet ?? string.Empty,
        IngestProgressKind.OtherTool => $"Инструмент: {ToolName}",
        _ => string.Empty,
    };
}
