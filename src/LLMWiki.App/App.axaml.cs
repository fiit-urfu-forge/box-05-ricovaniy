using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LLMWiki.App.Agent;
using LLMWiki.App.ViewModels;
using LLMWiki.Core.Agent;
using LLMWiki.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LLMWiki.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private SingleInstanceLock? _instanceLock;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            Services = AppServices.Build();
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _instanceLock = new SingleInstanceLock();
                if (!_instanceLock.TryAcquire())
                {
                    desktop.Shutdown(0);
                    return;
                }

                desktop.Exit += OnExit;

                var vm = Services.GetRequiredService<MainWindowViewModel>();
                var window = new MainWindow { DataContext = vm };
                desktop.MainWindow = window;

                window.Opened += async (_, _) =>
                {
                    try
                    {
                        await PostStartupAsync(window, vm);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Post-startup failed");
                        await ShowMessageAsync(window, "Ошибка запуска",
                            $"Не удалось завершить инициализацию: {ex.Message}");
                    }
                };
            }
        }
        catch (Exception ex)
        {
            CrashLogger.WriteCrash(ex, "OnFrameworkInitializationCompleted");
            try { Log.Logger.Fatal(ex, "Application failed to start"); } catch { }
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.Shutdown(1);
            return;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task PostStartupAsync(MainWindow window, MainWindowViewModel vm)
    {
        await vm.InitializeAsync();

        var hasClaude = await ClaudeCliChecker.IsInstalledAsync();
        if (!hasClaude)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "(empty)";
            var expectedName = ClaudeCliResolver.ExecutableName;
            await ShowMessageAsync(window, "Claude Code не найден",
                $"Искали `{expectedName}` в PATH и в стандартных npm-локациях, не нашли.\n\n" +
                "Установите Claude Code и убедитесь, что он в PATH:\n" +
                "  npm install -g @anthropic-ai/claude-code\n\n" +
                "На Windows глобальный npm bin обычно " +
                @"%APPDATA%\npm — добавьте его в PATH, " +
                "если ещё не добавлен.\n\n" +
                $"Текущий PATH:\n{pathVar}");
            return;
        }

        var auth = Services.GetRequiredService<ICredentialsChecker>().Check();
        if (auth.Status != ClaudeAuthStatus.Authorized)
        {
            var loginVm = Services.GetRequiredService<ClaudeLoginViewModel>();
            var login = new Views.ClaudeLoginWindow { DataContext = loginVm };
            await login.ShowDialog(window);
        }
    }

    private static Task ShowMessageAsync(Window owner, string title, string message)
    {
        var tcs = new TaskCompletionSource();
        var ok = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var dialog = new Window
        {
            Title = title,
            Width = 480,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                    ok,
                },
            },
        };
        ok.Click += (_, _) => dialog.Close();
        dialog.Closed += (_, _) => tcs.TrySetResult();

        Dispatcher.UIThread.Post(async () =>
        {
            try { await dialog.ShowDialog(owner); }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Failed to show message dialog");
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        if (ex is not null) CrashLogger.WriteCrash(ex, "Unhandled domain exception");
        try { Log.Logger.Fatal(ex, "Unhandled domain exception"); } catch { }
    }

    private static void OnUnobservedTaskException(
        object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashLogger.WriteCrash(e.Exception, "Unobserved task exception");
        try { Log.Logger.Error(e.Exception, "Unobserved task exception"); } catch { }
        e.SetObserved();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            if (Services is ServiceProvider sp)
            {
                var ingest = sp.GetService<Services.IngestService>();
                if (ingest is not null)
                    ingest.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));

                var git = sp.GetService<Services.GitSyncCoordinator>();
                if (git is not null)
                    git.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));

                sp.Dispose();
            }
        }
        catch (Exception ex)
        {
            try { Log.Logger.Error(ex, "Cleanup on exit failed"); } catch { }
        }
        finally
        {
            _instanceLock?.Dispose();
            _instanceLock = null;
            try { Log.CloseAndFlush(); } catch { }
        }
    }
}
