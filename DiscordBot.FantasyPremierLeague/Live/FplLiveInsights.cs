using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
            $"{ComputeLiveStateHash(gameweek)}";
    }

    private static string ComputeLiveStateHash(FplLiveGameweek gameweek)
    {
        var canonical = new StringBuilder();
        foreach (var manager in gameweek.Managers)
        {
            canonical.Append("m:")
                .Append(manager.EntryId).Append('|')
                .Append(manager.LiveTotalPoints).Append('|')
                .Append(manager.LiveGameweekPoints).Append('|')
                .Append(manager.LiveRank).Append('|')
                .Append(manager.RankChange).Append('|')
                .Append(manager.TransferCost).Append('|')
                .Append(manager.BenchPoints).Append('|')
                .Append(manager.PlayerProgress.Playing).Append('|')
                .Append(manager.PlayerProgress.YetToPlay).Append('|')
                .Append(manager.Captain.CaptainEffectivePoints).Append('|')
                .Append(manager.Captain.ViceCaptainEffectivePoints).Append(';');
        }

        foreach (var salvation in gameweek.AutomaticSubstitutionSalvations)
        {
            canonical.Append("s:")
                .Append(salvation.EntryId).Append('|')
                .Append(salvation.PlayerInName).Append('|')
                .Append(salvation.PlayerOutName).Append('|')
                .Append(salvation.SavedPoints).Append(';');
        }

        foreach (var insight in gameweek.SwingInsights)
        {
            switch (insight)
            {
                case UniqueRemainingPlayerInsight unique:
                    canonical.Append("u:")
                        .Append(unique.EntryId).Append('|')
                        .Append(unique.PlayerName).Append(';');
                    break;
                case CaptainClashInsight clash:
                    canonical.Append("c:")
                        .Append(clash.FirstEntryId).Append('|')
                        .Append(clash.FirstCaptain).Append('|')
                        .Append(clash.SecondEntryId).Append('|')
                        .Append(clash.SecondCaptain).Append(';');
                    break;
            }
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        var fingerprint = new StringBuilder(digest.Length * 2);
        for (var i = 0; i < 16; i++)
        {
            fingerprint.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return fingerprint.ToString();
    }
}
