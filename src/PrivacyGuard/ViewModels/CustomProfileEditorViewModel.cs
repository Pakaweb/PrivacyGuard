using PrivacyGuard.Helpers;
using PrivacyGuard.Services;

namespace PrivacyGuard.ViewModels;

/// <summary>
/// Editor for a user-authored profile: name plus per-setting include/target state.
/// </summary>
public partial class CustomProfileEditorViewModel : ObservableObject
{
    public ILocalizationService Loc { get; }

    public CustomProfileEditorViewModel(
        CustomProfileDocument? existing,
        PrivacySnapshot snapshot,
        IPrivacyService privacy,
        ILocalizationService loc)
    {
        Loc = loc;
        ExistingId = existing?.Id;
        CreatedAt = existing?.CreatedAt ?? DateTimeOffset.Now;
        _name = existing?.Name ?? loc.Get("profiles.customNamePlaceholder");
        Rows = [];

        foreach (var key in PrivacySettingKeys.All)
        {
            var live = privacy.GetCanonicalValue(key, snapshot);
            string? stored = null;
            existing?.Settings.TryGetValue(key, out stored);
            var included = existing is null || stored is not null;
            var canonical = string.IsNullOrWhiteSpace(stored) ? live : stored;

            Rows.Add(new CustomProfileSettingRow
            {
                SettingKey = key,
                Title = PrivacyCatalog.DisplayName(key),
                Description = PrivacyCatalog.Description(key),
                IsTelemetry = key == PrivacySettingKeys.TelemetryLevel,
                IsService = PrivacyCatalog.IsServiceControl(key),
                IsIncluded = included,
                TelemetryLevel = ParseTelemetry(canonical),
                ServiceState = ParseService(canonical),
                IsOn = canonical is "1" or "true" or "True" or "On" or "RunningAutomatic",
                IncludeLabel = loc.Get("common.include")
            });
        }
    }

    public string? ExistingId { get; }

    public DateTimeOffset CreatedAt { get; }

    public ObservableCollection<CustomProfileSettingRow> Rows { get; }

    public IReadOnlyList<TelemetryLevel> TelemetryLevels { get; } =
    [
        TelemetryLevel.Security,
        TelemetryLevel.Basic,
        TelemetryLevel.Enhanced,
        TelemetryLevel.Full
    ];

    public IReadOnlyList<DesiredServiceState> ServiceStates { get; } =
    [
        DesiredServiceState.RunningAutomatic,
        DesiredServiceState.StoppedManual,
        DesiredServiceState.StoppedDisabled
    ];

    [ObservableProperty]
    private string _name = string.Empty;

    public CustomProfileDocument? TryBuild(ILocalizationService loc, out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = loc.Get("profiles.customEmptyName");
            return null;
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows.Where(item => item.IsIncluded))
        {
            settings[row.SettingKey] = row.CanonicalValue;
        }

        if (settings.Count == 0)
        {
            error = loc.Get("profiles.customNoSettings");
            return null;
        }

        error = null;
        return new CustomProfileDocument
        {
            Id = ExistingId ?? Guid.NewGuid().ToString("N"),
            Name = Name.Trim(),
            Summary = loc.Get("profiles.customSummary", settings.Count),
            Settings = settings,
            CreatedAt = CreatedAt,
            UpdatedAt = DateTimeOffset.Now
        };
    }

    private static TelemetryLevel ParseTelemetry(string? value) =>
        int.TryParse(value, out var raw)
            ? raw switch
            {
                <= 0 => TelemetryLevel.Security,
                1 => TelemetryLevel.Basic,
                2 => TelemetryLevel.Enhanced,
                _ => TelemetryLevel.Full
            }
            : TelemetryLevel.Full;

    private static DesiredServiceState ParseService(string? value)
    {
        if (Enum.TryParse<DesiredServiceState>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return PrivacyCatalog.ParseServiceState(value ?? string.Empty);
    }
}

public partial class CustomProfileSettingRow : ObservableObject
{
    public required string SettingKey { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public string IncludeLabel { get; init; } = "Include";

    public bool IsTelemetry { get; init; }

    public bool IsService { get; init; }

    public bool IsToggle => !IsTelemetry && !IsService;

    public IReadOnlyList<TelemetryLevel> TelemetryLevels { get; } =
    [
        TelemetryLevel.Security,
        TelemetryLevel.Basic,
        TelemetryLevel.Enhanced,
        TelemetryLevel.Full
    ];

    public IReadOnlyList<DesiredServiceState> ServiceStates { get; } =
    [
        DesiredServiceState.RunningAutomatic,
        DesiredServiceState.StoppedManual,
        DesiredServiceState.StoppedDisabled
    ];

    [ObservableProperty]
    private bool _isIncluded = true;

    [ObservableProperty]
    private TelemetryLevel _telemetryLevel = TelemetryLevel.Full;

    [ObservableProperty]
    private DesiredServiceState _serviceState = DesiredServiceState.RunningAutomatic;

    [ObservableProperty]
    private bool _isOn;

    public string CanonicalValue
    {
        get
        {
            if (IsTelemetry)
            {
                return ((int)TelemetryLevel).ToString();
            }

            if (IsService)
            {
                return ServiceState.ToString();
            }

            return IsOn ? "1" : "0";
        }
    }
}
