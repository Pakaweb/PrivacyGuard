using System.Diagnostics;
using Microsoft.UI.Xaml;
using PrivacyGuard.Helpers;
using PrivacyGuard.Services;

namespace PrivacyGuard.ViewModels;

    /// <summary>
    /// Application preferences (theme, auto-start, tray, history). Does not change Windows privacy policy.
    /// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly ILocalizationService _loc;
    private readonly AutoStartService _autoStart;
    private readonly IDialogService _dialogs;
    private readonly IProfileTransferService _transfer;
    private readonly IFilePickerService _files;
    private readonly ITrayIconService _tray;
    private readonly IResetMonitorService _reset;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _suppressPreferenceChanges = true;

    public SettingsViewModel(
        ISettingsService settings,
        IThemeService theme,
        ILocalizationService localization,
        AutoStartService autoStart,
        IDialogService dialogs,
        IProfileTransferService transfer,
        IFilePickerService files,
        ITrayIconService tray,
        IResetMonitorService reset,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _theme = theme;
        _loc = localization;
        Loc = localization;
        _autoStart = autoStart;
        _dialogs = dialogs;
        _transfer = transfer;
        _files = files;
        _tray = tray;
        _reset = reset;
        _logger = logger;

        Themes =
        [
            new ThemeOption
            {
                Theme = ElementTheme.Default,
                Title = _loc.Get("settings.themeDefault"),
                Subtitle = _loc.Get("settings.themeDefaultSub")
            },
            new ThemeOption
            {
                Theme = ElementTheme.Light,
                Title = _loc.Get("settings.themeLight"),
                Subtitle = _loc.Get("settings.themeLightSub")
            },
            new ThemeOption
            {
                Theme = ElementTheme.Dark,
                Title = _loc.Get("settings.themeDark"),
                Subtitle = _loc.Get("settings.themeDarkSub")
            }
        ];

        _selectedTheme = Themes.FirstOrDefault(option => option.Theme == settings.Current.Theme) ?? Themes[0];
        _selectedLanguage = _loc.Resolve(settings.Current.Language);
        _confirmBeforeApply = settings.Current.ConfirmBeforeApply;
        _recordHistory = settings.Current.RecordHistory;
        _startWithWindows = autoStart.IsEnabled();
        _enableTray = settings.Current.EnableTray;
        _closeToTray = settings.Current.CloseToTray;
        _checkForWindowsResets = settings.Current.CheckForWindowsResets;
    }

    public ILocalizationService Loc { get; }

    public void AllowPreferenceChanges() => _suppressPreferenceChanges = false;

    public IReadOnlyList<ThemeOption> Themes { get; }

    public IReadOnlyList<LanguageOption> Languages => _loc.Languages;

    public string LanguageLabel => SelectedLanguage.NativeName;

    public bool IsThemeDefault
    {
        get => SelectedTheme.Theme == ElementTheme.Default;
        set
        {
            if (value)
            {
                SelectedTheme = Themes[0];
            }
        }
    }

    public bool IsThemeLight
    {
        get => SelectedTheme.Theme == ElementTheme.Light;
        set
        {
            if (value)
            {
                SelectedTheme = Themes[1];
            }
        }
    }

    public bool IsThemeDark
    {
        get => SelectedTheme.Theme == ElementTheme.Dark;
        set
        {
            if (value)
            {
                SelectedTheme = Themes[2];
            }
        }
    }

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _confirmBeforeApply = true;

    [ObservableProperty]
    private bool _recordHistory = true;

    [ObservableProperty]
    private bool _enableTray = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _checkForWindowsResets = true;

    public string VersionLabel => _loc.Get("app.version");

    public string LogsPath { get; } = AppPaths.LogsDirectory;

    public string DatabasePath { get; } = AppPaths.DatabasePath;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await _dialogs.ShowMessageAsync(
            _loc.Get("settings.upToDate"),
            _loc.Get("settings.upToDateBody"));
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        OnPropertyChanged(nameof(IsThemeDefault));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));

        if (_suppressPreferenceChanges || value is null || _settings.Current.Theme == value.Theme)
        {
            return;
        }

        _theme.Apply(value.Theme);
        _ = PersistAsync();
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        OnPropertyChanged(nameof(LanguageLabel));

        if (_suppressPreferenceChanges || value is null)
        {
            return;
        }

        if (string.Equals(_settings.Current.Language, value.Code, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_loc.CurrentCode, value.Code, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = ApplyLanguageAsync(value.Code);
    }

    private async Task ApplyLanguageAsync(string code)
    {
        _settings.Current.Language = code;
        await PersistAsync();
        _loc.Apply(code);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_suppressPreferenceChanges)
        {
            return;
        }

        if (!_autoStart.SetEnabled(value))
        {
            _startWithWindows = !value;
            OnPropertyChanged(nameof(StartWithWindows));
            _ = _dialogs.ShowErrorAsync(_loc.Get("settings.autoStart"), _loc.Get("settings.autoStartError"));
            return;
        }

        _settings.Current.StartWithWindows = value;
        _ = PersistAsync();
    }

    partial void OnConfirmBeforeApplyChanged(bool value)
    {
        if (_suppressPreferenceChanges)
        {
            return;
        }

        _settings.Current.ConfirmBeforeApply = value;
        _ = PersistAsync();
    }

    partial void OnRecordHistoryChanged(bool value)
    {
        if (_suppressPreferenceChanges)
        {
            return;
        }

        _settings.Current.RecordHistory = value;
        _ = PersistAsync();
    }

    partial void OnEnableTrayChanged(bool value)
    {
        if (_suppressPreferenceChanges)
        {
            return;
        }

        _settings.Current.EnableTray = value;
        _tray.SetEnabled(value);
        _ = PersistAsync();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (_suppressPreferenceChanges)
        {
            return;
        }

        _settings.Current.CloseToTray = value;
        _ = PersistAsync();
    }

    partial void OnCheckForWindowsResetsChanged(bool value)
    {
        if (_suppressPreferenceChanges)
        {
            return;
        }

        _settings.Current.CheckForWindowsResets = value;
        _ = PersistAsync();
        _reset.ApplyPreferences();
        _ = _reset.CheckAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var options = await _dialogs.ShowExportOptionsAsync();
        if (options is null)
        {
            return;
        }

        var path = await _files.PickSaveAsync($"PrivacyGuard-{DateTime.Now:yyyyMMdd}");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await _transfer.ExportAsync(options, path);
            await _dialogs.ShowMessageAsync(_loc.Get("profiles.exportTitle"), _loc.Get("profiles.exportSuccess"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed.");
            await _dialogs.ShowErrorAsync(_loc.Get("profiles.exportFailed"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = await _files.PickOpenAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var package = await _transfer.ReadAsync(path);
            var selection = await _dialogs.ShowImportOptionsAsync(package);
            if (selection is null || !selection.Any)
            {
                if (selection is { Any: false })
                {
                    await _dialogs.ShowMessageAsync(_loc.Get("profiles.importTitle"), _loc.Get("profiles.importNothing"));
                }

                return;
            }

            var confirmed = await _dialogs.ConfirmAsync(
                _loc.Get("profiles.importConfirm"),
                _loc.Get("profiles.importBody"),
                _loc.Get("profiles.importWarning"));
            if (!confirmed)
            {
                return;
            }

            var message = await _transfer.ImportAsync(package, selection);
            await _dialogs.ShowMessageAsync(_loc.Get("profiles.importTitle"), message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed.");
            await _dialogs.ShowErrorAsync(_loc.Get("profiles.importFailed"), ex.Message);
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            AppPaths.EnsureCreated();
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.LogsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs folder.");
            _ = _dialogs.ShowErrorAsync(_loc.Get("settings.logs"), _loc.Get("settings.openFolderError"));
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            await _settings.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist app settings.");
        }
    }
}
