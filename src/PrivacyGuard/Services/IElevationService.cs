namespace PrivacyGuard.Services;

public enum ElevationRestartResult
{
    Started,
    Cancelled,
    Failed
}

/// <summary>
/// Reports whether the process is elevated and can request a UAC relaunch.
/// </summary>
public interface IElevationService
{
    bool IsElevated { get; }

    /// <summary>
    /// Relaunches the current executable with the runas verb.
    /// </summary>
    ElevationRestartResult TryRestartElevated();
}
