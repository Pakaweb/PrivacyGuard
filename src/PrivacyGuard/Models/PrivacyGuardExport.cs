namespace PrivacyGuard.Models;

/// <summary>
/// Portable backup of custom profiles and optional history / restore points.
/// </summary>
public sealed class PrivacyGuardExport
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;

    public string App { get; set; } = "PrivacyGuard";

    public List<CustomProfileDocument> CustomProfiles { get; set; } = [];

    public List<ChangeRecord>? History { get; set; }

    public List<RestorePoint>? RestorePoints { get; set; }
}

/// <summary>
/// User choices for what to write into an export file.
/// </summary>
public sealed class ExportOptions
{
    public bool IncludeHistoryAndRestorePoints { get; init; }
}

/// <summary>
/// User choices for what to load from an import file.
/// </summary>
public sealed class ImportSelection
{
    public bool CustomProfiles { get; init; }

    public bool History { get; init; }

    public bool RestorePoints { get; init; }

    public bool Any => CustomProfiles || History || RestorePoints;
}
