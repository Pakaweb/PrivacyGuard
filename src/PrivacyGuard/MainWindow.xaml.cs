using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PrivacyGuard.Services;
using PrivacyGuard.ViewModels;
using PrivacyGuard.Views;
using WinRT.Interop;

namespace PrivacyGuard;

public sealed partial class MainWindow : Window, IAppWindowController
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ISettingsService _settings;
    private AppWindow? _appWindow;
    private bool _exitRequested;

    public MainViewModel ViewModel { get; }

    public MainWindow(
        MainViewModel viewModel,
        INavigationService navigation,
        IDialogService dialogs,
        ILocalizationService localization,
        ISettingsService settings)
    {
        ViewModel = viewModel;
        _navigation = navigation;
        _dialogs = dialogs;
        _loc = localization;
        _settings = settings;

        InitializeComponent();
        ApplyChrome();

        TrySetBackdrop();
        ConfigureTitleBar();
        SetWindowSize(1280, 840);

        _navigation.Frame = ContentFrame;
        _dialogs.XamlRoot = Content.XamlRoot;

        RootNavigation.SelectedItem = DashboardNavItem;
        Activated += MainWindow_Activated;
        _loc.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => _loc.LanguageChanged -= OnLanguageChanged;
    }

    Microsoft.UI.Dispatching.DispatcherQueue IAppWindowController.DispatcherQueue => DispatcherQueue;

    public nint Handle => WindowNative.GetWindowHandle(this);

    void IAppWindowController.Show()
    {
        _appWindow?.Show();
        Activate();
    }

    public void Hide() => _appWindow?.Hide();

    public void RequestExit()
    {
        _exitRequested = true;
        Close();
    }

    public async Task TryShowFirstRunAsync()
    {
        if (_settings.Current.HasSeenFirstRun)
        {
            return;
        }

        for (var attempt = 0; attempt < 20 && Content.XamlRoot is null; attempt++)
        {
            await Task.Delay(50);
        }

        _dialogs.XamlRoot = Content.XamlRoot;
        var accepted = await _dialogs.ShowFirstRunAsync();
        if (!accepted)
        {
            RequestExit();
            return;
        }

        _settings.Current.HasSeenFirstRun = true;
        await _settings.SaveAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyChrome();
            _navigation.ReloadCurrent();
        });
    }

    private void ApplyChrome()
    {
        RootLayout.FlowDirection = _loc.FlowDirection;
        Title = _loc.Get("app.title");
        AppTitleText.Text = _loc.Get("app.title");
        OverviewHeader.Content = _loc.Get("nav.overview");
        DashboardNavItem.Content = _loc.Get("nav.dashboard");
        ProfilesNavItem.Content = _loc.Get("nav.profiles");
        RecordsHeader.Content = _loc.Get("nav.records");
        HistoryNavItem.Content = _loc.Get("nav.history");
        SettingsNavItem.Content = _loc.Get("nav.settings");
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _dialogs.XamlRoot = Content.XamlRoot;
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        switch (tag)
        {
            case "dashboard":
                _navigation.Navigate<DashboardPage>();
                break;
            case "profiles":
                _navigation.Navigate<ProfilesPage>();
                break;
            case "history":
                _navigation.Navigate<HistoryPage>();
                break;
            case "settings":
                _navigation.Navigate<SettingsPage>();
                break;
        }
    }

    private void TrySetBackdrop()
    {
        try
        {
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
            }
            else
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
        catch
        {
            // Backdrop is cosmetic; a solid theme background is fine.
        }
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow.TitleBar is { } titleBar)
            {
                titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            }

            _appWindow.Closing += OnAppWindowClosing;
        }
        catch
        {
            // Title bar customization is best-effort on older Windows 10 builds.
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitRequested || !_settings.Current.CloseToTray || !_settings.Current.EnableTray)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();
    }

    private void SetWindowSize(int width, int height)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            _appWindow ??= appWindow;
        }
        catch
        {
            // Ignore if the windowing API is unavailable.
        }
    }
}
