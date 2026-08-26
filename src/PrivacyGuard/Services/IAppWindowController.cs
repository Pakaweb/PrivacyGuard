using Microsoft.UI.Dispatching;

namespace PrivacyGuard.Services;

/// <summary>
/// Show, hide, and exit the main window without creating a DI cycle with the tray icon.
/// </summary>
public interface IAppWindowController
{
    DispatcherQueue DispatcherQueue { get; }

    nint Handle { get; }

    void Show();

    void Hide();

    void RequestExit();
}
