namespace PrivacyGuard.Services;

public interface ITrayCommandHandler
{
    void ShowMainWindow();

    void Exit();

    Task ApplyRecommendedAsync();

    Task ApplyMaximumAsync();

    Task ToggleMonitoringAsync();
}
