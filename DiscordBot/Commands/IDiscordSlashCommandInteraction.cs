namespace DiscordBot.Commands;

public interface IDiscordSlashCommandInteraction
{
    string Name { get; }

    string UserMention { get; }

    Task RespondAsync(string content);

    Task DeferAsync();

    Task ModifyOriginalResponseAsync(string content);

    Task FollowupAsync(string content);
}
