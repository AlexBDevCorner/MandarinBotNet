using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DiscordBot.FantasyPremierLeague.Historical;

public sealed class SqliteFplStatisticsStore : IFplStatisticsStore
{
    private const int CommandTimeoutSeconds = 30;
    private readonly string _connectionString;

    public SqliteFplStatisticsStore(string databasePath)
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

    public bool IsSnapshotStored(string season, int eventId)
    {
        ValidateSeasonAndEvent(season, eventId);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT EXISTS (
                SELECT 1
                FROM fpl_gameweek_snapshots
                WHERE season = $season
                  AND event_id = $event_id
            );
            """;
        command.Parameters.AddWithValue("$season", season);
        command.Parameters.AddWithValue("$event_id", eventId);

        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }

    public void SaveSnapshot(FplGameweekSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            DeleteSnapshot(connection, transaction, snapshot.Season, snapshot.EventId);
            InsertSnapshot(connection, transaction, snapshot);
            InsertManagers(connection, transaction, snapshot);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public FplGameweekSnapshot? GetSnapshot(string season, int eventId)
    {
        return GetSnapshots(season, eventId).FirstOrDefault();
    }

    public IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
        string season,
        int? eventId = null,
        int? managerEntryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ValidateOptionalPositiveNumber(eventId, nameof(eventId));
        ValidateOptionalPositiveNumber(managerEntryId, nameof(managerEntryId));

        using var connection = OpenConnection();
        var metadata = ReadSnapshotMetadata(connection, season, eventId, managerEntryId);

        return metadata
            .Select(item => new FplGameweekSnapshot(
                item.Season,
                item.EventId,
                item.DeadlineUtc,
                item.StandingsUpdatedAtUtc,
                item.CapturedAtUtc,
                ReadManagers(connection, item.Season, item.EventId, managerEntryId)))
            .ToList();
    }

    private static void DeleteSnapshot(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string season,
        int eventId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            DELETE FROM fpl_gameweek_snapshots
            WHERE season = $season
              AND event_id = $event_id;
            """;
        command.Parameters.AddWithValue("$season", season);
        command.Parameters.AddWithValue("$event_id", eventId);
        command.ExecuteNonQuery();
    }

