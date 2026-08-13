using Discord;

namespace DiscordBot.Commands;

public static class DiscordApplicationCommands
{
    public const string DeadlineName = "deadline";
    public const string HugMeName = "hugme";
    public const string StandingsName = "standings";

    public static ApplicationCommandProperties[] BuildDesiredSet()
    {
        return
        [
            new SlashCommandBuilder()
                .WithName(HugMeName)
                .WithDescription("Hugs you!")
                .Build(),
            new SlashCommandBuilder()
                .WithName(StandingsName)
                .WithDescription("Shows the current configured FPL league standings.")
                .Build(),
            new SlashCommandBuilder()
                .WithName(DeadlineName)
                .WithDescription("Shows the next FPL Gameweek deadline in Riga time.")
                .Build()
        ];
    }
}
