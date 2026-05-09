namespace LLMWiki.Core.Domain;

public enum FileType
{
    Text,
    Pdf,
    Image,
    Other
}

public enum NodeType
{
    RawFile,
    WikiPage,
    IndexPage
}

public enum MessageRole
{
    User,
    Assistant
}

public enum GitSyncState
{
    NotConfigured,
    Idle,
    Pushing,
    Pulling,
    Conflict,
    Error
}

public enum ChatMode
{
    WikiOnly,
    Extended
}
