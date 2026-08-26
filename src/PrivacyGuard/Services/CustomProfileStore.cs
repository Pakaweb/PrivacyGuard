using System.Text.Json;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class CustomProfileStore : ICustomProfileStore
{
    private readonly ILogger<CustomProfileStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CustomProfileStore(ILogger<CustomProfileStore> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomProfileDocument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var documents = await ReadAsync(cancellationToken);
        return documents
            .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<CustomProfileDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var documents = await ReadAsync(cancellationToken);
        return documents.FirstOrDefault(profile =>
            string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(CustomProfileDocument profile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("Custom profile name is required.");
        }

        profile.Name = profile.Name.Trim();
        profile.UpdatedAt = DateTimeOffset.Now;
        if (profile.CreatedAt == default)
        {
            profile.CreatedAt = profile.UpdatedAt;
        }

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString("N");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var documents = await ReadUnlockedAsync(cancellationToken);
            var index = documents.FindIndex(existing =>
                string.Equals(existing.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                documents[index] = profile;
            }
            else
            {
                documents.Add(profile);
            }

            await WriteUnlockedAsync(documents, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var documents = await ReadUnlockedAsync(cancellationToken);
            documents.RemoveAll(profile =>
                string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
            await WriteUnlockedAsync(documents, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<CustomProfileDocument>> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadUnlockedAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<CustomProfileDocument>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(AppPaths.CustomProfilesPath))
            {
                return [];
            }

            await using var stream = File.OpenRead(AppPaths.CustomProfilesPath);
            var loaded = await JsonSerializer.DeserializeAsync<List<CustomProfileDocument>>(
                stream,
                JsonOptions.Default,
                cancellationToken);
            if (loaded is null)
            {
                return [];
            }

            foreach (var profile in loaded)
            {
                profile.Settings = new Dictionary<string, string>(
                    profile.Settings ?? [],
                    StringComparer.OrdinalIgnoreCase);
            }

            return loaded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read custom profiles; starting empty.");
            return [];
        }
    }

    private async Task WriteUnlockedAsync(List<CustomProfileDocument> documents, CancellationToken cancellationToken)
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(documents, JsonOptions.Default);
        await File.WriteAllTextAsync(AppPaths.CustomProfilesPath, json, cancellationToken);
    }
}
