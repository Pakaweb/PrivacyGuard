using Microsoft.UI.Xaml.Controls;
using PrivacyGuard.Views;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class NavigationService : INavigationService
{
    public Frame? Frame { get; set; }

    public void Navigate<TPage>() where TPage : Page
    {
        if (Frame is null)
        {
            return;
        }

        if (Frame.Content is TPage)
        {
            return;
        }

        Frame.Content = App.GetService<TPage>();
    }

    public void ReloadCurrent()
    {
        if (Frame is null)
        {
            return;
        }

        switch (Frame.Content)
        {
            case DashboardPage:
                Frame.Content = App.GetService<DashboardPage>();
                break;
            case ProfilesPage:
                Frame.Content = App.GetService<ProfilesPage>();
                break;
            case HistoryPage:
                Frame.Content = App.GetService<HistoryPage>();
                break;
            case SettingsPage:
                Frame.Content = App.GetService<SettingsPage>();
                break;
        }
    }
}
