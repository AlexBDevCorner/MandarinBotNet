namespace DiscordBot.FantasyPremierLeague.Recognition;

public interface IFplRecognitionStore
{
    FplRecognitionResult? GetCompletedResult(
        int leagueId,
        string season,
        int eventId);

    IReadOnlyList<FplAchievementAward> GetAchievementAwards(
        int leagueId,
        string season,
        int? eventId = null);

    string? GetLatestSeason(int leagueId);

    int? GetFirstCompletedEventId(
        int leagueId,
        string season);

    void Save(FplRecognitionRun run, FplRecognitionResult result);
}
