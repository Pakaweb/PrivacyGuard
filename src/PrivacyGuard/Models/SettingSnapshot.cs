namespace PrivacyGuard.Models;

/// <summary>
/// Point-in-time capture of a single privacy setting, used for backup and restore.
/// </summary>
public sealed class SettingSnapshot
{
    public required string SettingKey { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Canonical string value. Null means the value/key was absent (Windows default).
    /// </summary>
    public string? Value { get; init; }

    public bool RequiresAdmin { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
}
