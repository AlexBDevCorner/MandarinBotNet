using Microsoft.Data.Sqlite;

namespace DiscordBot.FantasyPremierLeague.PriceChanges;

public sealed class SqliteFplPriceSnapshotStore : IFplPriceSnapshotStore
{
    private const int CommandTimeoutSeconds = 30;
    private readonly string _connectionString;

    public SqliteFplPriceSnapshotStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullDatabasePath = Path.GetFullPath(databasePath);
        var databaseDirectory = Path.GetDirectoryName(fullDatabasePath)
            ?? throw new ArgumentException(
                "The database path must include a directory.",
                nameof(databasePath));

        Directory.CreateDirectory(databaseDirectory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString();

        InitializeDatabase();
    }

    public FplPriceSnapshot? GetSnapshot()
    {
        using var connection = OpenConnection();

        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandTimeout = CommandTimeoutSeconds;
        versionCommand.CommandText =
            """
            SELECT snapshot_version
            FROM fpl_price_snapshot_metadata
            WHERE snapshot_id = 1;
            """;
        var versionValue = versionCommand.ExecuteScalar();
        if (versionValue is null || versionValue is DBNull)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT player_id, now_cost
            FROM fpl_player_price_snapshot;
            """;

        var prices = new Dictionary<int, int>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            prices.Add(
                Convert.ToInt32(reader.GetInt64(0)),
                Convert.ToInt32(reader.GetInt64(1)));
        }

        return new FplPriceSnapshot(Convert.ToInt64(versionValue), prices);
    }

    public void SaveSnapshot(IReadOnlyDictionary<int, int> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            using var versionCommand = connection.CreateCommand();
            versionCommand.Transaction = transaction;
            versionCommand.CommandTimeout = CommandTimeoutSeconds;
            versionCommand.CommandText =
                """
                SELECT COALESCE(MAX(snapshot_version), 0)
                FROM fpl_price_snapshot_metadata;
                """;
            var nextVersion = checked(Convert.ToInt64(versionCommand.ExecuteScalar()) + 1);

            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandTimeout = CommandTimeoutSeconds;
                deleteCommand.CommandText =
                    "DELETE FROM fpl_player_price_snapshot;";
                deleteCommand.ExecuteNonQuery();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandTimeout = CommandTimeoutSeconds;
            insertCommand.CommandText =
                """
                INSERT INTO fpl_player_price_snapshot (player_id, now_cost)
                VALUES ($player_id, $now_cost);
                """;
            var playerIdParameter = insertCommand.Parameters.AddWithValue(
                "$player_id",
                0);
            var nowCostParameter = insertCommand.Parameters.AddWithValue(
                "$now_cost",
                0);

            foreach (var price in prices)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price.Key);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price.Value);
                playerIdParameter.Value = price.Key;
                nowCostParameter.Value = price.Value;
                insertCommand.ExecuteNonQuery();
            }

            using var metadataCommand = connection.CreateCommand();
            metadataCommand.Transaction = transaction;
            metadataCommand.CommandTimeout = CommandTimeoutSeconds;
            metadataCommand.CommandText =
                """
                INSERT OR REPLACE INTO fpl_price_snapshot_metadata (
                    snapshot_id,
                    snapshot_version
                )
                VALUES (1, $snapshot_version);
                """;
            metadataCommand.Parameters.AddWithValue("$snapshot_version", nextVersion);
            metadataCommand.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();

        using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandTimeout = CommandTimeoutSeconds;
            walCommand.CommandText = "PRAGMA journal_mode = WAL;";
            walCommand.ExecuteNonQuery();
        }

        using (var metadataCommand = connection.CreateCommand())
        {
            metadataCommand.CommandTimeout = CommandTimeoutSeconds;
            metadataCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS fpl_price_snapshot_metadata (
                    snapshot_id INTEGER NOT NULL PRIMARY KEY CHECK (snapshot_id = 1),
                    snapshot_version INTEGER NOT NULL
                ) WITHOUT ROWID;
                """;
            metadataCommand.ExecuteNonQuery();
        }

        using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandTimeout = CommandTimeoutSeconds;
        schemaCommand.CommandText =
            """
            CREATE TABLE IF NOT EXISTS fpl_player_price_snapshot (
                player_id INTEGER NOT NULL PRIMARY KEY,
                now_cost INTEGER NOT NULL
            ) WITHOUT ROWID;
            """;
        schemaCommand.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString)
        {
            DefaultTimeout = CommandTimeoutSeconds
        };
        connection.Open();
        return connection;
    }
}
