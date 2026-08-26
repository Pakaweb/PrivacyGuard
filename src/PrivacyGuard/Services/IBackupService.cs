namespace PrivacyGuard.Services;

public interface IBackupService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<RestorePoint> CreateAsync(string description, IReadOnlyList<SettingSnapshot> settings, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestorePoint>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default);

    Task<RestorePoint?> GetAsync(long id, CancellationToken cancellationToken = default);
}
