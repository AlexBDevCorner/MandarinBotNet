using DiscordBot.FantasyPremierLeague.Historical;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public sealed class FplAchievementCalculationService(
    FantasyPremierLeagueOptions options)
{
    public IReadOnlyList<FplAchievementAward> Calculate(
        int leagueId,
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplGameweekSnapshot> seasonHistory,
        IReadOnlyList<FplAchievementAward> existingAwards,
        DateTimeOffset awardedAtUtc)
    {
        ValidateInputs(leagueId, snapshot, seasonHistory, existingAwards, awardedAtUtc);

        var orderedManagers = snapshot.Managers
            .OrderBy(manager => manager.Rank)
            .ThenBy(manager => manager.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manager => manager.EntryId)
            .ToArray();
        var captains = orderedManagers.ToDictionary(
            manager => manager.EntryId,
            GetCaptainData);
        var captainCounts = captains.Values
            .GroupBy(data => data.Captain.PlayerId)
            .ToDictionary(group => group.Key, group => group.Count());
        var highestScore = orderedManagers.Max(manager => manager.EventScore);
        var firstSnapshot = seasonHistory
            .Where(item => item.Season == snapshot.Season)
            .OrderBy(item => item.EventId)
            .FirstOrDefault();
        var isFirstRecordedGameweek = firstSnapshot?.EventId == snapshot.EventId;
        var awards = new List<FplAchievementAward>();
        var awardTime = awardedAtUtc.ToUniversalTime();

        foreach (var manager in orderedManagers)
        {
            var captainData = captains[manager.EntryId];
            foreach (var definition in GetDefinitions())
            {
                if (!MeetsRule(
                        definition.Key,
                        manager,
                        captainData,
                        captainCounts,
                        highestScore,
                        isFirstRecordedGameweek))
                {
                    continue;
                }

                if (!definition.IsRepeatable && existingAwards.Any(award =>
                        award.LeagueId == leagueId &&
                        award.Season == snapshot.Season &&
                        award.EntryId == manager.EntryId &&
                        award.AchievementKey == definition.Key))
                {
                    continue;
                }

                awards.Add(new FplAchievementAward(
                    leagueId,
                    snapshot.Season,
                    snapshot.EventId,
                    manager.EntryId,
                    manager.EntryName,
                    definition.Key,
                    definition.Name,
                    definition.Description,
                    definition.IsRepeatable,
                    options.RecognitionRuleVersion,
                    awardTime));
            }
        }

        return awards;
    }

    private IReadOnlyList<FplAchievementDefinition> GetDefinitions()
    {
        return
        [
            new FplAchievementDefinition(
                FplAchievementKeys.FirstBlood,
                "First Blood",
                "Won the first recorded gameweek of the season.",
                IsRepeatable: false),
            new FplAchievementDefinition(
                FplAchievementKeys.BenchWarmer,
                "Bench Warmer",
                $"Left at least {options.LargeBenchPointsThreshold} points on the bench.",
                IsRepeatable: true),
            new FplAchievementDefinition(
                FplAchievementKeys.CaptainDisaster,
                "Captain Disaster",
                $"Captained a player on at most {options.CaptainDisasterPointsThreshold} raw " +
                "points while the vice-captain scored at least " +
                $"{options.CaptainDisasterViceCaptainPointsThreshold}.",
                IsRepeatable: true),
            new FplAchievementDefinition(
                FplAchievementKeys.DifferentialMerchant,
                "Differential Merchant",
                "Was the only manager in the league to captain that player.",
                IsRepeatable: true),
            new FplAchievementDefinition(
                FplAchievementKeys.MinusEightEnjoyer,
                "-8 Enjoyer",
                $"Took at least {options.TransferCostAchievementThreshold} points in " +
                "transfer hits.",
                IsRepeatable: true)
        ];
    }

    private bool MeetsRule(
        string achievementKey,
        FplManagerGameweekStatistics manager,
        CaptainData captainData,
        IReadOnlyDictionary<int, int> captainCounts,
        int highestScore,
        bool isFirstRecordedGameweek)
    {
        return achievementKey switch
        {
            FplAchievementKeys.FirstBlood => isFirstRecordedGameweek &&
                manager.EventScore == highestScore,
            FplAchievementKeys.BenchWarmer => manager.BenchPoints >= options.LargeBenchPointsThreshold,
            FplAchievementKeys.CaptainDisaster => captainData.Captain.Points <=
                    options.CaptainDisasterPointsThreshold &&
                captainData.ViceCaptain.Points >=
                    options.CaptainDisasterViceCaptainPointsThreshold &&
                captainData.Captain.Points < captainData.ViceCaptain.Points,
            FplAchievementKeys.DifferentialMerchant => captainCounts[captainData.Captain.PlayerId] == 1,
            FplAchievementKeys.MinusEightEnjoyer => manager.TransferCost >=
                options.TransferCostAchievementThreshold,
            _ => throw new ArgumentOutOfRangeException(nameof(achievementKey))
        };
    }

    private static CaptainData GetCaptainData(FplManagerGameweekStatistics manager)
    {
        var captains = manager.Lineup.Where(pick => pick.IsCaptain).ToArray();
        var viceCaptains = manager.Lineup.Where(pick => pick.IsViceCaptain).ToArray();
        if (captains.Length != 1 || viceCaptains.Length != 1)
        {
            throw new InvalidDataException(
                $"Manager entry {manager.EntryId} must have exactly one captain and " +
                "vice-captain.");
        }

        return new CaptainData(captains[0], viceCaptains[0]);
    }

    private static void ValidateInputs(
        int leagueId,
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplGameweekSnapshot> seasonHistory,
        IReadOnlyList<FplAchievementAward> existingAwards,
        DateTimeOffset awardedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(seasonHistory);
        ArgumentNullException.ThrowIfNull(existingAwards);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.Season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(snapshot.EventId);
        ArgumentNullException.ThrowIfNull(snapshot.Managers);
        if (snapshot.Managers.Count == 0)
        {
            throw new InvalidDataException("The snapshot must contain at least one manager.");
        }

        if (awardedAtUtc == default)
        {
            throw new ArgumentException(
                "The achievement calculation must include an award timestamp.",
                nameof(awardedAtUtc));
        }

        var entryIds = new HashSet<int>();
        foreach (var manager in snapshot.Managers)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(manager.EntryId);
            ArgumentException.ThrowIfNullOrWhiteSpace(manager.EntryName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(manager.Rank);
            ArgumentNullException.ThrowIfNull(manager.Lineup);
            if (!entryIds.Add(manager.EntryId))
            {
                throw new InvalidDataException(
                    $"The snapshot contains duplicate manager entry {manager.EntryId}.");
            }
        }
    }

    private sealed record CaptainData(
        FplLineupPick Captain,
        FplLineupPick ViceCaptain);
}
