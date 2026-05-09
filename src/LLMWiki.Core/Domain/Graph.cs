namespace LLMWiki.Core.Domain;

public sealed record GraphNode(
    string Id,
    string Label,
    NodeType Type,
    bool IsGhost = false,
    bool IsOrphan = false);

public sealed record GraphEdge(
    string Source,
    string Target,
    int Weight);

public sealed record KnowledgeGraph(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);
