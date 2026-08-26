using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace PrivacyGuard.Helpers;

/// <summary>
/// Light/dark fills for custom chrome that cannot use ThemeResource bindings.
/// </summary>
public static class ThemePalette
{
    public static bool IsLight()
    {
        if (App.MainWindow?.Content is FrameworkElement root)
        {
            return root.ActualTheme == ElementTheme.Light
                || (root.ActualTheme == ElementTheme.Default
                    && Application.Current.RequestedTheme == ApplicationTheme.Light);
        }

        return Application.Current.RequestedTheme == ApplicationTheme.Light;
    }

    public static Color Overlay(byte darkAlpha, byte lightAlpha) =>
        IsLight()
            ? ColorHelper.FromArgb(lightAlpha, 0, 0, 0)
            : ColorHelper.FromArgb(darkAlpha, 255, 255, 255);

    public static Color Accent(byte darkAlpha, byte lightAlpha) =>
        IsLight()
            ? ColorHelper.FromArgb(lightAlpha, 0, 95, 184)
            : ColorHelper.FromArgb(darkAlpha, 96, 205, 255);
}
