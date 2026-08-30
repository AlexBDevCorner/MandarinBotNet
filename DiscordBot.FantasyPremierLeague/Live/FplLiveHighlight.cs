namespace DiscordBot.FantasyPremierLeague.Live;

public abstract record FplLiveHighlight(
    string Key,
    DateTimeOffset DetectedAtUtc);

public sealed record LeaderChangedHighlight(
    int PreviousLeaderEntryId,
    string PreviousLeaderName,
    int NewLeaderEntryId,
    string NewLeaderName,
    int NewLeaderPoints,
    int Gap,
    DateTimeOffset DetectedAtUtc)
    : FplLiveHighlight("leader", DetectedAtUtc);

public sealed record SignificantRankChangeHighlight(
    int EntryId,
    string EntryName,
    int PreviousRank,
    int CurrentRank,
    DateTimeOffset DetectedAtUtc)
    : FplLiveHighlight($"rank:{EntryId}", DetectedAtUtc);

public sealed record BenchThresholdReachedHighlight(
    int EntryId,
    string EntryName,
    int BenchPoints,
    DateTimeOffset DetectedAtUtc)
    : FplLiveHighlight($"bench:{EntryId}", DetectedAtUtc);

public sealed record CaptainSuccessHighlight(
    int EntryId,
    string EntryName,
    string CaptainName,
    int EffectivePoints,
    DateTimeOffset DetectedAtUtc)
    : FplLiveHighlight($"captain-success:{EntryId}", DetectedAtUtc);

public sealed record CaptainDisasterHighlight(
    int EntryId,
    string EntryName,
    string CaptainName,
    int CaptainPoints,
    string ViceCaptainName,
    int ViceCaptainPoints,
    DateTimeOffset DetectedAtUtc)
    : FplLiveHighlight($"captain-disaster:{EntryId}", DetectedAtUtc);

public sealed record AutomaticSubstitutionHighlight(
    int EntryId,
    string EntryName,
    string PlayerOutName,
    string PlayerInName,
    int SavedPoints,
    DateTimeOffset DetectedAtUtc)
    : FplLiveHighlight(
        $"autosub:{EntryId}:{PlayerOutName}:{PlayerInName}",
        DetectedAtUtc);
