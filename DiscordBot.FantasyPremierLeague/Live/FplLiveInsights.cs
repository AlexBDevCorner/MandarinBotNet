using DiscordBot.FantasyPremierLeague;

namespace DiscordBot.FantasyPremierLeague.Live;

public enum FplLiveInsightsAvailability
{
    Available,
    NoActiveGameweek,
    Stale,
    Unavailable
}

public sealed record FplLiveInsightsResult(
    FplLiveInsightsAvailability Availability,
    FplLiveGameweek? Gameweek = null,
    FantasyPremierLeagueFailureKind? FailureKind = null)
{
    public static FplLiveInsightsResult Available(FplLiveGameweek gameweek)
    {
        ArgumentNullException.ThrowIfNull(gameweek);
        return new(FplLiveInsightsAvailability.Available, gameweek);
    }

    public static FplLiveInsightsResult NoActiveGameweek()
    {
        return new(FplLiveInsightsAvailability.NoActiveGameweek);
    }

    public static FplLiveInsightsResult Stale(FplLiveGameweek gameweek)
    {
        ArgumentNullException.ThrowIfNull(gameweek);
        return new(FplLiveInsightsAvailability.Stale, gameweek);
    }

    public static FplLiveInsightsResult Unavailable(
        FantasyPremierLeagueFailureKind? failureKind = null)
    {
        return new(FplLiveInsightsAvailability.Unavailable, FailureKind: failureKind);
    }
}

public sealed record FplLiveGameweek(
    string Season,
    int EventId,
    DateTimeOffset SourceUpdatedAtUtc,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<FplLiveManagerInsights> Managers,
    IReadOnlyList<FplLiveManagerInsights> BenchAlerts,
    IReadOnlyList<FplLiveManagerInsights> CaptainDisasters,
    IReadOnlyList<FplLiveManagerInsights> CaptainSuccesses,
    IReadOnlyList<FplAutomaticSubstitutionSalvation> AutomaticSubstitutionSalvations,
    IReadOnlyList<FplLiveSwingInsight> SwingInsights);

public sealed record FplLiveManagerInsights(
    int EntryId,
    string EntryName,
    string ManagerName,
    int OfficialRank,
    int PreviousRank,
    int LiveRank,
    int RankChange,
    int PreviousTotalPoints,
    int OfficialTotalPoints,
    int RawLiveGameweekPoints,
    int TransferCost,
    int LiveGameweekPoints,
    int LiveTotalPoints,
    int GapToLeader,
    FplLivePlayerProgress PlayerProgress,
    int BenchPoints,
    FplLiveCaptainInsights Captain,
    IReadOnlyList<FplAutomaticSubstitutionSalvation> AutomaticSubstitutionSalvations);

public sealed record FplLivePlayerProgress(
    int Playing,
    int YetToPlay)
{
    public int Active => checked(Playing + YetToPlay);
}

public sealed record FplLivePlayerExposure(
    int PlayerId,
    string PlayerName,
    int EntryId,
    string EntryName,
    int Multiplier,
    bool IsCaptain);

public abstract record FplLiveSwingInsight;

public sealed record UniqueRemainingPlayerInsight(
    int EntryId,
    string EntryName,
    string PlayerName) : FplLiveSwingInsight;

public sealed record CaptainClashInsight(
    int FirstEntryId,
    string FirstEntryName,
    string FirstCaptain,
    int SecondEntryId,
    string SecondEntryName,
    string SecondCaptain) : FplLiveSwingInsight;

public sealed record FplLiveCaptainInsights(
    string CaptainName,
    int CaptainPoints,
    int CaptainEffectivePoints,
    string ViceCaptainName,
    int ViceCaptainPoints,
    int ViceCaptainEffectivePoints);

public sealed record FplAutomaticSubstitutionSalvation(
    int EntryId,
    string EntryName,
    string PlayerInName,
    int PlayerInPoints,
    string PlayerOutName,
    int PlayerOutPoints,
    int SavedPoints);

public static class FplLiveInsightsSourceIdentifier
{
    public static string Create(FplLiveGameweek gameweek)
    {
        ArgumentNullException.ThrowIfNull(gameweek);

        return $"{gameweek.Season}-event-{gameweek.EventId}-source-" +
            $"{gameweek.SourceUpdatedAtUtc.ToUnixTimeSeconds()}";
    }
}
