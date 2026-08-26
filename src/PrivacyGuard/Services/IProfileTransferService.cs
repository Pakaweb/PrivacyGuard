namespace PrivacyGuard.Services;

/// <summary>
/// Export and import custom profiles plus optional history / restore points.
/// </summary>
public interface IProfileTransferService
{
    Task ExportAsync(ExportOptions options, string path, CancellationToken cancellationToken = default);

    Task<PrivacyGuardExport> ReadAsync(string path, CancellationToken cancellationToken = default);

    Task<string> ImportAsync(PrivacyGuardExport package, ImportSelection selection, CancellationToken cancellationToken = default);
}
