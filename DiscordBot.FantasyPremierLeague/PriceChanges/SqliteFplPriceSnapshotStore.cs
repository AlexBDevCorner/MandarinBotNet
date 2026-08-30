using System.Globalization;
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

    public FplPriceChangeBatch? GetLatestChanges()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT
                changes.player_id,
                changes.player_name,
                changes.previous_cost,
                changes.current_cost,
                COALESCE(metadata.checked_at_utc, changes.checked_at_utc),
                metadata.event_id
            FROM fpl_latest_price_changes AS changes
            LEFT JOIN fpl_latest_price_change_metadata AS metadata
                ON metadata.batch_id = 1
            ORDER BY changes.player_name COLLATE NOCASE, changes.player_id;
            """;

        var changes = new List<FplPlayerPriceChange>();
        DateTimeOffset? checkedAtUtc = null;
        int? eventId = null;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            changes.Add(new FplPlayerPriceChange(
                Convert.ToInt32(reader.GetInt64(0)),
                reader.GetString(1),
                Convert.ToInt32(reader.GetInt64(2)),
                Convert.ToInt32(reader.GetInt64(3))));
            checkedAtUtc ??= DateTimeOffset.Parse(
                reader.GetString(4),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            if (!reader.IsDBNull(5))
            {
                eventId ??= Convert.ToInt32(reader.GetInt64(5));
            }
        }

        return checkedAtUtc is null
            ? null
            : new FplPriceChangeBatch(checkedAtUtc.Value, eventId, changes);
    }

    public void SaveSnapshot(IReadOnlyDictionary<int, int> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        SaveSnapshot(prices, latestChanges: null);
    }

    public void SaveSnapshot(FplPriceChangeCheck priceCheck)
    {
        ArgumentNullException.ThrowIfNull(priceCheck);

        SaveSnapshot(
            priceCheck.CurrentPrices,
            priceCheck.Changes.Count == 0
                ? null
                : new FplPriceChangeBatch(
                    priceCheck.CheckedAtUtc,
                    priceCheck.CurrentEventId,
                    priceCheck.Changes));
    }

    private void SaveSnapshot(
        IReadOnlyDictionary<int, int> prices,
        FplPriceChangeBatch? latestChanges)
    {

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

            if (latestChanges is not null)
            {
                SaveLatestChanges(connection, transaction, latestChanges);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void SaveLatestChanges(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplPriceChangeBatch batch)
    {
        using (var metadataCommand = connection.CreateCommand())
        {
            metadataCommand.Transaction = transaction;
            metadataCommand.CommandTimeout = CommandTimeoutSeconds;
            metadataCommand.CommandText =
                """
                INSERT OR REPLACE INTO fpl_latest_price_change_metadata (
                    batch_id,
                    checked_at_utc,
                    event_id
                )
                VALUES (1, $checked_at_utc, $event_id);
                """;
            metadataCommand.Parameters.AddWithValue(
                "$checked_at_utc",
                batch.CheckedAtUtc
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture));
            metadataCommand.Parameters.AddWithValue(
                "$event_id",
                batch.EventId is null ? DBNull.Value : batch.EventId.Value);
            metadataCommand.ExecuteNonQuery();
        }

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandTimeout = CommandTimeoutSeconds;
            deleteCommand.CommandText = "DELETE FROM fpl_latest_price_changes;";
            deleteCommand.ExecuteNonQuery();
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandTimeout = CommandTimeoutSeconds;
        insertCommand.CommandText =
            """
            INSERT INTO fpl_latest_price_changes (
                player_id,
                player_name,
                previous_cost,
                current_cost,
                checked_at_utc
            )
            VALUES (
                $player_id,
                $player_name,
                $previous_cost,
                $current_cost,
                $checked_at_utc
            );
            """;
        var playerId = insertCommand.Parameters.AddWithValue("$player_id", 0);
        var playerName = insertCommand.Parameters.AddWithValue(
            "$player_name",
            string.Empty);
        var previousCost = insertCommand.Parameters.AddWithValue(
            "$previous_cost",
            0);
        var currentCost = insertCommand.Parameters.AddWithValue(
            "$current_cost",
            0);
        var checkedAtUtc = insertCommand.Parameters.AddWithValue(
            "$checked_at_utc",
            batch.CheckedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        foreach (var change in batch.Changes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.PlayerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(change.PlayerName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.PreviousCost);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.CurrentCost);

            playerId.Value = change.PlayerId;
            playerName.Value = change.PlayerName;
            previousCost.Value = change.PreviousCost;
            currentCost.Value = change.CurrentCost;
            checkedAtUtc.Value = batch.CheckedAtUtc
                .ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture);
            insertCommand.ExecuteNonQuery();
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

            CREATE TABLE IF NOT EXISTS fpl_latest_price_changes (
                player_id INTEGER NOT NULL PRIMARY KEY,
                player_name TEXT NOT NULL,
                previous_cost INTEGER NOT NULL,
                current_cost INTEGER NOT NULL,
                checked_at_utc TEXT NOT NULL
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS fpl_latest_price_change_metadata (
                batch_id INTEGER NOT NULL PRIMARY KEY CHECK (batch_id = 1),
                checked_at_utc TEXT NOT NULL,
                event_id INTEGER NULL CHECK (event_id IS NULL OR event_id > 0)
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
