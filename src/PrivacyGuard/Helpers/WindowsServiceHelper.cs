using System.ServiceProcess;
using Microsoft.Win32;
using PrivacyGuard.Services;

namespace PrivacyGuard.Helpers;

/// <summary>
/// Inspects and, when explicitly allowed, changes start type / status of a tiny allowlist
/// of non-critical privacy-related services. Critical OS services are never touched.
/// </summary>
public sealed class WindowsServiceHelper
{
    /// <summary>
    /// The only services PrivacyGuard is permitted to stop, start, or change start type for.
    /// </summary>
    public static readonly IReadOnlySet<string> ControllableServices =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PrivacySettingKeys.DiagTrackService,
            PrivacySettingKeys.DmwAppPushService
        };

    /// <summary>
    /// Defense-in-depth block list. Even if a future caller bypasses the allowlist,
    /// these names are refused.
    /// </summary>
    private static readonly HashSet<string> ProtectedServices =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Winlogon", "RpcSs", "DcomLaunch", "LSM", "EventLog", "PlugPlay",
            "BFE", "mpssvc", "CryptSvc", "Dnscache", "Dhcp", "nsi", "LanmanServer",
            "LanmanWorkstation", "ProfSvc", "SamSs", "Schedule", "Power", "Wcmsvc",
            "UserManager", "StateRepository", "Winmgmt", "EventSystem", "BrokerInfrastructure",
            "CoreMessagingRegistrar", "SystemEventsBroker", "TimeBrokerSvc", "gpsvc"
        };

    private readonly ILogger<WindowsServiceHelper> _logger;

    public WindowsServiceHelper(ILogger<WindowsServiceHelper> logger)
    {
        _logger = logger;
    }

    public ServiceStateInfo GetService(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Missing(serviceName);
        }

        try
        {
            using var controller = new ServiceController(serviceName);
            _ = controller.Status; // throws if the service does not exist

            return new ServiceStateInfo
            {
                ServiceName = serviceName,
                DisplayName = SafeDisplayName(controller),
                Status = controller.Status,
                StartType = ReadStartType(serviceName),
                Exists = true
            };
        }
        catch (InvalidOperationException)
        {
            return Missing(serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query service {Service}", serviceName);
            return Missing(serviceName);
        }
    }

    /// <summary>
    /// Applies a desired state to an allowlisted service. Returns an error message on failure.
    /// </summary>
    public string? ApplyDesiredState(string serviceName, DesiredServiceState desired)
    {
        if (!CanControl(serviceName))
        {
            var message = L("privacy.serviceNotAllowed", serviceName);
            _logger.LogError("{Message}", message);
            return message;
        }

        try
        {
            var startMode = desired switch
            {
                DesiredServiceState.RunningAutomatic => ServiceStartMode.Automatic,
                DesiredServiceState.StoppedManual => ServiceStartMode.Manual,
                DesiredServiceState.StoppedDisabled => ServiceStartMode.Disabled,
                _ => ServiceStartMode.Manual
            };

            if (!SetStartType(serviceName, startMode))
            {
                return L("privacy.serviceStartTypeAdmin", serviceName);
            }

            using var controller = new ServiceController(serviceName);
            if (desired == DesiredServiceState.RunningAutomatic)
            {
                if (controller.Status != ServiceControllerStatus.Running)
                {
                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                }
            }
            else
            {
                if (controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
            }

            _logger.LogInformation("Service {Service} set to {Desired}", serviceName, desired);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Service operation failed for {Service}", serviceName);
            return L("privacy.serviceRefused", serviceName, ex.Message);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Win32 service error for {Service}", serviceName);
            return L("privacy.serviceWin32", serviceName, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected service error for {Service}", serviceName);
            return L("privacy.serviceUnexpected", serviceName);
        }
    }

    public static bool CanControl(string serviceName) =>
        ControllableServices.Contains(serviceName) && !ProtectedServices.Contains(serviceName);

    private static string L(string key, params object[] args) =>
        LocalizationService.Current?.Get(key, args) ?? key;

    private static ServiceStateInfo Missing(string serviceName) => new()
    {
        ServiceName = serviceName,
        Exists = false,
        Status = ServiceControllerStatus.Stopped,
        StartType = ServiceStartMode.Disabled
    };

    private static string SafeDisplayName(ServiceController controller)
    {
        try
        {
            return controller.DisplayName;
        }
        catch
        {
            return controller.ServiceName;
        }
    }

    /// <summary>
    /// Reads start type from HKLM\SYSTEM\CurrentControlSet\Services\{name}\Start.
    /// 2 = Automatic, 3 = Manual, 4 = Disabled.
    /// </summary>
    private ServiceStartMode ReadStartType(string serviceName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: false);
            var start = key?.GetValue("Start") as int? ?? 3;
            return start switch
            {
                2 => ServiceStartMode.Automatic,
                3 => ServiceStartMode.Manual,
                4 => ServiceStartMode.Disabled,
                0 => ServiceStartMode.Boot,
                1 => ServiceStartMode.System,
                _ => ServiceStartMode.Manual
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read start type for {Service}", serviceName);
            return ServiceStartMode.Manual;
        }
    }

    private bool SetStartType(string serviceName, ServiceStartMode mode)
    {
        var dword = mode switch
        {
            ServiceStartMode.Automatic => 2,
            ServiceStartMode.Manual => 3,
            ServiceStartMode.Disabled => 4,
            _ => (int?)null
        };

        if (dword is null)
        {
            return false;
        }

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);
            if (key is null)
            {
                return false;
            }

            key.SetValue("Start", dword.Value, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set start type for {Service}", serviceName);
            return false;
        }
    }
}
