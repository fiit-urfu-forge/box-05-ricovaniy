using LLMWiki.App.Agent;
using LLMWiki.App.Services;
using LLMWiki.App.ViewModels;
using LLMWiki.Core.Agent;
using LLMWiki.Core.Files;
using LLMWiki.Core.Git;
using LLMWiki.Core.Graph;
using LLMWiki.Core.Infrastructure;
using LLMWiki.Core.Lint;
using LLMWiki.Core.Settings;
using LLMWiki.Core.Vault;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LLMWiki.App;

public static class AppServices
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        var logger = LoggingSetup.Configure();
        services.AddLogging(b =>
            b.AddProvider(new Serilog.Extensions.Logging.SerilogLoggerProvider(logger, dispose: false)));

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IGraphBuilder, GraphBuilder>();
        services.AddSingleton<LocalLintRunner>();
        services.AddSingleton<ICredentialsChecker>(_ => new CredentialsChecker());
        services.AddSingleton<IPatStorage, PlatformPatStorage>();
        services.AddSingleton<IClaudeAgentFactory, SdkClaudeAgentFactory>();
        services.AddSingleton<IngestService>();
        services.AddSingleton<GitSyncCoordinator>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<FilesViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<GraphViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ConflictResolutionViewModel>();
        services.AddTransient<ClaudeLoginViewModel>();

        return services.BuildServiceProvider();
    }
}
