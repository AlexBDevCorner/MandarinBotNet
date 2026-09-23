namespace DiscordBot.Commands;

public interface IDiscordSlashCommandInteraction
{
    string Name { get; }

    string UserMention { get; }

    string? GetStringOption(string name);

    long? GetIntegerOption(string name);

    Task RespondAsync(string content);

    Task DeferAsync();

    Task ModifyOriginalResponseAsync(string content);

    Task FollowupAsync(string content);

    string? GetSubcommandName()
    {
        return null;
    }
}
