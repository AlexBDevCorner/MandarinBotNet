using DiscordBot;
using Microsoft.Extensions.Logging;

namespace DiscordBot.UclFantasy;

/// <summary>
/// Resolves the current UCL matchday from the public fixtures feed unless a
/// matchday is pinned in configuration, then fetches the complete standings
/// for the configured league. Keeping matchday resolution here (rather than
/// hard-coding matchday 1) keeps standings current across matchdays.
/// </summary>
public sealed class UclLeagueStandingsProvider(
    IUclFantasyClient client,
    UclFantasyOptions leagueOptions,
    ILogger<UclLeagueStandingsProvider> logger)
{
    public UclFantasyOptions LeagueOptions => leagueOptions;

    public async Task<UclLeagueStandings> GetCurrentStandingsAsync(
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueOptions.LeagueId);

        var matchdayId = leagueOptions.MatchdayId;
        if (matchdayId <= 0)
        {
            matchdayId = await DetectCurrentMatchdayAsync(cancellationToken);
        }

        var query = new UclLeagueStandingsQuery(
            leagueOptions.LeagueId,
            leagueOptions.OptType,
            leagueOptions.PhaseId,
            matchdayId,
            leagueOptions.PageSize,
            leagueOptions.MaxStandingsPages);

        return await client.GetLeagueStandingsAsync(query, cancellationToken);
    }

    public UclLeagueStandingsQuery BuildQuery(int matchdayId)
    {
        return new UclLeagueStandingsQuery(
            leagueOptions.LeagueId,
            leagueOptions.OptType,
            leagueOptions.PhaseId,
            matchdayId,
            leagueOptions.PageSize,
            leagueOptions.MaxStandingsPages);
    }

    private async Task<int> DetectCurrentMatchdayAsync(CancellationToken cancellationToken)
    {
        try
        {
            var webConfiguration = await client.GetWebConfigurationAsync(cancellationToken);
            var configuration = webConfiguration.Data?.Value;
            if (configuration is null || configuration.TourId <= 0)
            {
                throw new UclFantasyApiException(
                    UclFantasyFailureKind.InvalidPayload,
                    "The UCL Fantasy web configuration did not include a tour identifier.");
            }

            var fixtures = await client.GetFixturesAsync(configuration, cancellationToken);
            var matchdays = fixtures.Data?.Value;
            if (matchdays is null || matchdays.Count == 0)
            {
                throw new UclFantasyApiException(
                    UclFantasyFailureKind.InvalidPayload,
                    "The UCL Fantasy fixtures feed did not include matchdays.");
            }

            var current = matchdays.FirstOrDefault(matchday => matchday.IsCurrent != 0);
            if (current is not null && current.MatchdayId > 0)
            {
                return current.MatchdayId;
            }

            var fallback = matchdays
                .Where(matchday => matchday.MatchdayId > 0)
                .MaxBy(matchday => matchday.MatchdayId);
            if (fallback is not null)
            {
                logger.LogWarning(
                    "UCL Fantasy fixtures feed has no current matchday flag; falling back to matchday {MatchdayId}.",
                    fallback.MatchdayId);
                return fallback.MatchdayId;
            }

            throw new UclFantasyApiException(
                UclFantasyFailureKind.InvalidPayload,
                "The UCL Fantasy fixtures feed did not include a usable matchday.");
        }
        catch (UclFantasyApiException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new UclFantasyApiException(
                UclFantasyFailureKind.InvalidPayload,
                "The UCL Fantasy current matchday could not be determined.",
                innerException: exception);
        }
    }
}
