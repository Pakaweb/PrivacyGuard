namespace PrivacyGuard.Models;

/// <summary>
/// One-click configuration profiles applied through <c>PrivacyService</c>.
/// </summary>
public enum PrivacyProfileKind
{
    Recommended,
    Maximum,
    Balanced,
    RestoreDefault,
    Custom
}
