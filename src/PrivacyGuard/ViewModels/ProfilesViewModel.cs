using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PrivacyGuard.Helpers;
using PrivacyGuard.Services;
using PrivacyGuard.Views;

namespace PrivacyGuard.ViewModels;

/// <summary>
/// One-click privacy profiles. Apply always confirms and backups first.
/// </summary>
public partial class ProfilesViewModel : ObservableObject
{
    private readonly IPrivacyService _privacy;
    private readonly ICustomProfileStore _customProfiles;
    private readonly IAppliedBaselineStore _baseline;
    private readonly IProfileTransferService _transfer;
    private readonly IFilePickerService _files;
    private readonly IDialogService _dialogs;
    private readonly IElevationService _elevation;
    private readonly IChangeHistoryService _history;
    private readonly ILocalizationService _loc;
    private readonly ILogger<ProfilesViewModel> _logger;

    public ProfilesViewModel(
        IPrivacyService privacy,
        ICustomProfileStore customProfiles,
        IAppliedBaselineStore baseline,
        IProfileTransferService transfer,
        IFilePickerService files,
        IDialogService dialogs,
        IElevationService elevation,
        IChangeHistoryService history,
        ILocalizationService localization,
        ILogger<ProfilesViewModel> logger)
    {
        _privacy = privacy;
        _customProfiles = customProfiles;
        _baseline = baseline;
        _transfer = transfer;
        _files = files;
        _dialogs = dialogs;
        _elevation = elevation;
        _history = history;
        _loc = localization;
        Loc = localization;
        _logger = logger;
        Profiles = [];
        _statusMessage = _loc.Get("profiles.choose");
    }

    public ILocalizationService Loc { get; }

    public ObservableCollection<ProfileOption> Profiles { get; }

    public ObservableCollection<ProfileOption> BuiltInProfiles { get; } = [];

    public ObservableCollection<ProfileOption> CustomProfiles { get; } = [];

