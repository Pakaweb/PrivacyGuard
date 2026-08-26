using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PrivacyGuard.Services;

/// <summary>
/// WinUI file pickers initialized with the main window HWND (required for unpackaged apps).
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickSaveAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".pgprofile",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeChoices.Add("PrivacyGuard profile", [".pgprofile"]);
        picker.FileTypeChoices.Add("JSON", [".json"]);
        Initialize(picker);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickOpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".pgprofile");
        picker.FileTypeFilter.Add(".json");
        Initialize(picker);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private static void Initialize(object picker)
    {
        var window = App.MainWindow ?? throw new InvalidOperationException("Main window is not ready.");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
    }
}
