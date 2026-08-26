namespace PrivacyGuard.Models;

/// <summary>
/// Last PrivacyGuard-applied desired state, used to detect Windows resetting settings.
/// </summary>
public sealed class AppliedBaseline
{
    public string? ProfileKind { get; set; }

    public string? CustomId { get; set; }

    public string? ProfileTitle { get; set; }

    public DateTimeOffset AppliedAt { get; set; }

    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? DismissedDriftFingerprint { get; set; }

    public string? LastLoggedDriftFingerprint { get; set; }
}
