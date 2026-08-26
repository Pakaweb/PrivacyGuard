using System.ServiceProcess;

namespace PrivacyGuard.Models;

/// <summary>
/// Snapshot of a Windows service that PrivacyGuard is allowed to inspect.
/// </summary>
public sealed class ServiceStateInfo
{
    public required string ServiceName { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public ServiceControllerStatus Status { get; init; }

    public ServiceStartMode StartType { get; init; }

    public bool Exists { get; init; }

    /// <summary>
    /// Canonical string used in history and restore snapshots, e.g. <c>Stopped:Disabled</c>.
    /// </summary>
    public string CanonicalValue => Exists
        ? $"{Status}:{StartType}"
        : "Missing";
}
