using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Standings;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Commands;

public sealed class StandingsCommandHandler(
    IFantasyPremierLeagueClient premierLeagueClient,
    FantasyPremierLeagueOptions options,
    PremierLeagueMessageCompositionService messageComposer,
    FplStandingsSelectionService standingsSelection,
    ILogger<StandingsCommandHandler> logger) : IStandingsCommandHandler
{
    private const int DiscordMessageLimit = 2_000;
    private const string RetryLaterMessage =
        "⚠️ Таблицы FPL сейчас недоступны. Попробуйте ещё раз позже.";
    private const string TopAroundConflictMessage =
        "⚠️ Используйте либо top, либо around — эти режимы нельзя комбинировать.";
    private const string AroundH2hConflictMessage =
        "⚠️ Режим around пока доступен только для классической лиги.";

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var query = await ResolveQueryAsync(interaction);
        if (query is null)
        {
            return;
        }

        await interaction.DeferAsync();

        var needsClassic = query.League is FplStandingsLeague.Both or FplStandingsLeague.Classic;
        var needsHeadToHead = query.League is FplStandingsLeague.Both or FplStandingsLeague.HeadToHead;

        FetchResult<ClassicStandingsResponse> classic =
            FetchResult<ClassicStandingsResponse>.Failure();
        FetchResult<HeadToHeadStandingsResponse> headToHead =
            FetchResult<HeadToHeadStandingsResponse>.Failure();

        if (needsClassic && needsHeadToHead)
        {
            var classicTask = CaptureAsync(
                "Classic",
                () => premierLeagueClient.GetClassicStandingsAsync(
                    options.ClassicLeagueId,
                    CancellationToken.None));
            var headToHeadTask = CaptureAsync(
                "HeadToHead",
                () => premierLeagueClient.GetHeadToHeadStandingsAsync(
                    options.HeadToHeadLeagueId,
                    CancellationToken.None));

            await Task.WhenAll(classicTask, headToHeadTask);
            classic = await classicTask;
            headToHead = await headToHeadTask;
        }
        else if (needsClassic)
        {
            classic = await CaptureAsync(
                "Classic",
                () => premierLeagueClient.GetClassicStandingsAsync(
                    options.ClassicLeagueId,
                    CancellationToken.None));
        }
        else
        {
            headToHead = await CaptureAsync(
                "HeadToHead",
                () => premierLeagueClient.GetHeadToHeadStandingsAsync(
                    options.HeadToHeadLeagueId,
                    CancellationToken.None));
        }

        if (query.League == FplStandingsLeague.Both &&
            !classic.Succeeded && !headToHead.Succeeded)
        {
            await interaction.ModifyOriginalResponseAsync(RetryLaterMessage);
            return;
        }

        var sections = new List<string>();

        if (needsClassic)
        {
            sections.Add(ComposeClassicSection(classic, query));
        }

        if (needsHeadToHead)
        {
            sections.Add(headToHead.Succeeded
                ? messageComposer.ComposeHeadToHeadStandingsSnapshot(
                    standingsSelection.SelectHeadToHead(
                        headToHead.Value!.HeadToHeadStandings.Results,
                        query.Top))
                : PremierLeagueMessageCompositionService
                    .ComposeUnavailableHeadToHeadStandings());
        }

        var chunks = DiscordMessageChunker.Split(
            string.Join("\n\n", sections),
            DiscordMessageLimit);

        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }

    private string ComposeClassicSection(
        FetchResult<ClassicStandingsResponse> classic,
        FplStandingsQuery query)
    {
        if (!classic.Succeeded)
        {
            return PremierLeagueMessageCompositionService
                .ComposeUnavailableClassicStandings();
        }

        if (query.Around is not null)
        {
            var result = standingsSelection.ResolveAround(
                classic.Value!.Standings.Results,
                query.Around);
            return result.Status switch
            {
                FplAroundLookupStatus.Available =>
                    messageComposer.ComposeClassicAroundStandings(
                        result, query.Around),
                FplAroundLookupStatus.NotFound =>
                    messageComposer.ComposeManagerNotFound(query.Around),
                _ => messageComposer.ComposeAmbiguousManager(
                    query.Around, result.Candidates)
            };
        }

        return messageComposer.ComposeClassicStandingsSnapshot(
            standingsSelection.SelectClassic(
                classic.Value!.Standings.Results,
                query.Top));
    }

    private async Task<FplStandingsQuery?> ResolveQueryAsync(
        IDiscordSlashCommandInteraction interaction)
    {
        var leagueValue = interaction.GetStringOption("league");
        var topValue = interaction.GetIntegerOption("top");
        var aroundValue = interaction.GetStringOption("around");

        if (topValue is not null && aroundValue is not null)
        {
            await interaction.RespondAsync(TopAroundConflictMessage);
            return null;
        }

        var league = leagueValue?.ToLowerInvariant() switch
        {
            "classic" => FplStandingsLeague.Classic,
            "h2h" => FplStandingsLeague.HeadToHead,
            _ => FplStandingsLeague.Both
        };

        if (aroundValue is not null && league == FplStandingsLeague.HeadToHead)
        {
            await interaction.RespondAsync(AroundH2hConflictMessage);
            return null;
        }

        if (aroundValue is not null && league == FplStandingsLeague.Both)
        {
            league = FplStandingsLeague.Classic;
        }

        int? top = topValue is null ? null : (int)topValue.Value;

        return new FplStandingsQuery(league, top, aroundValue);
    }

    private async Task<FetchResult<T>> CaptureAsync<T>(
        string leagueType,
        Func<Task<T>> fetch)
    {
        try
        {
            return FetchResult<T>.Success(await fetch());
        }
        catch (FantasyPremierLeagueApiException exception)
        {
            logger.LogWarning(
                exception,
                "FPL standings command request for {LeagueType} failed with {FailureKind} and HTTP status {StatusCode}.",
                leagueType,
                exception.FailureKind,
                exception.StatusCode);
            return FetchResult<T>.Failure();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "FPL standings command request for {LeagueType} failed with {FailureKind}.",
                leagueType,
                "Unexpected");
            return FetchResult<T>.Failure();
        }
    }

    private sealed record FetchResult<T>(bool Succeeded, T? Value)
    {
        public static FetchResult<T> Success(T value) => new(true, value);

        public static FetchResult<T> Failure() => new(false, default);
    }
}
