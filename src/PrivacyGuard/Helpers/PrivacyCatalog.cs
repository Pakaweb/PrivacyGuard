using System.ServiceProcess;

namespace PrivacyGuard.Helpers;

/// <summary>
/// Display names, warnings, and health mapping for catalogued privacy settings.
/// </summary>
public static class PrivacyCatalog
{
    public static IReadOnlyList<PrivacyProfile> Profiles { get; } =
    [
        new PrivacyProfile
        {
            Kind = PrivacyProfileKind.Recommended,
            Title = "Recommended Privacy",
            Summary = "Cuts advertising and optional diagnostics while keeping Windows Update and security healthy.",
            Details = "Telemetry: Basic. Advertising ID off. Activity upload off. Feedback prompts off. DiagTrack left running so Windows Update diagnostics still work.",
            Warning = "Some personalized tips, ads, and timeline sync will stop. This does not affect Windows Security or Update.",
            ResultingHealth = PrivacyHealth.Protected,
            TypicalScore = 76,
            Highlights =
            [
                "profile.recommended.h1",
                "profile.recommended.h2",
                "profile.recommended.h3",
                "profile.recommended.h4",
                "profile.recommended.h5"
            ]
        },
        new PrivacyProfile
        {
            Kind = PrivacyProfileKind.Maximum,
            Title = "Maximum Privacy",
            Summary = "Most restrictive supported configuration. Some optional Windows experiences may break.",
            Details = "Telemetry: Security (or Basic on Home/Pro). DiagTrack and dmwappushservice stopped. Advertising, activity history, Cortana/Copilot policy, and feedback off.",
            Warning = "Search highlights, timeline, optional diagnostic experiences, and some Store personalization may stop working. Reversible from History or Restore Default. On Home/Pro, Windows will not honor Security (0) telemetry.",
            ResultingHealth = PrivacyHealth.Protected,
            TypicalScore = 94,
            Highlights =
            [
                "profile.maximum.h1",
                "profile.maximum.h2",
                "profile.maximum.h3",
                "profile.maximum.h4",
                "profile.maximum.h5"
            ]
        },
        new PrivacyProfile
        {
            Kind = PrivacyProfileKind.Balanced,
            Title = "Balanced",
            Summary = "Keeps diagnostic services running, turns off advertising ID and activity upload.",
            Details = "Telemetry: Full. DiagTrack running. Advertising ID off. Local activity allowed, cloud upload off. Copilot left unchanged.",
            Warning = "Windows will still send optional diagnostic data. Only advertising and activity upload are reduced.",
            ResultingHealth = PrivacyHealth.Partial,
            TypicalScore = 50,
            Highlights =
            [
                "profile.balanced.h1",
                "profile.balanced.h2",
                "profile.balanced.h3",
                "profile.balanced.h4",
                "profile.balanced.h5"
            ]
        },
        new PrivacyProfile
        {
            Kind = PrivacyProfileKind.RestoreDefault,
            Title = "Restore Default",
            Summary = "Removes PrivacyGuard policy values and returns services to typical Windows defaults.",
            Details = "Deletes AllowTelemetry policy, re-enables DiagTrack (Automatic + Running), restores advertising ID, activity history, Cortana/Copilot, and feedback to defaults.",
            Warning = "This restores data-collection defaults. It does not roll back unrelated third-party tweaks.",
            ResultingHealth = PrivacyHealth.Collecting,
            TypicalScore = 8,
            Highlights =
            [
                "profile.restore.h1",
                "profile.restore.h2",
                "profile.restore.h3",
                "profile.restore.h4",
                "profile.restore.h5"
            ]
        }
    ];

    public static string DisplayName(string settingKey) => settingKey switch
    {
        PrivacySettingKeys.TelemetryLevel => L("setting.telemetry"),
        PrivacySettingKeys.DiagTrack => L("setting.diagTrack"),
        PrivacySettingKeys.DmwAppPush => L("setting.dmw"),
        PrivacySettingKeys.AdvertisingId => L("setting.advertising"),
        PrivacySettingKeys.ActivityHistory => L("setting.activity"),
        PrivacySettingKeys.Cortana => L("setting.cortana"),
        PrivacySettingKeys.Copilot => L("setting.copilot"),
        PrivacySettingKeys.Feedback => L("setting.feedback"),
        PrivacySettingKeys.TailoredExperiences => L("setting.tailored"),
        _ => settingKey
    };

