using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LLMWiki.App.Agent;
using LLMWiki.App.ViewModels;
using LLMWiki.Core.Agent;
using LLMWiki.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LLMWiki.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private SingleInstanceLock? _instanceLock;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        Services = AppServices.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _instanceLock = new SingleInstanceLock();
            if (!_instanceLock.TryAcquire())
            {
                desktop.Shutdown(0);
                return;
            }

            desktop.Exit += OnExit;
            desktop.ShutdownRequested += OnShutdownRequested;

            await EnsureClaudeAvailableAsync(desktop);

            var vm = Services.GetRequiredService<MainWindowViewModel>();
            await vm.InitializeAsync();

            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task EnsureClaudeAvailableAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!await ClaudeCliChecker.IsInstalledAsync())
        {
            await ShowMessageAsync(desktop,
                "Claude Code не установлен",
                "Требуется CLI `claude` в PATH. Установите Claude Code и перезапустите приложение.");
            return;
        }

        var auth = Services.GetRequiredService<ICredentialsChecker>().Check();
        if (auth.Status != ClaudeAuthStatus.Authorized)
        {
            var loginVm = Services.GetRequiredService<ClaudeLoginViewModel>();
            var window = new Views.ClaudeLoginWindow { DataContext = loginVm };
            await window.ShowDialog(GetAnyOpenWindow(desktop) ?? new Window
            {
                Width = 1, Height = 1, ShowInTaskbar = false, IsVisible = false,
            });
        }
    }

    private static Window? GetAnyOpenWindow(IClassicDesktopStyleApplicationLifetime desktop) =>
        desktop.MainWindow;

    private static async Task ShowMessageAsync(
        IClassicDesktopStyleApplicationLifetime desktop, string title, string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 480,
            Height = 200,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right },
                },
            },
        };
        if (desktop.MainWindow is null)
            window.Show();
        else
            await window.ShowDialog(desktop.MainWindow);
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // allow normal shutdown to proceed; cleanup happens in OnExit
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            if (Services is ServiceProvider sp)
            {
                var ingest = sp.GetService<Services.IngestService>();
                if (ingest is not null) ingest.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));

                var git = sp.GetService<Services.GitSyncCoordinator>();
                if (git is not null) git.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));

                sp.Dispose();
            }
        }
        finally
        {
            _instanceLock?.Dispose();
            _instanceLock = null;
        }
    }
}
