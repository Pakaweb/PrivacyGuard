using Microsoft.Win32;

namespace PrivacyGuard.Helpers;

/// <summary>
/// Thin, reversible wrapper around the 64-bit Windows registry view.
/// All PrivacyGuard registry I/O must go through this helper.
/// </summary>
public sealed class RegistryHelper
{
    private readonly ILogger<RegistryHelper> _logger;

    public RegistryHelper(ILogger<RegistryHelper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reads a DWORD value, or null if the key or value does not exist.
    /// </summary>
    public int? ReadDword(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var key = OpenKey(hive, subKey, writable: false);
            if (key is null)
            {
                return null;
            }

            var raw = key.GetValue(valueName);
            return raw switch
            {
                int i => i,
                long l => (int)l,
                byte b => b,
                _ => raw is null ? null : Convert.ToInt32(raw)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read DWORD {Hive}\\{SubKey}\\{Value}", hive, subKey, valueName);
            return null;
        }
    }

    /// <summary>
    /// Reads a string value, or null if missing.
    /// </summary>
    public string? ReadString(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var key = OpenKey(hive, subKey, writable: false);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read string {Hive}\\{SubKey}\\{Value}", hive, subKey, valueName);
            return null;
        }
    }

    /// <summary>
    /// Creates the key if needed and writes a DWORD. Returns false on access denied or other errors.
    /// </summary>
    public bool WriteDword(RegistryHive hive, string subKey, string valueName, int value)
    {
        try
        {
            using var key = OpenOrCreateKey(hive, subKey);
            if (key is null)
            {
                return false;
            }

            key.SetValue(valueName, value, RegistryValueKind.DWord);
            _logger.LogInformation("Wrote DWORD {Hive}\\{SubKey}\\{Value} = {Data}", hive, subKey, valueName, value);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied writing {Hive}\\{SubKey}\\{Value}", hive, subKey, valueName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write DWORD {Hive}\\{SubKey}\\{Value}", hive, subKey, valueName);
            return false;
        }
    }

    /// <summary>
    /// Deletes a value if it exists. Missing values are treated as success (already at default).
    /// </summary>
    public bool DeleteValue(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var key = OpenKey(hive, subKey, writable: true);
            if (key is null)
            {
                return true;
            }

            if (key.GetValue(valueName) is null)
            {
                return true;
            }

            key.DeleteValue(valueName, throwOnMissingValue: false);
            _logger.LogInformation("Deleted {Hive}\\{SubKey}\\{Value}", hive, subKey, valueName);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting {Hive}\\{SubKey}\\{Value}", hive, subKey, valueName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Hive}\\{SubKey}\\{Value}", hive, subKey, valueName);
            return false;
        }
    }

    private static RegistryKey? OpenKey(RegistryHive hive, string subKey, bool writable)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return baseKey.OpenSubKey(subKey, writable);
    }

    private static RegistryKey? OpenOrCreateKey(RegistryHive hive, string subKey)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return baseKey.CreateSubKey(subKey, writable: true);
    }
}
