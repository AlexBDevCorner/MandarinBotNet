using Microsoft.Data.Sqlite;

namespace DiscordBot.BenchWarming;

public sealed class SqliteBenchWarmingLeagueStore : IBenchWarmingLeagueStore
{
    private const int CommandTimeoutSeconds = 30;
    private readonly string _connectionString;

    public SqliteBenchWarmingLeagueStore(string databasePath)
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

    public bool IsRoundCalculated(string season, int eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT EXISTS (
                SELECT 1
                FROM bench_warming_rounds
                WHERE season = $season
                  AND event_id = $event_id
            );
            """;
        command.Parameters.AddWithValue("$season", season);
        command.Parameters.AddWithValue("$event_id", eventId);

        return Convert.ToInt64(command.ExecuteScalar()) == 1;
    }

    public void SaveRound(
        string season,
        int eventId,
        IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
        DateTimeOffset calculatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        ArgumentNullException.ThrowIfNull(benchPoints);

        // The legacy player-only path cannot know whether the full manager/GW
        // snapshot is present (e.g. a Bench Boost manager stores no bench players),
        // so it must never mark the round as complete. The dashboard filters out
        // incomplete rounds, preventing entries from being silently dropped.
        var entryRounds = benchPoints
            .GroupBy(benchPoint => benchPoint.EntryId)
            .Select(group => new BenchWarmingEntryRoundStanding(
                eventId,
                group.Key,
                group.First().EntryName,
                group.Sum(benchPoint => benchPoint.Points)))
            .ToArray();

        SaveRound(season, eventId, entryRounds, benchPoints, calculatedAtUtc, complete: false);
    }

    public void SaveRound(
        string season,
        int eventId,
        IReadOnlyList<BenchWarmingEntryRoundStanding> entryRounds,
        IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
        DateTimeOffset calculatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        ArgumentNullException.ThrowIfNull(entryRounds);
        ArgumentNullException.ThrowIfNull(benchPoints);

        SaveRound(season, eventId, entryRounds, benchPoints, calculatedAtUtc, complete: true);
    }

    private void SaveRound(
        string season,
        int eventId,
        IReadOnlyList<BenchWarmingEntryRoundStanding> entryRounds,
        IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
        DateTimeOffset calculatedAtUtc,
        bool complete)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            using (var roundCommand = connection.CreateCommand())
            {
                roundCommand.Transaction = transaction;
                roundCommand.CommandTimeout = CommandTimeoutSeconds;
                roundCommand.CommandText =
                    """
                    INSERT INTO bench_warming_rounds (
                        season,
                        event_id,
                        calculated_at_utc,
                        entry_rounds_complete
                    )
                    VALUES (
                        $season,
                        $event_id,
                        $calculated_at_utc,
                        $entry_rounds_complete
                    )
                    ON CONFLICT (season, event_id) DO UPDATE SET
                        calculated_at_utc = excluded.calculated_at_utc,
                        entry_rounds_complete = MAX(entry_rounds_complete, excluded.entry_rounds_complete);
                    """;
                roundCommand.Parameters.AddWithValue("$season", season);
                roundCommand.Parameters.AddWithValue("$event_id", eventId);
                roundCommand.Parameters.AddWithValue(
                    "$calculated_at_utc",
                    calculatedAtUtc.UtcDateTime.ToString("O"));
                roundCommand.Parameters.AddWithValue(
                    "$entry_rounds_complete",
                    complete ? 1 : 0);
                roundCommand.ExecuteNonQuery();
            }

            foreach (var entryRound in entryRounds)
            {
                using var entryRoundCommand = connection.CreateCommand();
                entryRoundCommand.Transaction = transaction;
                entryRoundCommand.CommandTimeout = CommandTimeoutSeconds;
                entryRoundCommand.CommandText =
                    """
                    INSERT OR REPLACE INTO bench_warming_entry_rounds (
                        season,
                        event_id,
                        entry_id,
                        entry_name,
                        points,
                        active_chip
                    )
                    VALUES (
                        $season,
                        $event_id,
                        $entry_id,
                        $entry_name,
                        $points,
                        $active_chip
                    );
                    """;
                entryRoundCommand.Parameters.AddWithValue("$season", season);
                entryRoundCommand.Parameters.AddWithValue("$event_id", eventId);
                entryRoundCommand.Parameters.AddWithValue("$entry_id", entryRound.EntryId);
                entryRoundCommand.Parameters.AddWithValue("$entry_name", entryRound.EntryName);
                entryRoundCommand.Parameters.AddWithValue("$points", entryRound.Points);
                entryRoundCommand.Parameters.AddWithValue(
                    "$active_chip",
                    (object?)entryRound.ActiveChip ?? DBNull.Value);
                entryRoundCommand.ExecuteNonQuery();
            }

            foreach (var benchPoint in benchPoints)
            {
                using var pointsCommand = connection.CreateCommand();
                pointsCommand.Transaction = transaction;
                pointsCommand.CommandTimeout = CommandTimeoutSeconds;
                pointsCommand.CommandText =
                    """
                    INSERT OR REPLACE INTO bench_warming_bench_points (
                        season,
                        event_id,
                        entry_id,
                        entry_name,
                        player_id,
                        player_web_name,
                        points
                    )
                    VALUES (
                        $season,
                        $event_id,
                        $entry_id,
                        $entry_name,
                        $player_id,
                        $player_web_name,
                        $points
                    );
                    """;
                pointsCommand.Parameters.AddWithValue("$season", season);
                pointsCommand.Parameters.AddWithValue("$event_id", eventId);
                pointsCommand.Parameters.AddWithValue("$entry_id", benchPoint.EntryId);
                pointsCommand.Parameters.AddWithValue("$entry_name", benchPoint.EntryName);
                pointsCommand.Parameters.AddWithValue("$player_id", benchPoint.PlayerId);
                pointsCommand.Parameters.AddWithValue(
                    "$player_web_name",
                    benchPoint.PlayerWebName);
                pointsCommand.Parameters.AddWithValue("$points", benchPoint.Points);
                pointsCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IReadOnlyList<BenchWarmingEntryStanding> GetSeasonStandings(string season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT rounds.entry_id,
                   (
                       SELECT latest.entry_name
                       FROM bench_warming_entry_rounds latest
                       JOIN bench_warming_rounds r
                         ON r.season = latest.season
                        AND r.event_id = latest.event_id
                        AND r.entry_rounds_complete = 1
                       WHERE latest.season = $season
                         AND latest.entry_id = rounds.entry_id
                       ORDER BY latest.event_id DESC
                       LIMIT 1
                   ) AS entry_name,
                   SUM(rounds.points) AS total_points
            FROM bench_warming_entry_rounds rounds
            WHERE rounds.season = $season
              AND rounds.event_id IN (
                  SELECT event_id
                  FROM bench_warming_rounds
                  WHERE season = $season
                    AND entry_rounds_complete = 1
              )
            GROUP BY rounds.entry_id
            ORDER BY total_points DESC, entry_name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$season", season);

        var standings = new List<BenchWarmingEntryStanding>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            standings.Add(new BenchWarmingEntryStanding(
                Convert.ToInt32(reader.GetInt64(0)),
                reader.GetString(1),
                Convert.ToInt32(reader.GetInt64(2))));
        }

        return standings;
    }

    public IReadOnlyList<BenchWarmingEntryRoundStanding> GetSeasonRoundStandings(string season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT event_id,
                   entry_id,
                   entry_name,
                   points,
                   active_chip
            FROM bench_warming_entry_rounds
            WHERE season = $season
              AND event_id IN (
                  SELECT event_id
                  FROM bench_warming_rounds
                  WHERE season = $season
                    AND entry_rounds_complete = 1
              )
            ORDER BY event_id ASC,
                     points DESC,
                     entry_name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$season", season);

        var standings = new List<BenchWarmingEntryRoundStanding>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            standings.Add(new BenchWarmingEntryRoundStanding(
                Convert.ToInt32(reader.GetInt64(0)),
                Convert.ToInt32(reader.GetInt64(1)),
                reader.GetString(2),
                Convert.ToInt32(reader.GetInt64(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return standings;
    }

    public BenchWarmingTrackingInfo? GetTrackingInfo(string season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT MIN(event_id),
                   MAX(event_id),
                   COUNT(*)
            FROM bench_warming_rounds
            WHERE season = $season
              AND entry_rounds_complete = 1;
            """;
        command.Parameters.AddWithValue("$season", season);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(2))
        {
            return null;
        }

