using System.Globalization;
using LLMWiki.Core.Domain;
using YamlDotNet.RepresentationModel;

namespace LLMWiki.Core.Parsing;

public static class FrontmatterParser
{
    private const string Delimiter = "---";

    public static ParsedWikiPage Parse(string content, string fileNameForFallbackTitle)
    {
        ArgumentNullException.ThrowIfNull(content);

        var (frontmatterRaw, body, hasFrontmatter) = SplitFrontmatter(content);

        WikiFrontmatter? frontmatter = null;
        var malformed = false;

        if (hasFrontmatter)
        {
            try
            {
                frontmatter = ParseYaml(frontmatterRaw!);
            }
            catch
            {
                malformed = true;
            }
        }

        var title = ExtractH1(body)
            ?? Path.GetFileNameWithoutExtension(fileNameForFallbackTitle);

        return new ParsedWikiPage(title, body, frontmatter, malformed);
    }

    public static string Compose(WikiFrontmatter? frontmatter, string body)
    {
        if (frontmatter is null) return body;

        var lines = new List<string> { Delimiter };

        if (!string.IsNullOrEmpty(frontmatter.Source))
            lines.Add($"source: {frontmatter.Source}");
        if (frontmatter.GeneratedAt is { } gen)
            lines.Add($"generated_at: {gen.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
        if (frontmatter.Orphaned)
            lines.Add("orphaned: true");

        lines.Add(Delimiter);
        lines.Add(string.Empty);
        return string.Join('\n', lines) + body;
    }

    private static (string? frontmatter, string body, bool hasFrontmatter)
        SplitFrontmatter(string content)
    {
        if (!content.StartsWith(Delimiter, StringComparison.Ordinal))
            return (null, content, false);

        var afterFirst = content[Delimiter.Length..];
        if (afterFirst.Length == 0 || afterFirst[0] is not ('\n' or '\r'))
            return (null, content, false);

        var searchFrom = SkipNewline(afterFirst, 0);
        var endIdx = FindClosingDelimiter(afterFirst, searchFrom);
        if (endIdx < 0) return (null, content, false);

        var fm = afterFirst[searchFrom..endIdx];
        var afterClose = SkipNewline(afterFirst, endIdx + Delimiter.Length);
        var body = afterFirst[afterClose..];

        return (fm, body, true);
    }

    private static int FindClosingDelimiter(string text, int from)
    {
        var idx = from;
        while (idx < text.Length)
        {
            if (idx + Delimiter.Length <= text.Length
                && text.AsSpan(idx, Delimiter.Length).SequenceEqual(Delimiter))
            {
                var afterDelim = idx + Delimiter.Length;
                var atLineStart = idx == 0 || text[idx - 1] == '\n';
                var atLineEnd =
                    afterDelim == text.Length
                    || text[afterDelim] is '\n' or '\r';
                if (atLineStart && atLineEnd) return idx;
            }

            var nl = text.IndexOf('\n', idx);
            if (nl < 0) return -1;
            idx = nl + 1;
        }
        return -1;
    }

    private static int SkipNewline(string text, int pos)
    {
        if (pos < text.Length && text[pos] == '\r') pos++;
        if (pos < text.Length && text[pos] == '\n') pos++;
        return pos;
    }

    private static WikiFrontmatter ParseYaml(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);

        if (stream.Documents.Count == 0)
            return new WikiFrontmatter(null, null, false);

        var root = stream.Documents[0].RootNode;
        if (root is not YamlMappingNode map)
            return new WikiFrontmatter(null, null, false);

        string? source = null;
        DateTime? generatedAt = null;
        var orphaned = false;

        foreach (var entry in map.Children)
        {
            if (entry.Key is not YamlScalarNode key) continue;
            var keyName = key.Value?.Trim().ToLowerInvariant();
            var value = entry.Value as YamlScalarNode;

            switch (keyName)
            {
                case "source":
                    source = value?.Value;
                    break;
                case "generated_at":
                    if (DateTime.TryParse(
                            value?.Value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out var parsed))
                        generatedAt = parsed.ToUniversalTime();
                    break;
                case "orphaned":
                    orphaned = string.Equals(
                        value?.Value, "true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        return new WikiFrontmatter(source, generatedAt, orphaned);
    }

    private static string? ExtractH1(string body)
    {
        using var reader = new StringReader(body);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                return trimmed[2..].Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#') && !IsBlank(trimmed))
                continue;
        }
        return null;
    }

    private static bool IsBlank(string s) =>
        string.IsNullOrWhiteSpace(s);
}
