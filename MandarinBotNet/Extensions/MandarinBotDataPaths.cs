namespace MandarinBotNet.Extensions;

internal static class MandarinBotDataPaths
{
    internal static string NotificationDatabasePath =>
        GetDataPath("notification-state.db");

    internal static string BenchWarmingDatabasePath =>
        GetDataPath("bench-warming-league.db");

    internal static string FplStatisticsDatabasePath =>
        GetDataPath("fpl-statistics.db");

    internal static string FplRecognitionDatabasePath =>
        GetDataPath("fpl-recognition.db");

    internal static string FplPriceSnapshotDatabasePath =>
        GetDataPath("fpl-price-snapshot.db");

    internal static string FplLiveNotificationDatabasePath =>
        GetDataPath("fpl-live.db");

    internal static string HealthStatePath =>
        GetDataPath("health-state.json");

    private static string GetDataPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "data", fileName);
}
