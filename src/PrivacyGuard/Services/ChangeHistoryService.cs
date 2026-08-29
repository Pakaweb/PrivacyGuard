using Microsoft.Data.Sqlite;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class ChangeHistoryService : IChangeHistoryService
{
    private readonly ILogger<ChangeHistoryService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ChangeHistoryService(ILogger<ChangeHistoryService> logger)
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
            CREATE TABLE IF NOT EXISTS ChangeHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                SettingKey TEXT NOT NULL,
                SettingName TEXT NOT NULL,
                OldValue TEXT,
                NewValue TEXT,
                ProfileName TEXT,
                RestorePointId INTEGER,
                IsReverted INTEGER NOT NULL DEFAULT 0,
                Error TEXT
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChangeRecord>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Timestamp, SettingKey, SettingName, OldValue, NewValue, ProfileName, RestorePointId, IsReverted, Error
            FROM ChangeHistory
            ORDER BY Id DESC
            LIMIT $take;
            """;
        command.Parameters.AddWithValue("$take", take);

        var results = new List<ChangeRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadRecord(reader));
        }

        return results;
    }

    public async Task<long> InsertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO ChangeHistory (Timestamp, SettingKey, SettingName, OldValue, NewValue, ProfileName, RestorePointId, IsReverted, Error)
                VALUES ($ts, $key, $name, $old, $new, $profile, $restore, $reverted, $error);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$ts", record.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$key", record.SettingKey);
            command.Parameters.AddWithValue("$name", record.SettingName);
            command.Parameters.AddWithValue("$old", (object?)record.OldValue ?? DBNull.Value);
            command.Parameters.AddWithValue("$new", (object?)record.NewValue ?? DBNull.Value);
            command.Parameters.AddWithValue("$profile", (object?)record.ProfileName ?? DBNull.Value);
            command.Parameters.AddWithValue("$restore", (object?)record.RestorePointId ?? DBNull.Value);
            command.Parameters.AddWithValue("$reverted", record.IsReverted ? 1 : 0);
            command.Parameters.AddWithValue("$error", (object?)record.Error ?? DBNull.Value);

            var id = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert change history row.");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkRevertedAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ChangeHistory SET IsReverted = 1 WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
                command.CommandText = "DELETE FROM ChangeHistory WHERE Id = $id;";
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
            command.CommandText = "DELETE FROM ChangeHistory;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string GetConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = AppPaths.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    private static ChangeRecord ReadRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Timestamp = DateTimeOffset.Parse(reader.GetString(1)),
        SettingKey = reader.GetString(2),
        SettingName = reader.GetString(3),
        OldValue = reader.IsDBNull(4) ? null : reader.GetString(4),
        NewValue = reader.IsDBNull(5) ? null : reader.GetString(5),
        ProfileName = reader.IsDBNull(6) ? null : reader.GetString(6),
        RestorePointId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
        IsReverted = reader.GetInt32(8) == 1,
        Error = reader.IsDBNull(9) ? null : reader.GetString(9)
    };
}