    public static string Description(string settingKey) => settingKey switch
    {
        PrivacySettingKeys.TelemetryLevel => L("setting.telemetry.desc"),
        PrivacySettingKeys.DiagTrack => L("setting.diagTrack.desc"),
        PrivacySettingKeys.DmwAppPush => L("setting.dmw.desc"),
        PrivacySettingKeys.AdvertisingId => L("setting.advertising.desc"),
        PrivacySettingKeys.ActivityHistory => L("setting.activity.desc"),
        PrivacySettingKeys.Cortana => L("setting.cortana.desc"),
        PrivacySettingKeys.Copilot => L("setting.copilot.desc"),
        PrivacySettingKeys.Feedback => L("setting.feedback.desc"),
        PrivacySettingKeys.TailoredExperiences => L("setting.tailored.desc"),
        _ => string.Empty
    };

    public static string? SideEffect(string settingKey) => settingKey switch
    {
        PrivacySettingKeys.TelemetryLevel => L("setting.telemetry.side"),
        PrivacySettingKeys.DiagTrack => L("setting.diagTrack.side"),
        PrivacySettingKeys.DmwAppPush => L("setting.dmw.side"),
        PrivacySettingKeys.Cortana => L("setting.cortana.side"),
        PrivacySettingKeys.Copilot => L("setting.copilot.side"),
        PrivacySettingKeys.ActivityHistory => L("setting.activity.side"),
        _ => null
    };

    public static bool RequiresAdmin(string settingKey) => settingKey is
        PrivacySettingKeys.TelemetryLevel or
        PrivacySettingKeys.DiagTrack or
        PrivacySettingKeys.DmwAppPush or
        PrivacySettingKeys.ActivityHistory or
        PrivacySettingKeys.Cortana or
        PrivacySettingKeys.Copilot;

    public static PrivacyHealth HealthFor(string settingKey, string? canonicalValue)
    {
        return settingKey switch
        {
            PrivacySettingKeys.TelemetryLevel => canonicalValue switch
            {
                "0" => PrivacyHealth.Protected,
                "1" => PrivacyHealth.Protected,
                "2" => PrivacyHealth.Partial,
                _ => PrivacyHealth.Collecting
            },
            PrivacySettingKeys.DiagTrack or PrivacySettingKeys.DmwAppPush =>
                ServiceLooksHardened(canonicalValue) ? PrivacyHealth.Protected : PrivacyHealth.Collecting,
            PrivacySettingKeys.AdvertisingId or
            PrivacySettingKeys.ActivityHistory or
            PrivacySettingKeys.Cortana or
            PrivacySettingKeys.Copilot or
            PrivacySettingKeys.Feedback or
            PrivacySettingKeys.TailoredExperiences =>
                canonicalValue is "0" or "False" or "Off"
                    ? PrivacyHealth.Protected
                    : PrivacyHealth.Collecting,
            _ => PrivacyHealth.Partial
        };
    }

    /// <summary>
    /// Dashboard status color. DiagTrack left running is a Recommended trade-off, not a failure.
    /// Scoring still uses <see cref="HealthFor"/> / service-running checks.
    /// </summary>
    public static PrivacyHealth DisplayHealthFor(string settingKey, string? canonicalValue)
    {
        if (IsRecommendedServiceTradeOff(settingKey, canonicalValue))
        {
            return PrivacyHealth.Partial;
        }

        return HealthFor(settingKey, canonicalValue);
    }

    public static bool IsRecommendedServiceTradeOff(string settingKey, string? canonicalValue) =>
        settingKey == PrivacySettingKeys.DiagTrack && !ServiceLooksHardened(canonicalValue);

    public static string FormatValue(string settingKey, string? canonicalValue)
    {
        if (canonicalValue is null)
        {
            return L("common.windowsDefault");
        }

        return settingKey switch
        {
            PrivacySettingKeys.TelemetryLevel => canonicalValue switch
            {
                "0" => L("telemetry.value.security"),
                "1" => L("telemetry.value.basic"),
                "2" => L("telemetry.value.enhanced"),
                "3" => L("telemetry.value.full"),
                _ => canonicalValue
            },
            PrivacySettingKeys.DiagTrack or PrivacySettingKeys.DmwAppPush => FormatService(canonicalValue),
            _ => canonicalValue is "1" or "True" or "On" ? L("common.on") : L("common.off")
        };
    }