    private static void InsertSnapshot(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplGameweekSnapshot snapshot)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            INSERT INTO fpl_gameweek_snapshots (
                season,
                event_id,
                deadline_utc,
                standings_updated_at_utc,
                captured_at_utc
            )
            VALUES (
                $season,
                $event_id,
                $deadline_utc,
                $standings_updated_at_utc,
                $captured_at_utc
            );
            """;
        command.Parameters.AddWithValue("$season", snapshot.Season);
        command.Parameters.AddWithValue("$event_id", snapshot.EventId);
        command.Parameters.AddWithValue(
            "$deadline_utc",
            FormatTimestamp(snapshot.DeadlineUtc));
        command.Parameters.AddWithValue(
            "$standings_updated_at_utc",
            FormatTimestamp(snapshot.StandingsUpdatedAtUtc));
        command.Parameters.AddWithValue(
            "$captured_at_utc",
            FormatTimestamp(snapshot.CapturedAtUtc));
        command.ExecuteNonQuery();
    }

    private static void InsertManagers(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplGameweekSnapshot snapshot)
    {
        using var managerCommand = connection.CreateCommand();
        managerCommand.Transaction = transaction;
        managerCommand.CommandTimeout = CommandTimeoutSeconds;
        managerCommand.CommandText =
            """
            INSERT INTO fpl_manager_gameweek_stats (
                season,
                event_id,
                entry_id,
                entry_name,
                manager_name,
                event_score,
                total_score,
                rank,
                last_rank,
                rank_change,
                bench_points,
                transfer_cost
            )
            VALUES (
                $season,
                $event_id,
                $entry_id,
                $entry_name,
                $manager_name,
                $event_score,
                $total_score,
                $rank,
                $last_rank,
                $rank_change,
                $bench_points,
                $transfer_cost
            );
            """;
        managerCommand.Parameters.AddWithValue("$season", snapshot.Season);
        managerCommand.Parameters.AddWithValue("$event_id", snapshot.EventId);
        var entryIdParameter = managerCommand.Parameters.AddWithValue("$entry_id", 0);
        var entryNameParameter = managerCommand.Parameters.AddWithValue("$entry_name", "");
        var managerNameParameter = managerCommand.Parameters.AddWithValue(
            "$manager_name",
            "");
        var eventScoreParameter = managerCommand.Parameters.AddWithValue("$event_score", 0);
        var totalScoreParameter = managerCommand.Parameters.AddWithValue("$total_score", 0);
        var rankParameter = managerCommand.Parameters.AddWithValue("$rank", 0);
        var lastRankParameter = managerCommand.Parameters.AddWithValue("$last_rank", 0);
        var rankChangeParameter = managerCommand.Parameters.AddWithValue("$rank_change", 0);
        var benchPointsParameter = managerCommand.Parameters.AddWithValue("$bench_points", 0);
        var transferCostParameter = managerCommand.Parameters.AddWithValue("$transfer_cost", 0);

        using var lineupCommand = connection.CreateCommand();
        lineupCommand.Transaction = transaction;
        lineupCommand.CommandTimeout = CommandTimeoutSeconds;
        lineupCommand.CommandText =
            """
            INSERT INTO fpl_lineup_picks (
                season,
                event_id,
                entry_id,
                player_id,
                player_name,
                position,
                multiplier,
                is_captain,
                is_vice_captain,
                points
            )
            VALUES (
                $season,
                $event_id,
                $entry_id,
                $player_id,
                $player_name,
                $position,
                $multiplier,
                $is_captain,
                $is_vice_captain,
                $points
            );
            """;
        lineupCommand.Parameters.AddWithValue("$season", snapshot.Season);
        lineupCommand.Parameters.AddWithValue("$event_id", snapshot.EventId);
        var lineupEntryIdParameter = lineupCommand.Parameters.AddWithValue("$entry_id", 0);
        var playerIdParameter = lineupCommand.Parameters.AddWithValue("$player_id", 0);
        var playerNameParameter = lineupCommand.Parameters.AddWithValue("$player_name", "");
        var positionParameter = lineupCommand.Parameters.AddWithValue("$position", 0);
        var multiplierParameter = lineupCommand.Parameters.AddWithValue("$multiplier", 0);
        var captainParameter = lineupCommand.Parameters.AddWithValue("$is_captain", 0);
        var viceCaptainParameter = lineupCommand.Parameters.AddWithValue(
            "$is_vice_captain",
            0);
        var pointsParameter = lineupCommand.Parameters.AddWithValue("$points", 0);

        foreach (var manager in snapshot.Managers)
        {
            entryIdParameter.Value = manager.EntryId;
            entryNameParameter.Value = manager.EntryName;
            managerNameParameter.Value = manager.ManagerName;
            eventScoreParameter.Value = manager.EventScore;
            totalScoreParameter.Value = manager.TotalScore;
            rankParameter.Value = manager.Rank;
            lastRankParameter.Value = manager.LastRank;
            rankChangeParameter.Value = manager.RankChange;
            benchPointsParameter.Value = manager.BenchPoints;
            transferCostParameter.Value = manager.TransferCost;
            managerCommand.ExecuteNonQuery();

            foreach (var pick in manager.Lineup)
            {
                lineupEntryIdParameter.Value = manager.EntryId;
                playerIdParameter.Value = pick.PlayerId;
                playerNameParameter.Value = pick.PlayerName;
                positionParameter.Value = pick.Position;
                multiplierParameter.Value = pick.Multiplier;
                captainParameter.Value = pick.IsCaptain ? 1 : 0;
                viceCaptainParameter.Value = pick.IsViceCaptain ? 1 : 0;
                pointsParameter.Value = pick.Points;
                lineupCommand.ExecuteNonQuery();
            }
        }
    }

    private static List<SnapshotMetadata> ReadSnapshotMetadata(
        SqliteConnection connection,
        string season,
        int? eventId,
        int? managerEntryId)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;

        var predicates = new List<string> { "snapshot.season = $season" };
        command.Parameters.AddWithValue("$season", season);

        if (eventId is not null)
        {
            predicates.Add("snapshot.event_id = $event_id");
            command.Parameters.AddWithValue("$event_id", eventId.Value);
        }

        if (managerEntryId is not null)
        {
            predicates.Add(
                "EXISTS (" +
                "SELECT 1 FROM fpl_manager_gameweek_stats manager " +
                "WHERE manager.season = snapshot.season " +
                "AND manager.event_id = snapshot.event_id " +
                "AND manager.entry_id = $manager_entry_id)");
            command.Parameters.AddWithValue("$manager_entry_id", managerEntryId.Value);
        }

        command.CommandText = $"""
            SELECT snapshot.season,
                   snapshot.event_id,
                   snapshot.deadline_utc,
                   snapshot.standings_updated_at_utc,
                   snapshot.captured_at_utc
            FROM fpl_gameweek_snapshots snapshot
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY snapshot.event_id;
            """;

        var metadata = new List<SnapshotMetadata>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            metadata.Add(new SnapshotMetadata(
                reader.GetString(0),
                Convert.ToInt32(reader.GetInt64(1)),
                ParseTimestamp(reader.GetString(2)),
                ParseTimestamp(reader.GetString(3)),
                ParseTimestamp(reader.GetString(4))));
        }

        return metadata;
    }

    private static List<FplManagerGameweekStatistics> ReadManagers(
        SqliteConnection connection,
        string season,
        int eventId,
        int? managerEntryId)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        var managerPredicate = managerEntryId is null
            ? string.Empty
            : "AND entry_id = $manager_entry_id";
        command.CommandText = $"""
            SELECT entry_id,
                   entry_name,
                   manager_name,
                   event_score,
                   total_score,
                   rank,
                   last_rank,
                   rank_change,
                   bench_points,
                   transfer_cost
            FROM fpl_manager_gameweek_stats
            WHERE season = $season
              AND event_id = $event_id
              {managerPredicate}
            ORDER BY rank, entry_id;
            """;
        command.Parameters.AddWithValue("$season", season);
        command.Parameters.AddWithValue("$event_id", eventId);
        if (managerEntryId is not null)
        {
            command.Parameters.AddWithValue("$manager_entry_id", managerEntryId.Value);
        }

        var managers = new List<FplManagerGameweekStatistics>();
        var picks = ReadPicks(connection, season, eventId, managerEntryId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var entryId = Convert.ToInt32(reader.GetInt64(0));
            managers.Add(new FplManagerGameweekStatistics(
                entryId,
                reader.GetString(1),
                reader.GetString(2),
                Convert.ToInt32(reader.GetInt64(3)),
                Convert.ToInt32(reader.GetInt64(4)),
                Convert.ToInt32(reader.GetInt64(5)),
                Convert.ToInt32(reader.GetInt64(6)),
                Convert.ToInt32(reader.GetInt64(7)),
                Convert.ToInt32(reader.GetInt64(8)),
                picks.GetValueOrDefault(entryId) ?? [])
            {
                TransferCost = Convert.ToInt32(reader.GetInt64(9))
            });
        }

        return managers;
    }

    private static Dictionary<int, List<FplLineupPick>> ReadPicks(
        SqliteConnection connection,
        string season,
        int eventId,
        int? managerEntryId)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        var managerPredicate = managerEntryId is null
            ? string.Empty
            : "AND entry_id = $manager_entry_id";
        command.CommandText = $"""
            SELECT entry_id,
                   player_id,
                   player_name,
                   position,
                   multiplier,
                   is_captain,
                   is_vice_captain,
                   points
            FROM fpl_lineup_picks
            WHERE season = $season
              AND event_id = $event_id
              {managerPredicate}
            ORDER BY entry_id, position, player_id;
            """;
        command.Parameters.AddWithValue("$season", season);
        command.Parameters.AddWithValue("$event_id", eventId);
        if (managerEntryId is not null)
        {
            command.Parameters.AddWithValue("$manager_entry_id", managerEntryId.Value);
        }

        var picks = new Dictionary<int, List<FplLineupPick>>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var entryId = Convert.ToInt32(reader.GetInt64(0));
            if (!picks.TryGetValue(entryId, out var entryPicks))
            {
                entryPicks = [];
                picks.Add(entryId, entryPicks);
            }

            entryPicks.Add(new FplLineupPick(
                Convert.ToInt32(reader.GetInt64(1)),
                reader.GetString(2),
                Convert.ToInt32(reader.GetInt64(3)),
                Convert.ToInt32(reader.GetInt64(4)),
                Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture) == 1,
                Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture) == 1,
                Convert.ToInt32(reader.GetInt64(7))));
        }

        return picks;
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

        using (var snapshotCommand = connection.CreateCommand())
        {
            snapshotCommand.CommandTimeout = CommandTimeoutSeconds;
            snapshotCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS fpl_gameweek_snapshots (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    deadline_utc TEXT NOT NULL,
                    standings_updated_at_utc TEXT NOT NULL,
                    captured_at_utc TEXT NOT NULL,
                    PRIMARY KEY (season, event_id)
                ) WITHOUT ROWID;
                """;
            snapshotCommand.ExecuteNonQuery();
        }

        using (var managerCommand = connection.CreateCommand())
        {
            managerCommand.CommandTimeout = CommandTimeoutSeconds;
            managerCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS fpl_manager_gameweek_stats (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    entry_name TEXT NOT NULL,
                    manager_name TEXT NOT NULL,
                    event_score INTEGER NOT NULL,
                    total_score INTEGER NOT NULL,
                    rank INTEGER NOT NULL,
                    last_rank INTEGER NOT NULL,
                    rank_change INTEGER NOT NULL,
                    bench_points INTEGER NOT NULL,
                    PRIMARY KEY (season, event_id, entry_id),
                    FOREIGN KEY (season, event_id)
                        REFERENCES fpl_gameweek_snapshots (season, event_id)
                        ON DELETE CASCADE
                ) WITHOUT ROWID;
                """;
            managerCommand.ExecuteNonQuery();
        }

        EnsureManagerTransferCostColumn(connection);

        using (var managerIndexCommand = connection.CreateCommand())
        {
            managerIndexCommand.CommandTimeout = CommandTimeoutSeconds;
            managerIndexCommand.CommandText =
                """
                CREATE INDEX IF NOT EXISTS ix_fpl_manager_stats_entry
                ON fpl_manager_gameweek_stats (season, entry_id, event_id);
                """;
            managerIndexCommand.ExecuteNonQuery();
        }

        using var lineupCommand = connection.CreateCommand();
        lineupCommand.CommandTimeout = CommandTimeoutSeconds;
        lineupCommand.CommandText =
            """
            CREATE TABLE IF NOT EXISTS fpl_lineup_picks (
                season TEXT NOT NULL,
                event_id INTEGER NOT NULL,
                entry_id INTEGER NOT NULL,
                player_id INTEGER NOT NULL,
                player_name TEXT NOT NULL,
                position INTEGER NOT NULL,
                multiplier INTEGER NOT NULL,
                is_captain INTEGER NOT NULL,
                is_vice_captain INTEGER NOT NULL,
                points INTEGER NOT NULL,
                PRIMARY KEY (season, event_id, entry_id, player_id),
                FOREIGN KEY (season, event_id, entry_id)
                    REFERENCES fpl_manager_gameweek_stats (season, event_id, entry_id)
                    ON DELETE CASCADE
            ) WITHOUT ROWID;
            """;
        lineupCommand.ExecuteNonQuery();
    }

    private static void EnsureManagerTransferCostColumn(SqliteConnection connection)
    {
        var hasTransferCostColumn = false;
        using (var infoCommand = connection.CreateCommand())
        {
            infoCommand.CommandTimeout = CommandTimeoutSeconds;
            infoCommand.CommandText = "PRAGMA table_info(fpl_manager_gameweek_stats);";
            using var reader = infoCommand.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(
                        reader.GetString(1),
                        "transfer_cost",
                        StringComparison.OrdinalIgnoreCase))
                {
                    hasTransferCostColumn = true;
                    break;
                }
            }
        }

        if (hasTransferCostColumn)
        {
            return;
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandTimeout = CommandTimeoutSeconds;
        alterCommand.CommandText =
            "ALTER TABLE fpl_manager_gameweek_stats " +
            "ADD COLUMN transfer_cost INTEGER NOT NULL DEFAULT 0;";
        alterCommand.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString)
        {
            DefaultTimeout = CommandTimeoutSeconds
        };
        connection.Open();

        using var foreignKeysCommand = connection.CreateCommand();
        foreignKeysCommand.CommandTimeout = CommandTimeoutSeconds;
        foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON;";
        foreignKeysCommand.ExecuteNonQuery();

        return connection;
    }

    private static void ValidateSnapshot(FplGameweekSnapshot snapshot)
    {
        ValidateSeasonAndEvent(snapshot.Season, snapshot.EventId);
        ArgumentNullException.ThrowIfNull(snapshot.Managers);
        if (snapshot.Managers.Count == 0)
        {
            throw new ArgumentException(
                "The snapshot must contain at least one manager.",
                nameof(snapshot));
        }

        var entryIds = new HashSet<int>();
        foreach (var manager in snapshot.Managers)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(manager.EntryId);
            ArgumentException.ThrowIfNullOrWhiteSpace(manager.EntryName);
            ArgumentException.ThrowIfNullOrWhiteSpace(manager.ManagerName);
            ArgumentOutOfRangeException.ThrowIfNegative(manager.TransferCost);
            ArgumentNullException.ThrowIfNull(manager.Lineup);
            if (manager.Lineup.Count == 0)
            {
                throw new ArgumentException(
                    $"Manager entry {manager.EntryId} must contain at least one lineup pick.",
                    nameof(snapshot));
            }

            if (!entryIds.Add(manager.EntryId))
            {
                throw new ArgumentException(
                    $"The snapshot contains duplicate manager entry {manager.EntryId}.",
                    nameof(snapshot));
            }

            var playerIds = new HashSet<int>();
            foreach (var pick in manager.Lineup)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pick.PlayerId);
                ArgumentException.ThrowIfNullOrWhiteSpace(pick.PlayerName);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pick.Position);
                ArgumentOutOfRangeException.ThrowIfNegative(pick.Multiplier);

                if (!playerIds.Add(pick.PlayerId))
                {
                    throw new ArgumentException(
                        $"The snapshot contains duplicate player {pick.PlayerId} for manager " +
                        $"entry {manager.EntryId}.",
                        nameof(snapshot));
                }
            }
        }
    }

    private static void ValidateSeasonAndEvent(string season, int eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
    }

    private static void ValidateOptionalPositiveNumber(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be positive.");
        }
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    private sealed record SnapshotMetadata(
        string Season,
        int EventId,
        DateTimeOffset DeadlineUtc,
        DateTimeOffset StandingsUpdatedAtUtc,
        DateTimeOffset CapturedAtUtc);
}
