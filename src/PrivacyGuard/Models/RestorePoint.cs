namespace PrivacyGuard.Models;

/// <summary>
/// A named backup of settings taken automatically before a mutating operation.
/// </summary>
public sealed class RestorePoint
{
    public long Id { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<SettingSnapshot> Settings { get; init; } = [];

    public string CreatedAtLabel => CreatedAt.ToLocalTime().ToString("g");
}
