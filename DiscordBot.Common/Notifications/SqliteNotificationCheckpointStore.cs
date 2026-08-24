using Microsoft.Data.Sqlite;

namespace DiscordBot.Notifications
{
    public sealed class SqliteNotificationCheckpointStore : INotificationCheckpointStore
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

            return Convert.ToInt64(command.ExecuteScalar()) == 1;
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
