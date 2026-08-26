namespace PrivacyGuard.Models;

/// <summary>
/// Live dashboard snapshot of Windows privacy-related state.
/// </summary>
public sealed class PrivacySnapshot
{
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;

    public TelemetryLevel TelemetryLevel { get; init; }

    public bool TelemetryLevelIsPolicyEnforced { get; init; }

    public bool SecurityTelemetrySupported { get; init; }

    public ServiceStateInfo DiagTrack { get; init; } = new()
    {
        ServiceName = PrivacySettingKeys.DiagTrackService,
        Exists = false
    };

    public ServiceStateInfo DmwAppPush { get; init; } = new()
    {
        ServiceName = PrivacySettingKeys.DmwAppPushService,
        Exists = false
    };

    public bool AdvertisingIdEnabled { get; init; }

    public bool ActivityHistoryEnabled { get; init; }

    public bool ActivityUploadEnabled { get; init; }

    public bool CortanaEnabled { get; init; }

    public bool CopilotEnabled { get; init; }

    public bool FeedbackEnabled { get; init; }

    public bool TailoredExperiencesEnabled { get; init; }

    public bool IsElevated { get; init; }

    public string WindowsEdition { get; init; } = string.Empty;

    public PrivacyHealth OverallHealth { get; init; }

    public int PrivacyScore { get; init; }

    public IReadOnlyList<SettingSnapshot> AllSettings { get; init; } = [];
}
