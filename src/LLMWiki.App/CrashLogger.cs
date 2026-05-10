using LLMWiki.Core.Infrastructure;

namespace LLMWiki.App;

internal static class CrashLogger
{
    public static void WriteCrash(Exception ex, string context)
    {
        try
        {
            Directory.CreateDirectory(LLMWikiPaths.AppData);
            var path = Path.Combine(LLMWikiPaths.AppData, "crash.log");
            var entry = $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}] {context}\n{ex}\n\n";
            File.AppendAllText(path, entry);
        }
        catch
        {
            // last-resort: nothing more we can do
        }
    }

    public static void WriteCrash(string message, string context)
    {
        try
        {
            Directory.CreateDirectory(LLMWikiPaths.AppData);
            var path = Path.Combine(LLMWikiPaths.AppData, "crash.log");
            var entry = $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}] {context}: {message}\n\n";
            File.AppendAllText(path, entry);
        }
        catch { }
    }
}