        var trackedRoundCount = Convert.ToInt32(reader.GetInt64(2));
        if (trackedRoundCount == 0)
        {
            return null;
        }

        return new BenchWarmingTrackingInfo(
            Convert.ToInt32(reader.GetInt64(0)),
            Convert.ToInt32(reader.GetInt64(1)),
            trackedRoundCount);
    }

    public IReadOnlyList<BenchWarmingPlayerPoints> GetRoundBenchPoints(
        string season,
        int eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT entry_id,
                   entry_name,
                   player_id,
                   player_web_name,
                   points
            FROM bench_warming_bench_points
            WHERE season = $season
              AND event_id = $event_id;
            """;
        command.Parameters.AddWithValue("$season", season);
        command.Parameters.AddWithValue("$event_id", eventId);

        var benchPoints = new List<BenchWarmingPlayerPoints>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            benchPoints.Add(new BenchWarmingPlayerPoints(
                Convert.ToInt32(reader.GetInt64(0)),
                reader.GetString(1),
                Convert.ToInt32(reader.GetInt64(2)),
                reader.GetString(3),
                Convert.ToInt32(reader.GetInt64(4))));
        }

        return benchPoints;
    }

    public string? GetLatestSeason()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT season
            FROM bench_warming_rounds
            ORDER BY season DESC
            LIMIT 1;
            """;

        return command.ExecuteScalar() as string;
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

        using (var roundsCommand = connection.CreateCommand())
        {
            roundsCommand.CommandTimeout = CommandTimeoutSeconds;
            roundsCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS bench_warming_rounds (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    calculated_at_utc TEXT NOT NULL,
                    entry_rounds_complete INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (season, event_id)
                ) WITHOUT ROWID;
                """;
            roundsCommand.ExecuteNonQuery();
        }

        EnsureRoundCompletenessColumn(connection);

        using var pointsCommand = connection.CreateCommand();
        pointsCommand.CommandTimeout = CommandTimeoutSeconds;
        pointsCommand.CommandText =
            """
            CREATE TABLE IF NOT EXISTS bench_warming_bench_points (
                season TEXT NOT NULL,
                event_id INTEGER NOT NULL,
                entry_id INTEGER NOT NULL,
                entry_name TEXT NOT NULL,
                player_id INTEGER NOT NULL,
                player_web_name TEXT NOT NULL,
                points INTEGER NOT NULL,
                PRIMARY KEY (season, event_id, entry_id, player_id)
            ) WITHOUT ROWID;
            """;
        pointsCommand.ExecuteNonQuery();

        using (var entryRoundsCommand = connection.CreateCommand())
        {
            entryRoundsCommand.CommandTimeout = CommandTimeoutSeconds;
            entryRoundsCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS bench_warming_entry_rounds (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    entry_name TEXT NOT NULL,
                    points INTEGER NOT NULL,
                    active_chip TEXT,
                    PRIMARY KEY (season, event_id, entry_id)
                ) WITHOUT ROWID;
                """;
            entryRoundsCommand.ExecuteNonQuery();
        }

        using (var backfillCommand = connection.CreateCommand())
        {
            backfillCommand.CommandTimeout = CommandTimeoutSeconds;
            backfillCommand.CommandText =
                """
                INSERT OR IGNORE INTO bench_warming_entry_rounds (
                    season,
                    event_id,
                    entry_id,
                    entry_name,
                    points,
                    active_chip
                )
                SELECT season,
                       event_id,
                       entry_id,
                       MAX(entry_name),
                       SUM(points),
                       NULL
                FROM bench_warming_bench_points
                GROUP BY season, event_id, entry_id;
                """;
            backfillCommand.ExecuteNonQuery();
        }
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

    private static void EnsureRoundCompletenessColumn(SqliteConnection connection)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandTimeout = CommandTimeoutSeconds;
        checkCommand.CommandText =
            "SELECT COUNT(*) FROM pragma_table_info('bench_warming_rounds') " +
            "WHERE name = 'entry_rounds_complete';";
        if (Convert.ToInt64(checkCommand.ExecuteScalar()) == 1)
        {
            return;
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandTimeout = CommandTimeoutSeconds;
        alterCommand.CommandText =
            "ALTER TABLE bench_warming_rounds ADD COLUMN entry_rounds_complete INTEGER NOT NULL DEFAULT 0;";
        alterCommand.ExecuteNonQuery();
    }
}
