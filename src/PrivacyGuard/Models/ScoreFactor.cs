namespace PrivacyGuard.Models;

/// <summary>
/// One contributing factor in the dashboard privacy score breakdown.
/// </summary>
public sealed class ScoreFactor
{
    public required string Name { get; init; }

    public required string Detail { get; init; }

    public required int Points { get; init; }

    public required int MaxPoints { get; init; }

    public required bool IsProtected { get; init; }

    public string PointsLabel => $"{Points}/{MaxPoints}";
}
