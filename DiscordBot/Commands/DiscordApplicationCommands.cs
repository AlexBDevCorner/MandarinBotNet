using Discord;

namespace DiscordBot.Commands;

public static class DiscordApplicationCommands
{
    public const string HugMeName = "hugme";

    public static ApplicationCommandProperties[] BuildDesiredSet()
    {
        return
        [
            new SlashCommandBuilder()
                .WithName(HugMeName)
                .WithDescription("Hugs you!")
                .Build()
        ];
    }
}
