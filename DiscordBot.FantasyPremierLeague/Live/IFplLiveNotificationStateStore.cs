namespace DiscordBot.FantasyPremierLeague.Live;

public interface IFplLiveNotificationStateStore
{
    FplLiveNotificationState? Get(
        int leagueId,
        string season,
        int eventId);

    void Save(FplLiveNotificationState state);
}