    private static bool ServiceLooksHardened(string? canonical)
    {
        if (string.IsNullOrEmpty(canonical) || canonical == "Missing")
        {
            return true;
        }

        return canonical.Contains("Stopped", StringComparison.OrdinalIgnoreCase)
               && (canonical.Contains("Disabled", StringComparison.OrdinalIgnoreCase)
                   || canonical.Contains("Manual", StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatService(string canonical)
    {
        if (canonical == "Missing")
        {
            return L("service.missing");
        }

        var parts = canonical.Split(':');
        if (parts.Length != 2)
        {
            return canonical;
        }

        return $"{TranslateServicePart(parts[0])} · {TranslateServicePart(parts[1])}";
    }

    private static string TranslateServicePart(string part) => part switch
    {
        "Running" => L("service.running"),
        "Stopped" => L("service.stopped"),
        "Automatic" => L("service.automatic"),
        "Manual" => L("service.manual"),
        "Disabled" => L("service.disabled"),
        _ => part
    };

    public static DesiredServiceState ParseServiceState(string canonical) => canonical switch
    {
        var s when s.Contains("Disabled", StringComparison.OrdinalIgnoreCase) => DesiredServiceState.StoppedDisabled,
        var s when s.Contains("Running", StringComparison.OrdinalIgnoreCase) => DesiredServiceState.RunningAutomatic,
        _ => DesiredServiceState.StoppedManual
    };

    public static bool IsServiceRunning(ServiceStateInfo info) =>
        info.Exists && info.Status == ServiceControllerStatus.Running;

    public static bool IsServiceControl(string settingKey) =>
        settingKey is PrivacySettingKeys.DiagTrack or PrivacySettingKeys.DmwAppPush;

    public static bool IsHighImpact(string settingKey) =>
        settingKey is PrivacySettingKeys.TelemetryLevel
            or PrivacySettingKeys.DiagTrack
            or PrivacySettingKeys.AdvertisingId
            or PrivacySettingKeys.ActivityHistory;

    public static string SectionId(string settingKey) => settingKey switch
    {
        PrivacySettingKeys.DiagTrack or PrivacySettingKeys.DmwAppPush or PrivacySettingKeys.TelemetryLevel
            => "core",
        PrivacySettingKeys.AdvertisingId or PrivacySettingKeys.TailoredExperiences or PrivacySettingKeys.ActivityHistory
            => "ads",
        PrivacySettingKeys.Cortana or PrivacySettingKeys.Copilot
            => "ai",
        _ => "other"
    };

    public static string SectionTitle(string settingKey) => L($"dashboard.section.{SectionId(settingKey)}");

    public static string SectionSubtitle(string sectionId) => sectionId switch
    {
        "core" => L("dashboard.section.coreSub"),
        "ads" => L("dashboard.section.adsSub"),
        "ai" => L("dashboard.section.aiSub"),
        _ => L("dashboard.section.otherSub")
    };

    /// <summary>
    /// Localization key for the Privacy Score card summary. DiagTrack copy follows the live service state.
    /// </summary>
    public static string DashboardScoreSummaryKey(PrivacySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "dashboard.scoreLoading";
        }

        return snapshot.OverallHealth switch
        {
            PrivacyHealth.Protected => IsServiceRunning(snapshot.DiagTrack)
                ? "dashboard.scoreProtected"
                : "dashboard.scoreProtectedStopped",
            PrivacyHealth.Partial => "dashboard.scorePartial",
            _ => "dashboard.scoreCollecting"
        };
    }

    private static string DiagTrackScoreDetail(ServiceStateInfo info)
    {
        var status = FormatValue(PrivacySettingKeys.DiagTrack, info.CanonicalValue);
        var note = IsServiceRunning(info)
            ? L("dashboard.scoreFactorDiagRunning")
            : L("dashboard.scoreFactorDiagStopped");
        return $"{status} — {note}";
    }

    /// <summary>
    /// Score weights used by the dashboard breakdown. Keep aligned with <c>PrivacyService</c>.
    /// </summary>
    public static IReadOnlyList<ScoreFactor> BuildScoreFactors(PrivacySnapshot snapshot)
    {
        var telemetryPoints = snapshot.TelemetryLevel switch
        {
            TelemetryLevel.Security => 20,
            TelemetryLevel.Basic => 16,
            TelemetryLevel.Enhanced => 8,
            _ => 0
        };

        return
        [
            Factor(L("factor.telemetry"), FormatValue(PrivacySettingKeys.TelemetryLevel, ((int)snapshot.TelemetryLevel).ToString()), telemetryPoints, 20, telemetryPoints >= 16),
            Factor(L("factor.diagTrack"), DiagTrackScoreDetail(snapshot.DiagTrack), IsServiceRunning(snapshot.DiagTrack) ? 0 : 12, 12, !IsServiceRunning(snapshot.DiagTrack)),
            Factor(L("factor.dmw"), FormatValue(PrivacySettingKeys.DmwAppPush, snapshot.DmwAppPush.CanonicalValue), IsServiceRunning(snapshot.DmwAppPush) ? 0 : 8, 8, !IsServiceRunning(snapshot.DmwAppPush)),
            Factor(L("factor.advertising"), snapshot.AdvertisingIdEnabled ? L("common.on") : L("common.off"), snapshot.AdvertisingIdEnabled ? 0 : 12, 12, !snapshot.AdvertisingIdEnabled),
            Factor(L("factor.activity"), snapshot.ActivityHistoryEnabled ? L("common.on") : L("common.off"), snapshot.ActivityHistoryEnabled ? 0 : 12, 12, !snapshot.ActivityHistoryEnabled),
            Factor(L("factor.cortana"), snapshot.CortanaEnabled ? L("common.on") : L("common.off"), snapshot.CortanaEnabled ? 0 : 8, 8, !snapshot.CortanaEnabled),
            Factor(L("factor.copilot"), snapshot.CopilotEnabled ? L("common.on") : L("common.off"), snapshot.CopilotEnabled ? 0 : 8, 8, !snapshot.CopilotEnabled),
            Factor(L("factor.feedback"), snapshot.FeedbackEnabled ? L("common.on") : L("common.off"), snapshot.FeedbackEnabled ? 0 : 10, 10, !snapshot.FeedbackEnabled),
            Factor(L("factor.tailored"), snapshot.TailoredExperiencesEnabled ? L("common.on") : L("common.off"), snapshot.TailoredExperiencesEnabled ? 0 : 10, 10, !snapshot.TailoredExperiencesEnabled)
        ];
    }

    /// <summary>
    /// Privacy score from canonical setting values (0–100). Missing keys score as unprotected.
    /// </summary>
    public static int ScoreFromValues(IReadOnlyDictionary<string, string> values)
    {
        var points = 0;
        values.TryGetValue(PrivacySettingKeys.TelemetryLevel, out var telemetry);
        points += telemetry switch
        {
            "0" => 20,
            "1" => 16,
            "2" => 8,
            _ => 0
        };

        points += ServiceLooksHardened(Get(values, PrivacySettingKeys.DiagTrack)) ? 12 : 0;
        points += ServiceLooksHardened(Get(values, PrivacySettingKeys.DmwAppPush)) ? 8 : 0;
        points += IsOff(Get(values, PrivacySettingKeys.AdvertisingId)) ? 12 : 0;
        points += IsOff(Get(values, PrivacySettingKeys.ActivityHistory)) ? 12 : 0;
        points += IsOff(Get(values, PrivacySettingKeys.Cortana)) ? 8 : 0;
        points += IsOff(Get(values, PrivacySettingKeys.Copilot)) ? 8 : 0;
        points += IsOff(Get(values, PrivacySettingKeys.Feedback)) ? 10 : 0;
        points += IsOff(Get(values, PrivacySettingKeys.TailoredExperiences)) ? 10 : 0;
        return Math.Clamp(points, 0, 100);
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static bool IsOff(string? value) =>
        value is "0" or "False" or "Off" or "false";

    /// <summary>
    /// True when a candidate value is a known canonical form for the setting.
    /// </summary>
    public static bool IsValidCanonical(string settingKey, string value)
    {
        if (!PrivacySettingKeys.All.Contains(settingKey) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return settingKey switch
        {
            PrivacySettingKeys.TelemetryLevel => value is "0" or "1" or "2" or "3",
            PrivacySettingKeys.DiagTrack or PrivacySettingKeys.DmwAppPush =>
                Enum.TryParse<DesiredServiceState>(value, ignoreCase: true, out _)
                || value.Contains(':', StringComparison.Ordinal),
            _ => value is "0" or "1" or "true" or "false" or "True" or "False" or "On" or "Off"
        };
    }

    private static ScoreFactor Factor(string name, string detail, int points, int max, bool isProtected) => new()
    {
        Name = name,
        Detail = detail,
        Points = points,
        MaxPoints = max,
        IsProtected = isProtected
    };

    private static string L(string key) =>
        Services.LocalizationService.Current?.Get(key) ?? LocalizationCatalog.Get(AppPreferences.DefaultLanguage, key);
}
