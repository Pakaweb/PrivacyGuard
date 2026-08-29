using System.Text.Json;
using Microsoft.Data.Sqlite;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS RestorePoints (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedAt TEXT NOT NULL,
                Description TEXT NOT NULL,
                SnapshotJson TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<RestorePoint> CreateAsync(string description, IReadOnlyList<SettingSnapshot> settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var createdAt = DateTimeOffset.Now;
            var json = JsonSerializer.Serialize(settings, JsonOptions.Default);

            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO RestorePoints (CreatedAt, Description, SnapshotJson)
                VALUES ($ts, $desc, $json);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$ts", createdAt.ToString("O"));
            command.Parameters.AddWithValue("$desc", description);
            command.Parameters.AddWithValue("$json", json);

            var id = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
            _logger.LogInformation("Created restore point {Id}: {Description}", id, description);

            return new RestorePoint
            {
                Id = id,
                CreatedAt = createdAt,
                Description = description,
                Settings = settings
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RestorePoint>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, CreatedAt, Description, SnapshotJson
            FROM RestorePoints
            ORDER BY Id DESC
            LIMIT $take;
            """;
        command.Parameters.AddWithValue("$take", take);

        var results = new List<RestorePoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadRestorePoint(reader));
        }

        return results;
    }

    public async Task<RestorePoint?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, CreatedAt, Description, SnapshotJson
            FROM RestorePoints
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRestorePoint(reader);
    }

    public async Task DeleteAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            foreach (var id in ids)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = "DELETE FROM RestorePoints WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", id);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RestorePoints;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static RestorePoint ReadRestorePoint(SqliteDataReader reader)
    {
        var json = reader.GetString(3);
        var settings = JsonSerializer.Deserialize<List<SettingSnapshot>>(json, JsonOptions.Default) ?? [];

        return new RestorePoint
        {
            Id = reader.GetInt64(0),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(1)),
            Description = reader.GetString(2),
            Settings = settings
        };
    }

    private static string GetConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = AppPaths.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();
}
