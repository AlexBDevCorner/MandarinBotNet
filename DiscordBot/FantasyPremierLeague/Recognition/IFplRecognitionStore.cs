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

    IReadOnlyList<FplManagerRating> GetManagerRatings(
        int leagueId,
        string season,
        int? eventId = null);

    void Save(FplRecognitionRun run, FplRecognitionResult result);
}
