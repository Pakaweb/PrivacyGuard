namespace PrivacyGuard.Models;

/// <summary>
/// Canonical keys for every setting PrivacyGuard is allowed to read or change.
/// Keep this list as the single source of identity used by history, backup, and UI.
/// </summary>
public static class PrivacySettingKeys
{
    public const string TelemetryLevel = "TelemetryLevel";
    public const string DiagTrack = "DiagTrack";
    public const string DmwAppPush = "DmwAppPush";
    public const string AdvertisingId = "AdvertisingId";
    public const string ActivityHistory = "ActivityHistory";
    public const string Cortana = "Cortana";
    public const string Copilot = "Copilot";
    public const string Feedback = "Feedback";
    public const string TailoredExperiences = "TailoredExperiences";

    public const string DiagTrackService = "DiagTrack";
    public const string DmwAppPushService = "dmwappushservice";

    public static IReadOnlyList<string> All { get; } =
    [
        TelemetryLevel,
        DiagTrack,
        DmwAppPush,
        AdvertisingId,
        ActivityHistory,
        Cortana,
        Copilot,
        Feedback,
        TailoredExperiences
    ];
}
