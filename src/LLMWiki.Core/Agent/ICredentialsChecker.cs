namespace LLMWiki.Core.Agent;

public enum ClaudeAuthStatus
{
    Authorized,
    NoCredentials,
    InvalidApiKey
}

public sealed record ClaudeAuthState(
    ClaudeAuthStatus Status,
    string? CredentialsPath,
    string? Detail);

public interface ICredentialsChecker
{
    ClaudeAuthState Check();
}
