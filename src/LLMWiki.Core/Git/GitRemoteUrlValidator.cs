namespace LLMWiki.Core.Git;

public enum GitRemoteUrlValidation
{
    Valid,
    NotHttps,
    NotGithubHost,
    EmbeddedCredentials,
    ContainsShellMetacharacters,
    Empty,
    Malformed,
}

public sealed record GitRemoteUrlResult(
    GitRemoteUrlValidation Status,
    string? Detail)
{
    public bool IsValid => Status == GitRemoteUrlValidation.Valid;
}

public static class GitRemoteUrlValidator
{
    private static readonly char[] ShellMetacharacters =
        { ';', '|', '&', '`', '$', '<', '>', '\n', '\r', '\t' };

    public static GitRemoteUrlResult Validate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new GitRemoteUrlResult(GitRemoteUrlValidation.Empty,
                "URL is empty");

        if (url.IndexOfAny(ShellMetacharacters) >= 0)
            return new GitRemoteUrlResult(
                GitRemoteUrlValidation.ContainsShellMetacharacters,
                "URL contains shell metacharacters");

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return new GitRemoteUrlResult(GitRemoteUrlValidation.Malformed,
                "URL is malformed");

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            return new GitRemoteUrlResult(GitRemoteUrlValidation.NotHttps,
                $"Only https is supported, got '{uri.Scheme}'");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return new GitRemoteUrlResult(GitRemoteUrlValidation.EmbeddedCredentials,
                "URL contains embedded credentials (user:pass@...)");

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return new GitRemoteUrlResult(GitRemoteUrlValidation.NotGithubHost,
                $"Only github.com is supported, got '{uri.Host}'");

        if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
            return new GitRemoteUrlResult(GitRemoteUrlValidation.Malformed,
                "URL is missing the user/repo path");

        return new GitRemoteUrlResult(GitRemoteUrlValidation.Valid, null);
    }
}
