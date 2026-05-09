namespace LLMWiki.Core.Domain;

public sealed record ChatMessage(
    MessageRole Role,
    string Content,
    DateTime Timestamp);
