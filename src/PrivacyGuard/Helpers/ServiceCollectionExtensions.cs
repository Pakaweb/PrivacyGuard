using PrivacyGuard.Services;
using PrivacyGuard.ViewModels;
using PrivacyGuard.Views;

namespace PrivacyGuard.Helpers;

/// <summary>
/// Registers application services and view models with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPrivacyGuard(this IServiceCollection services)
    {
        services.AddSingleton<RegistryHelper>();
        services.AddSingleton<WindowsServiceHelper>();

        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IChangeHistoryService, ChangeHistoryService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IPrivacyService, PrivacyService>();
        services.AddSingleton<ICustomProfileStore, CustomProfileStore>();
        services.AddSingleton<IAppliedBaselineStore, AppliedBaselineStore>();
        services.AddSingleton<IProfileTransferService, ProfileTransferService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IResetMonitorService, ResetMonitorService>();
        services.AddSingleton<ITrayCommandHandler, TrayCommandHandler>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<AutoStartService>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<IAppWindowController>(sp => sp.GetRequiredService<MainWindow>());
        services.AddSingleton<MainViewModel>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfilesViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<DashboardPage>();
        services.AddTransient<ProfilesPage>();
        services.AddTransient<HistoryPage>();
        services.AddTransient<SettingsPage>();

        return services;
    }
}
