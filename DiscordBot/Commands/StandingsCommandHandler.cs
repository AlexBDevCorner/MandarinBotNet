using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Commands;

public sealed class StandingsCommandHandler(
    IFantasyPremierLeagueClient premierLeagueClient,
    FantasyPremierLeagueOptions options,
    PremierLeagueMessageCompositionService messageComposer,
    ILogger<StandingsCommandHandler> logger) : IStandingsCommandHandler
{
    private const int DiscordMessageLimit = 2_000;
    private const string RetryLaterMessage =
        "⚠️ Таблицы FPL сейчас недоступны. Попробуйте ещё раз позже.";

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

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
        var classic = await classicTask;
        var headToHead = await headToHeadTask;

        if (!classic.Succeeded && !headToHead.Succeeded)
        {
            await interaction.ModifyOriginalResponseAsync(RetryLaterMessage);
            return;
        }

        var sections = new[]
        {
            classic.Succeeded
                ? messageComposer.ComposeClassicStandingsSnapshot(
                    classic.Value!.Standings.Results)
                : PremierLeagueMessageCompositionService
                    .ComposeUnavailableClassicStandings(),
            headToHead.Succeeded
                ? messageComposer.ComposeHeadToHeadStandingsSnapshot(
                    headToHead.Value!.HeadToHeadStandings.Results)
                : PremierLeagueMessageCompositionService
                    .ComposeUnavailableHeadToHeadStandings()
        };
        var chunks = DiscordMessageChunker.Split(
            string.Join("\n\n", sections),
            DiscordMessageLimit);

        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
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
