namespace DiscordBot.FantasyPremierLeague.PriceChanges;

public sealed record FplPriceSnapshot(
    long Version,
    IReadOnlyDictionary<int, int> Prices);

public interface IFplPriceSnapshotStore
{
    FplPriceSnapshot? GetSnapshot();

    void SaveSnapshot(IReadOnlyDictionary<int, int> prices);
}
