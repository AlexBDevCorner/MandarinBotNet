using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class SqliteFplLiveNotificationStateStore
    : IFplLiveNotificationStateStore
{
    private const int CommandTimeoutSeconds = 30;
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public SqliteFplLiveNotificationStateStore(string databasePath)
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

    public FplLiveNotificationState? Get(
        int leagueId,
        string season,
        int eventId)
    {
        ValidateKey(leagueId, season, eventId);

        using var connection = OpenConnection();
        var snapshot = GetSnapshot(connection, leagueId, season, eventId);
        if (snapshot is null)
        {
            return null;
        }

        var targets = GetTargets(connection, leagueId, season, eventId);
        LoadPendingHighlights(connection, leagueId, season, eventId, targets);

        return new FplLiveNotificationState(
            leagueId,
            season,
            eventId,
            snapshot,
            targets.Values
                .OrderBy(target => target.GuildId)
                .ThenBy(target => target.ChannelId)
                .Select(target => target.Build())
                .ToArray());
    }

    public void Save(FplLiveNotificationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateState(state);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            SaveObservation(connection, transaction, state);
            DeleteTargetState(connection, transaction, state);
            SaveTargets(connection, transaction, state);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static FplLiveGameweek? GetSnapshot(
        SqliteConnection connection,
        int leagueId,
        string season,
        int eventId)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT snapshot_json
            FROM fpl_live_observation
            WHERE league_id = $league_id
              AND season = $season
              AND event_id = $event_id;
            """;
        AddGameweekKeyParameters(command, leagueId, season, eventId);
        var value = command.ExecuteScalar();

        return value is null or DBNull
            ? null
            : JsonSerializer.Deserialize<FplLiveGameweek>(
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
                SerializerOptions)
              ?? throw new InvalidDataException(
                  "The persisted FPL live observation could not be deserialized.");
    }

    private static Dictionary<FplLiveTargetKey, TargetStateBuilder> GetTargets(
        SqliteConnection connection,
        int leagueId,
        string season,
        int eventId)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT
                guild_id,
                channel_id,
                last_published_at_utc,
                next_digest_sequence
            FROM fpl_live_publication_state
            WHERE league_id = $league_id
              AND season = $season
              AND event_id = $event_id;
            """;
        AddGameweekKeyParameters(command, leagueId, season, eventId);

        var targets = new Dictionary<FplLiveTargetKey, TargetStateBuilder>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var guildId = ulong.Parse(reader.GetString(0), CultureInfo.InvariantCulture);
            var channelId = ulong.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
            var target = new TargetStateBuilder(
                guildId,
                channelId,
                reader.IsDBNull(2) ? null : ParseTimestamp(reader.GetString(2)),
                reader.GetInt64(3));
            targets.Add(new FplLiveTargetKey(guildId, channelId), target);
        }

        return targets;
    }

    private static void LoadPendingHighlights(
        SqliteConnection connection,
        int leagueId,
        string season,
        int eventId,
        IReadOnlyDictionary<FplLiveTargetKey, TargetStateBuilder> targets)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT
                guild_id,
                channel_id,
                highlight_type,
                payload_json
            FROM fpl_live_pending_highlights
            WHERE league_id = $league_id
              AND season = $season
              AND event_id = $event_id
            ORDER BY detected_at_utc, highlight_key;
            """;
        AddGameweekKeyParameters(command, leagueId, season, eventId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var key = new FplLiveTargetKey(
                ulong.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                ulong.Parse(reader.GetString(1), CultureInfo.InvariantCulture));
            if (!targets.TryGetValue(key, out var target))
            {
                throw new InvalidDataException(
                    "A pending FPL live highlight has no publication state.");
            }

            target.PendingHighlights.Add(DeserializeHighlight(
                reader.GetString(2),
                reader.GetString(3)));
        }
    }

    private static void SaveObservation(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplLiveNotificationState state)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            INSERT INTO fpl_live_observation (
                league_id,
                season,
                event_id,
                snapshot_json,
                observed_at_utc
            )
            VALUES (
                $league_id,
                $season,
                $event_id,
                $snapshot_json,
                $observed_at_utc
            )
            ON CONFLICT (league_id, season, event_id) DO UPDATE SET
                snapshot_json = excluded.snapshot_json,
                observed_at_utc = excluded.observed_at_utc;
            """;
        AddGameweekKeyParameters(command, state.ClassicLeagueId, state.Season, state.EventId);
        command.Parameters.AddWithValue(
            "$snapshot_json",
            JsonSerializer.Serialize(state.LastObservedSnapshot, SerializerOptions));
        command.Parameters.AddWithValue(
            "$observed_at_utc",
            FormatTimestamp(state.LastObservedSnapshot.CapturedAtUtc));
        command.ExecuteNonQuery();
    }

    private static void DeleteTargetState(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplLiveNotificationState state)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            DELETE FROM fpl_live_publication_state
            WHERE league_id = $league_id
              AND season = $season
              AND event_id = $event_id;
            """;
        AddGameweekKeyParameters(command, state.ClassicLeagueId, state.Season, state.EventId);
        command.ExecuteNonQuery();
    }

    private static void SaveTargets(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplLiveNotificationState state)
    {
        foreach (var target in state.Targets)
        {
            SaveTarget(connection, transaction, state, target);
            foreach (var highlight in target.PendingHighlights)
            {
                SaveHighlight(connection, transaction, state, target, highlight);
            }
        }
    }

    private static void SaveTarget(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplLiveNotificationState state,
        FplLiveTargetNotificationState target)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            INSERT INTO fpl_live_publication_state (
                league_id,
                season,
                event_id,
                guild_id,
                channel_id,
                last_published_at_utc,
                next_digest_sequence
            )
            VALUES (
                $league_id,
                $season,
                $event_id,
                $guild_id,
                $channel_id,
                $last_published_at_utc,
                $next_digest_sequence
            );
            """;
        AddGameweekKeyParameters(command, state.ClassicLeagueId, state.Season, state.EventId);
        AddTargetParameters(command, target.GuildId, target.ChannelId);
        command.Parameters.AddWithValue(
            "$last_published_at_utc",
            target.LastPublishedAtUtc is null
                ? DBNull.Value
                : FormatTimestamp(target.LastPublishedAtUtc.Value));
        command.Parameters.AddWithValue(
            "$next_digest_sequence",
            target.NextDigestSequence);
        command.ExecuteNonQuery();
    }

    private static void SaveHighlight(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FplLiveNotificationState state,
        FplLiveTargetNotificationState target,
        FplLiveHighlight highlight)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            INSERT INTO fpl_live_pending_highlights (
                league_id,
                season,
                event_id,
                guild_id,
                channel_id,
                highlight_key,
                highlight_type,
                payload_json,
                detected_at_utc
            )
            VALUES (
                $league_id,
                $season,
                $event_id,
                $guild_id,
                $channel_id,
                $highlight_key,
                $highlight_type,
                $payload_json,
                $detected_at_utc
            );
            """;
        AddGameweekKeyParameters(command, state.ClassicLeagueId, state.Season, state.EventId);
        AddTargetParameters(command, target.GuildId, target.ChannelId);
        command.Parameters.AddWithValue("$highlight_key", highlight.Key);
        command.Parameters.AddWithValue("$highlight_type", GetHighlightType(highlight));
        command.Parameters.AddWithValue(
            "$payload_json",
            JsonSerializer.Serialize(highlight, highlight.GetType(), SerializerOptions));
        command.Parameters.AddWithValue(
            "$detected_at_utc",
            FormatTimestamp(highlight.DetectedAtUtc));
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

        using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandTimeout = CommandTimeoutSeconds;
        schemaCommand.CommandText =
            """
            CREATE TABLE IF NOT EXISTS fpl_live_observation (
                league_id INTEGER NOT NULL,
                season TEXT NOT NULL,
                event_id INTEGER NOT NULL,
                snapshot_json TEXT NOT NULL,
                observed_at_utc TEXT NOT NULL,
                PRIMARY KEY (league_id, season, event_id)
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS fpl_live_publication_state (
                league_id INTEGER NOT NULL,
                season TEXT NOT NULL,
                event_id INTEGER NOT NULL,
                guild_id TEXT NOT NULL,
                channel_id TEXT NOT NULL,
                last_published_at_utc TEXT NULL,
                next_digest_sequence INTEGER NOT NULL CHECK (next_digest_sequence > 0),
                PRIMARY KEY (
                    league_id,
                    season,
                    event_id,
                    guild_id,
                    channel_id
                ),
                FOREIGN KEY (league_id, season, event_id)
                    REFERENCES fpl_live_observation (league_id, season, event_id)
                    ON DELETE CASCADE
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS fpl_live_pending_highlights (
                league_id INTEGER NOT NULL,
                season TEXT NOT NULL,
                event_id INTEGER NOT NULL,
                guild_id TEXT NOT NULL,
                channel_id TEXT NOT NULL,
                highlight_key TEXT NOT NULL,
                highlight_type TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                detected_at_utc TEXT NOT NULL,
                PRIMARY KEY (
                    league_id,
                    season,
                    event_id,
                    guild_id,
                    channel_id,
                    highlight_key
                ),
                FOREIGN KEY (
                    league_id,
                    season,
                    event_id,
                    guild_id,
                    channel_id
                ) REFERENCES fpl_live_publication_state (
                    league_id,
                    season,
                    event_id,
                    guild_id,
                    channel_id
                ) ON DELETE CASCADE
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

        using var foreignKeysCommand = connection.CreateCommand();
        foreignKeysCommand.CommandTimeout = CommandTimeoutSeconds;
        foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON;";
        foreignKeysCommand.ExecuteNonQuery();

        return connection;
    }

    private static string GetHighlightType(FplLiveHighlight highlight)
    {
        return highlight switch
        {
            LeaderChangedHighlight => "leader-changed",
            SignificantRankChangeHighlight => "significant-rank-change",
            BenchThresholdReachedHighlight => "bench-threshold-reached",
            CaptainSuccessHighlight => "captain-success",
            CaptainDisasterHighlight => "captain-disaster",
            AutomaticSubstitutionHighlight => "automatic-substitution",
            _ => throw new ArgumentOutOfRangeException(nameof(highlight))
        };
    }

    private static FplLiveHighlight DeserializeHighlight(
        string type,
        string payload)
    {
        return type switch
        {
            "leader-changed" => Deserialize<LeaderChangedHighlight>(payload),
            "significant-rank-change" =>
                Deserialize<SignificantRankChangeHighlight>(payload),
            "bench-threshold-reached" =>
                Deserialize<BenchThresholdReachedHighlight>(payload),
            "captain-success" => Deserialize<CaptainSuccessHighlight>(payload),
            "captain-disaster" => Deserialize<CaptainDisasterHighlight>(payload),
            "automatic-substitution" =>
                Deserialize<AutomaticSubstitutionHighlight>(payload),
            _ => throw new InvalidDataException(
                $"Unknown persisted FPL live highlight type '{type}'.")
        };
    }

    private static T Deserialize<T>(string payload)
        where T : FplLiveHighlight
    {
        return JsonSerializer.Deserialize<T>(payload, SerializerOptions)
            ?? throw new InvalidDataException(
                $"The persisted {typeof(T).Name} could not be deserialized.");
    }

    private static void AddGameweekKeyParameters(
        SqliteCommand command,
        int leagueId,
        string season,
        int eventId)
    {
        command.Parameters.AddWithValue("$league_id", leagueId);
        command.Parameters.AddWithValue("$season", season);
        command.Parameters.AddWithValue("$event_id", eventId);
    }

    private static void AddTargetParameters(
        SqliteCommand command,
        ulong guildId,
        ulong channelId)
    {
        command.Parameters.AddWithValue(
            "$guild_id",
            guildId.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "$channel_id",
            channelId.ToString(CultureInfo.InvariantCulture));
    }

    private static void ValidateState(FplLiveNotificationState state)
    {
        ValidateKey(state.ClassicLeagueId, state.Season, state.EventId);
        ArgumentNullException.ThrowIfNull(state.LastObservedSnapshot);
        ArgumentNullException.ThrowIfNull(state.Targets);

        if (!string.Equals(
                state.Season,
                state.LastObservedSnapshot.Season,
                StringComparison.Ordinal) ||
            state.EventId != state.LastObservedSnapshot.EventId)
        {
            throw new ArgumentException(
                "The persisted observation must match the state gameweek.",
                nameof(state));
        }

        var targetKeys = new HashSet<FplLiveTargetKey>();
        foreach (var target in state.Targets)
        {
            if (target.GuildId == 0 || target.ChannelId == 0)
            {
                throw new ArgumentException(
                    "FPL live notification targets must contain Discord IDs.",
                    nameof(state));
            }

            if (target.NextDigestSequence <= 0)
            {
                throw new ArgumentException(
                    "The next FPL live digest sequence must be positive.",
                    nameof(state));
            }

            if (!targetKeys.Add(new FplLiveTargetKey(
                    target.GuildId,
                    target.ChannelId)))
            {
                throw new ArgumentException(
                    "The FPL live notification state contains duplicate targets.",
                    nameof(state));
            }

            var highlightKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var highlight in target.PendingHighlights)
            {
                ArgumentNullException.ThrowIfNull(highlight);
                if (!highlightKeys.Add(highlight.Key))
                {
                    throw new ArgumentException(
                        "The FPL live notification state contains duplicate highlight keys.",
                        nameof(state));
                }
            }
        }
    }

    private static void ValidateKey(int leagueId, string season, int eventId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
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

    private sealed record FplLiveTargetKey(ulong GuildId, ulong ChannelId);

    private sealed class TargetStateBuilder(
        ulong guildId,
        ulong channelId,
        DateTimeOffset? lastPublishedAtUtc,
        long nextDigestSequence)
    {
        public ulong GuildId { get; } = guildId;

        public ulong ChannelId { get; } = channelId;

        public DateTimeOffset? LastPublishedAtUtc { get; } = lastPublishedAtUtc;

        public long NextDigestSequence { get; } = nextDigestSequence;

        public List<FplLiveHighlight> PendingHighlights { get; } = [];

        public FplLiveTargetNotificationState Build()
        {
            return new FplLiveTargetNotificationState(
                GuildId,
                ChannelId,
                LastPublishedAtUtc,
                NextDigestSequence,
                PendingHighlights.ToArray());
        }
    }
}
