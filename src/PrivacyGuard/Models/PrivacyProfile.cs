namespace PrivacyGuard.Models;

/// <summary>
/// A named one-click profile that maps to a set of reversible privacy operations.
/// </summary>
public sealed class PrivacyProfile
{
    public required PrivacyProfileKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Details { get; init; }

    /// <summary>
    /// User-facing warning shown in the confirmation dialog before apply.
    /// </summary>
    public required string Warning { get; init; }

    public PrivacyHealth ResultingHealth { get; init; }

    /// <summary>
    /// Short, scannable outcomes shown as chips/bullets on the Profiles page.
    /// </summary>
    public IReadOnlyList<string> Highlights { get; init; } = [];

    /// <summary>
    /// Typical privacy score after this profile is applied, used only for UI impact hints.
    /// </summary>
    public int TypicalScore { get; init; }

    /// <summary>Persisted id for <see cref="PrivacyProfileKind.Custom"/> profiles.</summary>
    public string? CustomId { get; init; }

    /// <summary>
    /// Canonical target values for custom profiles. Built-in profiles leave this empty
    /// and use <c>PrivacyService.BuildProfileOperations(Kind)</c> instead.
    /// </summary>
    public IReadOnlyDictionary<string, string> DesiredValues { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
