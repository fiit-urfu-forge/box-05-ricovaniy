namespace LLMWiki.Core.Git;

public interface IPatStorage
{
    string? Read(string key);

    void Write(string key, string value);

    void Delete(string key);
}
