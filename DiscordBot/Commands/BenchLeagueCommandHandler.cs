using DiscordBot.BenchWarming;
using DiscordBot.Notifications;

namespace DiscordBot.Commands;

public sealed class BenchLeagueCommandHandler(
    IBenchWarmingLeagueStore benchWarmingStore,
    BenchWarmingMessageComposer messageComposer) : IBenchLeagueCommandHandler
{
    private const int DiscordMessageLimit = 2_000;
    private const string NoDataMessage =
        "🔥 Лига обогревателей скамейки пока недоступна. Попробуйте ещё раз позже.";

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var season = benchWarmingStore.GetLatestSeason();
        if (season is null)
        {
            await interaction.ModifyOriginalResponseAsync(NoDataMessage);
            return;
        }

        var message = messageComposer.ComposeSeasonStandings(
            season,
            benchWarmingStore.GetSeasonStandings(season));
        var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);

        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
