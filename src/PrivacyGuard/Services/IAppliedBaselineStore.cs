namespace PrivacyGuard.Services;

/// <summary>
/// Last applied PrivacyGuard desired state, used for Windows-reset detection.
/// </summary>
public interface IAppliedBaselineStore
{
    Task<AppliedBaseline?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppliedBaseline baseline, CancellationToken cancellationToken = default);

    Task SetLastProfileAsync(
        string profileKind,
        string? customId,
        string? profileTitle,
        CancellationToken cancellationToken = default);

    Task MergeAsync(
        IEnumerable<PrivacyOperation> succeeded,
        string? profileKind,
        string? customId,
        string? profileTitle,
        CancellationToken cancellationToken = default);
}
