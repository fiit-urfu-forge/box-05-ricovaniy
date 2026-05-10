using System.Text.RegularExpressions;

namespace LLMWiki.Core.Parsing;

public sealed record WikiLink(string Target, string? Alias)
{
    public string Display => Alias ?? Target;
}

public static partial class WikiLinkParser
{
    [GeneratedRegex(@"\[\[(?<target>[^\[\]\|]+?)(?:\|(?<alias>[^\[\]]+?))?\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex FencedCodeRegex();

    [GeneratedRegex(@"`[^`\n]+`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    public static IReadOnlyList<WikiLink> ExtractLinks(string content)
    {
        if (string.IsNullOrEmpty(content)) return Array.Empty<WikiLink>();

        var sanitized = FencedCodeRegex().Replace(content, m => new string(' ', m.Length));
        sanitized = InlineCodeRegex().Replace(sanitized, m => new string(' ', m.Length));

        var matches = LinkRegex().Matches(sanitized);
        if (matches.Count == 0) return Array.Empty<WikiLink>();

        var links = new List<WikiLink>(matches.Count);
        foreach (Match m in matches)
        {
            var target = m.Groups["target"].Value.Trim();
            if (target.Length == 0) continue;

            var aliasGroup = m.Groups["alias"];
            string? alias = aliasGroup.Success ? aliasGroup.Value.Trim() : null;
            if (alias is { Length: 0 }) alias = null;

            links.Add(new WikiLink(target, alias));
        }
        return links;
    }
}
