namespace PrivacyGuard.Models;

/// <summary>
/// Privacy posture of a setting or the overall system from the user's point of view.
/// Maps to green / yellow / red in the UI.
/// </summary>
public enum PrivacyHealth
{
    /// <summary>Green. Data collection is limited or the control is in a privacy-protective state.</summary>
    Protected = 0,

    /// <summary>Yellow. Mixed, unknown, or partially configured.</summary>
    Partial = 1,

    /// <summary>Red. Default or data-collecting state.</summary>
    Collecting = 2
}
