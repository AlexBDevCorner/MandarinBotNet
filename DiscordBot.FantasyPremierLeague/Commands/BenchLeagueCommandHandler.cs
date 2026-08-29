using DiscordBot.BenchWarming;
using DiscordBot.Notifications;

namespace DiscordBot.Commands;

public sealed class BenchLeagueCommandHandler(
    BenchWarmingQueryService queryService,
    BenchWarmingMessageComposer messageComposer) : IBenchLeagueCommandHandler
{
    private const int DiscordMessageLimit = 2_000;
    private const string NoDataMessage =
        "🔥 Лига обогревателей скамейки пока недоступна. Попробуйте ещё раз позже.";
    private const string BothOptionsMessage =
        "Используйте либо `team`, либо `gw`, но не оба параметра одновременно.";

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var team = interaction.GetStringOption("team");
        var gw = interaction.GetIntegerOption("gw");

        string message;
        if (!string.IsNullOrWhiteSpace(team) && gw.HasValue)
        {
            message = BothOptionsMessage;
        }
        else if (!string.IsNullOrWhiteSpace(team))
        {
            message = ComposeTeamMessage(team!);
        }
        else if (gw.HasValue)
        {
            message = ComposeRoundMessage((int)gw.Value);
        }
        else
        {
            message = ComposeOverviewMessage();
        }

        var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);

        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }

    private string ComposeOverviewMessage()
    {
        var overview = queryService.GetSeasonOverview();
        return overview is null
            ? NoDataMessage
            : messageComposer.ComposeSeasonOverview(overview);
    }

    private string ComposeRoundMessage(int eventId)
    {
        var result = queryService.GetRound(eventId);
        return result.Outcome switch
        {
            BenchWarmingRoundSummaryOutcome.Available when result.Summary is not null
                => messageComposer.ComposeRoundStandings(result.Summary),
            BenchWarmingRoundSummaryOutcome.NotTracked when result.Tracking is not null
                => messageComposer.ComposeRoundNotTracked(eventId, result.Tracking),
            _ => NoDataMessage
        };
    }

    private string ComposeTeamMessage(string team)
    {
        var result = queryService.GetTeam(team);
        return result.Outcome switch
        {
            BenchWarmingTeamLookupOutcome.Available when result.Profile is not null
                => messageComposer.ComposeTeamProfile(result.Profile),
            BenchWarmingTeamLookupOutcome.NotFound
                => messageComposer.ComposeTeamNotFound(team),
            BenchWarmingTeamLookupOutcome.Ambiguous when result.Candidates is not null
                => messageComposer.ComposeAmbiguousTeam(team, result.Candidates),
            _ => NoDataMessage
        };
    }
}
