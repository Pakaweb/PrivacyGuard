using System.Text.Json;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        Current = new AppPreferences();
    }

    public AppPreferences Current { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(AppPaths.PreferencesPath))
            {
                Current = new AppPreferences();
                return;
            }

            await using var stream = File.OpenRead(AppPaths.PreferencesPath);
            var loaded = await JsonSerializer.DeserializeAsync<AppPreferences>(stream, JsonOptions.Default, cancellationToken);
            Current = loaded ?? new AppPreferences();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load preferences; using defaults.");
            Current = new AppPreferences();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            AppPaths.EnsureCreated();
            var json = JsonSerializer.Serialize(Current, JsonOptions.Default);
            await File.WriteAllTextAsync(AppPaths.PreferencesPath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save preferences.");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }
}