    public bool HasCustomProfiles => CustomProfiles.Count > 0;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        foreach (var option in Profiles)
        {
            option.CommandsBusy = value;
        }
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [RelayCommand]
    public async Task RefreshPresentationAsync()
    {
        try
        {
            await RebuildProfileListAsync();
            var snapshot = await _privacy.GetSnapshotAsync();
            IReadOnlyList<ChangeRecord> history = [];
            try
            {
                history = await _history.GetRecentAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "History unavailable for profile last-applied labels.");
            }

            foreach (var option in Profiles)
            {
                var pending = _privacy.BuildProfileOperations(option.Profile, snapshot);
                option.IsActive = pending.Count == 0 && option.Profile.DesiredValues.Count + (option.IsCustom ? 0 : 1) > 0;
                if (option.IsCustom && option.Profile.DesiredValues.Count == 0)
                {
                    option.IsActive = false;
                }

                var map = PrivacySettingKeys.All.ToDictionary(
                    key => key,
                    key => _privacy.GetCanonicalValue(key, snapshot) ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
                if (option.IsCustom)
                {
                    foreach (var pair in option.Profile.DesiredValues)
                    {
                        map[pair.Key] = pair.Value;
                    }
                }

                var projected = option.IsCustom
                    ? PrivacyCatalog.ScoreFromValues(map)
                    : option.Profile.TypicalScore;
                var delta = projected - snapshot.PrivacyScore;
                option.ScoreImpactLabel = delta == 0
                    ? _loc.Get("profiles.scoreNone")
                    : delta > 0
                        ? _loc.Get("profiles.scoreUp", delta)
                        : _loc.Get("profiles.scoreDown", delta);

                DateTimeOffset? lastApplied = null;
                if (option.IsActive)
                {
                    lastApplied = history
                        .Where(record =>
                            record.Succeeded
                            && (string.Equals(record.ProfileName, option.Profile.Title, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(record.ProfileName, option.Profile.Kind.ToString(), StringComparison.OrdinalIgnoreCase)
                                || string.Equals(record.ProfileName, option.Title, StringComparison.OrdinalIgnoreCase)))
                        .Select(record => (DateTimeOffset?)record.Timestamp)
                        .FirstOrDefault();
                }

                option.LastAppliedLabel = lastApplied is { } timestamp
                    ? FormatLastApplied(timestamp)
                    : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not refresh profile presentation state.");
        }
    }

    [RelayCommand]
    private async Task ApplyProfileAsync(PrivacyProfile? profile)
    {
        if (profile is null || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var snapshot = await _privacy.GetSnapshotAsync();
            var operations = _privacy.BuildProfileOperations(profile, snapshot);

            if (operations.Count == 0)
            {
                StatusMessage = _loc.Get("profiles.alreadyMsg", LocalizedTitle(profile));
                await _dialogs.ShowMessageAsync(_loc.Get("profiles.alreadyTitle"), StatusMessage);
                await RefreshPresentationAsync();
                return;
            }

            var summary = string.Join(
                Environment.NewLine,
                operations.Select(o =>
                    $"• {o.DisplayName}: {PrivacyCatalog.FormatValue(o.SettingKey, o.CurrentValue)} → {PrivacyCatalog.FormatValue(o.SettingKey, o.NewValue)}"));

            if (operations.Any(o => o.RequiresAdmin) && !_elevation.IsElevated)
            {
                await _dialogs.ShowErrorAsync(
                    _loc.Get("profiles.adminTitle"),
                    _loc.Get("profiles.adminBody"));
                return;
            }

            var confirmed = await _dialogs.ConfirmAsync(
                _loc.Get("profiles.applyTitle", LocalizedTitle(profile)),
                _loc.Get("profiles.confirmBody", summary),
                LocalizedWarning(profile));
            if (!confirmed)
            {
                return;
            }

            var result = await _privacy.ApplyOperationsAsync(operations, profile.Title);
            if (result.Success)
            {
                await _baseline.SetLastProfileAsync(
                    profile.Kind.ToString(),
                    profile.CustomId,
                    profile.Title);
            }

            StatusMessage = result.Message;
            await RefreshPresentationAsync();
            if (result.Success)
            {
                await _dialogs.ShowMessageAsync(_loc.Get("profiles.appliedTitle"), result.Message);
            }
            else
            {
                await _dialogs.ShowErrorAsync(_loc.Get("profiles.partialTitle"), string.Join(Environment.NewLine, result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply profile {Profile}", profile.Kind);
            await _dialogs.ShowErrorAsync(_loc.Get("profiles.failedTitle"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateCustomProfileAsync() => await EditCustomAsync(null);

    [RelayCommand]
    private async Task EditCustomProfileAsync(PrivacyProfile? profile)
    {
        if (profile?.CustomId is null)
        {
            return;
        }

        var document = await _customProfiles.GetAsync(profile.CustomId);
        if (document is null)
        {
            return;
        }

        await EditCustomAsync(document);
    }

    [RelayCommand]
    private async Task DeleteCustomProfileAsync(PrivacyProfile? profile)
    {
        if (profile?.CustomId is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _loc.Get("profiles.deleteCustom"),
            _loc.Get("profiles.deleteConfirmBody", profile.Title),
            null,
            _loc.Get("common.delete"));
        if (!confirmed)
        {
            return;
        }

        await _customProfiles.DeleteAsync(profile.CustomId);
        StatusMessage = _loc.Get("profiles.customDeleted");
        await RefreshPresentationAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var options = await _dialogs.ShowExportOptionsAsync();
        if (options is null)
        {
            return;
        }

        var path = await _files.PickSaveAsync($"PrivacyGuard-{DateTime.Now:yyyyMMdd}");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await _transfer.ExportAsync(options, path);
            await _dialogs.ShowMessageAsync(_loc.Get("profiles.exportTitle"), _loc.Get("profiles.exportSuccess"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed.");
            await _dialogs.ShowErrorAsync(_loc.Get("profiles.exportFailed"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = await _files.PickOpenAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var package = await _transfer.ReadAsync(path);
            var selection = await _dialogs.ShowImportOptionsAsync(package);
            if (selection is null || !selection.Any)
            {
                if (selection is { Any: false })
                {
                    await _dialogs.ShowMessageAsync(_loc.Get("profiles.importTitle"), _loc.Get("profiles.importNothing"));
                }

                return;
            }

            var confirmed = await _dialogs.ConfirmAsync(
                _loc.Get("profiles.importConfirm"),
                _loc.Get("profiles.importBody"),
                _loc.Get("profiles.importWarning"));
            if (!confirmed)
            {
                return;
            }

            var message = await _transfer.ImportAsync(package, selection);
            StatusMessage = message;
            await RefreshPresentationAsync();
            await _dialogs.ShowMessageAsync(_loc.Get("profiles.importTitle"), message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed.");
            await _dialogs.ShowErrorAsync(_loc.Get("profiles.importFailed"), ex.Message);
        }
    }

    private async Task EditCustomAsync(CustomProfileDocument? existing)
    {
        var snapshot = await _privacy.GetSnapshotAsync();
        var editorVm = new CustomProfileEditorViewModel(existing, snapshot, _privacy, _loc);
        var editor = new CustomProfileEditorView(editorVm);
        var dialog = new ContentDialog
        {
            Title = _loc.Get(existing is null ? "profiles.createCustom" : "profiles.editCustom"),
            Content = editor,
            PrimaryButtonText = _loc.Get("common.save"),
            CloseButtonText = _loc.Get("common.cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _dialogs.XamlRoot
                ?? (App.MainWindow?.Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        editor.CommitPendingEdits();
        var document = editorVm.TryBuild(_loc, out var error);
        if (document is null)
        {
            await _dialogs.ShowErrorAsync(_loc.Get("profiles.editCustom"), error ?? _loc.Get("profiles.customNoSettings"));
            return;
        }

        await _customProfiles.SaveAsync(document);
        StatusMessage = existing is null ? _loc.Get("profiles.customCreated") : _loc.Get("profiles.customSaved");
        await RefreshPresentationAsync();
    }

    private async Task RebuildProfileListAsync()
    {
        var busy = IsBusy;
        Profiles.Clear();
        BuiltInProfiles.Clear();
        CustomProfiles.Clear();
        foreach (var profile in _privacy.GetProfiles())
        {
            var option = CreateOption(profile, busy);
            BuiltInProfiles.Add(option);
            Profiles.Add(option);
        }

        foreach (var document in await _customProfiles.GetAllAsync())
        {
            var option = CreateOption(ToPrivacyProfile(document), busy);
            option.LastModifiedLabel = FormatLastModified(document.UpdatedAt);
            CustomProfiles.Add(option);
            Profiles.Add(option);
        }

        OnPropertyChanged(nameof(HasCustomProfiles));
    }

    private ProfileOption CreateOption(PrivacyProfile profile, bool busy) => new()
    {
        Profile = profile,
        ApplyCommand = ApplyProfileCommand,
        EditCommand = EditCustomProfileCommand,
        DeleteCommand = DeleteCustomProfileCommand,
        CommandsBusy = busy
    };

    private PrivacyProfile ToPrivacyProfile(CustomProfileDocument document)
    {
        var highlights = document.Settings.Keys
            .Take(5)
            .Select(PrivacyCatalog.DisplayName)
            .ToList();

        return new PrivacyProfile
        {
            Kind = PrivacyProfileKind.Custom,
            CustomId = document.Id,
            Title = document.Name,
            Summary = string.IsNullOrWhiteSpace(document.Summary)
                ? _loc.Get("profiles.customSummary", document.Settings.Count)
                : document.Summary,
            Details = string.Join(", ", document.Settings.Keys.Select(PrivacyCatalog.DisplayName)),
            Warning = _loc.Get("profiles.customWarning"),
            Highlights = highlights,
            TypicalScore = PrivacyCatalog.ScoreFromValues(document.Settings),
            ResultingHealth = PrivacyCatalog.ScoreFromValues(document.Settings) >= 70
                ? PrivacyHealth.Protected
                : PrivacyHealth.Partial,
            DesiredValues = document.Settings
        };
    }

    private string FormatLastApplied(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var today = DateTime.Today;
        var time = local.ToString("t");
        var day = local.Date == today
            ? _loc.Get("profiles.today")
            : local.Date == today.AddDays(-1)
                ? _loc.Get("profiles.yesterday")
                : local.ToString("MMM d");
        return _loc.Get("profiles.lastApplied", day, time);
    }

    private string FormatLastModified(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var today = DateTime.Today;
        var time = local.ToString("t");
        var day = local.Date == today
            ? _loc.Get("profiles.today")
            : local.Date == today.AddDays(-1)
                ? _loc.Get("profiles.yesterday")
                : local.ToString("MMM d");
        return _loc.Get("profiles.lastModified", day, time);
    }

    private string LocalizedTitle(PrivacyProfile profile) =>
        profile.Kind == PrivacyProfileKind.Custom
            ? profile.Title
            : _loc.Get(ProfileKey(profile.Kind, "title"));

    private string LocalizedWarning(PrivacyProfile profile) =>
        profile.Kind == PrivacyProfileKind.Custom
            ? profile.Warning
            : _loc.Get(ProfileKey(profile.Kind, "warning"));

    internal static string ProfileKey(PrivacyProfileKind kind, string part) => kind switch
    {
        PrivacyProfileKind.Recommended => $"profile.recommended.{part}",
        PrivacyProfileKind.Maximum => $"profile.maximum.{part}",
        PrivacyProfileKind.Balanced => $"profile.balanced.{part}",
        PrivacyProfileKind.Custom => $"profile.custom.{part}",
        _ => $"profile.restore.{part}"
    };
}

public partial class ProfileOption : ObservableObject
{
    public required PrivacyProfile Profile { get; init; }

    public required IAsyncRelayCommand<PrivacyProfile?> ApplyCommand { get; init; }

    public required IAsyncRelayCommand<PrivacyProfile?> EditCommand { get; init; }

    public required IAsyncRelayCommand<PrivacyProfile?> DeleteCommand { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyButtonLabel))]
    [NotifyPropertyChangedFor(nameof(CardState))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(ShowAccentApply))]
    [NotifyPropertyChangedFor(nameof(ShowStandardApply))]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(ShowAccentApply))]
    [NotifyPropertyChangedFor(nameof(ShowStandardApply))]
    private bool _commandsBusy;

    [ObservableProperty]
    private string _scoreImpactLabel = string.Empty;

    [ObservableProperty]
    private string? _lastAppliedLabel;

    [ObservableProperty]
    private string? _lastModifiedLabel;

    public string Title => Profile.Kind == PrivacyProfileKind.Custom
        ? Profile.Title
        : LocalizationService.Current.Get(ProfilesViewModel.ProfileKey(Profile.Kind, "title"));

    public string Summary => Profile.Kind == PrivacyProfileKind.Custom
        ? Profile.Summary
        : LocalizationService.Current.Get(ProfilesViewModel.ProfileKey(Profile.Kind, "summary"));

    public string Warning => Profile.Kind == PrivacyProfileKind.Custom
        ? Profile.Warning
        : LocalizationService.Current.Get(ProfilesViewModel.ProfileKey(Profile.Kind, "warning"));

    public IReadOnlyList<string> Highlights => Profile.Kind == PrivacyProfileKind.Custom
        ? Profile.Highlights
        : Profile.Highlights.Select(LocalizationService.Current.Get).ToList();

    public string RecommendedBadgeLabel => LocalizationService.Current.Get("profiles.recommendedBadge");

    public string CustomBadgeLabel => LocalizationService.Current.Get("profiles.customBadge");

    public string ActiveLabel => LocalizationService.Current.Get("profiles.active");

    public string BeforeContinueLabel => LocalizationService.Current.Get("profiles.beforeContinue");

    public string EditLabel => LocalizationService.Current.Get("common.edit");

    public string DeleteLabel => LocalizationService.Current.Get("common.delete");

    public bool IsRecommended => Profile.Kind == PrivacyProfileKind.Recommended;

    public bool IsMaximum => Profile.Kind == PrivacyProfileKind.Maximum;

    public bool IsBalanced => Profile.Kind == PrivacyProfileKind.Balanced;

    public bool IsRestore => Profile.Kind == PrivacyProfileKind.RestoreDefault;

    public bool IsCustom => Profile.Kind == PrivacyProfileKind.Custom;

    public bool CanApply => !IsActive && !CommandsBusy;

    public bool ShowAccentApply => CanApply && (IsRecommended || IsCustom);

    public bool ShowStandardApply => !ShowAccentApply;

    public string ApplyButtonLabel => IsActive
        ? LocalizationService.Current.Get("profiles.currentlyApplied")
        : LocalizationService.Current.Get("profiles.review");

    /// <summary>
    /// Binding token so accent chrome updates when the active profile changes.
    /// </summary>
    public string CardState => $"{Profile.Kind}:{(IsActive ? 1 : 0)}";

    public string Glyph => Profile.Kind switch
    {
        PrivacyProfileKind.Recommended => "\uE72E",
        PrivacyProfileKind.Maximum => "\uE71B",
        PrivacyProfileKind.Balanced => "\uE8F1",
        PrivacyProfileKind.RestoreDefault => "\uE777",
        PrivacyProfileKind.Custom => "\uE70F",
        _ => "\uE713"
    };
}
