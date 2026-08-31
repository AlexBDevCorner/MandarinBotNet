using DiscordBot.FantasyPremierLeague.Chips;

namespace DiscordBot.FantasyPremierLeague.ChipWatch;

public enum FplChipOpportunityLevel
{
    None,
    Consider,
    Strong,
    VeryStrong
}

public enum FplChipRecommendationReasonKind
{
    BlankPlayers,
    UnavailablePlayers,
    DoubtfulPlayers,
    DifficultFixtures
}

public sealed record FplChipRecommendationReason(
    FplChipRecommendationReasonKind Kind,
    int Count);

public sealed record FplChipRecommendation(
    FplChipType Chip,
    int OpportunityScore,
    FplChipOpportunityLevel Level,
    FplChipUrgency Urgency,
    IReadOnlyList<FplChipRecommendationReason> Reasons);

public sealed record FplManagerChipWatch(
    int EntryId,
    string EntryName,
    string PlayerName,
    int? SourceSquadEventId,
    IReadOnlyList<FplChipAvailability> Chips,
    FplChipRecommendation? FreeHitRecommendation,
    bool ChipHistoryAvailable,
    bool SquadAvailable);

public sealed record FplChipWatchReport(
    int TargetEventId,
    DateTimeOffset DeadlineUtc,
    int? SourceSquadEventId,
    int FinalEventId,
    int ManagersTotal,
    int ManagersWithChipHistory,
    int ManagersWithSquadData,
    IReadOnlyList<FplManagerChipWatch> Managers);
