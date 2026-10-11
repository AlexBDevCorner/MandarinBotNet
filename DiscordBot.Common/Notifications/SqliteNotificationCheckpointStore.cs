using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DiscordBot.Notifications
{
    public sealed class SqliteNotificationCheckpointStore : INotificationCheckpointStore, IClassicStandingsPublicationStateStore
    {
        private const int CommandTimeoutSeconds = 30;
        private readonly string _connectionString;

        public SqliteNotificationCheckpointStore(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

            var fullDatabasePath = Path.GetFullPath(databasePath);
            var databaseDirectory = Path.GetDirectoryName(fullDatabasePath)
                ?? throw new ArgumentException("The database path must include a directory.", nameof(databasePath));

            Directory.CreateDirectory(databaseDirectory);

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = fullDatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = true
            }.ToString();

            InitializeDatabase();
        }

        public bool IsDelivered(NotificationCheckpoint checkpoint)
        {
            ArgumentNullException.ThrowIfNull(checkpoint);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM notification_delivery_checkpoints
                    WHERE guild_id = $guild_id
                      AND channel_id = $channel_id
                      AND source_identifier = $source_identifier
                      AND notification_type = $notification_type
                );
                """;
            AddKeyParameters(command, checkpoint);

            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        }

        public void RecordDelivered(NotificationCheckpoint checkpoint, DateTimeOffset deliveredAtUtc)
        {
            ArgumentNullException.ThrowIfNull(checkpoint);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText =
                """
                INSERT OR IGNORE INTO notification_delivery_checkpoints (
                    guild_id,
                    channel_id,
                    source_identifier,
                    notification_type,
                    delivered_at_utc
                )
                VALUES (
                    $guild_id,
                    $channel_id,
                    $source_identifier,
                    $notification_type,
                    $delivered_at_utc
                );
                """;
            AddKeyParameters(command, checkpoint);
            command.Parameters.AddWithValue("$delivered_at_utc", deliveredAtUtc.UtcDateTime.ToString("O"));
            command.ExecuteNonQuery();
        }

        public ClassicStandingsPublicationState? GetPublicationState(
            ulong guildId,
            ulong channelId,
            int leagueId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText =
                """
                SELECT fingerprint, publication_seq, updated_at_utc
                FROM classic_standings_publication_state
                WHERE guild_id = $guild_id
                  AND channel_id = $channel_id
                  AND league_id = $league_id;
                """;
            command.Parameters.AddWithValue("$guild_id", guildId.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$channel_id", channelId.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$league_id", leagueId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new ClassicStandingsPublicationState(
                reader.GetString(0),
                reader.GetInt64(1),
                DateTimeOffset.Parse(
                    reader.GetString(2),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind));
        }

        public void SetPublicationState(
            ulong guildId,
            ulong channelId,
            int leagueId,
            string fingerprint,
            long sequence,
            DateTimeOffset updatedAtUtc)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);
            ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
            ArgumentOutOfRangeException.ThrowIfNegative(sequence);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText =
                """
                INSERT INTO classic_standings_publication_state (
                    guild_id,
                    channel_id,
                    league_id,
                    fingerprint,
                    publication_seq,
                    updated_at_utc
                )
                VALUES (
                    $guild_id,
                    $channel_id,
                    $league_id,
                    $fingerprint,
                    $publication_seq,
                    $updated_at_utc
                )
                ON CONFLICT (guild_id, channel_id, league_id) DO UPDATE SET
                    fingerprint = excluded.fingerprint,
                    publication_seq = excluded.publication_seq,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$guild_id", guildId.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$channel_id", channelId.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$league_id", leagueId);
            command.Parameters.AddWithValue("$fingerprint", fingerprint);
            command.Parameters.AddWithValue("$publication_seq", sequence);
            command.Parameters.AddWithValue("$updated_at_utc", updatedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
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
                CREATE TABLE IF NOT EXISTS notification_delivery_checkpoints (
                    guild_id TEXT NOT NULL,
                    channel_id TEXT NOT NULL,
                    source_identifier TEXT NOT NULL,
                    notification_type TEXT NOT NULL,
                    delivered_at_utc TEXT NOT NULL,
                    PRIMARY KEY (
                        guild_id,
                        channel_id,
                        source_identifier,
                        notification_type
                    )
                ) WITHOUT ROWID;

                CREATE TABLE IF NOT EXISTS classic_standings_publication_state (
                    guild_id TEXT NOT NULL,
                    channel_id TEXT NOT NULL,
                    league_id INTEGER NOT NULL,
                    fingerprint TEXT NOT NULL,
                    publication_seq INTEGER NOT NULL DEFAULT 0,
                    updated_at_utc TEXT NOT NULL,
                    PRIMARY KEY (
                        guild_id,
                        channel_id,
                        league_id
                    )
                ) WITHOUT ROWID;
                """;
            schemaCommand.ExecuteNonQuery();

            EnsurePublicationSequenceColumn(connection);
        }

        private void EnsurePublicationSequenceColumn(SqliteConnection connection)
        {
            // Databases created before the publication sequence existed have
            // the state table without this column; migrate them in place so
            // existing baselines keep working with sequence 0.
            using var infoCommand = connection.CreateCommand();
            infoCommand.CommandTimeout = CommandTimeoutSeconds;
            infoCommand.CommandText = "PRAGMA table_info(classic_standings_publication_state);";
            using var reader = infoCommand.ExecuteReader();
            var columnIndex = reader.GetOrdinal("name");
            while (reader.Read())
            {
                if (string.Equals(
                        reader.GetString(columnIndex),
                        "publication_seq",
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            reader.Close();
            using var migrateCommand = connection.CreateCommand();
            migrateCommand.CommandTimeout = CommandTimeoutSeconds;
            migrateCommand.CommandText =
                """
                ALTER TABLE classic_standings_publication_state
                ADD COLUMN publication_seq INTEGER NOT NULL DEFAULT 0;
                """;
            migrateCommand.ExecuteNonQuery();
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

        private static void AddKeyParameters(
            SqliteCommand command,
            NotificationCheckpoint checkpoint)
        {
            command.Parameters.AddWithValue("$guild_id", checkpoint.GuildId.ToString());
            command.Parameters.AddWithValue("$channel_id", checkpoint.ChannelId.ToString());
            command.Parameters.AddWithValue("$source_identifier", checkpoint.SourceIdentifier);
            command.Parameters.AddWithValue("$notification_type", checkpoint.NotificationType);
        }
    }
}
