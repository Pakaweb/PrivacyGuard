namespace PrivacyGuard.Services;

public interface IChangeHistoryService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangeRecord>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(ChangeRecord record, CancellationToken cancellationToken = default);

    Task MarkRevertedAsync(long id, CancellationToken cancellationToken = default);

    Task DeleteAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
