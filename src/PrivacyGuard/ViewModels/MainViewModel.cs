using Microsoft.UI.Xaml;
using PrivacyGuard.Services;

namespace PrivacyGuard.ViewModels;

/// <summary>
/// Shell view model: elevation badge and window title metadata.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IElevationService _elevation;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;

    public MainViewModel(IElevationService elevation, ILocalizationService localization, IDialogService dialogs)
    {
        _elevation = elevation;
        _loc = localization;
        Loc = localization;
        _dialogs = dialogs;
        IsElevated = elevation.IsElevated;
    }

    public ILocalizationService Loc { get; }

    public bool IsElevated { get; }

    public Visibility ElevatedVisibility =>
        IsElevated ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StandardUserVisibility =>
        IsElevated ? Visibility.Collapsed : Visibility.Visible;

    public string ElevationLabel => IsElevated
        ? Loc.Get("shell.administrator")
        : Loc.Get("shell.standardUser");

    [RelayCommand]
    private async Task RestartElevatedAsync()
    {
        if (IsElevated)
        {
            return;
        }

        switch (_elevation.TryRestartElevated())
        {
            case ElevationRestartResult.Started:
                App.Current.Exit();
                break;
            case ElevationRestartResult.Cancelled:
                await _dialogs.ShowMessageAsync(
                    _loc.Get("shell.elevationCancelled"),
                    _loc.Get("shell.elevationCancelledBody"));
                break;
            default:
                await _dialogs.ShowErrorAsync(
                    _loc.Get("shell.elevationFailed"),
                    _loc.Get("shell.elevationFailedBody"));
                break;
        }
    }
}
