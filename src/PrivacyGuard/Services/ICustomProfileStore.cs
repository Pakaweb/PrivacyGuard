namespace PrivacyGuard.Services;

/// <summary>
/// Local JSON store for user-created privacy profiles.
/// </summary>
public interface ICustomProfileStore
{
    Task<IReadOnlyList<CustomProfileDocument>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CustomProfileDocument?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(CustomProfileDocument profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
