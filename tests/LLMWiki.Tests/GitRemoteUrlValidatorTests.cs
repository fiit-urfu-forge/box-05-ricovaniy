using LLMWiki.Core.Git;

namespace LLMWiki.Tests;

[TestFixture]
public class GitRemoteUrlValidatorTests
{
    [TestCase("https://github.com/user/repo.git", GitRemoteUrlValidation.Valid)]
    [TestCase("https://github.com/user/repo", GitRemoteUrlValidation.Valid)]
    [TestCase("http://github.com/user/repo", GitRemoteUrlValidation.NotHttps)]
    [TestCase("git@github.com:user/repo.git", GitRemoteUrlValidation.Malformed)]
    [TestCase("https://gitlab.com/user/repo", GitRemoteUrlValidation.NotGithubHost)]
    [TestCase("https://user:pat@github.com/u/r", GitRemoteUrlValidation.EmbeddedCredentials)]
    [TestCase("https://github.com/user/repo;rm -rf /", GitRemoteUrlValidation.ContainsShellMetacharacters)]
    [TestCase("", GitRemoteUrlValidation.Empty)]
    [TestCase("   ", GitRemoteUrlValidation.Empty)]
    [TestCase("https://github.com/", GitRemoteUrlValidation.Malformed)]
    [TestCase("not a url", GitRemoteUrlValidation.Malformed)]
    public void Validate_Cases(string url, GitRemoteUrlValidation expected)
    {
        GitRemoteUrlValidator.Validate(url).Status.Should().Be(expected);
    }
}
