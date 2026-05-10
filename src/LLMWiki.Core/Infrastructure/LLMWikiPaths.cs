using System.Runtime.InteropServices;

namespace LLMWiki.Core.Infrastructure;

public static class LLMWikiPaths
{
    private const string AppFolderName = "LLMWiki";

    public static string AppData { get; } = ResolveAppData();

    public static string Logs { get; } = ResolveLogs();

    public static string SettingsFile => Path.Combine(AppData, "settings.json");

    public static string AppLockFile => Path.Combine(AppData, "app.lock");

    public static string ConflictResolutionStateFile =>
        Path.Combine(AppData, "conflict_resolution_state.json");

    public static string ClaudeCredentialsPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude", ".credentials.json");
    }

    public static void EnsureAppDirectories()
    {
        Directory.CreateDirectory(AppData);
        Directory.CreateDirectory(Logs);
    }

    private static string ResolveAppData()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppFolderName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", AppFolderName);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, AppFolderName);

        var linuxHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(linuxHome, ".config", AppFolderName);
    }

    private static string ResolveLogs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(AppData, "logs");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Logs", AppFolderName);
        }

        var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataRoot = !string.IsNullOrWhiteSpace(xdgData)
            ? xdgData
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");
        return Path.Combine(dataRoot, AppFolderName, "logs");
    }
}
