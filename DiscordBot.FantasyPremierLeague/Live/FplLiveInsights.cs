using DiscordBot.FantasyPremierLeague;

namespace DiscordBot.FantasyPremierLeague.Live;

public enum FplLiveInsightsAvailability
{
    Available,
    NoActiveGameweek,
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

        return $"{gameweek.Season}-event-{gameweek.EventId}-live-" +
            $"{ComputeLiveStateHash(gameweek):x8}";
    }

    private static int ComputeLiveStateHash(FplLiveGameweek gameweek)
    {
        var hash = new HashCode();
        foreach (var manager in gameweek.Managers)
        {
            hash.Add(manager.EntryId);
            hash.Add(manager.LiveTotalPoints);
            hash.Add(manager.LiveGameweekPoints);
            hash.Add(manager.LiveRank);
            hash.Add(manager.RankChange);
            hash.Add(manager.TransferCost);
            hash.Add(manager.BenchPoints);
            hash.Add(manager.PlayerProgress.Playing);
            hash.Add(manager.PlayerProgress.YetToPlay);
            hash.Add(manager.Captain.CaptainEffectivePoints);
            hash.Add(manager.Captain.ViceCaptainEffectivePoints);
        }

        foreach (var salvation in gameweek.AutomaticSubstitutionSalvations)
        {
            hash.Add(salvation.EntryId);
            hash.Add(salvation.PlayerInName);
            hash.Add(salvation.PlayerOutName);
            hash.Add(salvation.SavedPoints);
        }

        foreach (var insight in gameweek.SwingInsights)
        {
            switch (insight)
            {
                case UniqueRemainingPlayerInsight unique:
                    hash.Add(unique.EntryId);
                    hash.Add(unique.PlayerName);
                    break;
                case CaptainClashInsight clash:
                    hash.Add(clash.FirstEntryId);
                    hash.Add(clash.FirstCaptain);
                    hash.Add(clash.SecondEntryId);
                    hash.Add(clash.SecondCaptain);
                    break;
            }
        }

        return hash.ToHashCode();
    }
}
