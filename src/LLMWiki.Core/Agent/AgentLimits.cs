namespace LLMWiki.Core.Agent;

public static class AgentLimits
{
    public const int IngestMaxTurns = 200;

    public const int LintMaxTurns = 100;

    public const int QueryMaxTurns = 50;

    public static readonly TimeSpan ClaudeTimeout = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan StalledStreamTimeout = TimeSpan.FromSeconds(90);

    public const int CircuitBreakerFailureThreshold = 5;

    public static readonly TimeSpan CircuitBreakerCooldown = TimeSpan.FromMinutes(5);
}
