using Discord;
using Discord.WebSocket;

namespace DiscordBot.Commands;

public interface IDiscordApplicationCommandClient
{
    Task BulkOverwriteGlobalCommandsAsync(
        ApplicationCommandProperties[] commands,
        CancellationToken cancellationToken);

    Task BulkOverwriteGuildCommandsAsync(
        ApplicationCommandProperties[] commands,
        ulong guildId,
        CancellationToken cancellationToken);
}

public sealed class DiscordApplicationCommandClient(DiscordSocketClient client)
    : IDiscordApplicationCommandClient
{
    public async Task BulkOverwriteGlobalCommandsAsync(
        ApplicationCommandProperties[] commands,
        CancellationToken cancellationToken)
    {
        await client.Rest.BulkOverwriteGlobalCommands(
            commands,
            new RequestOptions { CancelToken = cancellationToken });
    }

    public async Task BulkOverwriteGuildCommandsAsync(
        ApplicationCommandProperties[] commands,
        ulong guildId,
        CancellationToken cancellationToken)
    {
        await client.Rest.BulkOverwriteGuildCommands(
            commands,
            guildId,
            new RequestOptions { CancelToken = cancellationToken });
    }
}
