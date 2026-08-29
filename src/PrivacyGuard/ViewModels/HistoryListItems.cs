using Microsoft.UI.Xaml;
using PrivacyGuard.Helpers;
using PrivacyGuard.Services;

namespace PrivacyGuard.ViewModels;

public enum HistoryFilterKind
{
    All = 0,
    Active = 1,
    Reverted = 2
}

public sealed class HistoryDayGroup
{
    public required string Title { get; init; }

    public bool ShowTitle { get; init; }

    public required IReadOnlyList<HistoryChangeItem> Items { get; init; }
}

public partial class HistoryChangeItem : ObservableObject
{
    public HistoryChangeItem(ChangeRecord record)
    {
        Record = record;
        OldLabel = PrivacyCatalog.FormatValue(record.SettingKey, record.OldValue);
        NewLabel = PrivacyCatalog.FormatValue(record.SettingKey, record.NewValue);
        OldTone = ToneFor(record.SettingKey, record.OldValue);
        NewTone = ToneFor(record.SettingKey, record.NewValue);
        TimeLabel = record.Timestamp.ToLocalTime().ToString("t");
        DateLabel = record.Timestamp.ToLocalTime().ToString("MMM d");
        ProfileLabel = LocalizationService.Current.FormatProfileReason(record.ProfileName);
        RestorePointLabel = record.RestorePointId is { } id
            ? LocalizationService.Current.Get("history.restorePointN", id)
            : null;
        SettingKeyPrefix = LocalizationService.Current.Get("history.settingKey");
        SourcePrefix = LocalizationService.Current.Get("history.source");
        RevertedLabel = LocalizationService.Current.Get("history.reverted");
    }

    public ChangeRecord Record { get; }

    public string OldLabel { get; }

    public string NewLabel { get; }

    public string OldTone { get; }

    public string NewTone { get; }

    public string TimeLabel { get; }

    public string DateLabel { get; }

    public string ProfileLabel { get; }

    public string? RestorePointLabel { get; }

    public string SettingName => PrivacyCatalog.DisplayName(Record.SettingKey);

    public string SettingKeyPrefix { get; }

    public string SourcePrefix { get; }

    public string RevertedLabel { get; }

    public string SettingKey => Record.SettingKey;

    public bool IsReverted => Record.IsReverted;

    public bool Succeeded => Record.Succeeded;

    public bool CanRevert => !Record.IsReverted && Record.Succeeded;

    public string? ErrorLabel => Record.Error;

    public string Glyph => Record.SettingKey switch
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

    public string ChevronGlyph => IsExpanded ? "\uE70E" : "\uE70D";

    public string StatusKind =>
        Record.IsReverted ? "reverted" : Record.Succeeded ? NewTone : "error";

    public string CardState => $"{StatusKind}:{(IsSelected ? 1 : 0)}";

    public Thickness CardBorderThickness => IsSelected ? new Thickness(2) : new Thickness(1);

    public string MetaLabel => ShowDateInMeta
        ? $"{DateLabel} · {TimeLabel} · {ProfileLabel}"
        : $"{TimeLabel} · {ProfileLabel}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardState))]
    [NotifyPropertyChangedFor(nameof(CardBorderThickness))]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    private bool _isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaLabel))]
    private bool _showDateInMeta;

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    private static string ToneFor(string settingKey, string? newValue) =>
        PrivacyCatalog.HealthFor(settingKey, newValue) switch
        {
            PrivacyHealth.Protected => "protect",
            PrivacyHealth.Collecting => "collect",
            _ => "neutral"
        };
}

public partial class HistoryRestoreItem : ObservableObject
{
    public HistoryRestoreItem(RestorePoint point)
    {
        Point = point;
        SettingCountLabel = point.Settings.Count == 1
            ? LocalizationService.Current.Get("history.settingCapturedOne")
            : LocalizationService.Current.Get("history.settingCapturedMany", point.Settings.Count);
        PreviewSettings = point.Settings.Select(setting => PrivacyCatalog.DisplayName(setting.SettingKey)).Take(8).ToList();
    }

    public RestorePoint Point { get; }

    public string Description => LocalizationService.Current.FormatProfileReason(Point.Description);

    public string CreatedAtLabel => Point.CreatedAt.ToLocalTime().ToString("g");

    public string SettingCountLabel { get; }

    public IReadOnlyList<string> PreviewSettings { get; }

    public string ChevronGlyph => IsExpanded ? "\uE70E" : "\uE70D";

    public string CardState => IsSelected ? "selected" : "idle";

    public Thickness CardBorderThickness => IsSelected ? new Thickness(2) : new Thickness(1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardState))]
    [NotifyPropertyChangedFor(nameof(CardBorderThickness))]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    private bool _isExpanded;

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}
