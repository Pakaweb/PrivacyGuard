using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <summary>
/// Tray context-menu actions. Confirmation dialogs still go through PrivacyService.
/// </summary>
public sealed class TrayCommandHandler : ITrayCommandHandler
{
    private readonly IAppWindowController _window;
    private readonly IPrivacyService _privacy;
    private readonly IDialogService _dialogs;
    private readonly IElevationService _elevation;
    private readonly IResetMonitorService _reset;
    private readonly IAppliedBaselineStore _baseline;
    private readonly ILocalizationService _loc;
    private readonly ILogger<TrayCommandHandler> _logger;

    public TrayCommandHandler(
        IAppWindowController window,
        IPrivacyService privacy,
        IDialogService dialogs,
        IElevationService elevation,
        IResetMonitorService reset,
        IAppliedBaselineStore baseline,
        ILocalizationService localization,
        ILogger<TrayCommandHandler> logger)
    {
        _window = window;
        _privacy = privacy;
        _dialogs = dialogs;
        _elevation = elevation;
        _reset = reset;
        _baseline = baseline;
        _loc = localization;
        _logger = logger;
    }

    public void ShowMainWindow() => _window.Show();

    public void Exit() => _window.RequestExit();

    public Task ApplyRecommendedAsync() => ApplyProfileAsync(PrivacyProfileKind.Recommended);

    public Task ApplyMaximumAsync() => ApplyProfileAsync(PrivacyProfileKind.Maximum);

    public Task ToggleMonitoringAsync() => _reset.SetMonitoringPausedAsync(!_reset.IsMonitoringPaused);

    private async Task ApplyProfileAsync(PrivacyProfileKind kind)
    {
        _window.Show();
        try
        {
            var snapshot = await _privacy.GetSnapshotAsync();
            var operations = _privacy.BuildProfileOperations(kind, snapshot);
            var profile = _privacy.GetProfiles().First(item => item.Kind == kind);
            var title = _loc.Get(kind == PrivacyProfileKind.Maximum
                ? "profile.maximum.title"
                : "profile.recommended.title");

            if (operations.Count == 0)
            {
                await _dialogs.ShowMessageAsync(_loc.Get("profiles.alreadyTitle"), _loc.Get("profiles.alreadyMsg", title));
                return;
            }

            if (operations.Any(op => op.RequiresAdmin) && !_elevation.IsElevated)
            {
                await _dialogs.ShowErrorAsync(_loc.Get("profiles.adminTitle"), _loc.Get("profiles.adminBody"));
                return;
            }

            var summary = string.Join(
                Environment.NewLine,
                operations.Select(op =>
                    $"• {op.DisplayName}: {PrivacyCatalog.FormatValue(op.SettingKey, op.CurrentValue)} → {PrivacyCatalog.FormatValue(op.SettingKey, op.NewValue)}"));

            var confirmed = await _dialogs.ConfirmAsync(
                _loc.Get("profiles.applyTitle", title),
                _loc.Get("profiles.confirmBody", summary),
                _loc.Get(kind == PrivacyProfileKind.Maximum ? "profile.maximum.warning" : "profile.recommended.warning"));
            if (!confirmed)
            {
                return;
            }

            var result = await _privacy.ApplyOperationsAsync(operations, profile.Title);
            if (result.Success)
            {
                await _baseline.SetLastProfileAsync(kind.ToString(), null, profile.Title);
                await _dialogs.ShowMessageAsync(_loc.Get("profiles.appliedTitle"), result.Message);
            }
            else
            {
                await _dialogs.ShowErrorAsync(_loc.Get("profiles.partialTitle"), string.Join(Environment.NewLine, result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tray apply {Kind} failed.", kind);
            await _dialogs.ShowErrorAsync(_loc.Get("profiles.failedTitle"), ex.Message);
        }
    }
}
