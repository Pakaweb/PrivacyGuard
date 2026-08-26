namespace PrivacyGuard.Services;

public interface ISettingsService
{
    AppPreferences Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
