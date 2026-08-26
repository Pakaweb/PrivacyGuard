using System.ServiceProcess;
using Microsoft.Win32;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <summary>
/// Reads and safely mutates the documented Windows privacy settings that PrivacyGuard supports.
/// Every write path creates a restore point and a history row.
/// </summary>
public sealed class PrivacyService : IPrivacyService
{
    private static readonly HashSet<string> EnterpriseSkus = new(StringComparer.OrdinalIgnoreCase)
    {
        "Enterprise", "EnterpriseS", "EnterpriseN", "EnterpriseSN",
        "Education", "EducationN", "IoTUAP", "ServerRdsh",
        "ServerStandard", "ServerDatacenter", "ServerSolution"
    };

    private readonly RegistryHelper _registry;
    private readonly WindowsServiceHelper _services;
    private readonly IElevationService _elevation;
    private readonly IBackupService _backup;
    private readonly IChangeHistoryService _history;
    private readonly ISettingsService _settings;
    private readonly IAppliedBaselineStore _baseline;
    private readonly ILogger<PrivacyService> _logger;

    public PrivacyService(
        RegistryHelper registry,
        WindowsServiceHelper services,
        IElevationService elevation,
        IBackupService backup,
        IChangeHistoryService history,
        ISettingsService settings,
        IAppliedBaselineStore baseline,
        ILogger<PrivacyService> logger)
    {
        _registry = registry;
        _services = services;
        _elevation = elevation;
        _backup = backup;
        _history = history;
        _settings = settings;
        _baseline = baseline;
        _logger = logger;
    }

    public IReadOnlyList<PrivacyProfile> GetProfiles() => PrivacyCatalog.Profiles;

