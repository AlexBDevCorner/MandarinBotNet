namespace DiscordBot.FantasyPremierLeague.Historical;

public interface IFplStatisticsStore
{
    bool IsSnapshotStored(string season, int eventId);

    void SaveSnapshot(FplGameweekSnapshot snapshot);

    FplGameweekSnapshot? GetSnapshot(string season, int eventId);

    IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
        string season,
        int? eventId = null,
        int? managerEntryId = null);
}
