using Microsoft.UI.Xaml;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
    }

    public void Apply(ElementTheme theme)
    {
        _settings.Current.Theme = theme;

        if (App.MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }
}