    public async Task<PrivacySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(CaptureSnapshot, cancellationToken);
    }

    public PrivacyOperation? BuildToggleOperation(string settingKey, bool enabled, PrivacySnapshot current)
    {
        if (settingKey == PrivacySettingKeys.TelemetryLevel)
        {
            return null;
        }

        var newValue = settingKey switch
        {
            PrivacySettingKeys.DiagTrack => enabled
                ? DesiredServiceState.RunningAutomatic.ToString()
                : DesiredServiceState.StoppedDisabled.ToString(),
            PrivacySettingKeys.DmwAppPush => enabled
                ? DesiredServiceState.RunningAutomatic.ToString()
                : DesiredServiceState.StoppedDisabled.ToString(),
            _ => enabled ? "1" : "0"
        };

        var currentValue = GetCanonical(settingKey, current);
        if (string.Equals(NormalizeComparable(settingKey, currentValue), NormalizeComparable(settingKey, newValue), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new PrivacyOperation
        {
            SettingKey = settingKey,
            DisplayName = PrivacyCatalog.DisplayName(settingKey),
            CurrentValue = currentValue,
            NewValue = newValue,
            RequiresAdmin = PrivacyCatalog.RequiresAdmin(settingKey),
            SideEffectWarning = PrivacyCatalog.SideEffect(settingKey)
        };
    }

    public PrivacyOperation? BuildTelemetryOperation(TelemetryLevel level, PrivacySnapshot current)
    {
        var newValue = ((int)level).ToString();
        var currentValue = ((int)current.TelemetryLevel).ToString();
        if (newValue == currentValue)
        {
            return null;
        }

        return new PrivacyOperation
        {
            SettingKey = PrivacySettingKeys.TelemetryLevel,
            DisplayName = PrivacyCatalog.DisplayName(PrivacySettingKeys.TelemetryLevel),
            CurrentValue = currentValue,
            NewValue = newValue,
            RequiresAdmin = true,
            SideEffectWarning = PrivacyCatalog.SideEffect(PrivacySettingKeys.TelemetryLevel)
        };
    }

    public IReadOnlyList<PrivacyOperation> BuildProfileOperations(PrivacyProfileKind kind, PrivacySnapshot current)
    {
        var desired = kind switch
        {
            PrivacyProfileKind.Recommended => Recommended(current),
            PrivacyProfileKind.Maximum => Maximum(current),
            PrivacyProfileKind.Balanced => Balanced(current),
            PrivacyProfileKind.RestoreDefault => RestoreDefault(current),
            _ => []
        };

        return desired
            .Where(op => !string.Equals(
                NormalizeComparable(op.SettingKey, op.CurrentValue),
                NormalizeComparable(op.SettingKey, op.NewValue),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<PrivacyOperation> BuildProfileOperations(PrivacyProfile profile, PrivacySnapshot current)
    {
        if (profile.Kind == PrivacyProfileKind.Custom)
        {
            return BuildDesiredOperations(profile.DesiredValues, current);
        }

        return BuildProfileOperations(profile.Kind, current);
    }

    public IReadOnlyList<PrivacyOperation> BuildDesiredOperations(
        IReadOnlyDictionary<string, string> desired,
        PrivacySnapshot current)
    {
        return desired
            .Where(pair => PrivacySettingKeys.All.Contains(pair.Key) && PrivacyCatalog.IsValidCanonical(pair.Key, pair.Value))
            .Select(pair => Op(pair.Key, current, pair.Value))
            .Where(op => !ValuesMatch(op.SettingKey, op.CurrentValue, op.NewValue))
            .ToList();
    }

    public string? GetCanonicalValue(string settingKey, PrivacySnapshot snapshot) => GetCanonical(settingKey, snapshot);

    public bool ValuesMatch(string settingKey, string? left, string? right) =>
        string.Equals(
            NormalizeComparable(settingKey, left),
            NormalizeComparable(settingKey, right),
            StringComparison.OrdinalIgnoreCase);

    public async Task<PrivacyOperationResult> ApplyOperationsAsync(
        IReadOnlyList<PrivacyOperation> operations,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (operations.Count == 0)
        {
            return new PrivacyOperationResult
            {
                Success = true,
                Message = L("privacy.nothingToChange")
            };
        }

        var unknown = operations.FirstOrDefault(o => !PrivacySettingKeys.All.Contains(o.SettingKey));
        if (unknown is not null)
        {
            return PrivacyOperationResult.Failed(L("privacy.refusedUnknown", unknown.SettingKey));
        }

        if (operations.Any(o => o.RequiresAdmin) && !_elevation.IsElevated)
        {
            return PrivacyOperationResult.Failed(L("privacy.needsAdmin"));
        }

        try
        {
            var snapshot = CaptureSnapshot();
            var backups = operations
                .Select(op => new SettingSnapshot
                {
                    SettingKey = op.SettingKey,
                    DisplayName = op.DisplayName,
                    Value = GetCanonical(op.SettingKey, snapshot),
                    RequiresAdmin = op.RequiresAdmin
                })
                .ToList();

            var restorePoint = await _backup.CreateAsync(reason, backups, cancellationToken);
            var changes = new List<ChangeRecord>();
            var errors = new List<string>();

            foreach (var operation in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var error = ApplySingle(operation);
                var record = new ChangeRecord
                {
                    Timestamp = DateTimeOffset.Now,
                    SettingKey = operation.SettingKey,
                    SettingName = operation.DisplayName,
                    OldValue = operation.CurrentValue,
                    NewValue = operation.NewValue,
                    ProfileName = reason,
                    RestorePointId = restorePoint.Id,
                    Error = error
                };

                if (_settings.Current.RecordHistory)
                {
                    var id = await _history.InsertAsync(record, cancellationToken);
                    changes.Add(new ChangeRecord
                    {
                        Id = id,
                        Timestamp = record.Timestamp,
                        SettingKey = record.SettingKey,
                        SettingName = record.SettingName,
                        OldValue = record.OldValue,
                        NewValue = record.NewValue,
                        ProfileName = record.ProfileName,
                        RestorePointId = record.RestorePointId,
                        Error = record.Error
                    });
                }
                else
                {
                    changes.Add(record);
                }

                if (error is not null)
                {
                    errors.Add($"{operation.DisplayName}: {error}");
                }
            }

            var success = errors.Count == 0;
            var succeededOps = operations
                .Where(op => changes.Any(c =>
                    string.Equals(c.SettingKey, op.SettingKey, StringComparison.OrdinalIgnoreCase) && c.Succeeded))
                .ToList();
            if (succeededOps.Count > 0)
            {
                try
                {
                    await _baseline.MergeAsync(succeededOps, profileKind: null, customId: null, profileTitle: reason, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Baseline update after apply was skipped.");
                }
            }

            return new PrivacyOperationResult
            {
                Success = success,
                RestorePointId = restorePoint.Id,
                Changes = changes,
                Errors = errors,
                Message = success
                    ? L("privacy.applied", changes.Count, restorePoint.Id)
                    : L("privacy.completedErrors", errors.Count, restorePoint.Id)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply privacy operations.");
            return PrivacyOperationResult.Failed(L("privacy.couldNotComplete", ex.Message));
        }
    }

    public Task<PrivacyOperationResult> RevertChangeAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        var operation = new PrivacyOperation
        {
            SettingKey = record.SettingKey,
            DisplayName = record.SettingName,
            CurrentValue = record.NewValue,
            NewValue = record.OldValue ?? DefaultCanonical(record.SettingKey),
            RequiresAdmin = PrivacyCatalog.RequiresAdmin(record.SettingKey),
            SideEffectWarning = PrivacyCatalog.SideEffect(record.SettingKey)
        };

        return ApplyOperationsAsync([operation], $"Revert:{record.SettingKey}", cancellationToken);
    }

    public Task<PrivacyOperationResult> RestorePointAsync(RestorePoint restorePoint, CancellationToken cancellationToken = default)
    {
        var operations = restorePoint.Settings.Select(setting => new PrivacyOperation
        {
            SettingKey = setting.SettingKey,
            DisplayName = setting.DisplayName,
            CurrentValue = null,
            NewValue = setting.Value ?? DefaultCanonical(setting.SettingKey),
            RequiresAdmin = setting.RequiresAdmin,
            SideEffectWarning = PrivacyCatalog.SideEffect(setting.SettingKey)
        }).ToList();

        return ApplyOperationsAsync(operations, $"RestorePoint:{restorePoint.Id}", cancellationToken);
    }

    private PrivacySnapshot CaptureSnapshot()
    {
        var edition = _registry.ReadString(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.WindowsNtCurrentVersion,
            PrivacyRegistryPaths.EditionIdValue) ?? string.Empty;

        var product = _registry.ReadString(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.WindowsNtCurrentVersion,
            PrivacyRegistryPaths.ProductNameValue) ?? "Windows";

        var displayVersion = _registry.ReadString(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.WindowsNtCurrentVersion,
            PrivacyRegistryPaths.DisplayVersionValue);

        var policyTelemetry = _registry.ReadDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.DataCollectionPolicy,
            PrivacyRegistryPaths.AllowTelemetryValue);

        var currentTelemetry = _registry.ReadDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.DataCollectionCurrent,
            PrivacyRegistryPaths.AllowTelemetryValue);

        var telemetryRaw = policyTelemetry ?? currentTelemetry ?? (int)TelemetryLevel.Full;
        var telemetry = ClampTelemetry(telemetryRaw);

        var diagTrack = _services.GetService(PrivacySettingKeys.DiagTrackService);
        var dmw = _services.GetService(PrivacySettingKeys.DmwAppPushService);

        var advertising = _registry.ReadDword(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.AdvertisingInfoUser,
            PrivacyRegistryPaths.AdvertisingInfoEnabledValue) ?? 1;

        var publishActivities = _registry.ReadDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.SystemPolicy,
            PrivacyRegistryPaths.PublishUserActivitiesValue) ?? 1;

        var uploadActivities = _registry.ReadDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.SystemPolicy,
            PrivacyRegistryPaths.UploadUserActivitiesValue) ?? 1;

        var cortana = _registry.ReadDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.WindowsSearchPolicy,
            PrivacyRegistryPaths.AllowCortanaValue) ?? 1;

        var copilotOff = _registry.ReadDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.CopilotPolicy,
            PrivacyRegistryPaths.TurnOffWindowsCopilotValue) ?? 0;

        var feedbackCount = _registry.ReadDword(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.SiufRulesUser,
            PrivacyRegistryPaths.NumberOfSiufInPeriodValue);

        var feedbackPolicy = _registry.ReadDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.DataCollectionPolicy,
            PrivacyRegistryPaths.DoNotShowFeedbackNotificationsValue) ?? 0;

        var tailored = _registry.ReadDword(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.PrivacyUser,
            PrivacyRegistryPaths.TailoredExperiencesValue) ?? 1;

        var snapshot = new PrivacySnapshot
        {
            TelemetryLevel = telemetry,
            TelemetryLevelIsPolicyEnforced = policyTelemetry is not null,
            SecurityTelemetrySupported = EnterpriseSkus.Contains(edition),
            DiagTrack = diagTrack,
            DmwAppPush = dmw,
            AdvertisingIdEnabled = advertising != 0,
            ActivityHistoryEnabled = publishActivities != 0,
            ActivityUploadEnabled = uploadActivities != 0,
            CortanaEnabled = cortana != 0,
            CopilotEnabled = copilotOff == 0,
            FeedbackEnabled = feedbackPolicy == 0 && (feedbackCount is null or > 0),
            TailoredExperiencesEnabled = tailored != 0,
            IsElevated = _elevation.IsElevated,
            WindowsEdition = string.IsNullOrWhiteSpace(displayVersion) ? $"{product} ({edition})" : $"{product} {displayVersion} ({edition})"
        };

        var all = PrivacySettingKeys.All.Select(key => new SettingSnapshot
        {
            SettingKey = key,
            DisplayName = PrivacyCatalog.DisplayName(key),
            Value = GetCanonical(key, snapshot),
            RequiresAdmin = PrivacyCatalog.RequiresAdmin(key)
        }).ToList();

        var score = ComputeScore(snapshot);
        var health = score >= 75 ? PrivacyHealth.Protected : score >= 40 ? PrivacyHealth.Partial : PrivacyHealth.Collecting;

        return new PrivacySnapshot
        {
            CapturedAt = snapshot.CapturedAt,
            TelemetryLevel = snapshot.TelemetryLevel,
            TelemetryLevelIsPolicyEnforced = snapshot.TelemetryLevelIsPolicyEnforced,
            SecurityTelemetrySupported = snapshot.SecurityTelemetrySupported,
            DiagTrack = snapshot.DiagTrack,
            DmwAppPush = snapshot.DmwAppPush,
            AdvertisingIdEnabled = snapshot.AdvertisingIdEnabled,
            ActivityHistoryEnabled = snapshot.ActivityHistoryEnabled,
            ActivityUploadEnabled = snapshot.ActivityUploadEnabled,
            CortanaEnabled = snapshot.CortanaEnabled,
            CopilotEnabled = snapshot.CopilotEnabled,
            FeedbackEnabled = snapshot.FeedbackEnabled,
            TailoredExperiencesEnabled = snapshot.TailoredExperiencesEnabled,
            IsElevated = snapshot.IsElevated,
            WindowsEdition = snapshot.WindowsEdition,
            OverallHealth = health,
            PrivacyScore = score,
            AllSettings = all
        };
    }

    private string? ApplySingle(PrivacyOperation operation)
    {
        try
        {
            return operation.SettingKey switch
            {
                PrivacySettingKeys.TelemetryLevel => ApplyTelemetry(operation.NewValue),
                PrivacySettingKeys.DiagTrack => ApplyService(PrivacySettingKeys.DiagTrackService, operation.NewValue),
                PrivacySettingKeys.DmwAppPush => ApplyService(PrivacySettingKeys.DmwAppPushService, operation.NewValue),
                PrivacySettingKeys.AdvertisingId => ApplyAdvertising(operation.NewValue),
                PrivacySettingKeys.ActivityHistory => ApplyActivityHistory(operation.NewValue),
                PrivacySettingKeys.Cortana => ApplyCortana(operation.NewValue),
                PrivacySettingKeys.Copilot => ApplyCopilot(operation.NewValue),
                PrivacySettingKeys.Feedback => ApplyFeedback(operation.NewValue),
                PrivacySettingKeys.TailoredExperiences => ApplyTailored(operation.NewValue),
                _ => L("privacy.notImplemented", operation.SettingKey)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed applying {Key}", operation.SettingKey);
            return ex.Message;
        }
    }

    private string? ApplyTelemetry(string newValue)
    {
        if (!int.TryParse(newValue, out var level) || level is < 0 or > 3)
        {
            return L("privacy.telemetryRange");
        }

        if (level == 3)
        {
            // Restore default by removing the policy value so Windows owns the setting again.
            var deletedPolicy = _registry.DeleteValue(
                RegistryHive.LocalMachine,
                PrivacyRegistryPaths.DataCollectionPolicy,
                PrivacyRegistryPaths.AllowTelemetryValue);
            var deletedCurrent = _registry.DeleteValue(
                RegistryHive.LocalMachine,
                PrivacyRegistryPaths.DataCollectionCurrent,
                PrivacyRegistryPaths.AllowTelemetryValue);
            return deletedPolicy && deletedCurrent ? null : L("privacy.telemetryRestoreFail");
        }

        var policy = _registry.WriteDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.DataCollectionPolicy,
            PrivacyRegistryPaths.AllowTelemetryValue,
            level);
        var current = _registry.WriteDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.DataCollectionCurrent,
            PrivacyRegistryPaths.AllowTelemetryValue,
            level);

        return policy && current ? null : L("privacy.telemetryWriteFail");
    }

    private string? ApplyService(string serviceName, string newValue)
    {
        if (!WindowsServiceHelper.CanControl(serviceName))
        {
            return L("privacy.serviceNotAllowed", serviceName);
        }

        var desired = ParseDesiredService(newValue);
        return _services.ApplyDesiredState(serviceName, desired);
    }

    private string? ApplyAdvertising(string newValue)
    {
        var enabled = IsOn(newValue);
        return _registry.WriteDword(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.AdvertisingInfoUser,
            PrivacyRegistryPaths.AdvertisingInfoEnabledValue,
            enabled ? 1 : 0)
            ? null
            : L("privacy.advertisingFail");
    }

    private string? ApplyActivityHistory(string newValue)
    {
        var enabled = IsOn(newValue);
        var publish = _registry.WriteDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.SystemPolicy,
            PrivacyRegistryPaths.PublishUserActivitiesValue,
            enabled ? 1 : 0);
        var upload = _registry.WriteDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.SystemPolicy,
            PrivacyRegistryPaths.UploadUserActivitiesValue,
            enabled ? 1 : 0);
        return publish && upload ? null : L("privacy.activityFail");
    }

    private string? ApplyCortana(string newValue)
    {
        var enabled = IsOn(newValue);
        if (enabled)
        {
            return _registry.DeleteValue(
                RegistryHive.LocalMachine,
                PrivacyRegistryPaths.WindowsSearchPolicy,
                PrivacyRegistryPaths.AllowCortanaValue)
                ? null
                : L("privacy.cortanaRestoreFail");
        }

        return _registry.WriteDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.WindowsSearchPolicy,
            PrivacyRegistryPaths.AllowCortanaValue,
            0)
            ? null
            : L("privacy.cortanaWriteFail");
    }

    private string? ApplyCopilot(string newValue)
    {
        var enabled = IsOn(newValue);
        if (enabled)
        {
            var machine = _registry.DeleteValue(
                RegistryHive.LocalMachine,
                PrivacyRegistryPaths.CopilotPolicy,
                PrivacyRegistryPaths.TurnOffWindowsCopilotValue);
            var user = _registry.DeleteValue(
                RegistryHive.CurrentUser,
                PrivacyRegistryPaths.CopilotPolicy,
                PrivacyRegistryPaths.TurnOffWindowsCopilotValue);
            return machine && user ? null : L("privacy.copilotRestoreFail");
        }

        var wroteMachine = _registry.WriteDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.CopilotPolicy,
            PrivacyRegistryPaths.TurnOffWindowsCopilotValue,
            1);
        var wroteUser = _registry.WriteDword(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.CopilotPolicy,
            PrivacyRegistryPaths.TurnOffWindowsCopilotValue,
            1);
        return wroteMachine || wroteUser
            ? null
            : L("privacy.copilotWriteFail");
    }

    private string? ApplyFeedback(string newValue)
    {
        var enabled = IsOn(newValue);
        if (enabled)
        {
            _registry.DeleteValue(RegistryHive.CurrentUser, PrivacyRegistryPaths.SiufRulesUser, PrivacyRegistryPaths.NumberOfSiufInPeriodValue);
            _registry.DeleteValue(RegistryHive.CurrentUser, PrivacyRegistryPaths.SiufRulesUser, PrivacyRegistryPaths.PeriodInNanoSecondsValue);
            _registry.DeleteValue(RegistryHive.LocalMachine, PrivacyRegistryPaths.DataCollectionPolicy, PrivacyRegistryPaths.DoNotShowFeedbackNotificationsValue);
            return null;
        }

        var user = _registry.WriteDword(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.SiufRulesUser,
            PrivacyRegistryPaths.NumberOfSiufInPeriodValue,
            0);
        _registry.WriteDword(
            RegistryHive.LocalMachine,
            PrivacyRegistryPaths.DataCollectionPolicy,
            PrivacyRegistryPaths.DoNotShowFeedbackNotificationsValue,
            1);
        return user ? null : L("privacy.feedbackFail");
    }

    private string? ApplyTailored(string newValue)
    {
        var enabled = IsOn(newValue);
        return _registry.WriteDword(
            RegistryHive.CurrentUser,
            PrivacyRegistryPaths.PrivacyUser,
            PrivacyRegistryPaths.TailoredExperiencesValue,
            enabled ? 1 : 0)
            ? null
            : L("privacy.tailoredFail");
    }

    private IReadOnlyList<PrivacyOperation> Recommended(PrivacySnapshot current) =>
    [
        Op(PrivacySettingKeys.TelemetryLevel, current, "1"),
        Op(PrivacySettingKeys.DiagTrack, current, DesiredServiceState.RunningAutomatic.ToString()),
        Op(PrivacySettingKeys.DmwAppPush, current, DesiredServiceState.StoppedDisabled.ToString()),
        Op(PrivacySettingKeys.AdvertisingId, current, "0"),
        Op(PrivacySettingKeys.ActivityHistory, current, "0"),
        Op(PrivacySettingKeys.Feedback, current, "0"),
        Op(PrivacySettingKeys.TailoredExperiences, current, "0"),
        Op(PrivacySettingKeys.Cortana, current, "0")
    ];

    private IReadOnlyList<PrivacyOperation> Maximum(PrivacySnapshot current)
    {
        var telemetry = current.SecurityTelemetrySupported ? "0" : "1";
        return
        [
            Op(PrivacySettingKeys.TelemetryLevel, current, telemetry),
            Op(PrivacySettingKeys.DiagTrack, current, DesiredServiceState.StoppedDisabled.ToString()),
            Op(PrivacySettingKeys.DmwAppPush, current, DesiredServiceState.StoppedDisabled.ToString()),
            Op(PrivacySettingKeys.AdvertisingId, current, "0"),
            Op(PrivacySettingKeys.ActivityHistory, current, "0"),
            Op(PrivacySettingKeys.Cortana, current, "0"),
            Op(PrivacySettingKeys.Copilot, current, "0"),
            Op(PrivacySettingKeys.Feedback, current, "0"),
            Op(PrivacySettingKeys.TailoredExperiences, current, "0")
        ];
    }

    private IReadOnlyList<PrivacyOperation> Balanced(PrivacySnapshot current) =>
    [
        Op(PrivacySettingKeys.TelemetryLevel, current, "3"),
        Op(PrivacySettingKeys.DiagTrack, current, DesiredServiceState.RunningAutomatic.ToString()),
        Op(PrivacySettingKeys.DmwAppPush, current, DesiredServiceState.StoppedManual.ToString()),
        Op(PrivacySettingKeys.AdvertisingId, current, "0"),
        Op(PrivacySettingKeys.ActivityHistory, current, "0"),
        Op(PrivacySettingKeys.Feedback, current, "0"),
        Op(PrivacySettingKeys.TailoredExperiences, current, "0")
    ];

    private IReadOnlyList<PrivacyOperation> RestoreDefault(PrivacySnapshot current) =>
    [
        Op(PrivacySettingKeys.TelemetryLevel, current, "3"),
        Op(PrivacySettingKeys.DiagTrack, current, DesiredServiceState.RunningAutomatic.ToString()),
        Op(PrivacySettingKeys.DmwAppPush, current, DesiredServiceState.RunningAutomatic.ToString()),
        Op(PrivacySettingKeys.AdvertisingId, current, "1"),
        Op(PrivacySettingKeys.ActivityHistory, current, "1"),
        Op(PrivacySettingKeys.Cortana, current, "1"),
        Op(PrivacySettingKeys.Copilot, current, "1"),
        Op(PrivacySettingKeys.Feedback, current, "1"),
        Op(PrivacySettingKeys.TailoredExperiences, current, "1")
    ];

    private PrivacyOperation Op(string key, PrivacySnapshot current, string newValue) => new()
    {
        SettingKey = key,
        DisplayName = PrivacyCatalog.DisplayName(key),
        CurrentValue = GetCanonical(key, current),
        NewValue = newValue,
        RequiresAdmin = PrivacyCatalog.RequiresAdmin(key),
        SideEffectWarning = PrivacyCatalog.SideEffect(key)
    };

    private static string? GetCanonical(string key, PrivacySnapshot snapshot) => key switch
    {
        PrivacySettingKeys.TelemetryLevel => ((int)snapshot.TelemetryLevel).ToString(),
        PrivacySettingKeys.DiagTrack => snapshot.DiagTrack.CanonicalValue,
        PrivacySettingKeys.DmwAppPush => snapshot.DmwAppPush.CanonicalValue,
        PrivacySettingKeys.AdvertisingId => snapshot.AdvertisingIdEnabled ? "1" : "0",
        PrivacySettingKeys.ActivityHistory => snapshot.ActivityHistoryEnabled ? "1" : "0",
        PrivacySettingKeys.Cortana => snapshot.CortanaEnabled ? "1" : "0",
        PrivacySettingKeys.Copilot => snapshot.CopilotEnabled ? "1" : "0",
        PrivacySettingKeys.Feedback => snapshot.FeedbackEnabled ? "1" : "0",
        PrivacySettingKeys.TailoredExperiences => snapshot.TailoredExperiencesEnabled ? "1" : "0",
        _ => null
    };

    private static string DefaultCanonical(string key) => key switch
    {
        PrivacySettingKeys.TelemetryLevel => "3",
        PrivacySettingKeys.DiagTrack => DesiredServiceState.RunningAutomatic.ToString(),
        PrivacySettingKeys.DmwAppPush => DesiredServiceState.RunningAutomatic.ToString(),
        _ => "1"
    };

    private static string NormalizeComparable(string key, string? value)
    {
        if (value is null)
        {
            return DefaultCanonical(key);
        }

        if (key is PrivacySettingKeys.DiagTrack or PrivacySettingKeys.DmwAppPush)
        {
            return ParseDesiredService(value).ToString();
        }

        return IsOn(value) ? "1" : (value is "0" or "1" or "2" or "3" ? value : "0");
    }

    private static DesiredServiceState ParseDesiredService(string value)
    {
        if (Enum.TryParse<DesiredServiceState>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return PrivacyCatalog.ParseServiceState(value);
    }

    private static bool IsOn(string? value) =>
        value is "1" or "true" or "True" or "On" or "RunningAutomatic";

    private static TelemetryLevel ClampTelemetry(int raw) => raw switch
    {
        <= 0 => TelemetryLevel.Security,
        1 => TelemetryLevel.Basic,
        2 => TelemetryLevel.Enhanced,
        _ => TelemetryLevel.Full
    };

    private static int ComputeScore(PrivacySnapshot snapshot)
    {
        var points = 0;
        points += snapshot.TelemetryLevel switch
        {
            TelemetryLevel.Security => 20,
            TelemetryLevel.Basic => 16,
            TelemetryLevel.Enhanced => 8,
            _ => 0
        };
        points += PrivacyCatalog.IsServiceRunning(snapshot.DiagTrack) ? 0 : 12;
        points += PrivacyCatalog.IsServiceRunning(snapshot.DmwAppPush) ? 0 : 8;
        points += snapshot.AdvertisingIdEnabled ? 0 : 12;
        points += snapshot.ActivityHistoryEnabled ? 0 : 12;
        points += snapshot.CortanaEnabled ? 0 : 8;
        points += snapshot.CopilotEnabled ? 0 : 8;
        points += snapshot.FeedbackEnabled ? 0 : 10;
        points += snapshot.TailoredExperiencesEnabled ? 0 : 10;
        return Math.Clamp(points, 0, 100);
    }

    private static string L(string key) => LocalizationService.Current.Get(key);

    private static string L(string key, params object[] args) => LocalizationService.Current.Get(key, args);
}
