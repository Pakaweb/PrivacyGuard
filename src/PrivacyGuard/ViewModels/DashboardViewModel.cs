using PrivacyGuard.Helpers;
using PrivacyGuard.Services;

namespace PrivacyGuard.ViewModels;

/// <summary>
/// Dashboard: live privacy score, telemetry control, and per-setting actions.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IPrivacyService _privacy;
    private readonly IDialogService _dialogs;
    private readonly IElevationService _elevation;
    private readonly IChangeHistoryService _history;
    private readonly IResetMonitorService _reset;
    private readonly IAppliedBaselineStore _baseline;
    private readonly ILocalizationService _loc;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly HashSet<string> _recentlyChangedKeys = new(StringComparer.OrdinalIgnoreCase);

    public DashboardViewModel(
        IPrivacyService privacy,
        IDialogService dialogs,
        IElevationService elevation,
        IChangeHistoryService history,
        IResetMonitorService reset,
        IAppliedBaselineStore baseline,
        ILocalizationService localization,
        ILogger<DashboardViewModel> logger)
    {
        _privacy = privacy;
        _dialogs = dialogs;
        _elevation = elevation;
        _history = history;
        _reset = reset;
        _baseline = baseline;
        _loc = localization;
        Loc = localization;
        _logger = logger;
        StatusItems = [];
        ScoreFactors = [];
        Sections =
        [
            new PrivacySection { Id = "core", Title = _loc.Get("dashboard.section.core"), Subtitle = _loc.Get("dashboard.section.coreSub") },
            new PrivacySection { Id = "ads", Title = _loc.Get("dashboard.section.ads"), Subtitle = _loc.Get("dashboard.section.adsSub") },
            new PrivacySection { Id = "ai", Title = _loc.Get("dashboard.section.ai"), Subtitle = _loc.Get("dashboard.section.aiSub") },
            new PrivacySection { Id = "other", Title = _loc.Get("dashboard.section.other"), Subtitle = _loc.Get("dashboard.section.otherSub") }
        ];
        _statusMessage = _loc.Get("dashboard.loading");
        SyncResetBanner();
    }

    public void Attach() => _reset.StateChanged += OnResetStateChanged;

    public void Detach() => _reset.StateChanged -= OnResetStateChanged;

    private void OnResetStateChanged(object? sender, EventArgs e) => SyncResetBanner();

    public ILocalizationService Loc { get; }

    public ObservableCollection<PrivacyStatusItem> StatusItems { get; }

    public ObservableCollection<ScoreFactor> ScoreFactors { get; }

    public IReadOnlyList<PrivacySection> Sections { get; }

    [ObservableProperty]
    private PrivacySnapshot? _snapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyChanges))]
    [NotifyPropertyChangedFor(nameof(CanApplyRecommended))]
    [NotifyPropertyChangedFor(nameof(CanApplyTelemetry))]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ApplyTelemetryCommand.NotifyCanExecuteChanged();
        ApplyRecommendedCommand.NotifyCanExecuteChanged();
        ToggleSettingCommand.NotifyCanExecuteChanged();
        RestartElevatedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanApplyTelemetry));
        OnPropertyChanged(nameof(CanApplyRecommended));
    }

    [ObservableProperty]
    private bool _isRebuilding;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private InfoMessageKind _bannerKind = InfoMessageKind.Informational;

    [ObservableProperty]
    private bool _isToastOpen;

    [ObservableProperty]
    private string _toastTitle = string.Empty;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private InfoMessageKind _toastKind = InfoMessageKind.Success;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingRecommendedChanges))]
    [NotifyPropertyChangedFor(nameof(ImproveButtonLabel))]
    [NotifyPropertyChangedFor(nameof(CanApplyRecommended))]
    private int _pendingRecommendedCount;

    public bool IsElevated => _elevation.IsElevated;

    public bool CanApplyChanges => !IsBusy;

    public bool CanApplyRecommended => CanApplyChanges && HasPendingRecommendedChanges;

    public bool CanApplyTelemetry => CanApplyChanges && IsElevated;

    public double ScoreValue => Snapshot?.PrivacyScore ?? 0;

    public string ScoreLabel => Snapshot is null ? "—" : $"{Snapshot.PrivacyScore}";

    public string HealthLabel => Snapshot?.OverallHealth switch
    {
        PrivacyHealth.Protected => _loc.Get("health.protected"),
        PrivacyHealth.Partial => _loc.Get("health.needsAttention"),
        PrivacyHealth.Collecting => _loc.Get("health.collecting"),
        _ => _loc.Get("health.unknown")
    };

    public string ScoreSummary => Snapshot is null
        ? _loc.Get("dashboard.scoreLoading")
        : Snapshot.OverallHealth switch
        {
            PrivacyHealth.Protected => _loc.Get("dashboard.scoreProtected"),
            PrivacyHealth.Partial => _loc.Get("dashboard.scorePartial"),
            _ => _loc.Get("dashboard.scoreCollecting")
        };

    public string EditionLabel => Snapshot?.WindowsEdition ?? "Windows";

    public string LastRefreshedLabel =>
        Snapshot is null ? string.Empty : _loc.Get("dashboard.updated", Snapshot.CapturedAt.LocalDateTime.ToString("t"));

    public PrivacyHealth OverallHealth => Snapshot?.OverallHealth ?? PrivacyHealth.Partial;

    public bool HasPendingRecommendedChanges => PendingRecommendedCount > 0;

    public string ImproveButtonLabel => HasPendingRecommendedChanges
        ? _loc.Get("dashboard.improve")
        : _loc.Get("dashboard.improveDone");

    public IReadOnlyList<TelemetryLevel> TelemetryLevels { get; } =
    [
        TelemetryLevel.Security,
        TelemetryLevel.Basic,
        TelemetryLevel.Enhanced,
        TelemetryLevel.Full
    ];

    [ObservableProperty]
    private TelemetryLevel _selectedTelemetryLevel = TelemetryLevel.Full;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterAll))]
    [NotifyPropertyChangedFor(nameof(IsFilterCollecting))]
    [NotifyPropertyChangedFor(nameof(IsFilterProtected))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private int _cardFilterIndex;

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchQuery);

    public bool IsFilterAll => CardFilterIndex == 0;

    public bool IsFilterCollecting => CardFilterIndex == 1;

    public bool IsFilterProtected => CardFilterIndex == 2;

    public bool HasNoSearchResults =>
        Sections.All(section => !section.IsVisible)
        && (HasSearchText || CardFilterIndex != 0);

    [ObservableProperty]
    private bool _hasWindowsReset;

    [ObservableProperty]
    private string _resetSummary = string.Empty;

    [ObservableProperty]
    private bool _canReapplyLastProfile;

    partial void OnSearchQueryChanged(string value) => ApplySearchFilter();

    partial void OnCardFilterIndexChanged(int value) => ApplySearchFilter();

    [RelayCommand(CanExecute = nameof(CanApplyChanges))]
    public async Task RefreshAsync()
    {
        var ownsBusy = !IsBusy;
        if (ownsBusy)
        {
            IsBusy = true;
        }

        try
        {
            if (!IsToastOpen)
            {
                StatusMessage = _loc.Get("dashboard.reading");
                BannerKind = InfoMessageKind.Informational;
            }

            Snapshot = await _privacy.GetSnapshotAsync();
            SelectedTelemetryLevel = Snapshot.TelemetryLevel;
            await RebuildTilesAsync(Snapshot);

            NotifyScoreProperties();

            if (!IsToastOpen)
            {
                StatusMessage = _elevation.IsElevated
                    ? _loc.Get("dashboard.bannerElevated")
                    : _loc.Get("dashboard.bannerLimited");
                BannerKind = _elevation.IsElevated ? InfoMessageKind.Success : InfoMessageKind.Warning;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh privacy snapshot.");
            StatusMessage = _loc.Get("dashboard.refreshFailed");
            BannerKind = InfoMessageKind.Error;
            await _dialogs.ShowErrorAsync(_loc.Get("dashboard.refreshFailedTitle"), FriendlyError(ex));
        }
        finally
        {
            if (ownsBusy)
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyChanges))]
    private async Task ApplyTelemetryAsync()
    {
        if (Snapshot is null)
        {
            return;
        }

        var operation = _privacy.BuildTelemetryOperation(SelectedTelemetryLevel, Snapshot);
        if (operation is null)
        {
            ShowToast(_loc.Get("dashboard.alreadySet"), _loc.Get("dashboard.alreadySetMsg"), InfoMessageKind.Informational);
            return;
        }

        await ApplyWithConfirmationAsync(
            [operation],
            _loc.Get("dashboard.setTelemetry", SelectedTelemetryLevel),
            operation.SideEffectWarning);
    }

    [RelayCommand(CanExecute = nameof(CanApplyChanges))]
    private async Task ApplyRecommendedAsync()
    {
        if (Snapshot is null)
        {
            return;
        }

        var operations = _privacy.BuildProfileOperations(PrivacyProfileKind.Recommended, Snapshot);
        if (operations.Count == 0)
        {
            ShowToast(_loc.Get("dashboard.goodShape"), _loc.Get("dashboard.goodShapeMsg"), InfoMessageKind.Success);
            return;
        }

        var profile = _privacy.GetProfiles().First(p => p.Kind == PrivacyProfileKind.Recommended);
        var applied = await ApplyWithConfirmationAsync(operations, profile.Title, profile.Warning);
        if (applied)
        {
            await _baseline.SetLastProfileAsync(nameof(PrivacyProfileKind.Recommended), null, profile.Title);
        }
    }

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

    [RelayCommand(CanExecute = nameof(CanApplyChanges))]
    private async Task ToggleSettingAsync(PrivacyStatusItem? item)
    {
        if (item is null || item.IsTelemetry || Snapshot is null)
        {
            return;
        }

        await TrySetEnabledAsync(item, !item.IsOn);
    }

    /// <summary>
    /// Applies a toggle or enable/disable action. Returns false if the user cancelled or the write failed,
    /// so the UI can snap the switch back.
    /// </summary>
    public async Task<bool> TrySetEnabledAsync(PrivacyStatusItem item, bool enabled)
    {
        if (item.IsTelemetry || Snapshot is null || item.IsOn == enabled)
        {
            return true;
        }

        var operation = _privacy.BuildToggleOperation(item.SettingKey, enabled, Snapshot);
        if (operation is null)
        {
            return true;
        }

        var verb = enabled ? _loc.Get("dashboard.enable") : _loc.Get("dashboard.disable");
        return await ApplyWithConfirmationAsync(
            [operation],
            $"{verb} {item.Title}",
            operation.SideEffectWarning);
    }

    private async Task<bool> ApplyWithConfirmationAsync(
        IReadOnlyList<PrivacyOperation> operations,
        string title,
        string? warning)
    {
        var summary = string.Join(
            Environment.NewLine,
            operations.Select(o =>
                $"• {o.DisplayName}: {PrivacyCatalog.FormatValue(o.SettingKey, o.CurrentValue)} → {PrivacyCatalog.FormatValue(o.SettingKey, o.NewValue)}"));

        if (operations.Any(o => o.RequiresAdmin) && !_elevation.IsElevated)
        {
            await _dialogs.ShowErrorAsync(
                _loc.Get("profiles.adminTitle"),
                _loc.Get("profiles.adminBody"));
            return false;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            title,
            _loc.Get("dashboard.confirmBody", summary),
            warning);
        if (!confirmed)
        {
            return false;
        }

        try
        {
            IsBusy = true;
            var result = await _privacy.ApplyOperationsAsync(operations, title);
            _recentlyChangedKeys.Clear();
            foreach (var change in result.Changes.Where(c => c.Succeeded))
            {
                _recentlyChangedKeys.Add(change.SettingKey);
            }

            if (!result.Success)
            {
                await _dialogs.ShowErrorAsync(_loc.Get("dashboard.changeIncomplete"), string.Join(Environment.NewLine, result.Errors));
            }

            await RefreshAsync();
            ShowToast(
                result.Success ? _loc.Get("dashboard.privacyUpdated") : _loc.Get("dashboard.finishedErrors"),
                result.Message,
                result.Success ? InfoMessageKind.Success : InfoMessageKind.Error);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply dashboard change.");
            await _dialogs.ShowErrorAsync(_loc.Get("dashboard.changeFailed"), FriendlyError(ex));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RebuildTilesAsync(PrivacySnapshot snapshot)
    {
        IsRebuilding = true;
        try
        {
            IReadOnlyDictionary<string, DateTimeOffset> lastChanged = new Dictionary<string, DateTimeOffset>();
            try
            {
                var history = await _history.GetRecentAsync();
                lastChanged = history
                    .Where(c => c.Succeeded)
                    .GroupBy(c => c.SettingKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Timestamp, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "History unavailable while building dashboard tiles.");
            }

            StatusItems.Clear();
            foreach (var section in Sections)
            {
                section.Items.Clear();
            }

            ScoreFactors.Clear();
            foreach (var factor in PrivacyCatalog.BuildScoreFactors(snapshot))
            {
                ScoreFactors.Add(factor);
            }

            foreach (var setting in snapshot.AllSettings)
            {
                if (setting.SettingKey == PrivacySettingKeys.TelemetryLevel)
                {
                    continue;
                }

                var isOn = setting.SettingKey switch
                {
                    PrivacySettingKeys.DiagTrack => PrivacyCatalog.IsServiceRunning(snapshot.DiagTrack),
                    PrivacySettingKeys.DmwAppPush => PrivacyCatalog.IsServiceRunning(snapshot.DmwAppPush),
                    PrivacySettingKeys.AdvertisingId => snapshot.AdvertisingIdEnabled,
                    PrivacySettingKeys.ActivityHistory => snapshot.ActivityHistoryEnabled,
                    PrivacySettingKeys.Cortana => snapshot.CortanaEnabled,
                    PrivacySettingKeys.Copilot => snapshot.CopilotEnabled,
                    PrivacySettingKeys.Feedback => snapshot.FeedbackEnabled,
                    PrivacySettingKeys.TailoredExperiences => snapshot.TailoredExperiencesEnabled,
                    _ => false
                };

                lastChanged.TryGetValue(setting.SettingKey, out var changedAt);
                var item = new PrivacyStatusItem
                {
                    SettingKey = setting.SettingKey,
                    Title = setting.DisplayName,
                    Description = PrivacyCatalog.Description(setting.SettingKey),
                    Warning = PrivacyCatalog.SideEffect(setting.SettingKey),
                    RequiresAdmin = setting.RequiresAdmin,
                    IsTelemetry = false,
                    IsServiceControl = PrivacyCatalog.IsServiceControl(setting.SettingKey),
                    UseToggle = !PrivacyCatalog.IsServiceControl(setting.SettingKey),
                    IsHighImpact = PrivacyCatalog.IsHighImpact(setting.SettingKey),
                    NeedsElevationHint = setting.RequiresAdmin && !_elevation.IsElevated,
                    RequiresAdminTooltip = _loc.Get("dashboard.requiresAdmin"),
                    ValueLabel = PrivacyCatalog.FormatValue(setting.SettingKey, setting.Value),
                    CanonicalValue = setting.Value,
                    Health = PrivacyCatalog.HealthFor(setting.SettingKey, setting.Value),
                    IsOn = isOn,
                    LastChangedLabel = changedAt == default
                        ? _loc.Get("dashboard.noLocalChanges")
                        : _loc.Get("dashboard.lastChanged", changedAt.LocalDateTime.ToString("g")),
                    IsRecentlyChanged = _recentlyChangedKeys.Contains(setting.SettingKey),
                    ChangeCommand = ToggleSettingCommand
                };

                StatusItems.Add(item);
            }

            PendingRecommendedCount = _privacy.BuildProfileOperations(PrivacyProfileKind.Recommended, snapshot).Count;
            ApplySearchFilter();
            await _reset.CheckAsync();
            SyncResetBanner();
        }
        finally
        {
            IsRebuilding = false;
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchQuery = string.Empty;

    [RelayCommand]
    private void SetCardFilter(string? mode)
    {
        var next = mode switch
        {
            "Collecting" => 1,
            "Protected" => 2,
            _ => 0
        };

        if (CardFilterIndex == next)
        {
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterCollecting));
            OnPropertyChanged(nameof(IsFilterProtected));
            return;
        }

        CardFilterIndex = next;
    }

    [RelayCommand]
    private async Task DismissWindowsResetAsync() => await _reset.DismissAsync();

    [RelayCommand]
    private async Task ReapplyLastProfileAsync()
    {
        var operations = await _reset.BuildReapplyOperationsAsync();
        if (operations.Count == 0)
        {
            ShowToast(_loc.Get("dashboard.goodShape"), _loc.Get("dashboard.goodShapeMsg"), InfoMessageKind.Success);
            await _reset.DismissAsync();
            return;
        }

        var title = string.IsNullOrWhiteSpace(_reset.LastProfileTitle)
            ? _loc.Get("dashboard.reapplyLastProfile")
            : _reset.LastProfileTitle;
        var applied = await ApplyWithConfirmationAsync(operations, title, null);
        if (applied)
        {
            await _reset.CheckAsync();
            SyncResetBanner();
        }
    }

    private void ApplySearchFilter()
    {
        var query = SearchQuery.Trim();
        foreach (var section in Sections)
        {
            var matches = StatusItems
                .Where(item => PrivacyCatalog.SectionId(item.SettingKey) == section.Id)
                .Where(item => MatchesCardFilter(item, CardFilterIndex))
                .Where(item => string.IsNullOrEmpty(query) || MatchesSearch(item, query))
                .ToList();

            section.Items.Clear();
            foreach (var item in matches)
            {
                section.Items.Add(item);
            }

            section.IsVisible = matches.Count > 0;
        }

        OnPropertyChanged(nameof(HasSearchText));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }

    private static bool MatchesCardFilter(PrivacyStatusItem item, int filter) =>
        filter switch
        {
            1 => item.Health != PrivacyHealth.Protected,
            2 => item.Health == PrivacyHealth.Protected,
            _ => true
        };

    private static bool MatchesSearch(PrivacyStatusItem item, string query) =>
        (item.Title?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
        || (item.Description?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false);

    private void SyncResetBanner()
    {
        HasWindowsReset = _reset.HasWindowsReset;
        ResetSummary = _reset.ResetSummary;
        CanReapplyLastProfile = _reset.CanReapplyLast;
    }

    private void ShowToast(string title, string message, InfoMessageKind kind)
    {
        ToastTitle = title;
        ToastMessage = message;
        ToastKind = kind;
        IsToastOpen = true;
    }

    private void NotifyScoreProperties()
    {
        OnPropertyChanged(nameof(ScoreLabel));
        OnPropertyChanged(nameof(ScoreValue));
        OnPropertyChanged(nameof(HealthLabel));
        OnPropertyChanged(nameof(ScoreSummary));
        OnPropertyChanged(nameof(EditionLabel));
        OnPropertyChanged(nameof(LastRefreshedLabel));
        OnPropertyChanged(nameof(OverallHealth));
        OnPropertyChanged(nameof(HasPendingRecommendedChanges));
        OnPropertyChanged(nameof(ImproveButtonLabel));
    }

    private string FriendlyError(Exception ex) =>
        ex is UnauthorizedAccessException
            ? _loc.Get("dashboard.denied")
            : ex.Message;
}

public enum InfoMessageKind
{
    Informational,
    Success,
    Warning,
    Error
}
