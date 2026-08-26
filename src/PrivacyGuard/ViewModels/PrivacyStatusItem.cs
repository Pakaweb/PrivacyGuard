using PrivacyGuard.Services;

namespace PrivacyGuard.ViewModels;

/// <summary>
/// One dashboard tile bound to a catalogued privacy setting.
/// </summary>
public partial class PrivacyStatusItem : ObservableObject
{
    public required string SettingKey { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? Warning { get; init; }

    public bool RequiresAdmin { get; init; }

    public bool IsTelemetry { get; init; }

    public bool IsServiceControl { get; init; }

    public bool UseToggle { get; init; }

    public bool IsHighImpact { get; init; }

    public bool NeedsElevationHint { get; init; }

    public bool CanInteract => !NeedsElevationHint;

    [ObservableProperty]
    private string _valueLabel = string.Empty;

    [ObservableProperty]
    private string? _canonicalValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HealthLabel))]
    [NotifyPropertyChangedFor(nameof(ShowStrongBadge))]
    [NotifyPropertyChangedFor(nameof(ShowMediumBadge))]
    [NotifyPropertyChangedFor(nameof(ShowProtectedMark))]
    private PrivacyHealth _health;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionLabel))]
    [NotifyPropertyChangedFor(nameof(ToggleOnContent))]
    [NotifyPropertyChangedFor(nameof(ToggleOffContent))]
    private bool _isOn;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _lastChangedLabel = string.Empty;

    [ObservableProperty]
    private bool _isRecentlyChanged;

    public IAsyncRelayCommand<PrivacyStatusItem?>? ChangeCommand { get; set; }

    public string Glyph => SettingKey switch
    {
        PrivacySettingKeys.TelemetryLevel => "\uE9D9",
        PrivacySettingKeys.DiagTrack => "\uE753",
        PrivacySettingKeys.DmwAppPush => "\uE715",
        PrivacySettingKeys.AdvertisingId => "\uE7B3",
        PrivacySettingKeys.ActivityHistory => "\uE81C",
        PrivacySettingKeys.Cortana => "\uE77B",
        PrivacySettingKeys.Copilot => "\uE945",
        PrivacySettingKeys.Feedback => "\uE76E",
        PrivacySettingKeys.TailoredExperiences => "\uE790",
        _ => "\uE713"
    };

    public string HealthLabel => Health switch
    {
        PrivacyHealth.Protected => LocalizationService.Current.Get("health.protected"),
        PrivacyHealth.Partial => LocalizationService.Current.Get("health.mixed"),
        _ => LocalizationService.Current.Get("privacy.collectingData")
    };

    public bool ShowStatusChip => false;

    public bool ShowStrongBadge => false;

    public bool ShowMediumBadge => false;

    public bool ShowProtectedMark => false;

    public string ActionLabel => IsOn
        ? LocalizationService.Current.Get("dashboard.disable")
        : LocalizationService.Current.Get("dashboard.enable");

    public string RequiresAdminTooltip { get; init; } = string.Empty;

    public bool ShowValueLabel => IsServiceControl;

    public string ToggleOnContent => string.Empty;

    public string ToggleOffContent => string.Empty;
}
