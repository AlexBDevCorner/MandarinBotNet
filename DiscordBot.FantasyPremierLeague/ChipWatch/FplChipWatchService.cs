using System.Collections.Concurrent;
using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.ChipWatch;

public sealed record FplChipWatchContext(
    int TargetEventId,
    DateTimeOffset DeadlineUtc,
    int FinalEventId,
    int? SourceSquadEventId,
    BootstrapStaticResponse Bootstrap);

public sealed class FplChipWatchService(
    IFantasyPremierLeagueClient client,
    FantasyPremierLeagueOptions options,
    DeadlineSelectionService deadlineSelection,
    FplChipUsageService chipUsageService,
    FplFreeHitOpportunityService freeHitService,
    TimeProvider timeProvider,
    ILogger<FplChipWatchService> logger)
{
    internal const int ManagerConcurrencyLimit = 5;

    public async Task<FplChipWatchContext?> GetUpcomingContextAsync(CancellationToken cancellationToken)
    {
        var bootstrap = await client.GetBootstrapStaticAsync(cancellationToken);
        var nextDeadline = deadlineSelection.SelectNext(bootstrap.Events);
        if (nextDeadline is null)
        {
            logger.LogInformation("Chip Watch: no upcoming Gameweek found; skipping report.");
            return null;
        }

        var targetEventId = nextDeadline.EventId;
        var deadlineUtc = nextDeadline.DeadlineUtc;
        var finalEventId = bootstrap.Events.Max(e => e.Id);
        var sourceEventId = ResolveSourceEventId(bootstrap.Events, targetEventId, timeProvider.GetUtcNow());

        return new FplChipWatchContext(targetEventId, deadlineUtc, finalEventId, sourceEventId, bootstrap);
    }

    public async Task<FplChipWatchReport?> CreateReportAsync(CancellationToken cancellationToken)
    {
        var context = await GetUpcomingContextAsync(cancellationToken);
        if (context is null)
        {
            return null;
        }

        return await CreateReportAsync(context, cancellationToken);
    }

    public async Task<FplChipWatchReport> CreateReportAsync(
        FplChipWatchContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bootstrap = context.Bootstrap;
        var targetEventId = context.TargetEventId;
        var deadlineUtc = context.DeadlineUtc;
        var finalEventId = context.FinalEventId;
        var sourceEventId = context.SourceSquadEventId;

        var fixtures = await client.GetFixturesAsync(targetEventId, cancellationToken);
        var fixturesByTeam = BuildFixturesByTeam(fixtures);

        var standings = await client.GetClassicStandingsAsync(options.ClassicLeagueId, cancellationToken);
        var managers = standings.Standings.Results.ToArray();

        var elementsById = bootstrap.Elements.ToDictionary(e => e.Id);

        var results = new ConcurrentBag<FplManagerChipWatch>();
        var historyAvailableCount = 0;
        var squadAvailableCount = 0;

        var countsLock = new object();

        await Parallel.ForEachAsync(
            managers,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = ManagerConcurrencyLimit
            },
            async (manager, ct) =>
            {
                var entryId = manager.Entry;
                IReadOnlyList<FplPlayedChip>? history = null;
                bool chipHistoryAvailable = false;
                IReadOnlyList<FplChipAvailability>? availabilities = null;
                EntryEventPicksResponse? picksResponse = null;
                bool squadAvailable = false;
                int managerStartedEventId = 1;

                // Fetch entry metadata for opening GW restriction
                try
                {
                    var entryResponse = await client.GetEntryAsync(entryId, ct);
                    if (entryResponse.StartedEvent > 0)
                    {
                        managerStartedEventId = entryResponse.StartedEvent;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to load entry metadata for FPL entry {EntryId}; assuming started GW1.",
                        entryId);
                }

                // Fetch history
                try
                {
                    var historyResponse = await client.GetEntryHistoryAsync(entryId, ct);
                    history = chipUsageService.MapHistory(historyResponse.Chips);
                    availabilities = chipUsageService.GetAvailabilities(
                        targetEventId,
                        finalEventId,
                        history,
                        managerStartedEventId);
                    chipHistoryAvailable = true;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to load chip history for FPL entry {EntryId}.",
                        entryId);
                }

                // Fetch picks if source exists
                if (sourceEventId is not null)
                {
                    try
                    {
                        picksResponse = await client.GetEntryEventPicksAsync(entryId, sourceEventId.Value, ct);
                        squadAvailable = picksResponse.Picks is not null;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to load public squad for FPL entry {EntryId} and GW {EventId}.",
                            entryId,
                            sourceEventId.Value);
                        squadAvailable = false;
                    }
                }

                // Build availabilities if history failed? Then we cannot know availability; keep empty list but mark history unavailable.
                // For display, we need availabilities even if history fails? Spec says show chip availability where possible, but without history we cannot safely know.
                // So if history unavailable, availabilities stays null and we will create empty chips list.

                IReadOnlyList<FplChipAvailability> chipsForManager;
                if (availabilities is not null)
                {
                    chipsForManager = availabilities;
                }
                else
                {
                    // No history: create empty list with no availability? Alternatively create list with IsAvailable false and null urgency?
                    // To keep consistent, create availabilities with no history but mark as not available? Spec says don't guess.
                    // We'll create list with IsAvailable=false and no UsedEventId and urgency None, but ChipHistoryAvailable false will indicate not to recommend.
                    // However spec says "Missing chip history prevents recommendations for that manager." So FreeHitRecommendation must be null.
                    // We'll produce empty chips list but keep count.
                    chipsForManager = Array.Empty<FplChipAvailability>();
                }

                FplChipRecommendation? recommendation = null;
                if (chipHistoryAvailable && squadAvailable && picksResponse is not null && availabilities is not null)
                {
                    var freeHitAvailability = availabilities.FirstOrDefault(a => a.Chip == FplChipType.FreeHit);
                    if (freeHitAvailability is not null)
                    {
                        // Only recommend if chip is available (usage service already handled GW1 and consecutive)
                        // freeHitService returns null if unavailable
                        try
                        {
                            var picks = picksResponse.Picks;
                            if (picks is not null)
                            {
                                recommendation = freeHitService.Evaluate(
                                    freeHitAvailability,
                                    picks,
                                    elementsById,
                                    fixturesByTeam);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(
                                ex,
                                "Failed to evaluate Free Hit opportunity for FPL entry {EntryId}.",
                                entryId);
                        }
                    }
                }

                if (chipHistoryAvailable)
                {
                    lock (countsLock)
                    {
                        historyAvailableCount++;
                    }
                }

                if (squadAvailable)
                {
                    lock (countsLock)
                    {
                        squadAvailableCount++;
                    }
                }

                var managerResult = new FplManagerChipWatch(
                    EntryId: entryId,
                    EntryName: manager.EntryName,
                    PlayerName: manager.PlayerName,
                    SourceSquadEventId: sourceEventId,
                    Chips: chipsForManager,
                    FreeHitRecommendation: recommendation,
                    ChipHistoryAvailable: chipHistoryAvailable,
                    SquadAvailable: squadAvailable);

                results.Add(managerResult);
            });

        // Deterministic ordering: FreeHit Score descending, then EntryName case-insensitive, then EntryId
        // But for report, we want managers sorted deterministically for later composition.
        // We'll sort by that rule for those with recommendation, but also for overall list we need deterministic.
        // Spec says for Free Hit recommendations sort by score descending, etc., and for expiry-only entries sort by highest urgency descending.
        // The service should return managers in deterministic order overall: we'll sort by EntryName case-insensitive, EntryId as fallback, but also keep recommendation order stable.
        // Simpler: sort all managers by EntryName case-insensitive then EntryId, and let composers sort subsets.
        // However spec says "Managers are returned in deterministic order." We'll provide sorted by EntryName.

        var sortedManagers = results
            .OrderBy(m => m.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.EntryId)
            .ToList();

        // Alternative: sort by FreeHit score descending for those with strong signals? But report itself should be deterministic regardless.
        // We'll keep sorted by name for report; composers will re-sort their subsets.

        logger.LogInformation(
            "Chip Watch report created for GW {TargetEventId} from source GW {SourceEventId} for league {LeagueId} with {ManagerCount} managers, {HistoryCount} with history, {SquadCount} with squad, deadline {DeadlineUtc}.",
            targetEventId,
            sourceEventId,
            options.ClassicLeagueId,
            managers.Length,
            historyAvailableCount,
            squadAvailableCount,
            deadlineUtc);

        return new FplChipWatchReport(
            TargetEventId: targetEventId,
            DeadlineUtc: deadlineUtc,
            SourceSquadEventId: sourceEventId,
            FinalEventId: finalEventId,
            ManagersTotal: managers.Length,
            ManagersWithChipHistory: historyAvailableCount,
            ManagersWithSquadData: squadAvailableCount,
            Managers: sortedManagers);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<PremierLeagueFixture>> BuildFixturesByTeam(
        IReadOnlyList<PremierLeagueFixture> fixtures)
    {
        var dict = new Dictionary<int, List<PremierLeagueFixture>>();
        foreach (var fixture in fixtures)
        {
            if (!dict.TryGetValue(fixture.HomeTeamId, out var homeList))
            {
                homeList = [];
                dict[fixture.HomeTeamId] = homeList;
            }

            homeList.Add(fixture);

            if (!dict.TryGetValue(fixture.AwayTeamId, out var awayList))
            {
                awayList = [];
                dict[fixture.AwayTeamId] = awayList;
            }

            awayList.Add(fixture);
        }

        return dict.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<PremierLeagueFixture>)kvp.Value.AsReadOnly());
    }

    private static int? ResolveSourceEventId(
        List<PremierLeagueEvent> events,
        int targetEventId,
        DateTimeOffset now)
    {
        var source = events
            .Where(e => e.Id < targetEventId && DateTimeOffset.FromUnixTimeSeconds(e.DeadlineTimeEpoch) <= now)
            .OrderByDescending(e => e.Id)
            .FirstOrDefault();

        return source?.Id;
    }
}
