namespace LLMWiki.Core.Domain;

public sealed class AppSettings
{
    public string? VaultPath { get; set; }

    public string? ClaudeCredentialsPath { get; set; }

    public bool WikiOnlyMode { get; set; } = true;

    public string? GitRemoteUrl { get; set; }

    public bool GitAutoSync { get; set; }

    public int GitAutoSyncIntervalMinutes { get; set; } = 15;

    public static AppSettings Default() => new();
}
