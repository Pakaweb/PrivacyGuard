using Microsoft.UI.Dispatching;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class ResetMonitorService : IResetMonitorService, IDisposable
{
    public const string HistoryProfileName = "WindowsReset";

    /// <summary>How often to re-read Windows privacy settings while the app is running.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    private readonly IPrivacyService _privacy;
    private readonly IAppliedBaselineStore _baseline;
    private readonly ICustomProfileStore _customProfiles;
    private readonly IChangeHistoryService _history;
    private readonly ISettingsService _settings;
    private readonly ILogger<ResetMonitorService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DispatcherQueue? _dispatcher;
    private DispatcherQueueTimer? _timer;
    private bool _disposed;

    public ResetMonitorService(
        IPrivacyService privacy,
        IAppliedBaselineStore baseline,
        ICustomProfileStore customProfiles,
        IChangeHistoryService history,
        ISettingsService settings,
        ILogger<ResetMonitorService> logger)
    {
        _privacy = privacy;
        _baseline = baseline;
        _customProfiles = customProfiles;
        _history = history;
        _settings = settings;
        _logger = logger;
    }

    public bool HasWindowsReset { get; private set; }

    public string ResetSummary { get; private set; } = string.Empty;

    public bool CanReapplyLast { get; private set; }

    public string? LastProfileTitle { get; private set; }

    public bool IsMonitoringPaused => _settings.Current.MonitoringPaused;

    public event EventHandler? StateChanged;

    public void Start(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _timer?.Stop();
        _timer = dispatcher.CreateTimer();
        _timer.Interval = CheckInterval;
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => _ = CheckAsync();
        ApplyTimerState();
        _ = CheckAsync();
    }

    public void ApplyPreferences() => ApplyTimerState();

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_settings.Current.CheckForWindowsResets || _settings.Current.MonitoringPaused)
            {
                ClearBanner(notify: HasWindowsReset);
                return;
            }

            var baseline = await _baseline.LoadAsync(cancellationToken);
            if (baseline is null || baseline.Values.Count == 0)
            {
                ClearBanner(notify: HasWindowsReset);
                return;
            }

            LastProfileTitle = baseline.ProfileTitle;
            CanReapplyLast = await CanReapplyAsync(baseline, cancellationToken);

            var snapshot = await _privacy.GetSnapshotAsync(cancellationToken);
            var drifted = new List<string>();
            foreach (var pair in baseline.Values)
            {
                var current = _privacy.GetCanonicalValue(pair.Key, snapshot);
                if (!_privacy.ValuesMatch(pair.Key, pair.Value, current))
                {
                    drifted.Add(pair.Key);
                }
            }

            if (drifted.Count == 0)
            {
                baseline.DismissedDriftFingerprint = null;
                await _baseline.SaveAsync(baseline, cancellationToken);
                ClearBanner(notify: HasWindowsReset);
                return;
            }

            var fingerprint = string.Join("|", drifted.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
            if (string.Equals(fingerprint, baseline.DismissedDriftFingerprint, StringComparison.Ordinal))
            {
                if (HasWindowsReset)
                {
                    HasWindowsReset = false;
                    ResetSummary = string.Empty;
                    Notify();
                }

                return;
            }

            HasWindowsReset = true;
            ResetSummary = LocalizationService.Current.Get(
                "dashboard.windowsResetBody",
                string.Join(", ", drifted.Select(PrivacyCatalog.DisplayName)));

            if (!string.Equals(fingerprint, baseline.LastLoggedDriftFingerprint, StringComparison.Ordinal))
            {
                await LogDriftAsync(snapshot, baseline, drifted, fingerprint, cancellationToken);
                baseline.LastLoggedDriftFingerprint = fingerprint;
                await _baseline.SaveAsync(baseline, cancellationToken);
            }

            Notify();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Windows reset check skipped.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DismissAsync(CancellationToken cancellationToken = default)
    {
        var baseline = await _baseline.LoadAsync(cancellationToken);
        if (baseline is not null && baseline.Values.Count > 0)
        {
            var snapshot = await _privacy.GetSnapshotAsync(cancellationToken);
            var drifted = baseline.Values
                .Where(pair => !_privacy.ValuesMatch(pair.Key, pair.Value, _privacy.GetCanonicalValue(pair.Key, snapshot)))
                .Select(pair => pair.Key)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
            baseline.DismissedDriftFingerprint = string.Join("|", drifted);
            await _baseline.SaveAsync(baseline, cancellationToken);
        }

        HasWindowsReset = false;
        ResetSummary = string.Empty;
        Notify();
    }

    public async Task<IReadOnlyList<PrivacyOperation>> BuildReapplyOperationsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _privacy.GetSnapshotAsync(cancellationToken);
        var baseline = await _baseline.LoadAsync(cancellationToken);
        if (baseline is null)
        {
            return [];
        }

        if (string.Equals(baseline.ProfileKind, nameof(PrivacyProfileKind.Custom), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(baseline.CustomId))
        {
            var custom = await _customProfiles.GetAsync(baseline.CustomId, cancellationToken);
            if (custom is not null)
            {
                return _privacy.BuildDesiredOperations(custom.Settings, snapshot);
            }
        }

        if (Enum.TryParse<PrivacyProfileKind>(baseline.ProfileKind, ignoreCase: true, out var kind)
            && kind != PrivacyProfileKind.Custom)
        {
            return _privacy.BuildProfileOperations(kind, snapshot);
        }

        return _privacy.BuildDesiredOperations(baseline.Values, snapshot);
    }

    public async Task SetMonitoringPausedAsync(bool paused, CancellationToken cancellationToken = default)
    {
        _settings.Current.MonitoringPaused = paused;
        await _settings.SaveAsync(cancellationToken);
        ApplyTimerState();
        if (!paused)
        {
            await CheckAsync(cancellationToken);
        }

        Notify();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Stop();
        _timer = null;
    }

    private void ApplyTimerState()
    {
        if (_timer is null)
        {
            return;
        }

        var enabled = _settings.Current.CheckForWindowsResets && !_settings.Current.MonitoringPaused;
        if (enabled)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private async Task<bool> CanReapplyAsync(AppliedBaseline baseline, CancellationToken cancellationToken)
    {
        if (string.Equals(baseline.ProfileKind, nameof(PrivacyProfileKind.Custom), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(baseline.CustomId))
        {
            return await _customProfiles.GetAsync(baseline.CustomId, cancellationToken) is not null;
        }

        if (Enum.TryParse<PrivacyProfileKind>(baseline.ProfileKind, ignoreCase: true, out var kind)
            && kind != PrivacyProfileKind.Custom)
        {
            return true;
        }

        return baseline.Values.Count > 0;
    }

    private async Task LogDriftAsync(
        PrivacySnapshot snapshot,
        AppliedBaseline baseline,
        List<string> drifted,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var key in drifted)
            {
                await _history.InsertAsync(new ChangeRecord
                {
                    Timestamp = DateTimeOffset.Now,
                    SettingKey = key,
                    SettingName = PrivacyCatalog.DisplayName(key),
                    OldValue = baseline.Values.GetValueOrDefault(key),
                    NewValue = _privacy.GetCanonicalValue(key, snapshot),
                    ProfileName = HistoryProfileName,
                    Error = null
                }, cancellationToken);
            }

            _logger.LogInformation("Windows reset detected for {Count} setting(s): {Fingerprint}", drifted.Count, fingerprint);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not write Windows reset history rows.");
        }
    }

    private void ClearBanner(bool notify)
    {
        HasWindowsReset = false;
        ResetSummary = string.Empty;
        if (notify)
        {
            Notify();
        }
    }

    private void Notify()
    {
        void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            Raise();
            return;
        }

        _dispatcher.TryEnqueue(Raise);
    }
}
