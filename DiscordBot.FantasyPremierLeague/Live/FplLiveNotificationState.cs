namespace DiscordBot.FantasyPremierLeague.Live;

public sealed record FplLiveNotificationState(
    int ClassicLeagueId,
    string Season,
    int EventId,
    FplLiveGameweek LastObservedSnapshot,
    IReadOnlyList<FplLiveTargetNotificationState> Targets);

public sealed record FplLiveTargetNotificationState(
    ulong GuildId,
    ulong ChannelId,
    DateTimeOffset? LastPublishedAtUtc,
    long NextDigestSequence,
    IReadOnlyList<FplLiveHighlight> PendingHighlights);

public enum FplLiveNotificationOutcome
{
    BaselineEstablished,
    NothingInteresting,
    CooldownActive,
    DigestReady
}

public sealed record FplLiveNotificationEvaluation(
    FplLiveNotificationOutcome Outcome,
    int DetectedHighlightCount,
    IReadOnlyList<FplLiveDigest> Digests);

public sealed record FplLiveDigest(
    int ClassicLeagueId,
    string Season,
    int EventId,
    ulong GuildId,
    ulong ChannelId,
    long Sequence,
    string SourceIdentifier,
    IReadOnlyList<FplLiveHighlight> Highlights);
