namespace PrivacyGuard.Services;

/// <summary>
/// Central gate for every Windows privacy mutation. UI never writes the registry or services directly.
/// </summary>
public interface IPrivacyService
{
    Task<PrivacySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<PrivacyProfile> GetProfiles();

    IReadOnlyList<PrivacyOperation> BuildProfileOperations(PrivacyProfileKind kind, PrivacySnapshot current);

    IReadOnlyList<PrivacyOperation> BuildProfileOperations(PrivacyProfile profile, PrivacySnapshot current);

    IReadOnlyList<PrivacyOperation> BuildDesiredOperations(
        IReadOnlyDictionary<string, string> desired,
        PrivacySnapshot current);

    string? GetCanonicalValue(string settingKey, PrivacySnapshot snapshot);

    bool ValuesMatch(string settingKey, string? left, string? right);

    Task<PrivacyOperationResult> ApplyOperationsAsync(
        IReadOnlyList<PrivacyOperation> operations,
        string reason,
        CancellationToken cancellationToken = default);

    Task<PrivacyOperationResult> RevertChangeAsync(ChangeRecord record, CancellationToken cancellationToken = default);

    Task<PrivacyOperationResult> RestorePointAsync(RestorePoint restorePoint, CancellationToken cancellationToken = default);

    PrivacyOperation? BuildToggleOperation(string settingKey, bool enabled, PrivacySnapshot current);

    PrivacyOperation? BuildTelemetryOperation(TelemetryLevel level, PrivacySnapshot current);
}
