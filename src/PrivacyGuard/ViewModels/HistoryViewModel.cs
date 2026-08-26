using PrivacyGuard.Services;

namespace PrivacyGuard.ViewModels;

/// <summary>
/// SQLite-backed audit log and restore points.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IChangeHistoryService _history;
    private readonly IBackupService _backup;
    private readonly IPrivacyService _privacy;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogger<HistoryViewModel> _logger;
    private readonly List<HistoryChangeItem> _changeItems = [];
    private bool _syncingRestoreSelection;

    public HistoryViewModel(
        IChangeHistoryService history,
        IBackupService backup,
        IPrivacyService privacy,
        IDialogService dialogs,
        ILocalizationService localization,
        ILogger<HistoryViewModel> logger)
    {
        _history = history;
        _backup = backup;
        _privacy = privacy;
        _dialogs = dialogs;
        _loc = localization;
        Loc = localization;
        _logger = logger;
        ChangeGroups = [];
        RestoreItems = [];
        _statusMessage = _loc.Get("history.storedLocally");
        FilterLabels = [_loc.Get("history.filterAll"), _loc.Get("history.filterActive"), _loc.Get("history.filterReverted")];
    }

    public ILocalizationService Loc { get; }

    public IReadOnlyList<string> FilterLabels { get; }

    public ObservableCollection<HistoryDayGroup> ChangeGroups { get; }

    public ObservableCollection<HistoryRestoreItem> RestoreItems { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRevertNow))]
    [NotifyPropertyChangedFor(nameof(CanRestoreNow))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVisibleChanges))]
    [NotifyPropertyChangedFor(nameof(ShowChangesEmpty))]
    [NotifyPropertyChangedFor(nameof(EmptyChangesTitle))]
    [NotifyPropertyChangedFor(nameof(EmptyChangesMessage))]
    private bool _hasAnyChanges;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRestoreEmpty))]
    private bool _hasRestorePoints;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVisibleChanges))]
    [NotifyPropertyChangedFor(nameof(ShowChangesEmpty))]
    private int _filterIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeSelectionLabel))]
    [NotifyPropertyChangedFor(nameof(RevertButtonLabel))]
    [NotifyPropertyChangedFor(nameof(HasSelectedChanges))]
    [NotifyPropertyChangedFor(nameof(HasSelectedRevertable))]
    [NotifyPropertyChangedFor(nameof(CanRevertNow))]
    private int _selectedChangeCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRevertable))]
    [NotifyPropertyChangedFor(nameof(RevertButtonLabel))]
    [NotifyPropertyChangedFor(nameof(CanRevertNow))]
    private int _selectedRevertableCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RestoreSelectionLabel))]
    [NotifyPropertyChangedFor(nameof(HasSelectedRestorePoint))]
    [NotifyPropertyChangedFor(nameof(CanRestoreNow))]
    private int _selectedRestoreCount;

    public bool HasVisibleChanges => ChangeGroups.Count > 0;

    public bool ShowChangesEmpty => !HasVisibleChanges;

    public bool ShowRestoreEmpty => !HasRestorePoints;

    public bool HasSelectedChanges => SelectedChangeCount > 0;

    public bool HasSelectedRevertable => SelectedRevertableCount > 0;

    public bool HasSelectedRestorePoint => SelectedRestoreCount == 1;

    public bool CanRevertNow => HasSelectedRevertable && !IsBusy;

    public bool CanRestoreNow => HasSelectedRestorePoint && !IsBusy;

    public string ChangeSelectionLabel => SelectedChangeCount switch
    {
        0 => _loc.Get("history.noneSelected"),
        1 => _loc.Get("history.changeSelectedOne"),
        _ => _loc.Get("history.changeSelectedMany", SelectedChangeCount)
    };

    public string RestoreSelectionLabel => SelectedRestoreCount switch
    {
        0 => _loc.Get("history.noneSelected"),
        1 => _loc.Get("history.restoreSelectedOne"),
        _ => _loc.Get("history.restoreSelectedMany", SelectedRestoreCount)
    };

    public string RevertButtonLabel => SelectedRevertableCount > 1
        ? _loc.Get("history.revertN", SelectedRevertableCount)
        : _loc.Get("history.revertSelected");

    public string EmptyChangesTitle => HasAnyChanges ? _loc.Get("history.emptyFilterTitle") : _loc.Get("history.emptyTitle");

    public string EmptyChangesMessage => HasAnyChanges
        ? _loc.Get("history.emptyFilterMsg")
        : _loc.Get("history.emptyMsg");

    partial void OnFilterIndexChanged(int value) => RebuildChangeGroups();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var ownsBusy = !IsBusy;
        if (ownsBusy)
        {
            IsBusy = true;
        }

        try
        {
            var changes = await _history.GetRecentAsync();
            var points = await _backup.GetRecentAsync();

            foreach (var item in _changeItems)
            {
                item.PropertyChanged -= OnChangeItemPropertyChanged;
            }

            foreach (var item in RestoreItems)
            {
                item.PropertyChanged -= OnRestoreItemPropertyChanged;
            }

            _changeItems.Clear();
            foreach (var change in changes)
            {
                var item = new HistoryChangeItem(change);
                item.PropertyChanged += OnChangeItemPropertyChanged;
                _changeItems.Add(item);
            }

            RestoreItems.Clear();
            foreach (var point in points)
            {
                var item = new HistoryRestoreItem(point);
                item.PropertyChanged += OnRestoreItemPropertyChanged;
                RestoreItems.Add(item);
            }

            HasAnyChanges = _changeItems.Count > 0;
            HasRestorePoints = RestoreItems.Count > 0;
            RebuildChangeGroups();
            RecalculateChangeSelection();
            RecalculateRestoreSelection();

            StatusMessage = changes.Count == 0
                ? _loc.Get("history.statusEmpty")
                : _loc.Get("history.statusCounts", changes.Count, points.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history.");
            await _dialogs.ShowErrorAsync(_loc.Get("history.unavailable"), ex.Message);
        }
        finally
        {
            if (ownsBusy)
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private async Task RevertSelectedAsync()
    {
        var selected = _changeItems.Where(item => item.IsSelected && item.CanRevert).ToList();
        if (selected.Count == 0)
        {
            if (_changeItems.Any(item => item.IsSelected && item.IsReverted))
            {
                await _dialogs.ShowMessageAsync(_loc.Get("history.alreadyRevertedTitle"), _loc.Get("history.alreadyRevertedBody"));
            }

            return;
        }

        var summary = string.Join(
            Environment.NewLine,
            selected.Select(item =>
                $"• {item.SettingName}: {item.NewLabel} → {item.OldLabel}"));

        var confirmed = await _dialogs.ConfirmAsync(
            selected.Count == 1 ? _loc.Get("history.revertOne") : _loc.Get("history.revertMany", selected.Count),
            _loc.Get("history.revertBody", summary),
            _loc.Get("history.revertWarning"));
        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var errors = new List<string>();
            foreach (var item in selected)
            {
                var result = await _privacy.RevertChangeAsync(item.Record);
                if (result.Success)
                {
                    await _history.MarkRevertedAsync(item.Record.Id);
                }
                else
                {
                    errors.Add($"{item.SettingName}: {string.Join(" ", result.Errors)}");
                }
            }

            if (errors.Count > 0)
            {
                await _dialogs.ShowErrorAsync(_loc.Get("history.revertFailed"), string.Join(Environment.NewLine, errors));
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revert selected changes.");
            await _dialogs.ShowErrorAsync(_loc.Get("history.revertFailed"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreSelectedPointAsync()
    {
        var selected = RestoreItems.FirstOrDefault(item => item.IsSelected);
        if (selected is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _loc.Get("history.restoreTitle"),
            _loc.Get("history.restoreBody", selected.Point.Id, selected.Point.CreatedAt.LocalDateTime.ToString("g"), selected.Point.Settings.Count),
            _loc.Get("history.restoreWarning"));
        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var result = await _privacy.RestorePointAsync(selected.Point);
            if (!result.Success)
            {
                await _dialogs.ShowErrorAsync(_loc.Get("history.restoreFailed"), string.Join(Environment.NewLine, result.Errors));
            }
            else
            {
                await _dialogs.ShowMessageAsync(_loc.Get("history.restored"), result.Message);
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore point {Id}", selected.Point.Id);
            await _dialogs.ShowErrorAsync(_loc.Get("history.restoreFailed"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildChangeGroups()
    {
        var filter = (HistoryFilterKind)FilterIndex;
        var visible = _changeItems.Where(item => filter switch
        {
            HistoryFilterKind.Active => !item.IsReverted,
            HistoryFilterKind.Reverted => item.IsReverted,
            _ => true
        }).ToList();

        ChangeGroups.Clear();
        var showTitles = visible.Count > 1;
        foreach (var group in visible.GroupBy(item => DayTitle(item.Record.Timestamp)))
        {
            var items = group.ToList();
            var includeDate = group.Key == _loc.Get("history.earlier");
            foreach (var item in items)
            {
                item.ShowDateInMeta = includeDate;
            }

            ChangeGroups.Add(new HistoryDayGroup
            {
                Title = group.Key,
                ShowTitle = showTitles,
                Items = items
            });
        }

        OnPropertyChanged(nameof(HasVisibleChanges));
        OnPropertyChanged(nameof(ShowChangesEmpty));
        OnPropertyChanged(nameof(EmptyChangesTitle));
        OnPropertyChanged(nameof(EmptyChangesMessage));
    }

    private void OnChangeItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HistoryChangeItem.IsSelected))
        {
            RecalculateChangeSelection();
        }
    }

    private void OnRestoreItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(HistoryRestoreItem.IsSelected) || sender is not HistoryRestoreItem changed)
        {
            return;
        }

        if (!_syncingRestoreSelection && changed.IsSelected)
        {
            _syncingRestoreSelection = true;
            foreach (var item in RestoreItems)
            {
                if (!ReferenceEquals(item, changed))
                {
                    item.IsSelected = false;
                }
            }

            _syncingRestoreSelection = false;
        }

        RecalculateRestoreSelection();
    }

    private void RecalculateChangeSelection()
    {
        SelectedChangeCount = _changeItems.Count(item => item.IsSelected);
        SelectedRevertableCount = _changeItems.Count(item => item.IsSelected && item.CanRevert);
    }

    private void RecalculateRestoreSelection()
    {
        SelectedRestoreCount = RestoreItems.Count(item => item.IsSelected);
    }

    private string DayTitle(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime().Date;
        var today = DateTime.Today;
        if (local == today)
        {
            return _loc.Get("history.today");
        }

        if (local == today.AddDays(-1))
        {
            return _loc.Get("history.yesterday");
        }

        return _loc.Get("history.earlier");
    }
}
