namespace LLMWiki.Core.Ingest;

public enum IngestProgressKind
{
    Read,
    Write,
    Edit,
    Text,
    Glob,
    Grep,
    Search,
    WebFetch,
    Todo,
    Subagent,
    Notebook,
    OtherTool,
}

public sealed record IngestProgressEvent(
    IngestProgressKind Kind,
    string? RelativePath,
    string? ToolName,
    string? Snippet)
{
    public string Describe() => Kind switch
    {
        IngestProgressKind.Read =>     $"Читает: {RelativePath}",
        IngestProgressKind.Write =>    $"Создаёт: {RelativePath}",
        IngestProgressKind.Edit =>     $"Обновляет: {RelativePath}",
        IngestProgressKind.Glob =>     $"Ищет файлы по шаблону: {Snippet}",
        IngestProgressKind.Grep =>     $"Поиск по содержимому: {Snippet}",
        IngestProgressKind.Search =>   $"Веб-поиск: {Snippet}",
        IngestProgressKind.WebFetch => $"Загружает URL: {Snippet}",
        IngestProgressKind.Todo =>     "Обновляет внутренний план",
        IngestProgressKind.Subagent => $"Запускает sub-agent: {Snippet}",
        IngestProgressKind.Notebook => $"Правит ноутбук: {RelativePath}",
        IngestProgressKind.Text =>     Snippet ?? string.Empty,
        IngestProgressKind.OtherTool =>$"Инструмент: {ToolName}",
        _ => string.Empty,
    };
}
