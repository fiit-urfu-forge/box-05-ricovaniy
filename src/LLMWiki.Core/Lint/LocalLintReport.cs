namespace LLMWiki.Core.Lint;

public sealed record BrokenLinkIssue(string SourcePage, string Target);

public sealed record OrphanPageIssue(string Page, string? Source);

public sealed record IsolatedNodeIssue(string Page);

public sealed record DuplicateGroupIssue(IReadOnlyList<string> Pages, string NormalizedTitle);

public sealed record LocalLintReport(
    IReadOnlyList<BrokenLinkIssue> BrokenLinks,
    IReadOnlyList<OrphanPageIssue> OrphanPages,
    IReadOnlyList<IsolatedNodeIssue> IsolatedNodes,
    IReadOnlyList<DuplicateGroupIssue> Duplicates)
{
    public int IssueCount =>
        BrokenLinks.Count + OrphanPages.Count + IsolatedNodes.Count + Duplicates.Count;
}
