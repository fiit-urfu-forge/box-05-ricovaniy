namespace LLMWiki.Core.Git;

public sealed class InMemoryPatStorage : IPatStorage
{
    private readonly Dictionary<string, string> _store = new(StringComparer.Ordinal);

    public string? Read(string key) => _store.GetValueOrDefault(key);

    public void Write(string key, string value) => _store[key] = value;

    public void Delete(string key) => _store.Remove(key);
}
