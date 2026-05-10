using LLMWiki.Core.Domain;
using LLMWiki.Core.Graph;
using LLMWiki.Core.Parsing;
using DomainVault = LLMWiki.Core.Domain.Vault;

namespace LLMWiki.Core.Lint;

public sealed class LocalLintRunner
{
    private readonly IGraphBuilder _graphBuilder;

    public LocalLintRunner(IGraphBuilder graphBuilder)
    {
        _graphBuilder = graphBuilder;
    }

    public LocalLintReport Run(DomainVault vault)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var graph = _graphBuilder.BuildFromVault(vault);
        var pages = LoadPages(vault).ToList();

        return new LocalLintReport(
            CollectBrokenLinks(graph),
            CollectOrphans(pages),
            CollectIsolatedNodes(graph),
            CollectDuplicates(pages));
    }

    private static IReadOnlyList<BrokenLinkIssue> CollectBrokenLinks(KnowledgeGraph graph)
    {
        var ghosts = graph.Nodes
            .Where(n => n.IsGhost)
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ghosts.Count == 0) return Array.Empty<BrokenLinkIssue>();

        return graph.Edges
            .Where(e => ghosts.Contains(e.Target))
            .Select(e => new BrokenLinkIssue(e.Source, e.Target))
            .ToList();
    }

    private static IReadOnlyList<OrphanPageIssue> CollectOrphans(IReadOnlyList<PageRecord> pages)
    {
        return pages
            .Where(p => p.Frontmatter is { Source: { Length: > 0 } } &&
                        !p.SourceExists)
            .Select(p => new OrphanPageIssue(p.RelativePath, p.Frontmatter!.Source))
            .ToList();
    }

    private static IReadOnlyList<IsolatedNodeIssue> CollectIsolatedNodes(KnowledgeGraph graph)
    {
        var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            connected.Add(edge.Source);
            connected.Add(edge.Target);
        }

        return graph.Nodes
            .Where(n => n.Type == NodeType.WikiPage && !n.IsGhost && !connected.Contains(n.Id))
            .Select(n => new IsolatedNodeIssue(n.Id))
            .ToList();
    }

    private static IReadOnlyList<DuplicateGroupIssue> CollectDuplicates(
        IReadOnlyList<PageRecord> pages)
    {
        return pages
            .GroupBy(p => Normalize(p.Title), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
            .Select(g => new DuplicateGroupIssue(
                g.Select(p => p.RelativePath).ToList(),
                g.Key))
            .ToList();
    }

    private static IEnumerable<PageRecord> LoadPages(DomainVault vault)
    {
        if (!Directory.Exists(vault.WikiDirectory)) yield break;

        foreach (var path in Directory.EnumerateFiles(
                     vault.WikiDirectory, "*.md", SearchOption.AllDirectories))
        {
            string content;
            try { content = File.ReadAllText(path); }
            catch { continue; }

            var rel = Path.GetRelativePath(vault.Path, path).Replace('\\', '/');
            var parsed = FrontmatterParser.Parse(content, Path.GetFileName(path));
            var sourceExists = parsed.Frontmatter?.Source is { Length: > 0 } src
                && File.Exists(Path.Combine(vault.Path, src));

            yield return new PageRecord(rel, parsed.Title, parsed.Frontmatter, sourceExists);
        }
    }

    private static string Normalize(string title) =>
        new string(title.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    private sealed record PageRecord(
        string RelativePath,
        string Title,
        WikiFrontmatter? Frontmatter,
        bool SourceExists);
}
