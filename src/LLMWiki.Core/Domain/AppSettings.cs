namespace LLMWiki.Core.Domain;

public sealed class AppSettings
{
    public string? VaultPath { get; set; }

    public string? ClaudeCredentialsPath { get; set; }

    public bool WikiOnlyMode { get; set; } = true;

    public string? GitRemoteUrl { get; set; }

    public bool GitAutoSync { get; set; }

    public int GitAutoSyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum minutes a single Claude operation may run before being cancelled.
    /// Large HTML/PDF ingests routinely take 10+ minutes, so default is 15.
    /// </summary>
    public int ClaudeTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Seconds without any token from Claude that triggers stalled-stream cancel.
    /// </summary>
    public int StalledStreamSeconds { get; set; } = 90;

    public static AppSettings Default() => new();
}
