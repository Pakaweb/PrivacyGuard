namespace PrivacyGuard.Services;

public interface IFilePickerService
{
    Task<string?> PickSaveAsync(string suggestedFileName, CancellationToken cancellationToken = default);

    Task<string?> PickOpenAsync(CancellationToken cancellationToken = default);
}
