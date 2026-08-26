using Microsoft.Win32;

namespace PrivacyGuard.Models;

/// <summary>
/// Well-known Windows registry locations used by PrivacyGuard.
/// These are the documented policy / settings keys Microsoft uses for telemetry and privacy.
/// </summary>
public static class PrivacyRegistryPaths
{
    /// <summary>Machine policy for diagnostic data level.</summary>
    public const string DataCollectionPolicy = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";

    /// <summary>Non-policy machine setting that Settings UI also writes.</summary>
    public const string DataCollectionCurrent = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection";

    public const string AllowTelemetryValue = "AllowTelemetry";
    public const string DoNotShowFeedbackNotificationsValue = "DoNotShowFeedbackNotifications";

    public const string AdvertisingInfoUser = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo";
    public const string AdvertisingInfoEnabledValue = "Enabled";

    public const string AdvertisingInfoPolicy = @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo";
    public const string AdvertisingDisabledByPolicyValue = "DisabledByGroupPolicy";

    public const string SystemPolicy = @"SOFTWARE\Policies\Microsoft\Windows\System";
    public const string PublishUserActivitiesValue = "PublishUserActivities";
    public const string UploadUserActivitiesValue = "UploadUserActivities";

    public const string PrivacyUser = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy";
    public const string TailoredExperiencesValue = "TailoredExperiencesWithDiagnosticDataEnabled";

    public const string WindowsSearchPolicy = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search";
    public const string AllowCortanaValue = "AllowCortana";

    public const string CopilotPolicy = @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot";
    public const string TurnOffWindowsCopilotValue = "TurnOffWindowsCopilot";

    public const string SiufRulesUser = @"SOFTWARE\Microsoft\Siuf\Rules";
    public const string NumberOfSiufInPeriodValue = "NumberOfSIUFInPeriod";
    public const string PeriodInNanoSecondsValue = "PeriodInNanoSeconds";

    public const string WindowsNtCurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    public const string EditionIdValue = "EditionID";
    public const string ProductNameValue = "ProductName";
    public const string DisplayVersionValue = "DisplayVersion";

    public const string RunUser = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValueName = "PrivacyGuard";

    public static RegistryHive HiveFor(string settingKey) => settingKey switch
    {
        PrivacySettingKeys.AdvertisingId => RegistryHive.CurrentUser,
        PrivacySettingKeys.Feedback => RegistryHive.CurrentUser,
        PrivacySettingKeys.TailoredExperiences => RegistryHive.CurrentUser,
        PrivacySettingKeys.Copilot => RegistryHive.LocalMachine,
        _ => RegistryHive.LocalMachine
    };
}
