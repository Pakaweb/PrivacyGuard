namespace PrivacyGuard.Services;

/// <summary>
/// Detects when Windows has changed privacy settings away from the last PrivacyGuard apply.
/// </summary>
public interface IResetMonitorService
{
    bool HasWindowsReset { get; }

    string ResetSummary { get; }

    bool CanReapplyLast { get; }

    string? LastProfileTitle { get; }

    bool IsMonitoringPaused { get; }

    event EventHandler? StateChanged;

    void Start(Microsoft.UI.Dispatching.DispatcherQueue dispatcher);

    void ApplyPreferences();

    Task CheckAsync(CancellationToken cancellationToken = default);

    Task DismissAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrivacyOperation>> BuildReapplyOperationsAsync(CancellationToken cancellationToken = default);

    Task SetMonitoringPausedAsync(bool paused, CancellationToken cancellationToken = default);
}
