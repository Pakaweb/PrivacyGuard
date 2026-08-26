namespace PrivacyGuard.Models;

/// <summary>
/// An audit record of a single setting change performed by PrivacyGuard.
/// </summary>
public sealed class ChangeRecord
{
    public long Id { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public required string SettingKey { get; init; }

    public required string SettingName { get; init; }

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public string? ProfileName { get; init; }

    public long? RestorePointId { get; init; }

    public bool IsReverted { get; init; }

    public string? Error { get; init; }

    public bool Succeeded => string.IsNullOrEmpty(Error);

    public string TimestampLabel => Timestamp.ToLocalTime().ToString("g");

    public string ValueChangeLabel => $"{OldValue ?? "(default)"} → {NewValue ?? "(default)"}";
}
