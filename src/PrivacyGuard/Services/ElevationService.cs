using System.Diagnostics;
using System.Security.Principal;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class ElevationService : IElevationService
{
    private readonly ILogger<ElevationService> _logger;

    public ElevationService(ILogger<ElevationService> logger)
    {
        _logger = logger;
        IsElevated = DetectElevation();
    }

    public bool IsElevated { get; }

    public ElevationRestartResult TryRestartElevated()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return ElevationRestartResult.Failed;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            Process.Start(startInfo);
            return ElevationRestartResult.Started;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _logger.LogInformation("User cancelled the elevation prompt.");
            return ElevationRestartResult.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relaunch elevated.");
            return ElevationRestartResult.Failed;
        }
    }

    private bool DetectElevation()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine elevation status.");
            return false;
        }
    }
}
