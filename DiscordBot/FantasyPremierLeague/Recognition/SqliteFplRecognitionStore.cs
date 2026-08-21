using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public sealed class SqliteFplRecognitionStore : IFplRecognitionStore
{
    private const int CommandTimeoutSeconds = 30;
    private readonly string _connectionString;

    public SqliteFplRecognitionStore(string databasePath)
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

    public FplRecognitionResult? GetCompletedResult(
        int leagueId,
        string season,
        int eventId)
    {
        ValidateRunKey(leagueId, season, eventId);

        var runExists = false;
        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText =
                """
                SELECT 1
                FROM fpl_recognition_runs
                WHERE league_id = $league_id
                  AND season = $season
                  AND event_id = $event_id;
                """;
            command.Parameters.AddWithValue("$league_id", leagueId);
            command.Parameters.AddWithValue("$season", season);
            command.Parameters.AddWithValue("$event_id", eventId);
            runExists = command.ExecuteScalar() is not null;
        }

        return runExists
            ? new FplRecognitionResult(
                GetAchievementAwards(leagueId, season, eventId),
                GetManagerRatings(leagueId, season, eventId))
            : null;
    }

    public IReadOnlyList<FplAchievementAward> GetAchievementAwards(
        int leagueId,
        string season,
        int? eventId = null)
    {
        ValidateLeagueAndSeason(leagueId, season);
        ValidateOptionalEvent(eventId);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = $"""
            SELECT league_id,
                   season,
                   event_id,
                   entry_id,
                   entry_name,
                   achievement_key,
                   achievement_name,
                   description,
                   is_repeatable,
                   rule_version,
                   awarded_at_utc
            FROM fpl_achievement_awards
            WHERE league_id = $league_id
              AND season = $season
              {(eventId is null ? string.Empty : "AND event_id = $event_id")}
            ORDER BY event_id, entry_id, achievement_key;
            """;
        command.Parameters.AddWithValue("$league_id", leagueId);
        command.Parameters.AddWithValue("$season", season);
        if (eventId is not null)
        {
            command.Parameters.AddWithValue("$event_id", eventId.Value);
        }

        var awards = new List<FplAchievementAward>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            awards.Add(new FplAchievementAward(
                Convert.ToInt32(reader.GetInt64(0)),
                reader.GetString(1),
                Convert.ToInt32(reader.GetInt64(2)),
                Convert.ToInt32(reader.GetInt64(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                Convert.ToInt64(reader.GetValue(8)) == 1,
                reader.GetString(9),
                ParseTimestamp(reader.GetString(10))));
        }

        return awards;
    }

    public IReadOnlyList<FplManagerRating> GetManagerRatings(
        int leagueId,
        string season,
        int? eventId = null)
    {
        ValidateLeagueAndSeason(leagueId, season);
        ValidateOptionalEvent(eventId);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = $"""
            SELECT league_id,
                   season,
                   event_id,
                   entry_id,
                   entry_name,
                   manager_name,
                   rank,
                   fraud_rating,
                   maguire_index,
                   rule_version,
                   calculated_at_utc
            FROM fpl_manager_ratings
            WHERE league_id = $league_id
              AND season = $season
              {(eventId is null ? string.Empty : "AND event_id = $event_id")}
            ORDER BY event_id, rank, entry_name, entry_id;
            """;
        command.Parameters.AddWithValue("$league_id", leagueId);
        command.Parameters.AddWithValue("$season", season);
        if (eventId is not null)
        {
            command.Parameters.AddWithValue("$event_id", eventId.Value);
        }

        var ratings = new List<FplManagerRating>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ratings.Add(new FplManagerRating(
                Convert.ToInt32(reader.GetInt64(0)),
                reader.GetString(1),
                Convert.ToInt32(reader.GetInt64(2)),
                Convert.ToInt32(reader.GetInt64(3)),
                reader.GetString(4),
                reader.GetString(5),
                Convert.ToInt32(reader.GetInt64(6)),
                Convert.ToInt32(reader.GetInt64(7)),
                Convert.ToInt32(reader.GetInt64(8)),
                reader.GetString(9),
                ParseTimestamp(reader.GetString(10))));
        }

        return ratings;
    }

    public void Save(FplRecognitionRun run, FplRecognitionResult result)
    {
        ValidateRun(run);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Achievements);
        ArgumentNullException.ThrowIfNull(result.Ratings);

        foreach (var award in result.Achievements)
        {
            ValidateAward(award);
            ValidateMatchesRun(award.LeagueId, award.Season, award.EventId, award.RuleVersion, run);
        }

        foreach (var rating in result.Ratings)
        {
            ValidateRating(rating);
            ValidateMatchesRun(
                rating.LeagueId,
                rating.Season,
                rating.EventId,
                rating.RuleVersion,
                run);
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            InsertAwards(connection, transaction, result.Achievements);
            InsertRatings(connection, transaction, result.Ratings);
            InsertRecognitionRun(connection, transaction, run);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void InsertAwards(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<FplAchievementAward> awards)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            INSERT OR IGNORE INTO fpl_achievement_awards (
                league_id,
                season,
                event_id,
                entry_id,
                entry_name,
                achievement_key,
                achievement_name,
                description,
                is_repeatable,
                rule_version,
                awarded_at_utc,
                occurrence_key
            )
            VALUES (
                $league_id,
                $season,
                $event_id,
                $entry_id,
                $entry_name,
                $achievement_key,
                $achievement_name,
                $description,
                $is_repeatable,
                $rule_version,
                $awarded_at_utc,
                $occurrence_key
            );
            """;
        var leagueIdParameter = command.Parameters.AddWithValue("$league_id", 0);
        var seasonParameter = command.Parameters.AddWithValue("$season", string.Empty);
        var eventIdParameter = command.Parameters.AddWithValue("$event_id", 0);
        var entryIdParameter = command.Parameters.AddWithValue("$entry_id", 0);
        var entryNameParameter = command.Parameters.AddWithValue(
            "$entry_name",
            string.Empty);
        var keyParameter = command.Parameters.AddWithValue(
            "$achievement_key",
            string.Empty);
        var nameParameter = command.Parameters.AddWithValue(
            "$achievement_name",
            string.Empty);
        var descriptionParameter = command.Parameters.AddWithValue(
            "$description",
            string.Empty);
        var repeatableParameter = command.Parameters.AddWithValue("$is_repeatable", 0);
        var versionParameter = command.Parameters.AddWithValue(
            "$rule_version",
            string.Empty);
        var awardedAtParameter = command.Parameters.AddWithValue(
            "$awarded_at_utc",
            string.Empty);
        var occurrenceParameter = command.Parameters.AddWithValue(
            "$occurrence_key",
            string.Empty);

        foreach (var award in awards)
        {
            leagueIdParameter.Value = award.LeagueId;
            seasonParameter.Value = award.Season;
            eventIdParameter.Value = award.EventId;
            entryIdParameter.Value = award.EntryId;
            entryNameParameter.Value = award.EntryName;
            keyParameter.Value = award.AchievementKey;
            nameParameter.Value = award.AchievementName;
            descriptionParameter.Value = award.Description;
            repeatableParameter.Value = award.IsRepeatable ? 1 : 0;
            versionParameter.Value = award.RuleVersion;
            awardedAtParameter.Value = FormatTimestamp(award.AwardedAtUtc);
            occurrenceParameter.Value = award.OccurrenceKey;
            command.ExecuteNonQuery();
        }
    }

    private static void InsertRatings(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<FplManagerRating> ratings)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            INSERT OR IGNORE INTO fpl_manager_ratings (
                league_id,
                season,
                event_id,
                entry_id,
                entry_name,
                manager_name,
                rank,
                fraud_rating,
                maguire_index,
                rule_version,
                calculated_at_utc
            )
            VALUES (
                $league_id,
                $season,
                $event_id,
                $entry_id,
                $entry_name,
                $manager_name,
                $rank,
                $fraud_rating,
                $maguire_index,
                $rule_version,
                $calculated_at_utc
            );
            """;
        var leagueIdParameter = command.Parameters.AddWithValue("$league_id", 0);
        var seasonParameter = command.Parameters.AddWithValue("$season", string.Empty);
        var eventIdParameter = command.Parameters.AddWithValue("$event_id", 0);
        var entryIdParameter = command.Parameters.AddWithValue("$entry_id", 0);
        var entryNameParameter = command.Parameters.AddWithValue(
            "$entry_name",
            string.Empty);
        var managerNameParameter = command.Parameters.AddWithValue(
            "$manager_name",
            string.Empty);
        var rankParameter = command.Parameters.AddWithValue("$rank", 0);
        var fraudRatingParameter = command.Parameters.AddWithValue("$fraud_rating", 0);
        var maguireIndexParameter = command.Parameters.AddWithValue("$maguire_index", 0);
        var versionParameter = command.Parameters.AddWithValue(
            "$rule_version",
            string.Empty);
        var calculatedAtParameter = command.Parameters.AddWithValue(
            "$calculated_at_utc",
            string.Empty);

        foreach (var rating in ratings)
        {
            leagueIdParameter.Value = rating.LeagueId;
            seasonParameter.Value = rating.Season;
            eventIdParameter.Value = rating.EventId;
            entryIdParameter.Value = rating.EntryId;
            entryNameParameter.Value = rating.EntryName;
            managerNameParameter.Value = rating.ManagerName;
            rankParameter.Value = rating.Rank;
            fraudRatingParameter.Value = rating.FraudRating;
            maguireIndexParameter.Value = rating.MaguireIndex;
            versionParameter.Value = rating.RuleVersion;
            calculatedAtParameter.Value = FormatTimestamp(rating.CalculatedAtUtc);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertRecognitionRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplRecognitionRun run)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            INSERT OR IGNORE INTO fpl_recognition_runs (
                league_id,
                season,
                event_id,
                rule_version,
                calculated_at_utc
            )
            VALUES (
                $league_id,
                $season,
                $event_id,
                $rule_version,
                $calculated_at_utc
            );
            """;
        command.Parameters.AddWithValue("$league_id", run.LeagueId);
        command.Parameters.AddWithValue("$season", run.Season);
        command.Parameters.AddWithValue("$event_id", run.EventId);
        command.Parameters.AddWithValue("$rule_version", run.RuleVersion);
        command.Parameters.AddWithValue(
            "$calculated_at_utc",
            FormatTimestamp(run.CalculatedAtUtc));
        command.ExecuteNonQuery();
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

        using (var runsCommand = connection.CreateCommand())
        {
            runsCommand.CommandTimeout = CommandTimeoutSeconds;
            runsCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS fpl_recognition_runs (
                    league_id INTEGER NOT NULL,
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    rule_version TEXT NOT NULL,
                    calculated_at_utc TEXT NOT NULL,
                    PRIMARY KEY (league_id, season, event_id)
                ) WITHOUT ROWID;
                """;
            runsCommand.ExecuteNonQuery();
        }

        using (var awardsCommand = connection.CreateCommand())
        {
            awardsCommand.CommandTimeout = CommandTimeoutSeconds;
            awardsCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS fpl_achievement_awards (
                    league_id INTEGER NOT NULL,
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    entry_name TEXT NOT NULL,
                    achievement_key TEXT NOT NULL,
                    achievement_name TEXT NOT NULL,
                    description TEXT NOT NULL,
                    is_repeatable INTEGER NOT NULL,
                    rule_version TEXT NOT NULL,
                    awarded_at_utc TEXT NOT NULL,
                    occurrence_key TEXT NOT NULL,
                    PRIMARY KEY (
                        league_id,
                        season,
                        entry_id,
                        achievement_key,
                        occurrence_key
                    )
                ) WITHOUT ROWID;
                """;
            awardsCommand.ExecuteNonQuery();
        }

        using (var awardsIndexCommand = connection.CreateCommand())
        {
            awardsIndexCommand.CommandTimeout = CommandTimeoutSeconds;
            awardsIndexCommand.CommandText =
                """
                CREATE INDEX IF NOT EXISTS ix_fpl_achievement_awards_event
                ON fpl_achievement_awards (league_id, season, event_id, entry_id);
                """;
            awardsIndexCommand.ExecuteNonQuery();
        }

        using (var ratingsCommand = connection.CreateCommand())
        {
            ratingsCommand.CommandTimeout = CommandTimeoutSeconds;
            ratingsCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS fpl_manager_ratings (
                    league_id INTEGER NOT NULL,
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    entry_name TEXT NOT NULL,
                    manager_name TEXT NOT NULL,
                    rank INTEGER NOT NULL,
                    fraud_rating INTEGER NOT NULL,
                    maguire_index INTEGER NOT NULL,
                    rule_version TEXT NOT NULL,
                    calculated_at_utc TEXT NOT NULL,
                    PRIMARY KEY (league_id, season, event_id, entry_id)
                ) WITHOUT ROWID;
                """;
            ratingsCommand.ExecuteNonQuery();
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

    private static void ValidateAward(FplAchievementAward award)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(award.LeagueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(award.Season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(award.EventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(award.EntryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(award.EntryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(award.AchievementKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(award.AchievementName);
        ArgumentException.ThrowIfNullOrWhiteSpace(award.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(award.RuleVersion);
        if (award.AwardedAtUtc == default)
        {
            throw new ArgumentException(
                "The achievement award must include an award timestamp.",
                nameof(award));
        }
    }

    private static void ValidateRating(FplManagerRating rating)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rating.LeagueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rating.Season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rating.EventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rating.EntryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rating.EntryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rating.ManagerName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rating.Rank);
        if (rating.FraudRating is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                "The Fraud Rating must be between 0 and 100.");
        }

        if (rating.MaguireIndex is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                "The Maguire Index must be between 0 and 100.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rating.RuleVersion);
        if (rating.CalculatedAtUtc == default)
        {
            throw new ArgumentException(
                "The manager rating must include a calculation timestamp.",
                nameof(rating));
        }
    }

    private static void ValidateRun(FplRecognitionRun run)
    {
        ValidateRunKey(run.LeagueId, run.Season, run.EventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.RuleVersion);
        if (run.CalculatedAtUtc == default)
        {
            throw new ArgumentException(
                "The recognition run must include a calculation timestamp.",
                nameof(run));
        }
    }

    private static void ValidateMatchesRun(
        int leagueId,
        string season,
        int eventId,
        string ruleVersion,
        FplRecognitionRun run)
    {
        if (leagueId != run.LeagueId ||
            season != run.Season ||
            eventId != run.EventId ||
            ruleVersion != run.RuleVersion)
        {
            throw new ArgumentException(
                "All recognition records must belong to the recognition run.",
                nameof(run));
        }
    }

    private static void ValidateRunKey(int leagueId, string season, int eventId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
    }

    private static void ValidateLeagueAndSeason(int leagueId, string season)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
    }

    private static void ValidateOptionalEvent(int? eventId)
    {
        if (eventId is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventId),
                eventId.Value,
                "The event ID must be positive.");
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
}
