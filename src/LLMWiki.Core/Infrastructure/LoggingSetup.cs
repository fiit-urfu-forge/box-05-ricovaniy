using Serilog;
using Serilog.Events;

namespace LLMWiki.Core.Infrastructure;

public static class LoggingSetup
{
    private static bool _initialized;

    public static ILogger Configure()
    {
        if (_initialized) return Log.Logger;

        LLMWikiPaths.EnsureAppDirectories();
        var logFile = Path.Combine(LLMWikiPaths.Logs, "llmwiki-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("App", "LLMWiki")
            .WriteTo.File(
                path: logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(5),
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-ddTHH:mm:ssZ} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        _initialized = true;
        return Log.Logger;
    }
}
