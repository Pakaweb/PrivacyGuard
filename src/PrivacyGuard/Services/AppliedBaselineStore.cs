using System.Text.Json;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class AppliedBaselineStore : IAppliedBaselineStore
{
    private readonly ILogger<AppliedBaselineStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppliedBaselineStore(ILogger<AppliedBaselineStore> logger)
    {
        _logger = logger;
    }

    public async Task<AppliedBaseline?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(AppPaths.AppliedBaselinePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(AppPaths.AppliedBaselinePath);
            var loaded = await JsonSerializer.DeserializeAsync<AppliedBaseline>(stream, JsonOptions.Default, cancellationToken);
            if (loaded is null)
            {
                return null;
            }

            loaded.Values = new Dictionary<string, string>(loaded.Values, StringComparer.OrdinalIgnoreCase);
            return loaded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load applied baseline.");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppliedBaseline baseline, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            AppPaths.EnsureCreated();
            var json = JsonSerializer.Serialize(baseline, JsonOptions.Default);
            await File.WriteAllTextAsync(AppPaths.AppliedBaselinePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save applied baseline.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetLastProfileAsync(
        string profileKind,
        string? customId,
        string? profileTitle,
        CancellationToken cancellationToken = default)
    {
        var baseline = await LoadAsync(cancellationToken) ?? new AppliedBaseline();
        baseline.ProfileKind = profileKind;
        baseline.CustomId = customId;
        baseline.ProfileTitle = profileTitle;
        baseline.AppliedAt = DateTimeOffset.Now;
        await SaveAsync(baseline, cancellationToken);
    }

    public async Task MergeAsync(
        IEnumerable<PrivacyOperation> succeeded,
        string? profileKind,
        string? customId,
        string? profileTitle,
        CancellationToken cancellationToken = default)
    {
        var changes = succeeded.Where(op => !string.IsNullOrWhiteSpace(op.SettingKey)).ToList();
        if (changes.Count == 0 && string.IsNullOrWhiteSpace(profileKind))
        {
            return;
        }

        var baseline = await LoadAsync(cancellationToken) ?? new AppliedBaseline();
        foreach (var operation in changes)
        {
            baseline.Values[operation.SettingKey] = operation.NewValue;
        }

        if (!string.IsNullOrWhiteSpace(profileKind))
        {
            baseline.ProfileKind = profileKind;
            baseline.CustomId = customId;
            baseline.ProfileTitle = profileTitle;
        }

        baseline.AppliedAt = DateTimeOffset.Now;
        baseline.DismissedDriftFingerprint = null;
        await SaveAsync(baseline, cancellationToken);
    }
}
