using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Graph;

public interface IGraphBuilder
{
    KnowledgeGraph Build();

    KnowledgeGraph BuildFromVault(Domain.Vault vault);
}
