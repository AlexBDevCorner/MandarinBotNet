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
                    INSERT OR IGNORE INTO bench_warming_rounds (
                        season,
                        event_id,
                        calculated_at_utc
                    )
                    VALUES (
                        $season,
                        $event_id,
                        $calculated_at_utc
                    );
                    """;
                roundCommand.Parameters.AddWithValue("$season", season);
                roundCommand.Parameters.AddWithValue("$event_id", eventId);
                roundCommand.Parameters.AddWithValue(
                    "$calculated_at_utc",
                    calculatedAtUtc.UtcDateTime.ToString("O"));
                roundCommand.ExecuteNonQuery();
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
            SELECT entry_id,
                   entry_name,
                   SUM(points) AS total_points
            FROM bench_warming_bench_points
            WHERE season = $season
            GROUP BY entry_id, entry_name
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
                    PRIMARY KEY (season, event_id)
                ) WITHOUT ROWID;
                """;
            roundsCommand.ExecuteNonQuery();
        }

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
