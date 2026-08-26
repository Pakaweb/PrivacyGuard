using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using PrivacyGuard.Helpers;
using PrivacyGuard.Services;
using Serilog;

namespace PrivacyGuard;

/// <summary>
/// Application bootstrap: Serilog, dependency injection, and the main window.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The single main window instance, set after launch.
    /// </summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Root DI / logging host for the process.
    /// </summary>
    public IHost Host { get; }

    public App()
    {
        InitializeComponent();
        AppPaths.EnsureCreated();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDirectory, "privacyguard-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        UnhandledException += OnUnhandledException;

        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) => services.AddPrivacyGuard())
            .Build();
    }

    /// <summary>
    /// Resolves a service from the application container.
    /// </summary>
    public static T GetService<T>() where T : class =>
        ((App)Current).Host.Services.GetRequiredService<T>();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await GetService<ISettingsService>().LoadAsync();
            await GetService<IChangeHistoryService>().InitializeAsync();
            await GetService<IBackupService>().InitializeAsync();

            GetService<ILocalizationService>().Apply(GetService<ISettingsService>().Current.Language, notify: false);
            var window = GetService<MainWindow>();
            MainWindow = window;
            GetService<IThemeService>().Apply(GetService<ISettingsService>().Current.Theme);
            window.Activate();
            GetService<ITrayIconService>().Initialize(window);
            GetService<IResetMonitorService>().Start(window.DispatcherQueue);
            var dispatcher = window.DispatcherQueue;
            dispatcher.TryEnqueue(async () => await window.TryShowFirstRunAsync());
            window.Closed += (_, _) =>
            {
                GetService<ITrayIconService>().Dispose();
                Current.Exit();
            };
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            throw;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI exception: {Message}", e.Message);
        e.Handled = true;
    }
}
