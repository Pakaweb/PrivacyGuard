using Microsoft.UI.Xaml.Controls;

namespace PrivacyGuard.Services;

public interface INavigationService
{
    Frame? Frame { get; set; }

    void Navigate<TPage>() where TPage : Page;

    void ReloadCurrent();
}
