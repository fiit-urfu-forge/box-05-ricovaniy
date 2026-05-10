using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Core.Agent;

public sealed class CredentialsChecker : ICredentialsChecker
{
    private readonly string _credentialsPath;
    private readonly Func<string?>? _apiKeyResolver;

    public CredentialsChecker(string? credentialsPath = null, Func<string?>? apiKeyResolver = null)
    {
        _credentialsPath = credentialsPath ?? LLMWikiPaths.ClaudeCredentialsPath();
        _apiKeyResolver = apiKeyResolver;
    }

    public ClaudeAuthState Check()
    {
        if (File.Exists(_credentialsPath))
        {
            try
            {
                var info = new FileInfo(_credentialsPath);
                if (info.Length > 0)
                {
                    return new ClaudeAuthState(
                        ClaudeAuthStatus.Authorized, _credentialsPath, null);
                }
            }
            catch
            {
                // fall through and try the API key path
            }
        }

        var apiKey = _apiKeyResolver?.Invoke();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return new ClaudeAuthState(
                ClaudeAuthStatus.Authorized, null, "Using manually provided API key");
        }

        return new ClaudeAuthState(
            ClaudeAuthStatus.NoCredentials,
            _credentialsPath,
            "Run `claude login` or set an API key in settings");
    }
}
