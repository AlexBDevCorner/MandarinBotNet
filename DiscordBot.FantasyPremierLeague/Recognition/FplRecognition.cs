using System.Globalization;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public sealed record FplAchievementDefinition(
    string Key,
    string Name,
    string Description,
    bool IsRepeatable);

public sealed record FplAchievementAward(
    int LeagueId,
    string Season,
    int EventId,
    int EntryId,
    string EntryName,
    string AchievementKey,
    string AchievementName,
    string Description,
    bool IsRepeatable,
    string RuleVersion,
    DateTimeOffset AwardedAtUtc)
{
    public string OccurrenceKey => IsRepeatable
        ? EventId.ToString(CultureInfo.InvariantCulture)
        : "season";
}

public sealed record FplManagerRating(
    int LeagueId,
    string Season,
    int EventId,
    int EntryId,
    string EntryName,
    string ManagerName,
    int Rank,
    int FraudRating,
    int MaguireIndex,
    string RuleVersion,
    DateTimeOffset CalculatedAtUtc);

public sealed record FplRecognitionRun(
    int LeagueId,
    string Season,
    int EventId,
    string RuleVersion,
    DateTimeOffset CalculatedAtUtc);

public sealed record FplRecognitionResult(
    IReadOnlyList<FplAchievementAward> Achievements,
    IReadOnlyList<FplManagerRating> Ratings);
