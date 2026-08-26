using System.Text.Json;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class ProfileTransferService : IProfileTransferService
{
    private readonly ICustomProfileStore _profiles;
    private readonly IChangeHistoryService _history;
    private readonly IBackupService _backup;
    private readonly ILocalizationService _loc;
    private readonly ILogger<ProfileTransferService> _logger;

    public ProfileTransferService(
        ICustomProfileStore profiles,
        IChangeHistoryService history,
        IBackupService backup,
        ILocalizationService localization,
        ILogger<ProfileTransferService> logger)
    {
        _profiles = profiles;
        _history = history;
        _backup = backup;
        _loc = localization;
        _logger = logger;
    }

    public async Task ExportAsync(ExportOptions options, string path, CancellationToken cancellationToken = default)
    {
        var package = new PrivacyGuardExport
        {
            Version = PrivacyGuardExport.CurrentVersion,
            ExportedAt = DateTimeOffset.Now,
            CustomProfiles = (await _profiles.GetAllAsync(cancellationToken)).ToList()
        };

        if (options.IncludeHistoryAndRestorePoints)
        {
            package.History = (await _history.GetRecentAsync(10_000, cancellationToken)).ToList();
            package.RestorePoints = (await _backup.GetRecentAsync(1_000, cancellationToken)).ToList();
        }

        var json = JsonSerializer.Serialize(package, JsonOptions.Default);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public async Task<PrivacyGuardExport> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var package = await JsonSerializer.DeserializeAsync<PrivacyGuardExport>(stream, JsonOptions.Default, cancellationToken)
            ?? throw new InvalidOperationException("The file is empty.");

        if (!string.Equals(package.App, "PrivacyGuard", StringComparison.OrdinalIgnoreCase)
            && package.CustomProfiles.Count == 0
            && package.History is null
            && package.RestorePoints is null)
        {
            throw new InvalidOperationException("This file is not a PrivacyGuard export.");
        }

        if (package.Version is < 1 or > PrivacyGuardExport.CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported export version {package.Version}.");
        }

        Sanitize(package);
        return package;
    }

    public async Task<string> ImportAsync(
        PrivacyGuardExport package,
        ImportSelection selection,
        CancellationToken cancellationToken = default)
    {
        var importedProfiles = 0;
        var skippedSettings = 0;
        var importedHistory = 0;
        var importedRestore = 0;

        if (selection.CustomProfiles)
        {
            foreach (var profile in package.CustomProfiles)
            {
                var valid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in profile.Settings)
                {
                    if (PrivacyCatalog.IsValidCanonical(pair.Key, pair.Value))
                    {
                        valid[pair.Key] = pair.Value;
                    }
                    else
                    {
                        skippedSettings++;
                    }
                }

                if (valid.Count == 0 && profile.Settings.Count > 0)
                {
                    continue;
                }

                profile.Settings = valid;
                if (string.IsNullOrWhiteSpace(profile.Name))
                {
                    profile.Name = "Imported profile";
                }

                await _profiles.SaveAsync(profile, cancellationToken);
                importedProfiles++;
            }
        }

        if (selection.History && package.History is { Count: > 0 })
        {
            foreach (var record in package.History.OrderBy(item => item.Timestamp).ThenBy(item => item.Id))
            {
                if (!PrivacySettingKeys.All.Contains(record.SettingKey)
                    && record.SettingKey is not "WindowsReset")
                {
                    skippedSettings++;
                    continue;
                }

                await _history.InsertAsync(new ChangeRecord
                {
                    Timestamp = record.Timestamp == default ? DateTimeOffset.Now : record.Timestamp,
                    SettingKey = record.SettingKey,
                    SettingName = string.IsNullOrWhiteSpace(record.SettingName)
                        ? PrivacyCatalog.DisplayName(record.SettingKey)
                        : record.SettingName,
                    OldValue = record.OldValue,
                    NewValue = record.NewValue,
                    ProfileName = record.ProfileName,
                    RestorePointId = null,
                    Error = record.Error,
                    IsReverted = record.IsReverted
                }, cancellationToken);
                importedHistory++;
            }
        }

        if (selection.RestorePoints && package.RestorePoints is { Count: > 0 })
        {
            foreach (var point in package.RestorePoints.OrderBy(item => item.CreatedAt))
            {
                var settings = point.Settings
                    .Where(setting => PrivacySettingKeys.All.Contains(setting.SettingKey))
                    .ToList();
                skippedSettings += point.Settings.Count - settings.Count;
                if (settings.Count == 0)
                {
                    continue;
                }

                await _backup.CreateAsync(
                    string.IsNullOrWhiteSpace(point.Description)
                        ? "Imported restore point"
                        : $"Imported: {point.Description}",
                    settings,
                    cancellationToken);
                importedRestore++;
            }
        }

        _logger.LogInformation(
            "Imported {Profiles} profiles, {History} history rows, {Restore} restore points ({Skipped} skipped).",
            importedProfiles,
            importedHistory,
            importedRestore,
            skippedSettings);

        return _loc.Get(
            "profiles.importSuccess",
            importedProfiles,
            importedHistory,
            importedRestore,
            skippedSettings);
    }

    private static void Sanitize(PrivacyGuardExport package)
    {
        package.CustomProfiles ??= [];
        foreach (var profile in package.CustomProfiles)
        {
            profile.Settings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                profile.Id = Guid.NewGuid().ToString("N");
            }
        }
    }
}
