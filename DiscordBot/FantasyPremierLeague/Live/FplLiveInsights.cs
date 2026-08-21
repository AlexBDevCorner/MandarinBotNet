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
    IReadOnlyList<FplAutomaticSubstitutionSalvation> AutomaticSubstitutionSalvations);

public sealed record FplLiveManagerInsights(
    int EntryId,
    string EntryName,
    string ManagerName,
    int Rank,
    int LivePoints,
    int PlayersRemainingToPlay,
    int BenchPoints,
    FplLiveCaptainInsights Captain,
    IReadOnlyList<FplAutomaticSubstitutionSalvation> AutomaticSubstitutionSalvations);

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
