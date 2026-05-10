using LLMWiki.Core.Domain;
using LLMWiki.Core.Parsing;
using LLMWiki.Core.Vault;
using DomainVault = LLMWiki.Core.Domain.Vault;

namespace LLMWiki.Core.Graph;

public sealed class GraphBuilder : IGraphBuilder
{
    private readonly IVaultService _vaultService;

    public GraphBuilder(IVaultService vaultService)
    {
        _vaultService = vaultService;
    }

    public KnowledgeGraph Build()
    {
        var vault = _vaultService.Current
            ?? throw new InvalidOperationException("Vault is not open");
        return BuildFromVault(vault);
    }

    public KnowledgeGraph BuildFromVault(DomainVault vault)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var pages = LoadPages(vault).ToList();
        var pageById = new Dictionary<string, WikiPageEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
            pageById[page.Id] = page;

        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var ghostNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEdges = new HashSet<(string, string)>();

        AddIndexNodeIfExists(vault, "index.md", nodes);
        AddIndexNodeIfExists(vault, "log.md", nodes);

        foreach (var page in pages)
        {
            var orphan = page.Frontmatter is { Source: { Length: > 0 } src }
                && !RawSourceExists(vault, src);

            nodes.Add(new GraphNode(
                page.Id,
                Path.GetFileNameWithoutExtension(page.Id),
                NodeType.WikiPage,
                IsGhost: false,
                IsOrphan: orphan));
        }

        foreach (var page in pages)
        {
            var sourceId = page.Id;
            foreach (var link in page.Links)
            {
                var targetId = ResolveTarget(link.Target, pageById);
                if (string.Equals(targetId, sourceId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (targetId is null)
                {
                    var ghostId = NormalizeGhostId(link.Target);
                    if (ghostNodes.Add(ghostId))
                        nodes.Add(new GraphNode(
                            ghostId, link.Display, NodeType.WikiPage, IsGhost: true));
                    AddEdgeIfNew(edges, seenEdges, sourceId, ghostId);
                }
                else
                {
                    AddEdgeIfNew(edges, seenEdges, sourceId, targetId);
                }
            }
        }

        return new KnowledgeGraph(nodes, edges);
    }

    private static IEnumerable<WikiPageEntry> LoadPages(DomainVault vault)
    {
        if (!Directory.Exists(vault.WikiDirectory)) yield break;

        foreach (var path in Directory.EnumerateFiles(
                     vault.WikiDirectory, "*.md", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(vault.Path, path).Replace('\\', '/');

            string content;
            try { content = File.ReadAllText(path); }
            catch { continue; }

            var parsed = FrontmatterParser.Parse(content, Path.GetFileName(path));
            var links = WikiLinkParser.ExtractLinks(parsed.Body);

            yield return new WikiPageEntry(rel, parsed.Frontmatter, links);
        }
    }

    private static void AddIndexNodeIfExists(DomainVault vault, string fileName, List<GraphNode> nodes)
    {
        var path = Path.Combine(vault.Path, fileName);
        if (!File.Exists(path)) return;

        var rel = fileName;
        nodes.Add(new GraphNode(rel, Path.GetFileNameWithoutExtension(fileName), NodeType.IndexPage));
    }

    private static string? ResolveTarget(
        string linkTarget,
        IReadOnlyDictionary<string, WikiPageEntry> pages)
    {
        var normalized = linkTarget.Replace('\\', '/').Trim();
        if (normalized.Length == 0) return null;

        if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            normalized += ".md";

        var withWiki = $"wiki/{normalized}";
        if (pages.TryGetValue(withWiki, out var entry))
            return entry.Id;

        var fileName = Path.GetFileName(normalized);
        var match = pages.Values.FirstOrDefault(p =>
            string.Equals(Path.GetFileName(p.Id), fileName, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    private static string NormalizeGhostId(string linkTarget)
    {
        var normalized = linkTarget.Replace('\\', '/').Trim();
        if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            normalized += ".md";
        if (!normalized.StartsWith("wiki/", StringComparison.OrdinalIgnoreCase))
            normalized = $"wiki/{normalized}";
        return normalized;
    }

    private static void AddEdgeIfNew(
        List<GraphEdge> edges,
        HashSet<(string, string)> seen,
        string source,
        string target)
    {
        var key = (source.ToLowerInvariant(), target.ToLowerInvariant());
        if (!seen.Add(key)) return;
        edges.Add(new GraphEdge(source, target, 1));
    }

    private static bool RawSourceExists(DomainVault vault, string source)
    {
        var normalized = source.Replace('\\', '/');
        var combined = Path.Combine(vault.Path, normalized);
        return File.Exists(combined);
    }

    private sealed record WikiPageEntry(
        string Id,
        WikiFrontmatter? Frontmatter,
        IReadOnlyList<WikiLink> Links);
}
