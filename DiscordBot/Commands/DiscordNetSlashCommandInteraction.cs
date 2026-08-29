using Discord;
using Discord.WebSocket;

namespace DiscordBot.Commands;

internal sealed class DiscordNetSlashCommandInteraction(
    SocketSlashCommand command) : IDiscordSlashCommandInteraction
{
    public string Name => command.Data.Name;

    public string UserMention => command.User.Mention;

    public string? GetStringOption(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var option = command.Data.Options
            .FirstOrDefault(option => option.Name == name);
        if (option is null)
        {
            return null;
        }

        return option.Value as string;
    }

    public long? GetIntegerOption(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var option = command.Data.Options
            .FirstOrDefault(option => option.Name == name);
        if (option is null)
        {
            return null;
        }

        return (long?)option.Value;
    }

    public Task RespondAsync(string content)
    {
        return command.RespondAsync(content);
    }

    public Task DeferAsync()
    {
        return command.DeferAsync();
    }

    public async Task ModifyOriginalResponseAsync(string content)
    {
        await command.ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = content;
            properties.AllowedMentions = AllowedMentions.None;
        });
    }

    public async Task FollowupAsync(string content)
    {
        await command.FollowupAsync(
            text: content,
            allowedMentions: AllowedMentions.None);
    }
}
