using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.PriceChanges;

public sealed class FplLeaguePriceChangeService(
    IFantasyPremierLeagueClient premierLeagueClient,
    FantasyPremierLeagueOptions options,
    ILogger<FplLeaguePriceChangeService> logger)
{
    public async Task<FplLeaguePriceChangeReport> CreateReportAsync(
        IReadOnlyList<FplPlayerPriceChange> changes,
        DateTimeOffset checkedAtUtc,
        int? eventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return new FplLeaguePriceChangeReport(
                checkedAtUtc,
                [],
                [],
                0,
                0,
                LeagueDataAvailable: false);
        }

        if (options.ClassicLeagueId <= 0 || eventId is null)
        {
            return CreateGlobalReport(changes, checkedAtUtc);
        }

        ClassicStanding[] managers;
        try
        {
            var standings = await premierLeagueClient.GetClassicStandingsAsync(
                options.ClassicLeagueId,
                cancellationToken);
            managers = standings.Standings.Results.ToArray();
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Failed to load FPL league {LeagueId} while enriching price changes.",
                options.ClassicLeagueId);
            return CreateGlobalReport(changes, checkedAtUtc);
        }

        var squadTasks = managers.Select(manager => LoadSquadAsync(
            manager,
            eventId.Value,
            cancellationToken));
        var squads = await Task.WhenAll(squadTasks);
        var availableSquads = squads
            .Where(squad => squad is not null)
            .Select(squad => squad!)
            .ToArray();

        var playerChanges = changes
            .Select(change => new FplLeaguePlayerPriceChange(
                change,
                availableSquads
                    .Where(squad => squad.PlayerIds.Contains(change.PlayerId))
                    .Select(squad => squad.EntryName)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        var changeByPlayer = changes.ToDictionary(change => change.PlayerId);
        var teamImpacts = availableSquads
            .Select(squad => new FplTeamPriceImpact(
                squad.EntryId,
                squad.EntryName,
                squad.PlayerIds
                    .Where(changeByPlayer.ContainsKey)
                    .Sum(playerId => changeByPlayer[playerId].Difference)))
            .Where(impact => impact.Difference != 0)
            .OrderBy(impact => impact.Difference)
            .ThenBy(impact => impact.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FplLeaguePriceChangeReport(
            checkedAtUtc,
            playerChanges,
            teamImpacts,
            managers.Length,
            availableSquads.Length,
            LeagueDataAvailable: true);
    }

    private async Task<FplManagerSquad?> LoadSquadAsync(
        ClassicStanding manager,
        int eventId,
        CancellationToken cancellationToken)
    {
        try
        {
            var picks = await premierLeagueClient.GetEntryEventPicksAsync(
                manager.Entry,
                eventId,
                cancellationToken);
            return new FplManagerSquad(
                manager.Entry,
                manager.EntryName,
                picks.Picks.Select(pick => pick.Element).ToHashSet());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to load FPL squad for entry {EntryId} and GW {EventId} " +
                "while enriching price changes.",
                manager.Entry,
                eventId);
            return null;
        }
    }

    private static FplLeaguePriceChangeReport CreateGlobalReport(
        IReadOnlyList<FplPlayerPriceChange> changes,
        DateTimeOffset checkedAtUtc)
    {
        return new FplLeaguePriceChangeReport(
            checkedAtUtc,
            changes
                .Select(change => new FplLeaguePlayerPriceChange(change, []))
                .ToArray(),
            [],
            0,
            0,
            LeagueDataAvailable: false);
    }

    private sealed record FplManagerSquad(
        int EntryId,
        string EntryName,
        HashSet<int> PlayerIds);
}
