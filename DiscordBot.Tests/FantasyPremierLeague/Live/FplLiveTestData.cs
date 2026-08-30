using DiscordBot.FantasyPremierLeague.Live;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

internal static class FplLiveTestData
{
    internal static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    internal static FplLiveGameweek CreateGameweek(
        IReadOnlyList<FplLiveManagerInsights>? managers = null,
        IReadOnlyList<int>? captainDisasterEntryIds = null,
        IReadOnlyList<int>? captainSuccessEntryIds = null,
        IReadOnlyList<FplAutomaticSubstitutionSalvation>? substitutions = null,
        int eventId = 3,
        DateTimeOffset? capturedAtUtc = null,
        string season = "2026/27")
    {
        managers ??=
        [
            CreateManager(1, "Alpha", 1, 120),
            CreateManager(2, "Beta", 2, 117),
            CreateManager(3, "Gamma", 3, 110),
            CreateManager(4, "Delta", 4, 105)
        ];
        captainDisasterEntryIds ??= [];
        captainSuccessEntryIds ??= [];
        substitutions ??= [];
        var managerByEntry = managers.ToDictionary(manager => manager.EntryId);

        return new FplLiveGameweek(
            season,
            eventId,
            CapturedAtUtc.AddMinutes(-5),
            capturedAtUtc ?? CapturedAtUtc,
            managers,
            managers.Where(manager => manager.BenchPoints >= 8).ToArray(),
            captainDisasterEntryIds.Select(entryId => managerByEntry[entryId]).ToArray(),
            captainSuccessEntryIds.Select(entryId => managerByEntry[entryId]).ToArray(),
            substitutions,
            []);
    }

    internal static FplLiveManagerInsights CreateManager(
        int entryId,
        string entryName,
        int liveRank,
        int liveTotalPoints,
        int benchPoints = 0,
        int captainEffectivePoints = 0,
        FplLivePlayerProgress? playerProgress = null,
        int captainPoints = 0,
        int viceCaptainPoints = 0)
    {
        return new FplLiveManagerInsights(
            entryId,
            entryName,
            $"Manager {entryId}",
            liveRank,
            liveRank,
            liveRank,
            0,
            100,
            liveTotalPoints,
            liveTotalPoints - 100,
            0,
            liveTotalPoints - 100,
            liveTotalPoints,
            liveRank == 1 ? 0 : 3,
            playerProgress ?? new FplLivePlayerProgress(0, 0),
            benchPoints,
            new FplLiveCaptainInsights(
                "Captain",
                captainPoints,
                captainEffectivePoints,
                "Vice",
                viceCaptainPoints,
                viceCaptainPoints),
            []);
    }

    internal static FplAutomaticSubstitutionSalvation CreateSubstitution(
        int savedPoints,
        int entryId = 1,
        string entryName = "Alpha",
        string playerOutName = "Out",
        string playerInName = "In")
    {
        return new FplAutomaticSubstitutionSalvation(
            entryId,
            entryName,
            playerInName,
            savedPoints,
            playerOutName,
            0,
            savedPoints);
    }
}
