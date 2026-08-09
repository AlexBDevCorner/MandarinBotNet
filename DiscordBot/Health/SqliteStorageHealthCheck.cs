using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DiscordBot.Health;

public sealed class SqliteStorageHealthCheck(string databasePath) : IHealthCheck
{
    private const int CommandTimeoutSeconds = 2;
    private readonly string _connectionString = CreateConnectionString(databasePath);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString)
            {
                DefaultTimeout = CommandTimeoutSeconds
            };
            await connection.OpenAsync(cancellationToken);

            await using (var integrityCommand = connection.CreateCommand())
            {
                integrityCommand.CommandTimeout = CommandTimeoutSeconds;
                integrityCommand.CommandText = "PRAGMA quick_check;";
                var result = await integrityCommand.ExecuteScalarAsync(cancellationToken);
                if (!string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return HealthCheckResult.Unhealthy(
                        $"SQLite quick_check returned '{result ?? "no result"}'.");
                }
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var writeCommand = connection.CreateCommand();
            writeCommand.Transaction = (SqliteTransaction)transaction;
            writeCommand.CommandTimeout = CommandTimeoutSeconds;
            writeCommand.CommandText =
                "CREATE TABLE IF NOT EXISTS __health_probe (probe_id INTEGER NOT NULL);";
            await writeCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);

            return HealthCheckResult.Healthy("SQLite storage is readable and writable.");
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy(
                "SQLite storage is unavailable.",
                exception);
        }
    }

    private static string CreateConnectionString(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString();
    }
}
