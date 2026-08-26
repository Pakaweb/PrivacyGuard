using Microsoft.Win32;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <summary>
/// Toggles HKCU Run for auto-start. User-level only — no admin required.
/// </summary>
public sealed class AutoStartService
{
    private readonly RegistryHelper _registry;
    private readonly ILogger<AutoStartService> _logger;

    public AutoStartService(RegistryHelper registry, ILogger<AutoStartService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public bool IsEnabled()
    {
        var value = _registry.ReadString(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.RunUser,
            PrivacyRegistryPaths.RunValueName);
        return !string.IsNullOrWhiteSpace(value);
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                return _registry.DeleteValue(
                    RegistryHive.CurrentUser,
                    PrivacyRegistryPaths.RunUser,
                    PrivacyRegistryPaths.RunValueName);
            }

            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(PrivacyRegistryPaths.RunUser, true);
            key?.SetValue(PrivacyRegistryPaths.RunValueName, $"\"{path}\"", RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update auto-start.");
            return false;
        }
    }
}
