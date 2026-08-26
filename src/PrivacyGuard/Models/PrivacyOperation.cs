namespace PrivacyGuard.Models;

/// <summary>
/// A pending or applied mutation of a single catalogued privacy setting.
/// </summary>
public sealed class PrivacyOperation
{
    public required string SettingKey { get; init; }

    public required string DisplayName { get; init; }

    public string? CurrentValue { get; init; }

    public required string NewValue { get; init; }

    public bool RequiresAdmin { get; init; }

    public string? SideEffectWarning { get; init; }
}
