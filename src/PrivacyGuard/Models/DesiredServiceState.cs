namespace PrivacyGuard.Models;

/// <summary>
/// High-level desired state for a Windows service PrivacyGuard is allowed to manage.
/// </summary>
public enum DesiredServiceState
{
    /// <summary>Service is running and start type is Automatic.</summary>
    RunningAutomatic,

    /// <summary>Service is stopped and start type is Manual.</summary>
    StoppedManual,

    /// <summary>Service is stopped and start type is Disabled.</summary>
    StoppedDisabled
}
